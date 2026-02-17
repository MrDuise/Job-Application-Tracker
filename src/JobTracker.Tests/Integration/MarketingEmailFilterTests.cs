using JobTracker.Core.DTOs;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobTracker.Tests.Integration;

/// <summary>
/// Tests for the marketing pre-filter in EmailProcessingService.
/// The pre-filter runs BEFORE the LLM to avoid wasting Ollama calls on obvious
/// marketing/newsletter emails. We test it via ProcessEmailBatchAsync, checking
/// whether emails reach the LLM (mock gets called) or are skipped.
/// </summary>
public class MarketingEmailFilterTests
{
    private readonly Mock<IEmailRepository> _mockEmailRepo;
    private readonly Mock<IApplicationRepository> _mockAppRepo;
    private readonly Mock<ILLMService> _mockLlmService;
    private readonly EmailProcessingService _service;
    private int _llmCallCount;

    public MarketingEmailFilterTests()
    {
        _mockEmailRepo = new Mock<IEmailRepository>();
        _mockAppRepo = new Mock<IApplicationRepository>();
        _mockLlmService = new Mock<ILLMService>();
        var logger = new Mock<ILogger<EmailProcessingService>>();

        _mockEmailRepo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockEmailRepo.Setup(r => r.CreateAsync(It.IsAny<Email>())).ReturnsAsync((Email e) => e);

        // Default: classify everything as non-job-related (we're testing the pre-filter, not the LLM)
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .Callback(() => _llmCallCount++)
            .ReturnsAsync(new EmailClassificationResult { IsJobRelated = false });

        _service = new EmailProcessingService(
            _mockEmailRepo.Object, _mockAppRepo.Object,
            _mockLlmService.Object, logger.Object);
    }

    private static Email MakeEmail(string from, string subject, string body = "") =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            From = from,
            To = "me@gmail.com",
            Subject = subject,
            Body = body,
            ReceivedDate = DateTime.UtcNow
        };

    private async Task<bool> EmailReachesLLM(Email email)
    {
        _llmCallCount = 0;
        await _service.ProcessEmailBatchAsync(new List<Email> { email });
        return _llmCallCount > 0;
    }

    // ---------------------------------------------------------------
    // Legitimate application confirmations that MUST reach the LLM
    // (these were being incorrectly blocked by the noreply@ filter)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplicationConfirmation_FromNoreply_WithJobKeyword_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@google.com",
            "Your application for Software Engineer has been received",
            "Thank you for applying to Google. We have received your application and will review it shortly.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task ApplicationConfirmation_FromNoReplyDash_WithJobKeyword_ReachesLLM()
    {
        var email = MakeEmail(
            "no-reply@amazon.jobs",
            "Application Received - SDE II",
            "Your application for SDE II at Amazon has been submitted.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task LinkedInApplicationConfirmation_ReachesLLM()
    {
        // LinkedIn sends from various noreply-type addresses
        var email = MakeEmail(
            "jobs-noreply@linkedin.com",
            "Your application was sent to Microsoft",
            "You applied for Senior Software Engineer at Microsoft. Your application was sent to the hiring team.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task IndeedApplicationConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@indeed.com",
            "Your application to Stripe was submitted",
            "You applied for Backend Engineer at Stripe through Indeed.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task GreenhouseApplicationConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "no-reply@greenhouse.io",
            "Application Confirmation - Datadog",
            "Thank you for your interest in the Site Reliability Engineer role at Datadog.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task LeverApplicationConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@hire.lever.co",
            "Thanks for applying - Cloudflare",
            "We received your application for the DevOps Engineer position.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task RejectionFromNoreply_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@meta.com",
            "Update on your application",
            "After careful consideration, we have decided to move forward with other candidates for the position.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task InterviewInviteFromNoreply_ReachesLLM()
    {
        var email = MakeEmail(
            "no-reply@apple.com",
            "Interview invitation - iOS Engineer",
            "We would like to invite you to interview for the iOS Engineer role.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task OfferLetterFromNoreply_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@hr.netflix.com",
            "Your offer from Netflix",
            "Congratulations! We are pleased to offer you the position of Senior Software Engineer.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task WorkdayApplicationConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@myworkday.com",
            "Application Submitted - Salesforce",
            "Your application for the role of Software Engineer at Salesforce has been submitted.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task ICIMSApplicationConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "no-reply@icims.com",
            "Thank you for your application - Capital One",
            "We have received your application for Software Engineer.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task SmartRecruitersConfirmation_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@smartrecruiters.com",
            "Your application to Visa",
            "Thank you for applying to the Full Stack Developer position.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task RecruiterFollowup_ReachesLLM()
    {
        // Direct recruiter email (not noreply) should always reach the LLM
        var email = MakeEmail(
            "jane.smith@google.com",
            "Following up on your application",
            "Hi, I wanted to follow up on your interview last week. The team really liked you.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task CandidatePortalNotification_ReachesLLM()
    {
        var email = MakeEmail(
            "noreply@careers.jpmorgan.com",
            "Your candidate profile has been updated",
            "The hiring manager has reviewed your application for Software Engineer.");

        Assert.True(await EmailReachesLLM(email));
    }

    // ---------------------------------------------------------------
    // Marketing / newsletter emails that SHOULD be filtered out
    // ---------------------------------------------------------------

    [Fact]
    public async Task NewsletterFromNoreply_NoJobKeywords_IsFiltered()
    {
        var email = MakeEmail(
            "noreply@medium.com",
            "Your daily digest is here",
            "Here are today's top stories. Unsubscribe from this email. View in browser.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task MarketingPromotion_IsFiltered()
    {
        var email = MakeEmail(
            "marketing@shopify.com",
            "50% off all plans this week!",
            "Don't miss out on our biggest sale. Unsubscribe. Email preferences.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task SocialMediaNotification_IsFiltered()
    {
        var email = MakeEmail(
            "notifications@social.facebook.com",
            "You have 5 new notifications",
            "John commented on your post. View in browser. Opt out of emails.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task NewsletterFromNewsletter_IsFiltered()
    {
        var email = MakeEmail(
            "newsletter@techcrunch.com",
            "This week in tech",
            "The latest tech news. Unsubscribe here. Email preferences.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task PromotionalEmail_IsFiltered()
    {
        var email = MakeEmail(
            "promotions@udemy.com",
            "Flash sale: courses from $9.99",
            "Learn something new today! Unsubscribe. View in browser.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task DealsEmail_IsFiltered()
    {
        var email = MakeEmail(
            "deals@amazon.com",
            "Daily deals just for you",
            "Check out today's Lightning Deals! Unsubscribe. Email preferences.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task BulkMarketingWithMultipleIndicators_IsFiltered()
    {
        var email = MakeEmail(
            "hello@somecompany.com",
            "Check out our new features",
            "We're excited to announce new features. Unsubscribe from this list. View in browser. Update your email preferences.");

        Assert.False(await EmailReachesLLM(email));
    }

    // ---------------------------------------------------------------
    // LinkedIn-specific edge cases
    // ---------------------------------------------------------------

    [Fact]
    public async Task LinkedInJobDigest_IsFiltered()
    {
        // LinkedIn "jobs you might like" digests from info@linkedin.com
        // should be filtered since they are ads, not application confirmations
        var email = MakeEmail(
            "info@linkedin.com",
            "25 new jobs in your area",
            "Software Engineer at Google, Product Manager at Meta. View in browser. Unsubscribe.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task LinkedInConnectionRequest_IsFiltered()
    {
        var email = MakeEmail(
            "notifications@social.linkedin.com",
            "John Doe wants to connect",
            "Accept the invitation. Unsubscribe. Email preferences.");

        Assert.False(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task LinkedInApplicationConfirmation_FromInfoAddress_WithJobKeyword_ReachesLLM()
    {
        // But if someone applied through LinkedIn and it comes from info@linkedin.com
        // with application keywords, it should get through
        var email = MakeEmail(
            "info@linkedin.com",
            "Your application to Google was sent",
            "You applied for Software Engineer at Google.");

        Assert.True(await EmailReachesLLM(email));
    }

    // ---------------------------------------------------------------
    // Edge cases: marketing-looking but actually job-related
    // ---------------------------------------------------------------

    [Fact]
    public async Task CompanyNewsletter_WithApplicationKeyword_ReachesLLM()
    {
        // If a company newsletter mentions "application", it should get through
        // to let the LLM decide
        var email = MakeEmail(
            "noreply@spotify.com",
            "Update on your application at Spotify",
            "We wanted to let you know about the status of your application. Unsubscribe.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task AutoReply_WithInterviewKeyword_ReachesLLM()
    {
        var email = MakeEmail(
            "no-reply@calendly.com",
            "Interview scheduled with Coinbase",
            "Your interview has been confirmed. Opt out of reminders.");

        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task RejectionWithUnsubscribe_StillReachesLLM()
    {
        // Some ATS systems include unsubscribe links in rejection emails
        var email = MakeEmail(
            "noreply@greenhouse.io",
            "Update on your application - Rejected",
            "Unfortunately we have decided to move forward with other candidates. Unsubscribe from future emails. View in browser.");

        Assert.True(await EmailReachesLLM(email));
    }

    // ---------------------------------------------------------------
    // Emails from regular addresses (not noreply/marketing)
    // should always reach the LLM regardless of content
    // ---------------------------------------------------------------

    [Fact]
    public async Task RegularPersonEmail_AlwaysReachesLLM()
    {
        var email = MakeEmail(
            "sarah.jones@company.com",
            "Quick question",
            "Hey, just wanted to follow up. Unsubscribe. View in browser.");

        // Even with marketing indicators, a personal email should reach the LLM
        // because the sender doesn't match marketing patterns
        Assert.True(await EmailReachesLLM(email));
    }

    [Fact]
    public async Task RecruiterEmail_AlwaysReachesLLM()
    {
        var email = MakeEmail(
            "talent@company.com",
            "We found your profile interesting",
            "Hi, I came across your profile and thought you'd be a great fit.");

        Assert.True(await EmailReachesLLM(email));
    }
}
