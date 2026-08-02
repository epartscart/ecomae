namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for API client ping. Outbound pings are always blocked.
/// </summary>
public sealed class ApiClientPingDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "api-client-ping", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_clients", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_clients as lines client_id,endpoint (e.g. 7,/health).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["pings"] = "0",
                    ["valid_rows"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].StartsWith("/", StringComparison.Ordinal))
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
            ? $"Validated {valid} client ping target(s); outbound pings blocked."
            : "No valid client_id,endpoint rows found in sample_clients.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["pings"] = "0",
                ["valid_rows"] = valid.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample client rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}