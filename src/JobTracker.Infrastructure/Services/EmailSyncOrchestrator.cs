using System.Threading.Channels;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Services;

/// <summary>
/// Manages email sync lifecycle: prevents overlapping syncs, tracks progress,
/// and runs IMAP fetch + classification in parallel via producer-consumer.
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
        var emailRepo = scope.ServiceProvider.GetRequiredService<IEmailRepository>();
        var processingService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

        var account = await accountRepo.GetAccountAsync();
        if (account is null)
        {
            _status = "error";
            _lastError = "No email account configured";
            return;
        }

        // Refresh OAuth token if needed
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

        emailService.Configure(account);

        // Load known sender domains from DB and wire to both services
        var knownDomains = await emailRepo.GetLinkedSenderDomainsAsync();
        emailService.SetKnownSenderDomains(knownDomains);
        processingService.SetKnownSenderDomains(knownDomains);
        _logger.LogInformation("Loaded {Count} known sender domains", knownDomains.Count);

        // Set up producer-consumer pipeline
        _status = "fetching";
        var since = account.LastSyncDate ?? DateTime.UtcNow.AddMonths(-8);
        _logger.LogInformation("Starting email fetch since {Since}", since);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        var channel = Channel.CreateBounded<Core.Models.Email>(new BoundedChannelOptions(20)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });

        var errors = 0;

        // Producer: downloads bodies, writes to channel
        var producerTask = Task.Run(async () =>
        {
            try
            {
                await emailService.FetchAndStreamEmailsAsync(
                    since,
                    channel.Writer,
                    total => _totalEmails = total,
                    cts.Token);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cts.Token);

        // Consumer: reads from channel, processes each email
        _status = "processing";
        await foreach (var email in channel.Reader.ReadAllAsync(cts.Token))
        {
            try
            {
                await processingService.ProcessNewEmailAsync(email);
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
                    "Progress: {Processed}/{Total} ({Errors} errors)",
                    _processedEmails, _totalEmails, errors);
            }
        }

        // Propagate any producer exceptions
        await producerTask;

        // Update last sync date
        account.LastSyncDate = DateTime.UtcNow;
        await accountRepo.CreateOrUpdateAsync(account);

        _status = "completed";
        _logger.LogInformation(
            "Sync complete: {Processed} processed, {Errors} errors out of {Total} total",
            _processedEmails, errors, _totalEmails);
    }
}

public class SyncStatusDto
{
    public string Status { get; set; } = "idle";
    public int TotalEmails { get; set; }
    public int ProcessedEmails { get; set; }
    public string? LastError { get; set; }
}
