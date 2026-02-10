using JobTracker.Core.Enums;
using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Repositories;

public interface IApplicationRepository
{
    Task<List<Application>> GetAllAsync();
    Task<Application?> GetByIdAsync(int id);
    Task<List<Application>> GetByStatusAsync(ApplicationStatus status);
    Task<Application?> FindByCompanyAndTitleAsync(string companyName, string jobTitle);
    Task<Application> CreateAsync(Application application);
    Task<Application> UpdateAsync(Application application);
    Task UpdateStatusAsync(int id, ApplicationStatus status);
    Task DeleteAsync(int id);
    Task<Dictionary<ApplicationStatus, int>> GetStatusCountsAsync();
    Task<List<Application>> GetRecentAsync(int count);
}
