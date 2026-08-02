namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for UAE tax legislation cron. DB writes are always blocked.
/// </summary>
public sealed class UaeTaxLegislationDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "uae-tax-legislation", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_docs", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_docs as lines slug,title (e.g. vat-guide,VAT Guide).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["docs"] = "0",
                    ["valid_docs"] = "0",
                    ["invalid_docs"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].Length >= 2 && parts[1].Length >= 2)
            {
                valid++;
                slugs.Add(parts[0]);
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} tax legislation doc(s) / {slugs.Count} slug(s); KB writes blocked."
            : "No valid slug,title rows found in sample_docs.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["docs"] = "0",
                ["valid_docs"] = valid.ToString(),
                ["invalid_docs"] = invalid.ToString(),
                ["slugs"] = slugs.Count.ToString()
            },
            invalid > 0 ? ["Some sample docs were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
