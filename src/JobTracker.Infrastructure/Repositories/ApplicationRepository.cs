using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly JobTrackerDbContext _context;

    public ApplicationRepository(JobTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<List<Application>> GetAllAsync()
    {
        return await _context.Applications
            .Include(a => a.RelatedEmails)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync();
    }

    public async Task<Application?> GetByIdAsync(int id)
    {
        return await _context.Applications
            .Include(a => a.RelatedEmails)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Application>> GetByStatusAsync(ApplicationStatus status)
    {
        return await _context.Applications
            .Include(a => a.RelatedEmails)
            .Where(a => a.Status == status)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync();
    }

    public async Task<Application?> FindByCompanyAndTitleAsync(string companyName, string jobTitle)
    {
        return await _context.Applications
            .FirstOrDefaultAsync(a =>
                a.CompanyName.ToLower().Contains(companyName.ToLower()) ||
                a.JobTitle.ToLower().Contains(jobTitle.ToLower()));
    }

    public async Task<Application> CreateAsync(Application application)
    {
        _context.Applications.Add(application);
        await _context.SaveChangesAsync();
        return application;
    }

    public async Task<Application> UpdateAsync(Application application)
    {
        application.LastUpdated = DateTime.UtcNow;
        _context.Applications.Update(application);
        await _context.SaveChangesAsync();
        return application;
    }

    public async Task UpdateStatusAsync(int id, ApplicationStatus status)
    {
        var application = await _context.Applications.FindAsync(id);
        if (application is null)
            throw new KeyNotFoundException($"Application with ID {id} not found.");

        application.Status = status;
        application.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var application = await _context.Applications.FindAsync(id);
        if (application is null)
            throw new KeyNotFoundException($"Application with ID {id} not found.");

        _context.Applications.Remove(application);
        await _context.SaveChangesAsync();
    }

    public async Task<Dictionary<ApplicationStatus, int>> GetStatusCountsAsync()
    {
        return await _context.Applications
            .GroupBy(a => a.Status)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<List<Application>> GetRecentAsync(int count)
    {
        return await _context.Applications
            .OrderByDescending(a => a.LastUpdated ?? a.AppliedDate)
            .Take(count)
            .ToListAsync();
    }
}
