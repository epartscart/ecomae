namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for demo-expire cron replacement. Deletes/reminders are always blocked.
/// </summary>
public sealed class DemoExpireDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "demo-expire", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_tenants", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_tenants as lines tenant_key,expires_unix (e.g. demo-acme,1710000000).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["deletes"] = "0",
                    ["expired_candidates"] = "0",
                    ["active_candidates"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var now = request.RequestedAt.ToUnixTimeSeconds();
        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expired = 0;
        var active = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && parts[0].Length > 0
                && long.TryParse(parts[1], out var expiresUnix)
                && expiresUnix >= 0)
            {
                if (expiresUnix > 0 && expiresUnix <= now)
                {
                    expired++;
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

        var status = expired + active > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = expired + active > 0
            ? $"Previewed {expired} expired and {active} active demo tenant(s); deletes/reminders blocked."
            : "No valid tenant,expires_unix rows found in sample_tenants.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["deletes"] = "0",
                ["expired_candidates"] = expired.ToString(),
                ["active_candidates"] = active.ToString(),
                ["invalid_rows"] = invalid.ToString(),
                ["sample_lines"] = lines.Length.ToString()
            },
            invalid > 0 ? ["Some sample tenant rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
