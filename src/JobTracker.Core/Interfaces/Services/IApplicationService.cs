using JobTracker.Core.DTOs;
using JobTracker.Core.Enums;

namespace JobTracker.Core.Interfaces.Services;

public interface IApplicationService
{
    Task<List<ApplicationDto>> GetAllApplicationsAsync();
    Task<ApplicationDto?> GetApplicationByIdAsync(int id);
    Task<List<ApplicationDto>> GetApplicationsByStatusAsync(ApplicationStatus status);
    Task<ApplicationDto> CreateApplicationAsync(CreateApplicationDto dto);
    Task<ApplicationDto> UpdateApplicationAsync(int id, UpdateApplicationDto dto);
    Task UpdateApplicationStatusAsync(int id, ApplicationStatus status);
    Task AddNoteToApplicationAsync(int id, string note);
    Task DeleteApplicationAsync(int id);
    Task<DashboardStatsDto> GetDashboardStatsAsync();
}
