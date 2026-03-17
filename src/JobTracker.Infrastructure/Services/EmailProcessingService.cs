using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Services;

public class EmailProcessingService : IEmailProcessingService
{
    private readonly IEmailRepository _emailRepo;
    private readonly IApplicationRepository _applicationRepo;
    private readonly ILLMService _llmService;
    private readonly ILogger<EmailProcessingService> _logger;
    private HashSet<string> _knownSenderDomains = new(StringComparer.OrdinalIgnoreCase);

    public EmailProcessingService(
        IEmailRepository emailRepo,
        IApplicationRepository applicationRepo,
        ILLMService llmService,
        ILogger<EmailProcessingService> logger)
    {
        _emailRepo = emailRepo;
        _applicationRepo = applicationRepo;
        _llmService = llmService;
        _logger = logger;
    }

    public void SetKnownSenderDomains(HashSet<string> domains)
    {
        _knownSenderDomains = domains;
    }

    public async Task ProcessNewEmailAsync(Email email)
    {
        if (await _emailRepo.ExistsAsync(email.Id))
        {
            _logger.LogDebug("Email {EmailId} already exists, skipping", email.Id);
            return;
        }

        await _emailRepo.CreateAsync(email);
        await ClassifyAndLinkEmailAsync(email);
    }

    public async Task ClassifyAndLinkEmailAsync(Email email)
    {
        // 1. Thread detection — if this email is part of a known thread,
        //    link it immediately and check for status updates via rules
        var application = await DetectApplicationFromEmailAsync(email);
        if (application is not null)
        {
            await _emailRepo.LinkToApplicationAsync(email.Id, application.Id);
            _logger.LogDebug("Email {EmailId} linked to application {AppId} via thread", email.Id, application.Id);

            var threadClassification = RuleBasedEmailClassifier.Classify(email);
            if (threadClassification?.Status is not null)
            {
                var newStatus = MapStatusString(threadClassification.Status);
                if (newStatus.HasValue && newStatus.Value != application.Status)
                {
                    await _applicationRepo.UpdateStatusAsync(application.Id, newStatus.Value);
                    _logger.LogInformation("Updated application {AppId} status to {Status} via thread email", application.Id, newStatus.Value);
                }
            }
            return;
        }

        // 2. Check if sender is a known domain (company already in DB)
        var isKnownSender = IsKnownSenderDomain(email.From);

        // 3. Try rule-based classification (instant, no LLM call)
        var classification = RuleBasedEmailClassifier.Classify(email);

        // 4. Decide whether to call LLM
        if (classification is null)
        {
            // Rules uncertain → LLM decides
            var emailContent = $"Subject: {email.Subject}\nFrom: {email.From}\nBody: {email.Body}";
            classification = await _llmService.ClassifyEmailAsync(emailContent);
            _logger.LogDebug("Email {EmailId} classified by LLM: job={IsJobRelated}", email.Id, classification.IsJobRelated);
        }
        else if (isKnownSender && !classification.IsJobRelated)
        {
            // Rules say not job-related, but sender is known → LLM gets final say
            var emailContent = $"Subject: {email.Subject}\nFrom: {email.From}\nBody: {email.Body}";
            var llmResult = await _llmService.ClassifyEmailAsync(emailContent);
            _logger.LogDebug("Email {EmailId} known sender override: rules said skip, LLM says job={IsJobRelated}",
                email.Id, llmResult.IsJobRelated);
            classification = llmResult;
        }
        else
        {
            _logger.LogDebug("Email {EmailId} classified by rules: job={IsJobRelated} status={Status}",
                email.Id, classification.IsJobRelated, classification.Status);
        }

        if (!classification.IsJobRelated)
        {
            _logger.LogDebug("Email {EmailId} is not job-related", email.Id);
            return;
        }

        // If classified as job-related but no company name, skip
        if (classification.CompanyName is null)
        {
            application = null;
        }
        else
        {
            application = await FindMatchingApplicationAsync(
                classification.CompanyName,
                classification.JobTitle ?? string.Empty);
        }

        if (application is null)
        {
            if (classification.CompanyName is null)
            {
                _logger.LogDebug(
                    "Email {EmailId} classified as job-related but no company name extracted, skipping",
                    email.Id);
                return;
            }

            application = await CreateApplicationFromClassificationAsync(email, classification);
            _logger.LogInformation("Created new application from email: {Company}", application.CompanyName);
        }

        await _emailRepo.LinkToApplicationAsync(email.Id, application.Id);

        if (classification.Status is not null)
        {
            var newStatus = MapStatusString(classification.Status);
            if (newStatus.HasValue && newStatus.Value != application.Status)
            {
                await _applicationRepo.UpdateStatusAsync(application.Id, newStatus.Value);
                _logger.LogInformation("Updated application {AppId} status to {Status}", application.Id, newStatus.Value);
            }
        }
    }

    private bool IsKnownSenderDomain(string from)
    {
        var atIndex = from.LastIndexOf('@');
        if (atIndex < 0) return false;

        var rest = from[(atIndex + 1)..];
        var endIndex = rest.IndexOf('>');
        if (endIndex >= 0)
            rest = rest[..endIndex];

        return _knownSenderDomains.Contains(rest.Trim());
    }

    public async Task<Application?> DetectApplicationFromEmailAsync(Email email)
    {
        if (email.ThreadId is not null)
        {
            var threadEmails = await _emailRepo.GetByThreadIdAsync(email.ThreadId);
            var linkedEmail = threadEmails.FirstOrDefault(e => e.ApplicationId.HasValue);
            if (linkedEmail?.ApplicationId is not null)
            {
                return await _applicationRepo.GetByIdAsync(linkedEmail.ApplicationId.Value);
            }
        }
        return null;
    }

    public async Task<Application?> FindMatchingApplicationAsync(string companyName, string jobTitle)
    {
        return await _applicationRepo.FindByCompanyAndTitleAsync(companyName, jobTitle);
    }

    public async Task<Application> CreateApplicationFromEmailAsync(Email email)
    {
        var emailContent = $"Subject: {email.Subject}\nFrom: {email.From}\nBody: {email.Body}";
        var classification = await _llmService.ExtractApplicationDataAsync(emailContent);

        var application = new Application
        {
            CompanyName = classification.CompanyName ?? ExtractCompanyFromEmail(email.From),
            JobTitle = classification.JobTitle ?? "Unknown Position",
            Status = MapStatusString(classification.Status) ?? ApplicationStatus.Applied,
            AppliedDate = email.ReceivedDate
        };

        return await _applicationRepo.CreateAsync(application);
    }

    private async Task<Application> CreateApplicationFromClassificationAsync(
        Email email, EmailClassificationResult classification)
    {
        var application = new Application
        {
            CompanyName = classification.CompanyName ?? ExtractCompanyFromEmail(email.From),
            JobTitle = classification.JobTitle ?? "Unknown Position",
            Status = MapStatusString(classification.Status) ?? ApplicationStatus.Applied,
            AppliedDate = email.ReceivedDate
        };

        return await _applicationRepo.CreateAsync(application);
    }

    private static string ExtractCompanyFromEmail(string fromAddress)
    {
        var atIndex = fromAddress.IndexOf('@');
        if (atIndex < 0) return "Unknown Company";

        var domain = fromAddress[(atIndex + 1)..];
        var dotIndex = domain.IndexOf('.');
        if (dotIndex > 0) domain = domain[..dotIndex];

        return char.ToUpper(domain[0]) + domain[1..];
    }

    private static ApplicationStatus? MapStatusString(string? status)
    {
        return status?.ToLower() switch
        {
            "applied" => ApplicationStatus.Applied,
            "rejected" => ApplicationStatus.Rejected,
            "interview_request" or "interview" => ApplicationStatus.InterviewScheduled,
            "offer" => ApplicationStatus.Offer,
            "under_review" or "review" => ApplicationStatus.UnderReview,
            "technical" or "assessment" => ApplicationStatus.TechnicalAssessment,
            "withdrawn" => ApplicationStatus.Withdrawn,
            _ => null
        };
    }
}
