namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for cache warmup. Cache writes/outbound fills are always blocked.
/// </summary>
public sealed class CacheWarmupDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "cache-warmup", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_keys", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_keys as cache keys/actions (one per line).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["warms"] = "0",
                    ["valid_keys"] = "0",
                    ["invalid_keys"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            if (line.Length >= 2 && !line.Contains(' ', StringComparison.Ordinal))
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
            ? $"Validated {valid} cache key(s); warm writes blocked."
            : "No valid cache keys found in sample_keys.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["warms"] = "0",
                ["valid_keys"] = valid.ToString(),
                ["invalid_keys"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample keys were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
