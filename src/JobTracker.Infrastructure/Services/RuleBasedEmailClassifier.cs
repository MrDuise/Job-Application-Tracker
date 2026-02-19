using System.Text.RegularExpressions;
using JobTracker.Core.DTOs;
using JobTracker.Core.Models;

namespace JobTracker.Infrastructure.Services;

/// <summary>
/// Fast, pattern-based email classifier that runs BEFORE the LLM.
/// Returns a classification result when confident, or null to fall through to the LLM.
/// </summary>
public static class RuleBasedEmailClassifier
{
    // Known ATS domains — emails from these are almost always application confirmations
    private static readonly string[] AtsDomains =
    [
        "@greenhouse.io", "@hire.lever.co", "@jobs.lever.co",
        "@myworkday.com", "@icims.com", "@smartrecruiters.com",
        "@jobvite.com", "@ashbyhq.com", "@breezy.hr",
        "@notifications.greenhouse.io", "@talent.icims.com"
    ];

    // Known non-job notification domains
    private static readonly string[] NonJobDomains =
    [
        "@facebookmail.com", "@twitter.com", "@pinterest.com",
        "@medium.com", "@quora.com", "@stackoverflow.email",
        "@accounts.google.com", "@noreply.github.com",
        "@redditmail.com", "@discord.com", "@slack.com",
        "@venmo.com", "@paypal.com", "@cash.app",
        "@uber.com", "@doordash.com", "@grubhub.com"
    ];

    // --- Application Confirmation patterns ---
    private static readonly Regex[] AppliedPatterns =
    [
        new(@"thank you for (applying|your application)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"your application has been (received|submitted|confirmed)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"we (have )?received your application", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"application (received|submitted|confirmed)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"thanks for your interest in the .{1,80} (position|role)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"your application was sent", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"you applied for", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"application.{0,20}successfully submitted", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Rejection patterns ---
    private static readonly Regex[] RejectedPatterns =
    [
        new(@"decided to move forward with other candidates", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"unfortunately.{0,80}(will not|won't|unable to) (be )?mov(e|ing) forward", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"we (regret|are sorry) to inform", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"not (been )?selected for", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"position has been filled", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"will not be (proceeding|continuing) with your", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"after careful (consideration|review).{0,80}(other candidates|another direction)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"we('ve| have) decided to (go|proceed) (in )?a(nother)? (different )?direction", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Interview patterns ---
    private static readonly Regex[] InterviewPatterns =
    [
        new(@"interview (invitation|scheduled|confirmation)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"schedule.{0,40}(interview|phone screen|screening call)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(invite|inviting) you to (an )?interview", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(like|love|want) to (schedule|set up).{0,40}(call|chat|interview|screen)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"phone screen.{0,20}(scheduled|confirmed|set up)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"next (step|round|stage) in.{0,40}(interview|process)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"technical (assessment|challenge|interview)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"on-?site interview", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Offer patterns ---
    private static readonly Regex[] OfferPatterns =
    [
        new(@"offer letter", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(pleased|excited|happy|thrilled|delighted) to (extend|offer|present)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"congratulations.{0,60}offer", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"formal offer", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"we('d| would) like to offer you", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Recruiter cold outreach patterns (NOT job-related — user hasn't applied) ---
    private static readonly Regex[] ColdOutreachPatterns =
    [
        new(@"(I |i |we )?(came across|found|saw|noticed) your (profile|resume|background)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"are you (open|interested|looking).{0,30}(opportunit|role|position|new)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"would you be interested in (applying|exploring|learning|hearing)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(check out|take a look at) this (role|position|opportunity)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(great|perfect|excellent) (fit|match) for.{0,30}(role|position|team)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"I('m| am) (a )?(recruiter|sourcer|talent)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"reaching out.{0,40}(role|position|opportunity)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Job board digest patterns ---
    private static readonly Regex[] DigestPatterns =
    [
        new(@"\d+ new (jobs|positions|opportunities)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"jobs you might (like|be interested)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(new|recommended) jobs? (for you|matching|in your area)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(daily|weekly) job (alert|digest|update)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // --- Company/title extraction patterns ---
    private static readonly Regex CompanyFromSubjectDash = new(
        @"[-–—]\s*(.+?)$", RegexOptions.Compiled);
    private static readonly Regex CompanyFromAppliedTo = new(
        @"(?:application|applied) (?:to|at|for) (.+?)(?:\s*[-–—]|\s*$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CompanyFromSentTo = new(
        @"(?:was sent|submitted) to (.+?)(?:\s*[-–—]|\s*$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex JobTitlePattern = new(
        @"(?:for|as) (?:the |a )?(.+?) (?:position|role|opening|job)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Classify an email using pattern matching rules.
    /// Returns null if uncertain (caller should fall through to LLM).
    /// </summary>
    public static EmailClassificationResult? Classify(Email email)
    {
        var from = email.From.ToLowerInvariant();
        var subject = email.Subject;
        var body = email.Body;
        // Use first 2000 chars of body for pattern matching (enough for the important parts)
        var bodySnippet = body.Length > 2000 ? body[..2000] : body;

        // --- 1. Definite non-job: known non-job domains ---
        if (NonJobDomains.Any(d => from.Contains(d)))
            return new EmailClassificationResult { IsJobRelated = false };

        // --- 2. Definite non-job: job board digests ---
        if (DigestPatterns.Any(p => p.IsMatch(subject)))
            return new EmailClassificationResult { IsJobRelated = false };

        // --- 3. Definite non-job: recruiter cold outreach ---
        if (ColdOutreachPatterns.Any(p => p.IsMatch(bodySnippet))
            && !AppliedPatterns.Any(p => p.IsMatch(bodySnippet)))
        {
            // Only classify as cold outreach if there are NO application confirmation signals
            return new EmailClassificationResult { IsJobRelated = false };
        }

        // --- 4. Known ATS domain → applied ---
        if (AtsDomains.Any(d => from.Contains(d)))
        {
            var (company, title) = ExtractCompanyAndTitle(subject, bodySnippet, from);
            return new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = DetectStatusFromContent(subject, bodySnippet) ?? "applied"
            };
        }

        // --- 5. Application confirmation patterns ---
        if (AppliedPatterns.Any(p => p.IsMatch(subject) || p.IsMatch(bodySnippet)))
        {
            var (company, title) = ExtractCompanyAndTitle(subject, bodySnippet, from);
            return new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = "applied"
            };
        }

        // --- 6. Rejection patterns ---
        if (RejectedPatterns.Any(p => p.IsMatch(bodySnippet)))
        {
            var (company, title) = ExtractCompanyAndTitle(subject, bodySnippet, from);
            return new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = "rejected"
            };
        }

        // --- 7. Interview patterns ---
        if (InterviewPatterns.Any(p => p.IsMatch(subject) || p.IsMatch(bodySnippet)))
        {
            var (company, title) = ExtractCompanyAndTitle(subject, bodySnippet, from);
            return new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = "interview_request"
            };
        }

        // --- 8. Offer patterns ---
        if (OfferPatterns.Any(p => p.IsMatch(subject) || p.IsMatch(bodySnippet)))
        {
            var (company, title) = ExtractCompanyAndTitle(subject, bodySnippet, from);
            return new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = "offer"
            };
        }

        // --- 9. Uncertain — let the LLM handle it ---
        return null;
    }

    /// <summary>
    /// Try to detect a more specific status from body content (used for ATS emails
    /// that might be rejections/interviews rather than just "applied").
    /// </summary>
    private static string? DetectStatusFromContent(string subject, string bodySnippet)
    {
        if (RejectedPatterns.Any(p => p.IsMatch(bodySnippet)))
            return "rejected";
        if (InterviewPatterns.Any(p => p.IsMatch(subject) || p.IsMatch(bodySnippet)))
            return "interview_request";
        if (OfferPatterns.Any(p => p.IsMatch(subject) || p.IsMatch(bodySnippet)))
            return "offer";
        return null;
    }

    /// <summary>
    /// Extract company name and job title from subject line and body using common patterns.
    /// </summary>
    internal static (string? Company, string? Title) ExtractCompanyAndTitle(
        string subject, string bodySnippet, string from)
    {
        var company = ExtractCompany(subject, from);
        var title = ExtractJobTitle(subject, bodySnippet);
        return (company, title);
    }

    private static string? ExtractCompany(string subject, string from)
    {
        // Try: "Your application to {Company}"
        var match = CompanyFromAppliedTo.Match(subject);
        if (match.Success)
            return CleanExtractedValue(match.Groups[1].Value);

        // Try: "was sent to {Company}"
        match = CompanyFromSentTo.Match(subject);
        if (match.Success)
            return CleanExtractedValue(match.Groups[1].Value);

        // Try: "Something - {Company}" (common ATS pattern)
        match = CompanyFromSubjectDash.Match(subject);
        if (match.Success)
        {
            var value = CleanExtractedValue(match.Groups[1].Value);
            // Avoid extracting things that look like job titles rather than companies
            if (value is not null && value.Split(' ').Length <= 4)
                return value;
        }

        // Fallback: extract from sender domain
        return ExtractCompanyFromDomain(from);
    }

    private static string? ExtractJobTitle(string subject, string bodySnippet)
    {
        // Try subject first, then body
        var match = JobTitlePattern.Match(subject);
        if (match.Success)
            return CleanExtractedValue(match.Groups[1].Value);

        match = JobTitlePattern.Match(bodySnippet.Length > 500 ? bodySnippet[..500] : bodySnippet);
        if (match.Success)
            return CleanExtractedValue(match.Groups[1].Value);

        return null;
    }

    private static string? CleanExtractedValue(string value)
    {
        var cleaned = value.Trim().Trim('.', ',', '!', ':', ';');
        return string.IsNullOrWhiteSpace(cleaned) || cleaned.Length < 2 ? null : cleaned;
    }

    private static string? ExtractCompanyFromDomain(string from)
    {
        var atIndex = from.IndexOf('@');
        if (atIndex < 0) return null;

        var domain = from[(atIndex + 1)..].ToLowerInvariant();

        // Skip generic email providers — not useful as company names
        string[] genericDomains = ["gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "aol.com",
            "icloud.com", "mail.com", "protonmail.com", "proton.me"];
        if (genericDomains.Any(g => domain == g))
            return null;

        // Skip known ATS/platform domains — company name is in the subject, not the domain
        string[] platformDomains = ["greenhouse.io", "lever.co", "hire.lever.co", "jobs.lever.co",
            "myworkday.com", "icims.com", "smartrecruiters.com", "jobvite.com",
            "ashbyhq.com", "breezy.hr", "linkedin.com", "indeed.com"];
        if (platformDomains.Any(p => domain.Contains(p)))
            return null;

        var dotIndex = domain.IndexOf('.');
        if (dotIndex > 0)
            domain = domain[..dotIndex];

        if (domain.Length < 2) return null;
        return char.ToUpper(domain[0]) + domain[1..];
    }
}
