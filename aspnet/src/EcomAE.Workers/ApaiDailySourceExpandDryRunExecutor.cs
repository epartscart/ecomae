namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for APAI daily source expand. Expand writes are always blocked.
/// </summary>
public sealed class ApaiDailySourceExpandDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "apai-daily-source-expand", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_expansions", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_expansions as lines source,depth (e.g. brands,2).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["expansions"] = "0",
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
            if (parts.Length >= 2 && parts[0].Length > 0 && int.TryParse(parts[1], out var depth) && depth >= 0)
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
            ? $"Validated {valid} expansion target(s); writes blocked."
            : "No valid source,depth rows found in sample_expansions.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["expansions"] = "0",
                ["valid_rows"] = valid.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample expansion rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
