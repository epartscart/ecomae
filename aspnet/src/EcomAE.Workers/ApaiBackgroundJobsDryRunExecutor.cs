namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for APAI background jobs cron. Queue claims/writes are always blocked.
/// </summary>
public sealed class ApaiBackgroundJobsDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "apai-background-jobs", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_jobs", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_jobs as lines job_key,status (e.g. crawl_hourly,pending).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["claims"] = "0",
                    ["pending"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var pending = 0;
        var other = 0;
        var invalid = 0;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
            {
                keys.Add(parts[0]);
                if (string.Equals(parts[1], "pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[1], "queued", StringComparison.OrdinalIgnoreCase))
                {
                    pending++;
                }
                else
                {
                    other++;
                }
            }
            else
            {
                invalid++;
            }
        }

        var valid = pending + other;
        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Previewed {pending} pending APAI job(s) across {keys.Count} key(s); claims/writes blocked."
            : "No valid job_key,status rows found in sample_jobs.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["claims"] = "0",
                ["pending"] = pending.ToString(),
                ["other"] = other.ToString(),
                ["job_keys"] = keys.Count.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample job rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
