using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Repositories;

public class EmailAccountRepository : IEmailAccountRepository
{
    private readonly JobTrackerDbContext _context;

    public EmailAccountRepository(JobTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<EmailAccount?> GetAccountAsync()
    {
        return await _context.EmailAccounts.FirstOrDefaultAsync();
    }

    public async Task<EmailAccount> CreateOrUpdateAsync(EmailAccount account)
    {
        var existing = await _context.EmailAccounts.FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.EmailAddress = account.EmailAddress;
            existing.ImapServer = account.ImapServer;
            existing.ImapPort = account.ImapPort;
            existing.Username = account.Username;
            existing.EncryptedPassword = account.EncryptedPassword;
            existing.AuthType = account.AuthType;
            existing.EncryptedRefreshToken = account.EncryptedRefreshToken;
            existing.AccessToken = account.AccessToken;
            existing.TokenExpiresAt = account.TokenExpiresAt;
            _context.EmailAccounts.Update(existing);
        }
        else
        {
            _context.EmailAccounts.Add(account);
        }

        await _context.SaveChangesAsync();
        return existing ?? account;
    }

    public async Task DeleteAsync()
    {
        var account = await _context.EmailAccounts.FirstOrDefaultAsync();
        if (account is not null)
        {
            _context.EmailAccounts.Remove(account);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasAccountConfiguredAsync()
    {
        return await _context.EmailAccounts.AnyAsync();
    }
}
