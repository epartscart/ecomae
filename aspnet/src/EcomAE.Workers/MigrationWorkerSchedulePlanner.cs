namespace EcomAE.Workers;

public sealed class MigrationWorkerSchedulePlanner : IMigrationWorkerSchedulePlanner
{
    private readonly MigrationWorkerJobCatalog _catalog;

    public MigrationWorkerSchedulePlanner(MigrationWorkerJobCatalog catalog)
    {
        _catalog = catalog;
    }

    public MigrationWorkerJobSchedulePlan BuildPlan(string jobKey)
    {
        var job = _catalog.Jobs.FirstOrDefault(item => string.Equals(item.Key, jobKey, StringComparison.OrdinalIgnoreCase));

        if (job is null)
        {
            return new MigrationWorkerJobSchedulePlan(
                jobKey,
                "unknown",
                $"ecomae:worker:{Normalize(jobKey)}",
                "none",
                RequiresDistributedLock: true,
                ReadyForExecution: false,
                ReadinessReason: "No planned PHP job replacement is registered for this key.");
        }

        return new MigrationWorkerJobSchedulePlan(
            job.Key,
            job.Schedule,
            $"ecomae:worker:{job.Key}",
            ChooseRetryPolicy(job.Schedule),
            RequiresDistributedLock: true,
            ReadyForExecution: false,
            ReadinessReason: "Schedule is cataloged, but execution waits for a concrete worker implementation, distributed lock storage, retry telemetry, and production enablement.");
    }

    private static string ChooseRetryPolicy(string schedule)
    {
        return schedule switch
        {
            "queue-driven" => "exponential-backoff-with-dead-letter",
            "supplier-triggered and scheduled" => "bounded-retry-with-import-audit",
            "daily" => "daily-window-retry",
            "scheduled" => "scheduled-window-retry",
            _ => "manual-review"
        };
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
    }
}
