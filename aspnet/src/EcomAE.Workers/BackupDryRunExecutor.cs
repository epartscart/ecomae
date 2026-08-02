namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for backup worker replacement. Writes/archives are always blocked.
/// </summary>
public sealed class BackupDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "backups", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        parameters.TryGetValue("retention_days", out var retentionRaw);
        parameters.TryGetValue("targets", out var targetsRaw);

        var retentionOk = int.TryParse(retentionRaw, out var retentionDays) && retentionDays > 0;
        var targets = (targetsRaw ?? string.Empty)
            .Split([',', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var knownTargets = new HashSet<string>(["database", "files", "config"], StringComparer.OrdinalIgnoreCase);
        var validTargets = targets.Count(knownTargets.Contains);
        var invalidTargets = targets.Length - validTargets;

        if (!retentionOk || validTargets == 0)
        {
            const string warning = "Provide parameters.retention_days (>0) and parameters.targets (database,files,config).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["archives_created"] = "0",
                    ["valid_targets"] = validTargets.ToString(),
                    ["invalid_targets"] = invalidTargets.ToString()
                },
                [warning],
                WritesBlocked: true);
        }

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            "dry-run-validated",
            $"Backup dry-run accepted for {validTargets} target(s), retention {retentionDays} day(s); writes blocked.",
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["archives_created"] = "0",
                ["retention_days"] = retentionDays.ToString(),
                ["valid_targets"] = validTargets.ToString(),
                ["invalid_targets"] = invalidTargets.ToString()
            },
            [],
            WritesBlocked: true);
    }
}
