namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for offline resilience warm. HTTP/cache writes are always blocked.
/// </summary>
public sealed class OfflineResilienceWarmDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "offline-resilience-warm", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_targets", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_targets as warm targets (one URL/key per line).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["warms"] = "0",
                    ["valid_targets"] = "0",
                    ["invalid_targets"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            if (line.Length >= 3 && !line.Contains(' ', StringComparison.Ordinal))
            {
                valid++;
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} warm target(s); warm fetches/writes blocked."
            : "No valid warm targets found in sample_targets.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["warms"] = "0",
                ["valid_targets"] = valid.ToString(),
                ["invalid_targets"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample targets were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}