using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IEmailService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<bool> TestConnectionAsync(EmailAccount account);
    Task<List<Email>> FetchNewEmailsAsync();
    Task<List<Email>> FetchEmailsSinceAsync(DateTime since);
    Task<Email?> GetEmailByIdAsync(string emailId);
    Task MarkAsReadAsync(string emailId);
    void Configure(EmailAccount account);
}
