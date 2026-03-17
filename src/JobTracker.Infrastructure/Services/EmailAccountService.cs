using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;

namespace JobTracker.Infrastructure.Services;

public class EmailAccountService : IEmailAccountService
{
    private readonly IEmailAccountRepository _accountRepo;

    public EmailAccountService(IEmailAccountRepository accountRepo)
    {
        _accountRepo = accountRepo;
    }

    public async Task<EmailAccount?> GetAccountAsync()
    {
        return await _accountRepo.GetAccountAsync();
    }

    public async Task<bool> HasAccountConfiguredAsync()
    {
        return await _accountRepo.HasAccountConfiguredAsync();
    }

    public async Task DeleteAccountAsync()
    {
        await _accountRepo.DeleteAsync();
    }
}
