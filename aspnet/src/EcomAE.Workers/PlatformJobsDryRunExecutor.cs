namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for platform-jobs cron replacement. Claim/complete writes are always blocked.
/// </summary>
public sealed class PlatformJobsDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "platform-jobs", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_jobs", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_jobs as lines job_type,status (e.g. seo_warm,queued).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["claims"] = "0",
                    ["queued"] = "0",
                    ["running"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var queued = 0;
        var running = 0;
        var other = 0;
        var invalid = 0;
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
            {
                types.Add(parts[0]);
                if (string.Equals(parts[1], "queued", StringComparison.OrdinalIgnoreCase))
                {
                    queued++;
                }
                else if (string.Equals(parts[1], "running", StringComparison.OrdinalIgnoreCase))
                {
                    running++;
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

        var valid = queued + running + other;
        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Previewed {queued} queued / {running} running job row(s) across {types.Count} type(s); claim/complete blocked."
            : "No valid job_type,status rows found in sample_jobs.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["claims"] = "0",
                ["queued"] = queued.ToString(),
                ["running"] = running.ToString(),
                ["other"] = other.ToString(),
                ["job_types"] = types.Count.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample job rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
