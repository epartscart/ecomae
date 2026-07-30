namespace EcomAE.Workers;

public sealed class MigrationWorkerPlaceholder : BackgroundService
{
    private readonly ILogger<MigrationWorkerPlaceholder> _logger;
    private readonly MigrationWorkerJobCatalog _jobs;

    public MigrationWorkerPlaceholder(ILogger<MigrationWorkerPlaceholder> logger, MigrationWorkerJobCatalog jobs)
    {
        _logger = logger;
        _jobs = jobs;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ECOM AE worker migration placeholder started with {JobCount} planned PHP job replacements: {JobKeys}", _jobs.Jobs.Count, string.Join(", ", _jobs.Jobs.Select(job => job.Key)));
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
