namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for webhook processing. Delivery/retry sends are always blocked.
/// </summary>
public sealed class WebhooksProcessDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "webhooks-process", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_deliveries", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_deliveries as lines id,status (e.g. 12,pending).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["sends"] = "0",
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
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[0], out _) && parts[1].Length > 0)
            {
                if (string.Equals(parts[1], "pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[1], "retry", StringComparison.OrdinalIgnoreCase))
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
            ? $"Previewed {pending} pending/retry delivery(ies); sends blocked."
            : "No valid id,status rows found in sample_deliveries.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["sends"] = "0",
                ["pending"] = pending.ToString(),
                ["other"] = other.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample delivery rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
