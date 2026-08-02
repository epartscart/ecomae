namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for integrations cleanup. Deletes are always blocked.
/// </summary>
public sealed class IntegrationsCleanupDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "integrations-cleanup", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_integrations", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_integrations as lines key,stale_days (e.g. old_feed,90).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["deletes"] = "0",
                    ["stale_candidates"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var stale = 0;
        var active = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && int.TryParse(parts[1], out var days) && days >= 0)
            {
                if (days >= 30)
                {
                    stale++;
                }
                else
                {
                    active++;
                }
            }
            else
            {
                invalid++;
            }
        }

        var valid = stale + active;
        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Previewed {stale} stale integration(s); deletes blocked."
            : "No valid key,stale_days rows found in sample_integrations.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["deletes"] = "0",
                ["stale_candidates"] = stale.ToString(),
                ["active_candidates"] = active.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample integration rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
