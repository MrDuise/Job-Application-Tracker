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
        var errors = 0;

        foreach (var email in emails)
        {
            if (IsObviousMarketingEmail(email))
            {
                skipped++;
                _logger.LogDebug("Skipping marketing email {EmailId}: {Subject}", email.Id, email.Subject);
                continue;
            }

            try
            {
                await ProcessNewEmailAsync(email);
                processed++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to process email {EmailId}: {Subject}", email.Id, email.Subject);
            }
        }

        _logger.LogInformation(
            "Batch complete: {Processed} processed, {Skipped} marketing skipped, {Errors} errors out of {Total} total",
            processed, skipped, errors, emails.Count);
    }

    private static bool IsObviousMarketingEmail(Email email)
    {
        var subject = email.Subject.ToLowerInvariant();
        var from = email.From.ToLowerInvariant();
        var body = email.Body.ToLowerInvariant();

        // Skip emails from senders that are EXCLUSIVELY marketing.
        // NOTE: noreply@ and no-reply@ are intentionally NOT here — nearly every ATS
        // (Greenhouse, Lever, Workday, iCIMS, LinkedIn, Indeed) sends application
        // confirmations from noreply addresses.
        string[] marketingSenders =
        [
            "marketing@", "newsletter@", "promotions@",
            "deals@", "notifications@social", "info@linkedin.com"
        ];
        if (marketingSenders.Any(s => from.Contains(s)) && !HasJobKeyword(subject, body))
            return true;

        // Skip if the body has strong marketing indicators and no job keywords anywhere
        string[] marketingIndicators = ["unsubscribe", "view in browser", "email preferences", "opt out"];
        var marketingScore = marketingIndicators.Count(ind => body.Contains(ind));
        if (marketingScore >= 2 && !HasJobKeyword(subject, body))
            return true;

        return false;
    }

    private static bool HasJobKeyword(string subject, string body)
    {
        string[] jobKeywords =
        [
            "application", "interview", "position", "offer", "candidate",
            "applied", "rejected", "opportunity", "role", "hiring",
            "thank you for applying", "your application", "we received your",
            "application received", "application submitted", "application confirmed",
            "candidacy", "phone screen", "on-site", "onsite", "offer letter"
        ];
        // Check subject first (fast path), then first 500 chars of body
        var bodySnippet = body.Length > 500 ? body[..500] : body;
        return jobKeywords.Any(k => subject.Contains(k) || bodySnippet.Contains(k));
    }

    public async Task ClassifyAndLinkEmailAsync(Email email)
    {
        // 1. Thread detection first — if this email is part of a known thread,
        //    link it immediately and check for status updates via rules (fast)
        var application = await DetectApplicationFromEmailAsync(email);
        if (application is not null)
        {
            await _emailRepo.LinkToApplicationAsync(email.Id, application.Id);
            _logger.LogDebug("Email {EmailId} linked to application {AppId} via thread", email.Id, application.Id);

            // Still check for status updates (rules are instant)
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

        // 2. Try rule-based classification (instant, no LLM call)
        var classification = RuleBasedEmailClassifier.Classify(email);

        // 3. Fall back to LLM only if rules are uncertain
        if (classification is null)
        {
            var emailContent = $"Subject: {email.Subject}\nFrom: {email.From}\nBody: {email.Body}";
            classification = await _llmService.ClassifyEmailAsync(emailContent);
            _logger.LogDebug("Email {EmailId} classified by LLM: job={IsJobRelated}", email.Id, classification.IsJobRelated);
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
            // Try to find an existing application by company name match
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
