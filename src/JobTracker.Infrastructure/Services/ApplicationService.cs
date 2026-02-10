using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;

namespace JobTracker.Infrastructure.Services;

public class ApplicationService : IApplicationService
{
    private readonly IApplicationRepository _applicationRepo;

    public ApplicationService(IApplicationRepository applicationRepo)
    {
        _applicationRepo = applicationRepo;
    }

    public async Task<List<ApplicationDto>> GetAllApplicationsAsync()
    {
        var applications = await _applicationRepo.GetAllAsync();
        return applications.Select(MapToDto).ToList();
    }

    public async Task<ApplicationDto?> GetApplicationByIdAsync(int id)
    {
        var application = await _applicationRepo.GetByIdAsync(id);
        return application is null ? null : MapToDto(application);
    }

    public async Task<List<ApplicationDto>> GetApplicationsByStatusAsync(ApplicationStatus status)
    {
        var applications = await _applicationRepo.GetByStatusAsync(status);
        return applications.Select(MapToDto).ToList();
    }

    public async Task<ApplicationDto> CreateApplicationAsync(CreateApplicationDto dto)
    {
        var application = new Application
        {
            CompanyName = dto.CompanyName,
            JobTitle = dto.JobTitle,
            Status = dto.Status,
            AppliedDate = dto.AppliedDate ?? DateTime.UtcNow,
            Notes = dto.Notes
        };

        var created = await _applicationRepo.CreateAsync(application);
        return MapToDto(created);
    }

    public async Task<ApplicationDto> UpdateApplicationAsync(int id, UpdateApplicationDto dto)
    {
        var application = await _applicationRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Application with ID {id} not found.");

        if (dto.CompanyName is not null) application.CompanyName = dto.CompanyName;
        if (dto.JobTitle is not null) application.JobTitle = dto.JobTitle;
        if (dto.Status.HasValue) application.Status = dto.Status.Value;
        if (dto.Notes is not null) application.Notes = dto.Notes;

        var updated = await _applicationRepo.UpdateAsync(application);
        return MapToDto(updated);
    }

    public async Task UpdateApplicationStatusAsync(int id, ApplicationStatus status)
    {
        await _applicationRepo.UpdateStatusAsync(id, status);
    }

    public async Task AddNoteToApplicationAsync(int id, string note)
    {
        var application = await _applicationRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Application with ID {id} not found.");

        application.Notes = string.IsNullOrEmpty(application.Notes)
            ? note
            : $"{application.Notes}\n---\n{note}";

        await _applicationRepo.UpdateAsync(application);
    }

    public async Task DeleteApplicationAsync(int id)
    {
        await _applicationRepo.DeleteAsync(id);
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync()
    {
        var statusCounts = await _applicationRepo.GetStatusCountsAsync();
        var recent = await _applicationRepo.GetRecentAsync(10);

        return new DashboardStatsDto
        {
            TotalApplications = statusCounts.Values.Sum(),
            StatusCounts = statusCounts.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
            RecentActivity = recent.Select(a => new RecentActivityDto
            {
                ApplicationId = a.Id,
                CompanyName = a.CompanyName,
                JobTitle = a.JobTitle,
                Status = a.Status.ToString(),
                Date = a.LastUpdated ?? a.AppliedDate
            }).ToList()
        };
    }

    private static ApplicationDto MapToDto(Application app)
    {
        return new ApplicationDto
        {
            Id = app.Id,
            CompanyName = app.CompanyName,
            JobTitle = app.JobTitle,
            Status = app.Status,
            AppliedDate = app.AppliedDate,
            LastUpdated = app.LastUpdated,
            Notes = app.Notes,
            EmailCount = app.RelatedEmails?.Count ?? 0
        };
    }
}
