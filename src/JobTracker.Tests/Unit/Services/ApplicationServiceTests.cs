using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Services;
using Moq;
using Xunit;

namespace JobTracker.Tests.Unit.Services;

public class ApplicationServiceTests
{
    private readonly Mock<IApplicationRepository> _mockRepo;
    private readonly ApplicationService _service;

    public ApplicationServiceTests()
    {
        _mockRepo = new Mock<IApplicationRepository>();
        _service = new ApplicationService(_mockRepo.Object);
    }

    [Fact]
    public async Task GetAllApplicationsAsync_ReturnsAllApplications()
    {
        // Arrange
        var applications = new List<Application>
        {
            new() { Id = 1, CompanyName = "Google", JobTitle = "SWE", Status = ApplicationStatus.Applied, AppliedDate = DateTime.UtcNow },
            new() { Id = 2, CompanyName = "Meta", JobTitle = "SDE", Status = ApplicationStatus.InterviewScheduled, AppliedDate = DateTime.UtcNow },
        };
        _mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(applications);

        // Act
        var result = await _service.GetAllApplicationsAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Google", result[0].CompanyName);
        Assert.Equal("Meta", result[1].CompanyName);
    }

    [Fact]
    public async Task GetApplicationByIdAsync_ReturnsNull_WhenNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Application?)null);

        var result = await _service.GetApplicationByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetApplicationByIdAsync_ReturnsDto_WhenFound()
    {
        var app = new Application
        {
            Id = 1,
            CompanyName = "Google",
            JobTitle = "Software Engineer",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
        };
        _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(app);

        var result = await _service.GetApplicationByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Google", result!.CompanyName);
        Assert.Equal("Software Engineer", result.JobTitle);
    }

    [Fact]
    public async Task CreateApplicationAsync_CreatesAndReturnsDto()
    {
        var dto = new CreateApplicationDto
        {
            CompanyName = "Amazon",
            JobTitle = "Backend Engineer",
            Status = ApplicationStatus.Applied,
        };

        _mockRepo.Setup(r => r.CreateAsync(It.IsAny<Application>()))
            .ReturnsAsync((Application a) => { a.Id = 1; return a; });

        var result = await _service.CreateApplicationAsync(dto);

        Assert.Equal("Amazon", result.CompanyName);
        Assert.Equal("Backend Engineer", result.JobTitle);
        Assert.Equal(ApplicationStatus.Applied, result.Status);
        _mockRepo.Verify(r => r.CreateAsync(It.IsAny<Application>()), Times.Once);
    }

    [Fact]
    public async Task UpdateApplicationAsync_ThrowsKeyNotFound_WhenNotFound()
    {
        _mockRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Application?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateApplicationAsync(999, new UpdateApplicationDto()));
    }

    [Fact]
    public async Task UpdateApplicationAsync_UpdatesOnlyProvidedFields()
    {
        var existing = new Application
        {
            Id = 1,
            CompanyName = "Google",
            JobTitle = "SWE",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
            Notes = "Original note",
        };
        _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Application>())).ReturnsAsync((Application a) => a);

        var dto = new UpdateApplicationDto { Status = ApplicationStatus.InterviewScheduled };
        var result = await _service.UpdateApplicationAsync(1, dto);

        Assert.Equal("Google", result.CompanyName); // unchanged
        Assert.Equal(ApplicationStatus.InterviewScheduled, result.Status); // updated
        Assert.Equal("Original note", result.Notes); // unchanged
    }

    [Fact]
    public async Task AddNoteToApplicationAsync_AppendsNote()
    {
        var existing = new Application
        {
            Id = 1,
            CompanyName = "Google",
            JobTitle = "SWE",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
            Notes = "First note",
        };
        _mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);
        _mockRepo.Setup(r => r.UpdateAsync(It.IsAny<Application>())).ReturnsAsync((Application a) => a);

        await _service.AddNoteToApplicationAsync(1, "Second note");

        _mockRepo.Verify(r => r.UpdateAsync(It.Is<Application>(a =>
            a.Notes!.Contains("First note") && a.Notes.Contains("Second note"))), Times.Once);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ReturnsCorrectStats()
    {
        var statusCounts = new Dictionary<ApplicationStatus, int>
        {
            { ApplicationStatus.Applied, 5 },
            { ApplicationStatus.InterviewScheduled, 2 },
            { ApplicationStatus.Rejected, 1 },
        };
        var recent = new List<Application>
        {
            new() { Id = 1, CompanyName = "Google", JobTitle = "SWE", Status = ApplicationStatus.Applied, AppliedDate = DateTime.UtcNow },
        };

        _mockRepo.Setup(r => r.GetStatusCountsAsync()).ReturnsAsync(statusCounts);
        _mockRepo.Setup(r => r.GetRecentAsync(10)).ReturnsAsync(recent);

        var result = await _service.GetDashboardStatsAsync();

        Assert.Equal(8, result.TotalApplications);
        Assert.Equal(3, result.StatusCounts.Count);
        Assert.Single(result.RecentActivity);
    }
}
