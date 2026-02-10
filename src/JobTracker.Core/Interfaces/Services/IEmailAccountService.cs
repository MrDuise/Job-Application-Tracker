using JobTracker.Core.DTOs;
using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IEmailAccountService
{
    Task<EmailAccount?> GetAccountAsync();
    Task<EmailAccount> CreateOrUpdateAccountAsync(EmailAccountDto dto);
    Task<bool> TestConnectionAsync(EmailAccountDto dto);
    Task<bool> HasAccountConfiguredAsync();
    Task DeleteAccountAsync();
}
