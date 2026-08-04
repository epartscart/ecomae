using EcomAE.Workers.Observability;

namespace EcomAE.Workers;

public sealed class MigrationWorkerPlaceholder : BackgroundService
{
    private readonly ILogger<MigrationWorkerPlaceholder> _logger;
    private readonly MigrationWorkerJobCatalog _jobs;
    private readonly IMigrationWorkerBatchDryRunReporter _batchDryRunReporter;
    private readonly TimeProvider _timeProvider;

    public MigrationWorkerPlaceholder(
        ILogger<MigrationWorkerPlaceholder> logger,
        MigrationWorkerJobCatalog jobs,
        IMigrationWorkerBatchDryRunReporter batchDryRunReporter,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _jobs = jobs;
        _batchDryRunReporter = batchDryRunReporter;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = EcomAeWorkerActivitySources.Workers.StartActivity("workers.batch-dry-run.startup");
        activity?.SetTag("ecomae.workers.mode", "dry-run");

        var report = _batchDryRunReporter.BuildReport(_timeProvider.GetUtcNow(), "worker-host-startup");
        activity?.SetTag("ecomae.workers.dry_run_ready_jobs", report.DryRunEvidenceReadyJobs);

        _logger.LogInformation(
            "ECOM AE worker migration placeholder started with {JobCount} planned PHP job replacements: {JobKeys}",
            _jobs.Jobs.Count,
            string.Join(", ", _jobs.Jobs.Select(job => job.Key)));

        _logger.LogInformation(
            "Batch worker dry-run report {BatchKey}: {Status}; {DryRunEvidenceReadyJobs}/{TotalJobs} jobs have dry-run evidence; PHP fallback required: {PhpFallbackRequired}; blockers: {RemainingBlockers}",
            report.BatchKey,
            report.Status,
            report.DryRunEvidenceReadyJobs,
            report.TotalJobs,
            report.PhpFallbackRequired,
            string.Join(" | ", report.RemainingBlockers));

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
