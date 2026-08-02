namespace EcomAE.Workers;

public sealed class PriceImportDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    private static readonly string[] RequiredColumns = ["sku", "price", "currency"];

    public bool CanExecute(MigrationWorkerJob job)
    {
        return string.Equals(job.Key, "price-import", StringComparison.OrdinalIgnoreCase);
    }

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>();
        parameters.TryGetValue("sample_csv", out var sampleCsv);

        if (string.IsNullOrWhiteSpace(sampleCsv))
        {
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                "Price import dry run is implemented but requires a sample_csv parameter captured from the PHP importer baseline.",
                new Dictionary<string, string>
                {
                    ["rows_read"] = "0",
                    ["valid_rows"] = "0",
                    ["invalid_rows"] = "0",
                    ["writes"] = "0"
                },
                ["Provide sample_csv with sku,price,currency columns before parity comparison."],
                WritesBlocked: true);
        }

        var lines = sampleCsv.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            return EmptyInput(job.Key, "sample_csv did not contain any rows.");
        }

        var headers = SplitCsvLine(lines[0]);
        var missingColumns = RequiredColumns.Where(column => !headers.Contains(column, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (missingColumns.Length > 0)
        {
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-invalid-schema",
                $"Price import sample is missing required column(s): {string.Join(", ", missingColumns)}.",
                new Dictionary<string, string>
                {
                    ["rows_read"] = Math.Max(0, lines.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["valid_rows"] = "0",
                    ["invalid_rows"] = Math.Max(0, lines.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["writes"] = "0"
                },
                [$"Missing required column(s): {string.Join(", ", missingColumns)}."],
                WritesBlocked: true);
        }

        var skuIndex = Array.FindIndex(headers, item => string.Equals(item, "sku", StringComparison.OrdinalIgnoreCase));
        var priceIndex = Array.FindIndex(headers, item => string.Equals(item, "price", StringComparison.OrdinalIgnoreCase));
        var currencyIndex = Array.FindIndex(headers, item => string.Equals(item, "currency", StringComparison.OrdinalIgnoreCase));
        var validRows = 0;
        var invalidRows = 0;
        var currencies = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines.Skip(1))
        {
            var cells = SplitCsvLine(line);
            if (cells.Length <= Math.Max(skuIndex, Math.Max(priceIndex, currencyIndex))
                || string.IsNullOrWhiteSpace(cells[skuIndex])
                || !decimal.TryParse(cells[priceIndex], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var price)
                || price < 0
                || string.IsNullOrWhiteSpace(cells[currencyIndex]))
            {
                invalidRows++;
                continue;
            }

            validRows++;
            currencies.Add(cells[currencyIndex].Trim().ToUpperInvariant());
        }

        var status = invalidRows == 0 && validRows > 0 ? "dry-run-validated" : "dry-run-needs-review";
        var warnings = invalidRows == 0
            ? Array.Empty<string>()
            : [$"{invalidRows} row(s) need review before shadow parity can be accepted."];

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            $"Validated {validRows} price row(s) from the PHP baseline sample with {invalidRows} invalid row(s); no database writes were performed.",
            new Dictionary<string, string>
            {
                ["rows_read"] = Math.Max(0, lines.Length - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["valid_rows"] = validRows.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["invalid_rows"] = invalidRows.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["currencies"] = string.Join(",", currencies),
                ["writes"] = "0"
            },
            warnings,
            WritesBlocked: true);
    }

    private static MigrationWorkerJobDryRunOutput EmptyInput(string jobKey, string warning)
    {
        return new MigrationWorkerJobDryRunOutput(
            jobKey,
            "dry-run-needs-sample",
            warning,
            new Dictionary<string, string>
            {
                ["rows_read"] = "0",
                ["valid_rows"] = "0",
                ["invalid_rows"] = "0",
                ["writes"] = "0"
            },
            [warning],
            WritesBlocked: true);
    }

    private static string[] SplitCsvLine(string line)
    {
        return line.Split(',', StringSplitOptions.TrimEntries);
    }
}
