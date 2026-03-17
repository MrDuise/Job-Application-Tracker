using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace JobTracker.Tests.Unit.Services;

public class EmailProcessingServiceTests
{
    private readonly Mock<IEmailRepository> _mockEmailRepo;
    private readonly Mock<IApplicationRepository> _mockAppRepo;
    private readonly Mock<ILLMService> _mockLlmService;
    private readonly Mock<ILogger<EmailProcessingService>> _mockLogger;
    private readonly EmailProcessingService _service;

    public EmailProcessingServiceTests()
    {
        _mockEmailRepo = new Mock<IEmailRepository>();
        _mockAppRepo = new Mock<IApplicationRepository>();
        _mockLlmService = new Mock<ILLMService>();
        _mockLogger = new Mock<ILogger<EmailProcessingService>>();

        _service = new EmailProcessingService(
            _mockEmailRepo.Object,
            _mockAppRepo.Object,
            _mockLlmService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessNewEmailAsync_SkipsDuplicateEmails()
    {
        var email = new Email { Id = "123", Subject = "Test", From = "test@example.com", To = "me@example.com", Body = "Hello" };
        _mockEmailRepo.Setup(r => r.ExistsAsync("123")).ReturnsAsync(true);

        await _service.ProcessNewEmailAsync(email);

        _mockEmailRepo.Verify(r => r.CreateAsync(It.IsAny<Email>()), Times.Never);
    }

    [Fact]
    public async Task ProcessNewEmailAsync_CreatesEmailAndClassifies()
    {
        var email = new Email { Id = "456", Subject = "Application Received", From = "hr@google.com", To = "me@example.com", Body = "Thank you for applying" };
        _mockEmailRepo.Setup(r => r.ExistsAsync("456")).ReturnsAsync(false);
        _mockEmailRepo.Setup(r => r.CreateAsync(It.IsAny<Email>())).ReturnsAsync(email);
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult { IsJobRelated = false });

        await _service.ProcessNewEmailAsync(email);

        _mockEmailRepo.Verify(r => r.CreateAsync(email), Times.Once);
    }

    [Fact]
    public async Task ClassifyAndLinkEmailAsync_IgnoresNonJobEmails()
    {
        var email = new Email { Id = "789", Subject = "Newsletter", From = "news@spam.com", To = "me@example.com", Body = "Buy now!" };
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult { IsJobRelated = false });

        await _service.ClassifyAndLinkEmailAsync(email);

        _mockEmailRepo.Verify(r => r.LinkToApplicationAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ClassifyAndLinkEmailAsync_LinksToExistingApplication()
    {
        var email = new Email { Id = "101", Subject = "Interview Invite", From = "hr@google.com", To = "me@example.com", Body = "We'd like to interview you" };
        var existingApp = new Application { Id = 1, CompanyName = "Google", JobTitle = "SWE", Status = ApplicationStatus.Applied };

        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "Google",
                JobTitle = "SWE",
                Status = "interview_request"
            });

        _mockAppRepo.Setup(r => r.FindByCompanyAndTitleAsync("Google", "SWE"))
            .ReturnsAsync(existingApp);

        await _service.ClassifyAndLinkEmailAsync(email);

        _mockEmailRepo.Verify(r => r.LinkToApplicationAsync("101", 1), Times.Once);
        _mockAppRepo.Verify(r => r.UpdateStatusAsync(1, ApplicationStatus.InterviewScheduled), Times.Once);
    }

    [Fact]
    public async Task ClassifyAndLinkEmailAsync_CreatesNewApplication_WhenNoMatch()
    {
        var email = new Email { Id = "202", Subject = "Application Confirmed", From = "careers@newco.com", To = "me@example.com", Body = "Your application for Product Manager has been received." };

        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "NewCo",
                JobTitle = "Product Manager",
                Status = "applied"
            });

        _mockAppRepo.Setup(r => r.FindByCompanyAndTitleAsync("NewCo", "Product Manager"))
            .ReturnsAsync((Application?)null);

        _mockLlmService.Setup(s => s.ExtractApplicationDataAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult
            {
                IsJobRelated = true,
                CompanyName = "NewCo",
                JobTitle = "Product Manager",
                Status = "applied"
            });

        _mockAppRepo.Setup(r => r.CreateAsync(It.IsAny<Application>()))
            .ReturnsAsync((Application a) => { a.Id = 5; return a; });

        await _service.ClassifyAndLinkEmailAsync(email);

        _mockAppRepo.Verify(r => r.CreateAsync(It.Is<Application>(a =>
            a.CompanyName == "NewCo" && a.JobTitle == "Product Manager")), Times.Once);
        _mockEmailRepo.Verify(r => r.LinkToApplicationAsync("202", 5), Times.Once);
    }

    [Fact]
    public async Task ProcessNewEmailAsync_ProcessesMultipleEmails()
    {
        var emails = new List<Email>
        {
            new() { Id = "a1", Subject = "Test 1", From = "a@b.com", To = "me@b.com", Body = "Body 1" },
            new() { Id = "a2", Subject = "Test 2", From = "c@d.com", To = "me@b.com", Body = "Body 2" },
        };

        _mockEmailRepo.Setup(r => r.ExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        _mockEmailRepo.Setup(r => r.CreateAsync(It.IsAny<Email>())).ReturnsAsync((Email e) => e);
        _mockLlmService.Setup(s => s.ClassifyEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailClassificationResult { IsJobRelated = false });

        foreach (var email in emails)
            await _service.ProcessNewEmailAsync(email);

        _mockEmailRepo.Verify(r => r.CreateAsync(It.IsAny<Email>()), Times.Exactly(2));
    }
}
