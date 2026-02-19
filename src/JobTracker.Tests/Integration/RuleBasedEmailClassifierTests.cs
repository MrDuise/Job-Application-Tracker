using JobTracker.Core.Models;
using JobTracker.Infrastructure.Services;
using Xunit;

namespace JobTracker.Tests.Integration;

/// <summary>
/// Tests for the rule-based email classifier.
/// Each test creates a realistic email and verifies the classification result.
/// Tests are organized by rule category: ATS, applied, rejected, interview, offer,
/// cold outreach, digests, non-job, and edge cases.
/// </summary>
public class RuleBasedEmailClassifierTests
{
    private static Email MakeEmail(string from, string subject, string body) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            From = from,
            To = "me@gmail.com",
            Subject = subject,
            Body = body,
            ReceivedDate = DateTime.UtcNow
        };

    // =================================================================
    // ATS SENDER DOMAINS — should classify as "applied"
    // =================================================================

    [Theory]
    [InlineData("no-reply@greenhouse.io", "Application Confirmation - Datadog")]
    [InlineData("noreply@hire.lever.co", "Thanks for applying - Cloudflare")]
    [InlineData("noreply@jobs.lever.co", "Application Received - Stripe")]
    [InlineData("no-reply@myworkday.com", "Application Submitted - Salesforce")]
    [InlineData("no-reply@icims.com", "Thank you for your application - Capital One")]
    [InlineData("noreply@smartrecruiters.com", "Your application to Visa")]
    [InlineData("noreply@jobvite.com", "Application Confirmation - Twilio")]
    [InlineData("noreply@ashbyhq.com", "Application Received - Ramp")]
    [InlineData("noreply@notifications.greenhouse.io", "Application Confirmed - Figma")]
    public void ATS_Domains_ClassifyAsApplied(string from, string subject)
    {
        var email = MakeEmail(from, subject,
            "Thank you for applying! We have received your application and will review it.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void ATS_Greenhouse_Rejection_ClassifiesAsRejected()
    {
        var email = MakeEmail("no-reply@greenhouse.io",
            "Update on your application - Datadog",
            "After careful consideration, we have decided to move forward with other candidates.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void ATS_Lever_Interview_ClassifiesAsInterview()
    {
        var email = MakeEmail("noreply@hire.lever.co",
            "Interview Invitation - Cloudflare",
            "We would like to schedule an interview for the Backend Engineer position.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    // =================================================================
    // APPLICATION CONFIRMATION PATTERNS — from any sender
    // =================================================================

    [Fact]
    public void Applied_ThankYouForApplying()
    {
        var email = MakeEmail("careers@netflix.com",
            "Application Received",
            "Thank you for applying to Netflix! We have received your application for the Senior Software Engineer role.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_ApplicationSubmitted()
    {
        var email = MakeEmail("noreply@google.com",
            "Your application has been submitted",
            "Your application has been submitted for Software Engineer at Google. Our team will review it shortly.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_WeReceivedYourApplication()
    {
        var email = MakeEmail("hr@meta.com",
            "Application Confirmation",
            "We received your application for the Product Manager position. We will be in touch.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_LinkedIn_ApplicationSent()
    {
        var email = MakeEmail("jobs-noreply@linkedin.com",
            "Your application was sent to Microsoft",
            "You applied for Senior Software Engineer at Microsoft. Your application was sent to the hiring team.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_Indeed()
    {
        var email = MakeEmail("noreply@indeed.com",
            "You applied for Backend Engineer at Stripe",
            "You applied for Backend Engineer at Stripe through Indeed.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_ThanksForInterest()
    {
        var email = MakeEmail("recruiting@apple.com",
            "Thank you for your interest",
            "Thanks for your interest in the iOS Engineer position at Apple. We'll review your materials.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    [Fact]
    public void Applied_SuccessfullySubmitted()
    {
        var email = MakeEmail("noreply@oracle.com",
            "Application Successfully Submitted",
            "Your application has been successfully submitted for the Java Developer role.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    // =================================================================
    // REJECTION PATTERNS
    // =================================================================

    [Fact]
    public void Rejected_MovedForwardWithOthers()
    {
        var email = MakeEmail("hr@amazon.com",
            "Update on your application",
            "Thank you for your interest in Amazon. After careful review, we have decided to move forward with other candidates.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void Rejected_UnfortunatelyWontBeMovingForward()
    {
        var email = MakeEmail("careers@stripe.com",
            "Your application to Stripe",
            "Unfortunately, we won't be moving forward with your application at this time.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void Rejected_RegretToInform()
    {
        var email = MakeEmail("recruiting@meta.com",
            "Regarding your application",
            "We regret to inform you that the position has been filled by another candidate.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void Rejected_NotSelectedFor()
    {
        var email = MakeEmail("talent@coinbase.com",
            "Application Update",
            "After careful review, you have not been selected for the Software Engineer role at this time.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void Rejected_PositionFilled()
    {
        var email = MakeEmail("hr@startup.com",
            "Update: Backend Developer Position",
            "We wanted to let you know that the position has been filled. Thank you for your interest.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void Rejected_DifferentDirection()
    {
        var email = MakeEmail("talent@company.com",
            "Your application",
            "After careful consideration, we've decided to go in a different direction for this role.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    // =================================================================
    // INTERVIEW PATTERNS
    // =================================================================

    [Fact]
    public void Interview_InterviewInvitation()
    {
        var email = MakeEmail("recruiter@google.com",
            "Interview Invitation - Software Engineer",
            "We would like to invite you to interview for the Software Engineer position at Google.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void Interview_SchedulePhoneScreen()
    {
        var email = MakeEmail("talent@meta.com",
            "Next steps - PM Role",
            "Hi! I'd like to schedule a phone screen to discuss the Product Manager position.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void Interview_ScheduleInterview()
    {
        var email = MakeEmail("recruiting@apple.com",
            "Interview Request",
            "We would love to set up a call to discuss the iOS Engineer position with you.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void Interview_TechnicalAssessment()
    {
        var email = MakeEmail("engineering@company.com",
            "Technical Assessment - Next Steps",
            "As a next step in the interview process, we'd like you to complete a technical assessment.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void Interview_OnsiteInvitation()
    {
        var email = MakeEmail("hr@company.com",
            "On-site Interview Invitation",
            "We're excited to invite you to an on-site interview at our headquarters.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void Interview_InterviewScheduled()
    {
        var email = MakeEmail("no-reply@calendly.com",
            "Interview scheduled with Coinbase",
            "Your interview has been confirmed for March 15 at 2:00 PM.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    // =================================================================
    // OFFER PATTERNS
    // =================================================================

    [Fact]
    public void Offer_OfferLetter()
    {
        var email = MakeEmail("hr@netflix.com",
            "Your Offer Letter - Netflix",
            "Congratulations! Please find attached your formal offer letter for the Senior Software Engineer position.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("offer", result.Status);
    }

    [Fact]
    public void Offer_PleasedToOffer()
    {
        var email = MakeEmail("talent@stripe.com",
            "Exciting News!",
            "We are pleased to offer you the position of Backend Engineer at Stripe.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("offer", result.Status);
    }

    [Fact]
    public void Offer_CongratulationsOffer()
    {
        var email = MakeEmail("hr@company.com",
            "Congratulations!",
            "Congratulations! After a thorough process, we'd like to extend an official offer to join our team.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("offer", result.Status);
    }

    [Fact]
    public void Offer_WouldLikeToOfferYou()
    {
        var email = MakeEmail("recruiting@google.com",
            "Next Steps",
            "We'd like to offer you the Software Engineer L5 position at Google. Please review the attached details.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("offer", result.Status);
    }

    // =================================================================
    // RECRUITER COLD OUTREACH — should classify as NOT job-related
    // =================================================================

    [Fact]
    public void ColdOutreach_CameAcrossProfile()
    {
        var email = MakeEmail("recruiter@amazon.com",
            "Exciting opportunity at Amazon",
            "I came across your profile on LinkedIn and think you'd be a great fit for our SDE II role. Would you be interested?");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_AreYouOpen()
    {
        var email = MakeEmail("talent@meta.com",
            "Great role for you",
            "Hi! Are you open to new opportunities? We have a Frontend Engineer role that matches your skills.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_WouldYouBeInterested()
    {
        var email = MakeEmail("sourcer@google.com",
            "SWE position at Google",
            "Would you be interested in applying for a Software Engineer position at Google?");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_CheckOutThisRole()
    {
        var email = MakeEmail("recruiting@startup.io",
            "Perfect opportunity",
            "Hey! Check out this role at our company. I think it would be a perfect fit for your background.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_GreatFit()
    {
        var email = MakeEmail("talent@company.com",
            "You'd be perfect for this",
            "I think you'd be a great fit for our Senior Engineer role. We're building something exciting.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_ImARecruiter()
    {
        var email = MakeEmail("jane@staffing.com",
            "Java Developer Opportunity",
            "I'm a recruiter specializing in tech placements. I have a great Java Developer role I think you'd love.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_ReachingOut()
    {
        var email = MakeEmail("talent@company.com",
            "Interesting opportunity",
            "I'm reaching out about a role on our platform team. Would love to chat if you're interested.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void ColdOutreach_ButWithAppliedLanguage_StillClassifiesAsApplied()
    {
        // Edge case: email has BOTH cold outreach and application confirmation language
        // The applied patterns should win
        var email = MakeEmail("recruiter@company.com",
            "Thank you for applying",
            "I came across your profile. Thank you for applying to the Engineer position!");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
    }

    // =================================================================
    // JOB BOARD DIGESTS — should classify as NOT job-related
    // =================================================================

    [Fact]
    public void Digest_NewJobs()
    {
        var email = MakeEmail("noreply@indeed.com",
            "15 new jobs in your area",
            "Here are some jobs matching your search: Software Engineer at Google, PM at Meta.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void Digest_JobsYouMightLike()
    {
        var email = MakeEmail("jobs-noreply@linkedin.com",
            "Jobs you might be interested in",
            "Based on your profile: Software Engineer at Spotify, Backend Developer at Uber.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void Digest_RecommendedJobs()
    {
        var email = MakeEmail("noreply@glassdoor.com",
            "Recommended jobs for you",
            "New opportunities matching your preferences.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void Digest_DailyJobAlert()
    {
        var email = MakeEmail("noreply@indeed.com",
            "Daily job alert: Software Engineer",
            "Here are today's new listings for Software Engineer.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    // =================================================================
    // KNOWN NON-JOB DOMAINS — should classify as NOT job-related
    // =================================================================

    [Theory]
    [InlineData("notifications@facebookmail.com", "You have new notifications")]
    [InlineData("noreply@twitter.com", "Someone liked your tweet")]
    [InlineData("noreply@pinterest.com", "New ideas for you")]
    [InlineData("noreply@medium.com", "Your daily digest")]
    [InlineData("noreply@quora.com", "Answer requested")]
    [InlineData("noreply@noreply.github.com", "New issue on repo")]
    [InlineData("noreply@stackoverflow.email", "New answers to your question")]
    [InlineData("noreply@redditmail.com", "Trending on r/programming")]
    [InlineData("noreply@discord.com", "New messages in server")]
    [InlineData("noreply@slack.com", "New message from team")]
    public void NonJobDomains_ClassifyAsNotJobRelated(string from, string subject)
    {
        var email = MakeEmail(from, subject, "Some notification content.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    // =================================================================
    // UNCERTAIN EMAILS — should return null (fall through to LLM)
    // =================================================================

    [Fact]
    public void Uncertain_GenericEmail_ReturnsNull()
    {
        var email = MakeEmail("john@company.com",
            "Quick question",
            "Hey, wanted to follow up on our conversation from last week.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.Null(result);
    }

    [Fact]
    public void Uncertain_AmbiguousRecruiterEmail_ReturnsNull()
    {
        var email = MakeEmail("recruiter@company.com",
            "Following up",
            "Hi, I wanted to touch base about the role we discussed. Let me know your thoughts.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.Null(result);
    }

    [Fact]
    public void Uncertain_VagueUpdate_ReturnsNull()
    {
        var email = MakeEmail("talent@company.com",
            "Update regarding your candidacy",
            "We wanted to share an update with you. Please check your candidate portal.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.Null(result);
    }

    // =================================================================
    // COMPANY/TITLE EXTRACTION
    // =================================================================

    [Fact]
    public void Extraction_CompanyFromApplicationTo()
    {
        var email = MakeEmail("jobs-noreply@linkedin.com",
            "Your application to Google was sent",
            "You applied for Software Engineer at Google.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Google", result!.CompanyName);
    }

    [Fact]
    public void Extraction_CompanyFromDash()
    {
        var email = MakeEmail("no-reply@greenhouse.io",
            "Application Confirmation - Datadog",
            "Thank you for applying! We have received your application.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Datadog", result!.CompanyName);
    }

    [Fact]
    public void Extraction_CompanyFromSentTo()
    {
        var email = MakeEmail("jobs-noreply@linkedin.com",
            "Your application was sent to Microsoft",
            "Your application was sent to the hiring team.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Microsoft", result!.CompanyName);
    }

    [Fact]
    public void Extraction_CompanyFromDomain_Fallback()
    {
        var email = MakeEmail("careers@netflix.com",
            "Application Received",
            "Thank you for applying! We'll be in touch.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Netflix", result!.CompanyName);
    }

    [Fact]
    public void Extraction_JobTitleFromSubject()
    {
        var email = MakeEmail("no-reply@greenhouse.io",
            "Application for the Senior Backend Engineer position - Stripe",
            "Thank you for applying!");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Senior Backend Engineer", result!.JobTitle);
    }

    [Fact]
    public void Extraction_JobTitleFromBody()
    {
        var email = MakeEmail("careers@company.com",
            "Application Received",
            "Thank you for applying for the Full Stack Developer role at our company.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Equal("Full Stack Developer", result!.JobTitle);
    }

    [Fact]
    public void Extraction_NoCompanyFromGenericDomain()
    {
        // Gmail/Yahoo/etc should NOT be extracted as company names
        var email = MakeEmail("recruiter@gmail.com",
            "Application status",
            "Thank you for applying! We received your application.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.Null(result!.CompanyName);
    }

    [Fact]
    public void Extraction_NoCompanyFromPlatformDomain()
    {
        // ATS domains should NOT be extracted as company names
        var email = MakeEmail("no-reply@greenhouse.io",
            "Application Confirmed",
            "Thank you for applying! We received your application.");

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        // Company should be null because we can't extract from "greenhouse.io"
        // and the subject doesn't have a company name pattern
        Assert.Null(result!.CompanyName);
    }

    // =================================================================
    // REAL-WORLD EMAIL SCENARIOS
    // =================================================================

    [Fact]
    public void RealWorld_GoogleApplicationConfirmation()
    {
        var email = MakeEmail("noreply@google.com",
            "Thank you for applying to Google",
            """
            Thank you for your interest in the Software Engineer L4 role at Google.
            We have received your application and our team will review it.
            You can check your application status at careers.google.com/applications.
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("applied", result.Status);
        Assert.Equal("Google", result.CompanyName);
    }

    [Fact]
    public void RealWorld_AmazonRejection()
    {
        var email = MakeEmail("no-reply@amazon.jobs",
            "Your Amazon application",
            """
            Thank you for your interest in Amazon. We appreciate the time you invested
            in the interview process. After careful consideration, we have decided to
            move forward with other candidates for the SDE II position at this time.
            We encourage you to apply again in the future.
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("rejected", result.Status);
    }

    [Fact]
    public void RealWorld_MetaInterviewInvite()
    {
        var email = MakeEmail("recruiting@meta.com",
            "Interview Invitation - Product Manager",
            """
            Hi,

            Congratulations! We would like to invite you to interview for the Product
            Manager role at Meta. Please use the link below to schedule your phone screen
            at a time that works for you.

            Schedule here: calendly.com/meta-recruiting/phone-screen

            Best regards,
            Meta Recruiting Team
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("interview_request", result.Status);
    }

    [Fact]
    public void RealWorld_NetflixOffer()
    {
        var email = MakeEmail("hr@netflix.com",
            "Your offer from Netflix",
            """
            Dear Candidate,

            Congratulations! We are pleased to offer you the position of Senior Software
            Engineer at Netflix. Please find attached your formal offer letter with
            compensation details.

            Please review and let us know if you have any questions.

            Netflix HR Team
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
        Assert.Equal("offer", result.Status);
    }

    [Fact]
    public void RealWorld_LinkedInRecruiterOutreach()
    {
        var email = MakeEmail("messages-noreply@linkedin.com",
            "New message from Sarah at Google",
            """
            Hi! I found your profile on LinkedIn and I think you'd be a great fit for
            our Software Engineer role on the Cloud team at Google. Are you open to
            new opportunities? I'd love to chat more about the position.

            Best,
            Sarah
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void RealWorld_StaffingAgencyBlast()
    {
        var email = MakeEmail("info@roberthalf.com",
            "Urgent: Java Developer needed",
            """
            I'm a recruiter at Robert Half specializing in tech placements.
            We have an urgent opening for a Java Developer in your area.
            This is a great match for your background.
            If you or anyone you know is interested, please apply at our portal.
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    [Fact]
    public void RealWorld_IndeedJobDigest()
    {
        var email = MakeEmail("noreply@indeed.com",
            "25 new Software Engineer jobs in San Francisco",
            """
            New jobs matching your search:
            1. Software Engineer at Google - Mountain View, CA
            2. Backend Developer at Stripe - San Francisco, CA
            3. Full Stack at Uber - San Francisco, CA
            See all results on Indeed.
            Unsubscribe from job alerts.
            """);

        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.False(result!.IsJobRelated);
    }

    // =================================================================
    // EDGE CASES
    // =================================================================

    [Fact]
    public void EdgeCase_EmptyBody()
    {
        var email = MakeEmail("hr@company.com",
            "Application Received",
            "");

        // Subject alone should still trigger "applied" if it matches
        // But body patterns won't match — depends on subject patterns
        var result = RuleBasedEmailClassifier.Classify(email);

        // "Application Received" matches the applied pattern in subject
        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
    }

    [Fact]
    public void EdgeCase_VeryLongBody()
    {
        var longBody = new string('x', 5000) + " Thank you for applying to our company.";
        var email = MakeEmail("careers@company.com",
            "Application Confirmation",
            longBody);

        // The body snippet is limited to first 2000 chars, so this specific pattern
        // won't match in the body. But "Application Confirmation" matches in subject pattern.
        var result = RuleBasedEmailClassifier.Classify(email);

        Assert.NotNull(result);
        Assert.True(result!.IsJobRelated);
    }
}
