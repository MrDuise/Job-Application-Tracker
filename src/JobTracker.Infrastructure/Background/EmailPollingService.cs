using JobTracker.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobTracker.Infrastructure.Background;

public class EmailPollingService : BackgroundService
{
    private readonly EmailSyncOrchestrator _orchestrator;
    private readonly ILogger<EmailPollingService> _logger;
    private readonly int _intervalMinutes;
    private readonly bool _enabled;

    public EmailPollingService(
        EmailSyncOrchestrator orchestrator,
        IConfiguration configuration,
        ILogger<EmailPollingService> logger)
    {
        _orchestrator = orchestrator;
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
                await _orchestrator.RunSyncIfIdleAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during email polling");
            }

            await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
        }
    }
}
