namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for import orchestrator. Import writes are always blocked.
/// </summary>
public sealed class ImportOrchestratorDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "import-orchestrator", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_imports", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_imports as lines source,rows (e.g. uae_csv,120).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["imports"] = "0",
                    ["valid_rows"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        var totalRows = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && int.TryParse(parts[1], out var rows) && rows >= 0)
            {
                valid++;
                totalRows += rows;
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} import source(s) totaling {totalRows} row(s); writes blocked."
            : "No valid source,rows found in sample_imports.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["imports"] = "0",
                ["valid_rows"] = valid.ToString(),
                ["preview_rows"] = totalRows.ToString(),
                ["invalid_rows"] = invalid.ToString()
            },
            invalid > 0 ? ["Some sample import rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
