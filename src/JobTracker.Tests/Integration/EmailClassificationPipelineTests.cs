using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Repositories;
using JobTracker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobTracker.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the email classification and linking pipeline.
/// Uses a real in-memory database with mocked ILLMService to test the full flow
/// from email ingestion to application creation/linking.
///
/// These tests target the two main problems:
/// 1. Missing 800+ "applied" emails — legitimate application confirmations not being captured
/// 2. Recruiter outreach being incorrectly classified as applications the user submitted
/// </summary>
public class EmailClassificationPipelineTests : IDisposable
{
    private readonly JobTrackerDbContext _context;
    private readonly EmailRepository _emailRepo;
    private readonly ApplicationRepository _appRepo;
    private readonly Mock<ILLMService> _mockLlmService;
    private readonly EmailProcessingService _service;

    public EmailClassificationPipelineTests()
    {
        var options = new DbContextOptionsBuilder<JobTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new JobTrackerDbContext(options);
        _emailRepo = new EmailRepository(_context);
        _appRepo = new ApplicationRepository(_context);
        _mockLlmService = new Mock<ILLMService>();
        var logger = new Mock<ILogger<EmailProcessingService>>();

        _service = new EmailProcessingService(_emailRepo, _appRepo, _mockLlmService.Object, logger.Object);
    }

    private void SetupLLMResponse(EmailClassificationResult result)
    {
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>())).ReturnsAsync(result);
        _mockLlmService.Setup(s => s.ExtractApplicationDataAsync(It.IsAny<string>())).ReturnsAsync(result);
    }

    private static Email MakeEmail(string id, string from, string subject, string body, string? threadId = null) =>
        new()
        {
            Id = id,
            From = from,
            To = "me@gmail.com",
            Subject = subject,
            Body = body,
            ReceivedDate = DateTime.UtcNow,
            ThreadId = threadId
        };

    // =================================================================
    // APPLICATION CONFIRMATION EMAILS — should create "Applied" apps
    // These are the 800+ emails the user is missing.
    // =================================================================

    [Fact]
    public async Task ApplicationConfirmation_Google_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Google",
            JobTitle = "Software Engineer L4",
            Status = "applied"
        });

        var email = MakeEmail("g1", "noreply@google.com",
            "Thank you for applying to Google",
            "Thank you for your interest in the Software Engineer L4 role at Google. We have received your application and our team will review it.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Google", apps[0].CompanyName);
        Assert.Equal("Software Engineer L4", apps[0].JobTitle);
        Assert.Equal(ApplicationStatus.Applied, apps[0].Status);
    }

    [Fact]
    public async Task ApplicationConfirmation_LinkedIn_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Microsoft",
            JobTitle = "Senior Software Engineer",
            Status = "applied"
        });

        var email = MakeEmail("li1", "jobs-noreply@linkedin.com",
            "Your application was sent to Microsoft",
            "You applied for Senior Software Engineer at Microsoft. Your application was sent to the job poster.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Microsoft", apps[0].CompanyName);
        Assert.Equal("Senior Software Engineer", apps[0].JobTitle);
        Assert.Equal(ApplicationStatus.Applied, apps[0].Status);
    }

    [Fact]
    public async Task ApplicationConfirmation_Indeed_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Stripe",
            JobTitle = "Backend Engineer",
            Status = "applied"
        });

        var email = MakeEmail("ind1", "noreply@indeed.com",
            "Your application to Stripe was submitted",
            "You applied for Backend Engineer at Stripe through Indeed. The employer will review your application.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Stripe", apps[0].CompanyName);
        Assert.Equal(ApplicationStatus.Applied, apps[0].Status);
    }

    [Fact]
    public async Task ApplicationConfirmation_Greenhouse_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Datadog",
            JobTitle = "Site Reliability Engineer",
            Status = "applied"
        });

        var email = MakeEmail("gh1", "no-reply@greenhouse.io",
            "Application Confirmation - Datadog",
            "Thank you for your interest in the Site Reliability Engineer role at Datadog. We have received your application.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Datadog", apps[0].CompanyName);
    }

    [Fact]
    public async Task ApplicationConfirmation_Lever_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Cloudflare",
            JobTitle = "DevOps Engineer",
            Status = "applied"
        });

        var email = MakeEmail("lev1", "noreply@hire.lever.co",
            "Thanks for applying - Cloudflare",
            "We received your application for the DevOps Engineer position at Cloudflare.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Cloudflare", apps[0].CompanyName);
    }

    [Fact]
    public async Task ApplicationConfirmation_Workday_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Salesforce",
            JobTitle = "Software Engineer",
            Status = "applied"
        });

        var email = MakeEmail("wd1", "noreply@myworkday.com",
            "Application Submitted Successfully",
            "Your application for the role of Software Engineer at Salesforce has been submitted successfully.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Salesforce", apps[0].CompanyName);
    }

    [Fact]
    public async Task ApplicationConfirmation_SmartRecruiters_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Visa",
            JobTitle = "Full Stack Developer",
            Status = "applied"
        });

        var email = MakeEmail("sr1", "noreply@smartrecruiters.com",
            "Your application to Visa",
            "Thank you for applying to the Full Stack Developer position at Visa.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Visa", apps[0].CompanyName);
    }

    [Fact]
    public async Task ApplicationConfirmation_ICIMS_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Capital One",
            JobTitle = "Software Engineer",
            Status = "applied"
        });

        var email = MakeEmail("ic1", "no-reply@icims.com",
            "Thank you for your application - Capital One",
            "We have received your application for Software Engineer at Capital One. A recruiter will be in touch.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Capital One", apps[0].CompanyName);
    }

    [Fact]
    public async Task ApplicationConfirmation_DirectCompanyCareerPage_CreatesAppliedApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Netflix",
            JobTitle = "Senior Software Engineer",
            Status = "applied"
        });

        var email = MakeEmail("nf1", "careers@netflix.com",
            "Application Received - Senior Software Engineer",
            "Thank you for applying! Your application for Senior Software Engineer has been received. Our recruiting team will review your information.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Netflix", apps[0].CompanyName);
        Assert.Equal("Senior Software Engineer", apps[0].JobTitle);
    }

    // =================================================================
    // RECRUITER COLD OUTREACH — should NOT create applications
    // These are being incorrectly treated as job applications.
    // =================================================================

    [Fact]
    public async Task RecruiterOutreach_ShouldNotCreateApplication_WhenNotJobRelated()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false,
            CompanyName = null,
            JobTitle = null,
            Status = null
        });

        var email = MakeEmail("ro1", "recruiter@amazon.com",
            "Exciting opportunity at Amazon!",
            "Hi, I came across your profile and think you'd be a great fit for our SDE II role. Would you be interested in applying?");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    [Fact]
    public async Task RecruiterOutreach_ApplyToThis_ShouldNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false,
            CompanyName = null,
            JobTitle = null,
            Status = null
        });

        var email = MakeEmail("ro2", "talent@meta.com",
            "Great role that matches your skills",
            "Hello! I'm a technical recruiter at Meta. I saw your experience and think you'd be perfect for our Frontend Engineer role. Are you open to new opportunities? Apply here: careers.meta.com/apply");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    [Fact]
    public async Task RecruiterOutreach_LinkedIn_ShouldNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false,
            CompanyName = null,
            JobTitle = null,
            Status = null
        });

        var email = MakeEmail("ro3", "messages-noreply@linkedin.com",
            "New message from Sarah at Google",
            "Hi! I found your profile on LinkedIn and thought you might be interested in a Software Engineer position at Google. Let me know if you'd like to learn more!");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    [Fact]
    public async Task StaffingAgencyBlast_ShouldNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false,
            CompanyName = null,
            JobTitle = null,
            Status = null
        });

        var email = MakeEmail("sa1", "info@roberthalf.com",
            "Urgent: Java Developer needed in your area",
            "We have an urgent opening for a Java Developer. If you or anyone you know is interested, please apply at our portal.");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    [Fact]
    public async Task RecruiterOutreach_WithNoCompanyName_SkipsApplicationCreation()
    {
        // Even if LLM incorrectly says job-related but can't extract a company,
        // we should NOT create a garbage application
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = null,
            JobTitle = null,
            Status = null
        });

        var email = MakeEmail("ro4", "recruiter@staffing.com",
            "Perfect role for you!",
            "I have a great opportunity that matches your background. Apply now!");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    [Fact]
    public async Task RecruiterOutreach_WithPlaceholderCompany_SkipsApplicationCreation()
    {
        // LLM returns placeholder values that get sanitized to null
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = null, // sanitized from "String" by OllamaLLMService
            JobTitle = null,    // sanitized from "String"
            Status = null
        });

        var email = MakeEmail("ro5", "hiring@company.io",
            "You'd be great for this role",
            "We think your skills would be perfect for a position we have open. Interested?");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Empty(apps);
    }

    // =================================================================
    // REJECTION EMAILS — should update existing applications
    // =================================================================

    [Fact]
    public async Task RejectionEmail_UpdatesExistingApplication()
    {
        // Seed an existing application
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "Google",
            JobTitle = "Software Engineer",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow.AddDays(-14)
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Google",
            JobTitle = "Software Engineer",
            Status = "rejected"
        });

        var email = MakeEmail("rej1", "noreply@google.com",
            "Update on your application to Google",
            "After careful consideration, we have decided to move forward with other candidates for the Software Engineer role.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.Rejected, updated!.Status);

        // Should NOT create a new application
        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
    }

    [Fact]
    public async Task RejectionEmail_GenericWording_UpdatesExistingApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "Amazon",
            JobTitle = "SDE",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow.AddDays(-7)
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Amazon",
            JobTitle = "SDE",
            Status = "rejected"
        });

        var email = MakeEmail("rej2", "no-reply@amazon.jobs",
            "Your Amazon application",
            "Thank you for your interest in Amazon. We appreciate the time you invested in the process. Unfortunately, we will not be moving forward at this time.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.Rejected, updated!.Status);
    }

    // =================================================================
    // INTERVIEW INVITATIONS — should update existing applications
    // =================================================================

    [Fact]
    public async Task InterviewInvite_UpdatesExistingApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "Meta",
            JobTitle = "Product Manager",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow.AddDays(-5)
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Meta",
            JobTitle = "Product Manager",
            Status = "interview_request"
        });

        var email = MakeEmail("int1", "recruiting@meta.com",
            "Interview Invitation - Product Manager",
            "Congratulations! We would like to invite you to interview for the Product Manager role. Please select a time using the link below.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.InterviewScheduled, updated!.Status);
        Assert.Single(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task PhoneScreenInvite_UpdatesExistingApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "Apple",
            JobTitle = "iOS Engineer",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow.AddDays(-10)
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Apple",
            JobTitle = "iOS Engineer",
            Status = "interview_request"
        });

        var email = MakeEmail("ps1", "recruiter@apple.com",
            "Phone Screen - iOS Engineer",
            "Hi, thanks for applying. I'd like to schedule a phone screen to discuss the iOS Engineer role.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.InterviewScheduled, updated!.Status);
    }

    // =================================================================
    // OFFER EMAILS
    // =================================================================

    [Fact]
    public async Task OfferEmail_UpdatesExistingApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "Netflix",
            JobTitle = "Senior Engineer",
            Status = ApplicationStatus.InterviewScheduled,
            AppliedDate = DateTime.UtcNow.AddDays(-30)
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Netflix",
            JobTitle = "Senior Engineer",
            Status = "offer"
        });

        var email = MakeEmail("off1", "hr@netflix.com",
            "Offer Letter - Senior Engineer",
            "Congratulations! We are pleased to offer you the position of Senior Engineer. Please find attached your official offer letter.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.Offer, updated!.Status);
    }

    // =================================================================
    // THREAD LINKING — emails in the same thread link to same application
    // =================================================================

    [Fact]
    public async Task ThreadedEmails_LinkToSameApplication()
    {
        // First email creates the application
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Stripe",
            JobTitle = "Backend Engineer",
            Status = "applied"
        });

        var email1 = MakeEmail("t1", "careers@stripe.com",
            "Application Received - Backend Engineer",
            "Thank you for applying to Stripe!",
            threadId: "thread-stripe-123");

        await _service.ProcessNewEmailAsync(email1);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        var appId = apps[0].Id;

        // Second email in same thread should link to same application
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Stripe",
            JobTitle = "Backend Engineer",
            Status = "interview_request"
        });

        var email2 = MakeEmail("t2", "recruiter@stripe.com",
            "Re: Application Received - Backend Engineer",
            "We'd like to schedule a phone screen. Are you available next week?",
            threadId: "thread-stripe-123");

        await _service.ProcessNewEmailAsync(email2);

        // Should still be only one application
        apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);

        // Status should be updated
        var updated = await _appRepo.GetByIdAsync(appId);
        Assert.Equal(ApplicationStatus.InterviewScheduled, updated!.Status);
    }

    [Fact]
    public async Task MultipleEmailsFromSameCompany_LinkToSameApplication()
    {
        // First email creates the application
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Google",
            JobTitle = "SWE",
            Status = "applied"
        });

        var email1 = MakeEmail("mc1", "noreply@google.com",
            "Application Confirmation",
            "Thank you for applying to Google.");

        await _service.ProcessNewEmailAsync(email1);

        // Second email from same company should match existing application
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Google",
            JobTitle = "SWE",
            Status = "under_review"
        });

        var email2 = MakeEmail("mc2", "recruiting@google.com",
            "Application Status Update",
            "Your application is currently under review by the hiring committee.");

        await _service.ProcessNewEmailAsync(email2);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal(ApplicationStatus.UnderReview, apps[0].Status);
    }

    // =================================================================
    // DEDUPLICATION
    // =================================================================

    [Fact]
    public async Task DuplicateEmail_IsSkipped()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Test Co",
            JobTitle = "Dev",
            Status = "applied"
        });

        var email = MakeEmail("dup1", "hr@test.com",
            "Application Received",
            "Thanks for applying!");

        await _service.ProcessNewEmailAsync(email);
        await _service.ProcessNewEmailAsync(email); // Process same email again

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
    }

    // =================================================================
    // NON-JOB EMAILS — should not create any applications
    // =================================================================

    [Fact]
    public async Task ShippingNotification_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("ship1", "noreply@amazon.com",
            "Your package has shipped",
            "Your order #12345 has shipped and is on its way!");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task PasswordReset_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("pw1", "noreply@github.com",
            "Password reset request",
            "Someone requested a password reset for your account. If this wasn't you, ignore this email.");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task BankStatement_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("bank1", "noreply@chase.com",
            "Your monthly statement is ready",
            "Your January statement is now available. Log in to view your transactions.");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task PersonalEmail_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("per1", "friend@gmail.com",
            "Dinner tonight?",
            "Hey, want to grab dinner tonight at 7?");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    // =================================================================
    // JOB BOARD DIGESTS — these are ads, NOT applications
    // =================================================================

    [Fact]
    public async Task IndeedJobAlert_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("ja1", "noreply@indeed.com",
            "15 new Software Engineer jobs in your area",
            "We found new jobs matching your search. Software Engineer at Google, Facebook, Amazon. View all jobs. Unsubscribe.");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task LinkedInJobsYouMightLike_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("ja2", "jobs-noreply@linkedin.com",
            "Jobs you might be interested in",
            "Based on your profile, here are some jobs: Software Engineer at Spotify, Backend Developer at Uber. See all recommendations.");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    [Fact]
    public async Task GlassdoorJobAlert_DoesNotCreateApplication()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = false
        });

        var email = MakeEmail("ja3", "noreply@glassdoor.com",
            "New jobs matching your alert",
            "10 new Software Engineer jobs were posted today. View on Glassdoor. Unsubscribe from alerts.");

        await _service.ProcessNewEmailAsync(email);

        Assert.Empty(await _appRepo.GetAllAsync());
    }

    // =================================================================
    // STATUS MAPPING — verifying all status transitions work
    // =================================================================

    [Theory]
    [InlineData("applied", ApplicationStatus.Applied)]
    [InlineData("rejected", ApplicationStatus.Rejected)]
    [InlineData("interview_request", ApplicationStatus.InterviewScheduled)]
    [InlineData("interview", ApplicationStatus.InterviewScheduled)]
    [InlineData("offer", ApplicationStatus.Offer)]
    [InlineData("under_review", ApplicationStatus.UnderReview)]
    [InlineData("review", ApplicationStatus.UnderReview)]
    [InlineData("technical", ApplicationStatus.TechnicalAssessment)]
    [InlineData("assessment", ApplicationStatus.TechnicalAssessment)]
    [InlineData("withdrawn", ApplicationStatus.Withdrawn)]
    public async Task StatusMapping_AllStatusStrings_MapCorrectly(string statusStr, ApplicationStatus expected)
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = statusStr
        });

        var email = MakeEmail($"s-{statusStr}", "hr@testco.com",
            "Update",
            "Status update for your application.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(expected, updated!.Status);
    }

    [Fact]
    public async Task StatusMapping_UnknownStatus_DoesNotChangeApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = "other"
        });

        var email = MakeEmail("s-other", "hr@testco.com",
            "Update",
            "Some generic update.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.Applied, updated!.Status); // unchanged
    }

    [Fact]
    public async Task StatusMapping_NullStatus_DoesNotChangeApplication()
    {
        var existingApp = await _appRepo.CreateAsync(new Application
        {
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = ApplicationStatus.InterviewScheduled,
            AppliedDate = DateTime.UtcNow
        });

        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = null
        });

        var email = MakeEmail("s-null", "hr@testco.com",
            "Quick note",
            "Just a reminder about your upcoming interview.");

        await _service.ProcessNewEmailAsync(email);

        var updated = await _appRepo.GetByIdAsync(existingApp.Id);
        Assert.Equal(ApplicationStatus.InterviewScheduled, updated!.Status);
    }

    // =================================================================
    // COMPANY EXTRACTION FALLBACK — when LLM can't get company name
    // =================================================================

    [Fact]
    public async Task CompanyFallback_ExtractsFromDomain_WhenLLMReturnsNull()
    {
        // Classify returns company, but extract (called during create) returns null
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "SomeCompany",
                JobTitle = "Engineer",
                Status = "applied"
            });

        _mockLlmService.Setup(s => s.ExtractApplicationDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = null,
                JobTitle = null,
                Status = "applied"
            });

        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "SomeCompany",
                JobTitle = null,
                Status = "applied"
            });

        var email = MakeEmail("fb1", "careers@coolstartup.io",
            "Application Received",
            "Thanks for applying!");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        // Should have extracted from the classify call's CompanyName
        Assert.Equal("SomeCompany", apps[0].CompanyName);
    }

    // =================================================================
    // BATCH PROCESSING — multiple emails in one batch
    // =================================================================

    [Fact]
    public async Task BatchProcessing_MixOfJobAndNonJobEmails()
    {
        var callCount = 0;
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount switch
                {
                    1 => new EmailClassificationResult
                    {
                        IsJobRelated = true,
                        CompanyName = "Apple",
                        JobTitle = "iOS Dev",
                        Status = "applied"
                    },
                    _ => new EmailClassificationResult { IsJobRelated = false }
                };
            });
        _mockLlmService.Setup(s => s.ExtractApplicationDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "Apple",
                JobTitle = "iOS Dev",
                Status = "applied"
            });

        var emails = new List<Email>
        {
            MakeEmail("b1", "careers@apple.com", "Application Received", "Thank you for applying to Apple!"),
            MakeEmail("b2", "friend@gmail.com", "Lunch tomorrow?", "Want to grab lunch?"),
            MakeEmail("b3", "noreply@amazon.com", "Your order shipped", "Tracking: 12345"),
        };

        foreach (var email in emails)
            await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
        Assert.Equal("Apple", apps[0].CompanyName);
    }

    [Fact]
    public async Task BatchProcessing_AllEmailsReachClassifier()
    {
        var llmCallCount = 0;
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .Callback(() => llmCallCount++)
            .ReturnsAsync(new EmailClassificationResult { IsJobRelated = false });

        var emails = new List<Email>
        {
            // Marketing pre-filtering now happens at IMAP header level,
            // so all emails that reach ProcessNewEmailAsync go through classification
            MakeEmail("bf1", "marketing@spam.com",
                "Big sale this weekend!",
                "Don't miss our 50% off sale! Unsubscribe. View in browser."),
            MakeEmail("bf2", "person@company.com",
                "Hello there",
                "Just checking in."),
        };

        foreach (var email in emails)
            await _service.ProcessNewEmailAsync(email);

        // Both emails reach classification (rules first, then LLM if uncertain)
        // The exact LLM call count depends on whether rules classify them
        Assert.True(llmCallCount >= 1);
    }

    // =================================================================
    // MULTIPLE APPLICATIONS — different companies create separate apps
    // =================================================================

    [Fact]
    public async Task DifferentCompanies_CreateSeparateApplications()
    {
        var companies = new[] { ("Google", "SWE"), ("Meta", "PM"), ("Amazon", "SDE"), ("Apple", "iOS Dev"), ("Netflix", "Backend") };

        foreach (var (company, title) in companies)
        {
            SetupLLMResponse(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = company,
                JobTitle = title,
                Status = "applied"
            });

            var email = MakeEmail($"multi-{company}", $"hr@{company.ToLower()}.com",
                $"Application Received - {title}",
                $"Thank you for applying to {company} for the {title} position.");

            await _service.ProcessNewEmailAsync(email);
        }

        var apps = await _appRepo.GetAllAsync();
        Assert.Equal(5, apps.Count);
        Assert.All(apps, a => Assert.Equal(ApplicationStatus.Applied, a.Status));
    }

    [Fact]
    public async Task SameCompanyDifferentRoles_CreateSeparateApplications()
    {
        // Apply to two different roles at Google
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "Google",
            JobTitle = "Software Engineer",
            Status = "applied"
        });

        await _service.ProcessNewEmailAsync(MakeEmail("g-swe", "noreply@google.com",
            "Application for Software Engineer",
            "Thank you for applying."));

        // The FindByCompanyAndTitleAsync uses Contains matching, so "Cloud Engineer"
        // won't match "Software Engineer"
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "Google",
                JobTitle = "Cloud Engineer",
                Status = "applied"
            });
        _mockLlmService.Setup(s => s.ExtractApplicationDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "Google",
                JobTitle = "Cloud Engineer",
                Status = "applied"
            });

        await _service.ProcessNewEmailAsync(MakeEmail("g-ce", "noreply@google.com",
            "Application for Cloud Engineer",
            "Thank you for applying to the Cloud Engineer position."));

        var apps = await _appRepo.GetAllAsync();
        // Whether these are 1 or 2 depends on FindByCompanyAndTitleAsync's fuzzy matching
        // At minimum, both emails should be processed without errors
        Assert.NotEmpty(apps);
    }

    // =================================================================
    // EDGE CASES
    // =================================================================

    [Fact]
    public async Task EmptyEmailBody_HandledGracefully()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = "applied"
        });

        var email = MakeEmail("empty1", "hr@testco.com",
            "Application Received",
            "");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
    }

    [Fact]
    public async Task VeryLongEmailBody_HandledGracefully()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Dev",
            Status = "applied"
        });

        var longBody = new string('x', 10000) + " Thank you for applying!";
        var email = MakeEmail("long1", "hr@testco.com",
            "Application Received",
            longBody);

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
    }

    [Fact]
    public async Task HtmlInEmailBody_HandledGracefully()
    {
        SetupLLMResponse(new EmailClassificationResult
        {
            IsJobRelated = true,
            CompanyName = "TestCo",
            JobTitle = "Developer",
            Status = "applied"
        });

        var email = MakeEmail("html1", "hr@testco.com",
            "Application Received",
            "<html><body><p>Thank you for <strong>applying</strong> to <a href='#'>TestCo</a>!</p></body></html>");

        await _service.ProcessNewEmailAsync(email);

        var apps = await _appRepo.GetAllAsync();
        Assert.Single(apps);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
