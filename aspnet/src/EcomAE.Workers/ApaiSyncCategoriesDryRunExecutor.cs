namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for APAI category sync. Category writes are always blocked.
/// </summary>
public sealed class ApaiSyncCategoriesDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "apai-sync-categories", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_categories", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_categories as lines category_id,name (e.g. 10,Brakes).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["synced"] = "0",
                    ["valid_categories"] = "0",
                    ["invalid_categories"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && int.TryParse(parts[0], out var id) && id > 0 && parts[1].Length > 0)
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
            ? $"Validated {valid} APAI category row(s); sync writes blocked."
            : "No valid category_id,name rows found in sample_categories.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["synced"] = "0",
                ["valid_categories"] = valid.ToString(),
                ["invalid_categories"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample categories were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
