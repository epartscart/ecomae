namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for ERP fulfillment queue replacement. Claim/complete writes are always blocked.
/// </summary>
public sealed class FulfillmentQueueDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "fulfillment-queue", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_orders", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_orders as lines order_id,status (e.g. 1001,queued).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["claims"] = "0",
                    ["queued"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var queued = 0;
        var other = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && long.TryParse(parts[0], out _) && parts[1].Length > 0)
            {
                if (string.Equals(parts[1], "queued", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[1], "pending", StringComparison.OrdinalIgnoreCase))
                {
                    queued++;
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

        var valid = queued + other;
        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Previewed {queued} queued fulfillment order(s); claim/complete blocked."
            : "No valid order_id,status rows found in sample_orders.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["claims"] = "0",
                ["queued"] = queued.ToString(),
                ["other"] = other.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample order rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
