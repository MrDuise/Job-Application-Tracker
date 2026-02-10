using JobTracker.Core.Enums;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Data;
using JobTracker.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobTracker.Tests.Integration;

public class ApplicationRepositoryTests : IDisposable
{
    private readonly JobTrackerDbContext _context;
    private readonly ApplicationRepository _repository;

    public ApplicationRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<JobTrackerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new JobTrackerDbContext(options);
        _repository = new ApplicationRepository(_context);
    }

    [Fact]
    public async Task CreateAsync_AddsApplicationToDatabase()
    {
        var app = new Application
        {
            CompanyName = "Google",
            JobTitle = "Software Engineer",
            Status = ApplicationStatus.Applied,
            AppliedDate = DateTime.UtcNow,
        };

        var result = await _repository.CreateAsync(app);

        Assert.True(result.Id > 0);
        Assert.Equal("Google", result.CompanyName);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllApplications()
    {
        await SeedApplications();

        var result = await _repository.GetAllAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectApplication()
    {
        await SeedApplications();

        var result = await _repository.GetByIdAsync(1);

        Assert.NotNull(result);
        Assert.Equal("Google", result!.CompanyName);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersCorrectly()
    {
        await SeedApplications();

        var result = await _repository.GetByStatusAsync(ApplicationStatus.Applied);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatus()
    {
        await SeedApplications();

        await _repository.UpdateStatusAsync(1, ApplicationStatus.InterviewScheduled);

        var updated = await _repository.GetByIdAsync(1);
        Assert.Equal(ApplicationStatus.InterviewScheduled, updated!.Status);
        Assert.NotNull(updated.LastUpdated);
    }

    [Fact]
    public async Task DeleteAsync_RemovesApplication()
    {
        await SeedApplications();

        await _repository.DeleteAsync(1);

        var result = await _repository.GetAllAsync();
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsCorrectCounts()
    {
        await SeedApplications();

        var result = await _repository.GetStatusCountsAsync();

        Assert.Equal(2, result[ApplicationStatus.Applied]);
        Assert.Equal(1, result[ApplicationStatus.InterviewScheduled]);
    }

    [Fact]
    public async Task FindByCompanyAndTitleAsync_FindsMatch()
    {
        await SeedApplications();

        var result = await _repository.FindByCompanyAndTitleAsync("google", "engineer");

        Assert.NotNull(result);
        Assert.Equal("Google", result!.CompanyName);
    }

    private async Task SeedApplications()
    {
        _context.Applications.AddRange(
            new Application { Id = 1, CompanyName = "Google", JobTitle = "Software Engineer", Status = ApplicationStatus.Applied, AppliedDate = DateTime.UtcNow },
            new Application { Id = 2, CompanyName = "Meta", JobTitle = "Product Manager", Status = ApplicationStatus.InterviewScheduled, AppliedDate = DateTime.UtcNow },
            new Application { Id = 3, CompanyName = "Amazon", JobTitle = "SDE", Status = ApplicationStatus.Applied, AppliedDate = DateTime.UtcNow }
        );
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
