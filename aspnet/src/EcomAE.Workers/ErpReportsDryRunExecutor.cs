namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for ERP report worker replacement. Report writes/delivery are blocked.
/// </summary>
public sealed class ErpReportsDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "erp-reports", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        parameters.TryGetValue("report_key", out var reportKey);
        parameters.TryGetValue("period", out var period);

        var knownReports = new HashSet<string>(["sales", "inventory", "vat", "ar-aging"], StringComparer.OrdinalIgnoreCase);
        var reportOk = !string.IsNullOrWhiteSpace(reportKey) && knownReports.Contains(reportKey);
        var periodOk = !string.IsNullOrWhiteSpace(period) && period.Contains('-', StringComparison.Ordinal);

        if (!reportOk || !periodOk)
        {
            const string warning = "Provide parameters.report_key (sales|inventory|vat|ar-aging) and parameters.period (YYYY-MM).";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["reports_generated"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            "dry-run-validated",
            $"ERP report dry-run accepted for {reportKey}/{period}; generation and delivery blocked.",
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["reports_generated"] = "0",
                ["report_key"] = reportKey!,
                ["period"] = period!
            },
            [],
            WritesBlocked: true);
    }
}
