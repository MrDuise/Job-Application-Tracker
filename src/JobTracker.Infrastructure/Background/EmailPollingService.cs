using JobTracker.Core.Enums;
using JobTracker.Core.Interfaces.Repositories;
using JobTracker.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Background;

public class EmailPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailPollingService> _logger;
    private readonly int _intervalMinutes;
    private readonly bool _enabled;

    public EmailPollingService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<EmailPollingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _intervalMinutes = int.Parse(configuration["EmailPolling:IntervalMinutes"] ?? "5");
        _enabled = bool.Parse(configuration["EmailPolling:Enabled"] ?? "true");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Email polling is disabled");
            return;
        }

        _logger.LogInformation("Email polling service started with {Interval} minute interval", _intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollEmailsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email polling");
            }

            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }
    }

    private async Task PollEmailsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IEmailAccountRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var processingService = scope.ServiceProvider.GetRequiredService<IEmailProcessingService>();

        var account = await accountRepo.GetAccountAsync();
        if (account is null)
        {
            _logger.LogDebug("No email account configured, skipping poll");
            return;
        }

        // Refresh OAuth token if needed
        if (account.AuthType == EmailAuthType.GoogleOAuth)
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
                    _logger.LogWarning("OAuth token expired and no refresh token available, skipping poll");
                    return;
                }
            }
        }

        emailService.Configure(account);

        var since = account.LastSyncDate ?? DateTime.UtcNow.AddDays(-548);
        var emails = await emailService.FetchEmailsSinceAsync(since);

        if (emails.Count > 0)
        {
            _logger.LogInformation("Processing {Count} new emails", emails.Count);
            await processingService.ProcessEmailBatchAsync(emails);
        }

        account.LastSyncDate = DateTime.UtcNow;
        await accountRepo.CreateOrUpdateAsync(account);
    }
}
