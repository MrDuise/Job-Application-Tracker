using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Models;
using JobTracker.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Repositories;

public class EmailRepository : IEmailRepository
{
    private readonly JobTrackerDbContext _context;

    public EmailRepository(JobTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<List<Email>> GetAllAsync()
    {
        return await _context.Emails
            .Include(e => e.Application)
            .OrderByDescending(e => e.ReceivedDate)
            .ToListAsync();
    }

    public async Task<Email?> GetByIdAsync(string id)
    {
        return await _context.Emails
            .Include(e => e.Application)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<Email>> GetByApplicationIdAsync(int applicationId)
    {
        return await _context.Emails
            .Where(e => e.ApplicationId == applicationId)
            .OrderByDescending(e => e.ReceivedDate)
            .ToListAsync();
    }

    public async Task<List<Email>> GetUnlinkedEmailsAsync()
    {
        return await _context.Emails
            .Where(e => e.ApplicationId == null)
            .OrderByDescending(e => e.ReceivedDate)
            .ToListAsync();
    }

    public async Task<List<Email>> GetByThreadIdAsync(string threadId)
    {
        return await _context.Emails
            .Where(e => e.ThreadId == threadId)
            .OrderBy(e => e.ReceivedDate)
            .ToListAsync();
    }

    public async Task<Email> CreateAsync(Email email)
    {
        _context.Emails.Add(email);
        await _context.SaveChangesAsync();
        return email;
    }

    public async Task<Email> UpdateAsync(Email email)
    {
        _context.Emails.Update(email);
        await _context.SaveChangesAsync();
        return email;
    }

    public async Task LinkToApplicationAsync(string emailId, int applicationId)
    {
        var email = await _context.Emails.FindAsync(emailId);
        if (email is null)
            throw new KeyNotFoundException($"Email with ID {emailId} not found.");

        email.ApplicationId = applicationId;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(string emailId)
    {
        var email = await _context.Emails.FindAsync(emailId);
        if (email is null)
            throw new KeyNotFoundException($"Email with ID {emailId} not found.");

        email.IsRead = true;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(string id)
    {
        var email = await _context.Emails.FindAsync(id);
        if (email is null)
            throw new KeyNotFoundException($"Email with ID {id} not found.");

        _context.Emails.Remove(email);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string emailId)
    {
        return await _context.Emails.AnyAsync(e => e.Id == emailId);
    }

    public async Task<List<Email>> GetEmailsSinceAsync(DateTime since)
    {
        return await _context.Emails
            .Where(e => e.ReceivedDate >= since)
            .OrderByDescending(e => e.ReceivedDate)
            .ToListAsync();
    }
}
