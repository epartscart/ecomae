namespace EcomAE.Workers;

public sealed class MigrationWorkerPlaceholder : BackgroundService
{
    private readonly ILogger<MigrationWorkerPlaceholder> _logger;

    public MigrationWorkerPlaceholder(ILogger<MigrationWorkerPlaceholder> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ECOM AE worker migration placeholder started. Future jobs: price import, sitemap, notifications, backups, ERP scheduled reports.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
