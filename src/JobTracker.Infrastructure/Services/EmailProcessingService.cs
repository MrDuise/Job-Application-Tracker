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

    public async Task ProcessEmailBatchAsync(List<Email> emails)
    {
        var skipped = 0;
        var processed = 0;
        var jobRelated = 0;

        foreach (var email in emails)
        {
            if (IsObviousMarketingEmail(email))
            {
                skipped++;
                _logger.LogDebug("Skipping marketing email {EmailId}: {Subject}", email.Id, email.Subject);
                continue;
            }

            await ProcessNewEmailAsync(email);
            processed++;
        }

        _logger.LogInformation(
            "Batch complete: {Processed} processed, {Skipped} marketing skipped out of {Total} total",
            processed, skipped, emails.Count);
    }

    private static bool IsObviousMarketingEmail(Email email)
    {
        var subject = email.Subject.ToLowerInvariant();
        var from = email.From.ToLowerInvariant();
        var body = email.Body.ToLowerInvariant();

        // Skip emails from common marketing/notification senders
        string[] marketingSenders =
        [
            "noreply@", "no-reply@", "marketing@", "newsletter@", "promotions@",
            "deals@", "notifications@social", "info@linkedin.com"
        ];
        if (marketingSenders.Any(s => from.Contains(s)) && !SubjectHasJobKeyword(subject))
            return true;

        // Skip if the body has strong marketing indicators and no job keywords in subject
        string[] marketingIndicators = ["unsubscribe", "view in browser", "email preferences", "opt out"];
        var marketingScore = marketingIndicators.Count(ind => body.Contains(ind));
        if (marketingScore >= 2 && !SubjectHasJobKeyword(subject))
            return true;

        return false;
    }

    private static bool SubjectHasJobKeyword(string subject)
    {
        string[] jobKeywords =
        [
            "application", "interview", "position", "offer", "candidate",
            "applied", "rejected", "opportunity", "role", "hiring"
        ];
        return jobKeywords.Any(k => subject.Contains(k));
    }

    public async Task ClassifyAndLinkEmailAsync(Email email)
    {
        var emailContent = $"Subject: {email.Subject}\nFrom: {email.From}\nBody: {email.Body}";
        var classification = await _llmService.ClassifyEmailAsync(emailContent);

        if (!classification.IsJobRelated)
        {
            _logger.LogDebug("Email {EmailId} is not job-related", email.Id);
            return;
        }

        var application = await DetectApplicationFromEmailAsync(email);
        if (application is null && classification.CompanyName is not null)
        {
            application = await FindMatchingApplicationAsync(
                classification.CompanyName,
                classification.JobTitle ?? string.Empty);
        }

        if (application is null)
        {
            application = await CreateApplicationFromEmailAsync(email);
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
