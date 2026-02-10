using JobTracker.Core.Models;

namespace JobTracker.Core.Interfaces.Repositories;

public interface IEmailAccountRepository
{
    Task<EmailAccount?> GetAccountAsync();
    Task<EmailAccount> CreateOrUpdateAsync(EmailAccount account);
    Task DeleteAsync();
    Task<bool> HasAccountConfiguredAsync();
}
