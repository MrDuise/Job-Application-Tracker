using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Repositories;

public interface IEmailRepository
{
    Task<List<Email>> GetAllAsync();
    Task<Email?> GetByIdAsync(string id);
    Task<List<Email>> GetByApplicationIdAsync(int applicationId);
    Task<List<Email>> GetUnlinkedEmailsAsync();
    Task<List<Email>> GetByThreadIdAsync(string threadId);
    Task<Email> CreateAsync(Email email);
    Task<Email> UpdateAsync(Email email);
    Task LinkToApplicationAsync(string emailId, int applicationId);
    Task MarkAsReadAsync(string emailId);
    Task DeleteAsync(string id);
    Task<bool> ExistsAsync(string emailId);
    Task<List<Email>> GetEmailsSinceAsync(DateTime since);
}
