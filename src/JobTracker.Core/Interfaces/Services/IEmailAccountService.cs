using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Services;

public interface IEmailAccountService
{
    Task<EmailAccount?> GetAccountAsync();
    Task<bool> HasAccountConfiguredAsync();
    Task DeleteAccountAsync();
}
