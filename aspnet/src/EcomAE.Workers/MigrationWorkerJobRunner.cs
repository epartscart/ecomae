namespace EcomAE.Workers;

public sealed class MigrationWorkerJobRunner : IMigrationWorkerJobRunner
{
    private readonly MigrationWorkerJobCatalog _catalog;
    private readonly TimeProvider _timeProvider;
    private readonly IMigrationWorkerDryRunEvidenceProvider _evidenceProvider;
    private readonly IReadOnlyCollection<IMigrationWorkerJobDryRunExecutor> _dryRunExecutors;

    public MigrationWorkerJobRunner(MigrationWorkerJobCatalog catalog, TimeProvider timeProvider)
        : this(catalog, timeProvider, new MigrationWorkerDryRunEvidenceProvider(), [new PriceImportDryRunExecutor()])
    {
    }

    public MigrationWorkerJobRunner(
        MigrationWorkerJobCatalog catalog,
        TimeProvider timeProvider,
        IMigrationWorkerDryRunEvidenceProvider evidenceProvider)
        : this(catalog, timeProvider, evidenceProvider, [new PriceImportDryRunExecutor()])
    {
    }

    public MigrationWorkerJobRunner(
        MigrationWorkerJobCatalog catalog,
        TimeProvider timeProvider,
        IMigrationWorkerDryRunEvidenceProvider evidenceProvider,
        IEnumerable<IMigrationWorkerJobDryRunExecutor> dryRunExecutors)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
        _evidenceProvider = evidenceProvider;
        _dryRunExecutors = dryRunExecutors.ToArray();
    }

    public MigrationWorkerJobRunResult PlanRun(MigrationWorkerJobRunRequest request)
    {
        var job = _catalog.Jobs.FirstOrDefault(item => string.Equals(item.Key, request.JobKey, StringComparison.OrdinalIgnoreCase));
        var completedAt = _timeProvider.GetUtcNow();

        if (job is null)
        {
            return new MigrationWorkerJobRunResult(
                request.JobKey,
                "not-found",
                request.DryRun,
                $"No planned PHP job replacement is registered for '{request.JobKey}'.",
                request.RequestedAt,
                completedAt);
        }

        var status = request.DryRun ? "dry-run-planned" : "manual-approval-required";
        var message = request.DryRun
            ? $"Dry run accepted for {job.TargetService}; compare parity using: {job.RequiredParity}"
            : $"Execution is blocked until {job.TargetService} has a concrete implementation, retries, locks, and monitoring.";

        var evidence = request.DryRun ? _evidenceProvider.BuildEvidence(job, request) : null;
        var dryRunOutput = request.DryRun
            ? _dryRunExecutors.FirstOrDefault(executor => executor.CanExecute(job))?.Execute(job, request)
            : null;

        return new MigrationWorkerJobRunResult(
            job.Key,
            status,
            request.DryRun,
            message,
            request.RequestedAt,
            completedAt,
            evidence,
            dryRunOutput);
    }
}
