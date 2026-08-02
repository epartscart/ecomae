namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for currency live-rates worker replacement. Writes are always blocked.
/// </summary>
public sealed class CurrencyLiveRatesDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "currency-live-rates", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_rates", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_rates as lines currency_iso,rate (e.g. USD,3.6725).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["valid_rates"] = "0",
                    ["invalid_rates"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var lines = sample.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = 0;
        var invalid = 0;
        var currencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2
                && parts[0].Length is >= 3 and <= 5
                && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var rate)
                && rate > 0)
            {
                valid++;
                currencies.Add(parts[0].ToUpperInvariant());
            }
            else
            {
                invalid++;
            }
        }

        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} currency rate row(s) for {currencies.Count} ISO code(s); writes blocked."
            : "No valid currency,rate rows found in sample_rates.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["valid_rates"] = valid.ToString(),
                ["invalid_rates"] = invalid.ToString(),
                ["currencies"] = currencies.Count.ToString(),
                ["sample_lines"] = lines.Length.ToString()
            },
            invalid > 0 ? ["Some sample rate rows were invalid and skipped."] : [],
            WritesBlocked: true);
    }
}
