namespace EcomAE.Workers;

public sealed class MigrationWorkerJobRunner : IMigrationWorkerJobRunner
{
    private readonly MigrationWorkerJobCatalog _catalog;
    private readonly TimeProvider _timeProvider;

    public MigrationWorkerJobRunner(MigrationWorkerJobCatalog catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
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

        return new MigrationWorkerJobRunResult(
            job.Key,
            status,
            request.DryRun,
            message,
            request.RequestedAt,
            completedAt);
    }
}
