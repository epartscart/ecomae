namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for product exist-limit cron. Product writes are always blocked.
/// </summary>
public sealed class ProductExistLimitDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "product-exist-limit", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_products", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_products as lines sku,qty (e.g. A-1,3).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["over_limit"] = "0",
                    ["valid_rows"] = "0",
                    ["invalid_rows"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        _ = int.TryParse(parameters.GetValueOrDefault("limit"), out var limit);
        if (limit <= 0)
        {
            limit = 1;
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var over = 0;
        var invalid = 0;
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && parts[0].Length > 0 && int.TryParse(parts[1], out var qty) && qty >= 0)
            {
                valid++;
                if (qty > limit)
                {
                    over++;
                }
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} product row(s); {over} over limit={limit}; writes blocked."
            : "No valid sku,qty rows found in sample_products.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["over_limit"] = over.ToString(),
                ["valid_rows"] = valid.ToString(),
                ["invalid_rows"] = invalid.ToString(),
                ["limit"] = limit.ToString()
            },
            invalid > 0 ? ["Some sample product rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
