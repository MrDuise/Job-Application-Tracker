using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Services;

/// <summary>
/// Manages email sync lifecycle: prevents overlapping syncs, tracks progress,
/// and runs the heavy IMAP fetch + classification work off the HTTP request thread.
/// </summary>
public class EmailSyncOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailSyncOrchestrator> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    // Progress tracking — volatile for cross-thread visibility
    private volatile string _status = "idle";
    private volatile int _totalEmails;
    private volatile int _processedEmails;
    private volatile string? _lastError;

    public EmailSyncOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<EmailSyncOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public SyncStatusDto GetStatus() => new()
    {
        Status = _status,
        TotalEmails = _totalEmails,
        ProcessedEmails = _processedEmails,
        LastError = _lastError
    };

    public bool TryStartSync()
    {
        if (!_syncLock.Wait(0))
        {
            _logger.LogWarning("Sync already in progress, skipping");
            return false;
        }

        _status = "starting";
        _totalEmails = 0;
        _processedEmails = 0;
        _lastError = null;

        _ = Task.Run(async () =>
        {
            try
            {
                await RunSyncAsync();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _status = "error";
                _logger.LogError(ex, "Sync failed");
            }
            finally
            {
                _syncLock.Release();
            }
        });

        return true;
    }

    /// <summary>
    /// Called by EmailPollingService — uses the same lock to prevent overlap.
    /// </summary>
    public async Task RunSyncIfIdleAsync()
    {
        if (!_syncLock.Wait(0))
        {
            _logger.LogDebug("Sync already in progress, polling skipped");
            return;
        }

        try
        {
            _status = "starting";
            _totalEmails = 0;
            _processedEmails = 0;
            _lastError = null;
            await RunSyncAsync();
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _status = "error";
            _logger.LogError(ex, "Polling sync failed");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task RunSyncAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IEmailAccountRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var processingService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

        var account = await accountRepo.GetAccountAsync();
        if (account is null)
        {
            _status = "error";
            _lastError = "No email account configured";
            return;
        }

        // Refresh OAuth token if needed
        if (account.AuthType == Core.Enums.EmailAuthType.GoogleOAuth)
        {
            if (account.TokenExpiresAt.HasValue && account.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(2))
            {
                var googleOAuth = scope.ServiceProvider.GetRequiredService<IGoogleOAuthService>();
                if (!string.IsNullOrEmpty(account.EncryptedRefreshToken))
                {
                    account.AccessToken = await googleOAuth.RefreshAccessTokenAsync(account.EncryptedRefreshToken);
                    account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(3600);
                    await accountRepo.CreateOrUpdateAsync(account);
                    _logger.LogInformation("Refreshed Google OAuth access token");
                }
                else
                {
                    _status = "error";
                    _lastError = "OAuth token expired and no refresh token available";
                    return;
                }
            }
        }

        emailService.Configure(account);

        // Fetch emails
        _status = "fetching";
        var since = account.LastSyncDate ?? DateTime.UtcNow.AddDays(-548);
        _logger.LogInformation("Starting email fetch since {Since}", since);

        var emails = await emailService.FetchEmailsSinceAsync(since);
        _totalEmails = emails.Count;
        _logger.LogInformation("IMAP returned {Count} emails, starting classification", emails.Count);

        // Process in batches with progress updates
        _status = "processing";
        var skipped = 0;
        var processed = 0;
        var errors = 0;

        foreach (var email in emails)
        {
            try
            {
                if (IsObviousMarketing(email))
                {
                    skipped++;
                }
                else
                {
                    await processingService.ProcessNewEmailAsync(email);
                    processed++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to process email {EmailId}: {Subject}", email.Id, email.Subject);
            }

            _processedEmails++;

            if (_processedEmails % 50 == 0)
            {
                _logger.LogInformation(
                    "Progress: {Processed}/{Total} ({Skipped} marketing, {Errors} errors)",
                    _processedEmails, _totalEmails, skipped, errors);
            }
        }

        // Update last sync date
        account.LastSyncDate = DateTime.UtcNow;
        await accountRepo.CreateOrUpdateAsync(account);

        _status = "completed";
        _logger.LogInformation(
            "Sync complete: {Processed} processed, {Skipped} marketing skipped, {Errors} errors out of {Total} total",
            processed, skipped, errors, _totalEmails);
    }

    private static bool IsObviousMarketing(Core.Models.Email email)
    {
        var subject = email.Subject.ToLowerInvariant();
        var from = email.From.ToLowerInvariant();
        var body = email.Body.ToLowerInvariant();

        string[] marketingSenders =
        [
            "marketing@", "newsletter@", "promotions@",
            "deals@", "notifications@social", "info@linkedin.com"
        ];
        if (marketingSenders.Any(s => from.Contains(s)) && !HasJobKeyword(subject, body))
            return true;

        string[] marketingIndicators = ["unsubscribe", "view in browser", "email preferences", "opt out"];
        var marketingScore = marketingIndicators.Count(ind => body.Contains(ind));
        if (marketingScore >= 2 && !HasJobKeyword(subject, body))
            return true;

        return false;
    }

    private static bool HasJobKeyword(string subject, string body)
    {
        string[] jobKeywords =
        [
            "application", "interview", "position", "offer", "candidate",
            "applied", "rejected", "opportunity", "role", "hiring",
            "thank you for applying", "your application", "we received your",
            "application received", "application submitted", "application confirmed",
            "candidacy", "phone screen", "on-site", "onsite", "offer letter"
        ];
        var bodySnippet = body.Length > 500 ? body[..500] : body;
        return jobKeywords.Any(k => subject.Contains(k) || bodySnippet.Contains(k));
    }
}

public class SyncStatusDto
{
    public string Status { get; set; } = "idle";
    public int TotalEmails { get; set; }
    public int ProcessedEmails { get; set; }
    public string? LastError { get; set; }
}
