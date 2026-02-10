using JobTracker.Core.DTOs;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using JobTracker.Core.Models;
using Microsoft.AspNetCore.DataProtection;

namespace JobTracker.Infrastructure.Services;

public class EmailAccountService : IEmailAccountService
{
    private readonly IEmailAccountRepository _accountRepo;
    private readonly IEmailService _emailService;
    private readonly IDataProtector _protector;

    public EmailAccountService(
        IEmailAccountRepository accountRepo,
        IEmailService emailService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _accountRepo = accountRepo;
        _emailService = emailService;
        _protector = dataProtectionProvider.CreateProtector("EmailAccount.Password");
    }

    public async Task<EmailAccount?> GetAccountAsync()
    {
        return await _accountRepo.GetAccountAsync();
    }

    public async Task<EmailAccount> CreateOrUpdateAccountAsync(EmailAccountDto dto)
    {
        var account = new EmailAccount
        {
            EmailAddress = dto.EmailAddress,
            ImapServer = dto.ImapServer,
            ImapPort = dto.ImapPort,
            Username = dto.Username,
            EncryptedPassword = _protector.Protect(dto.Password)
        };

        return await _accountRepo.CreateOrUpdateAsync(account);
    }

    public async Task<bool> TestConnectionAsync(EmailAccountDto dto)
    {
        var account = new EmailAccount
        {
            EmailAddress = dto.EmailAddress,
            ImapServer = dto.ImapServer,
            ImapPort = dto.ImapPort,
            Username = dto.Username,
            EncryptedPassword = dto.Password // Use raw password for testing
        };

        return await _emailService.TestConnectionAsync(account);
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
