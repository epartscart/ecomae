namespace EcomAE.Workers;

public sealed record ZeroPhpBatchTwoWorkerDryRunExecutionItem(
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    string PlannerMessage);

public sealed record ZeroPhpBatchTwoWorkerDryRunExecutionReport(
    int BatchNumber,
    int TotalItems,
    int PlannedItems,
    bool ExactRouteOnly,
    bool PhpFallbackRequired,
    IReadOnlyCollection<ZeroPhpBatchTwoWorkerDryRunExecutionItem> Items);

public sealed class ZeroPhpBatchTwoWorkerDryRunExecutor
{
    private readonly ZeroPhpBatchTwoWorkerDryRunEvidenceManifest _manifest;
    private readonly ZeroPhpBatchTwoWorkerReplacementRunner _runner;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpBatchTwoWorkerDryRunExecutor(
        ZeroPhpBatchTwoWorkerDryRunEvidenceManifest manifest,
        ZeroPhpBatchTwoWorkerReplacementRunner runner,
        TimeProvider timeProvider)
    {
        _manifest = manifest;
        _runner = runner;
        _timeProvider = timeProvider;
    }

    public ZeroPhpBatchTwoWorkerDryRunExecutionReport PlanAll(string requestedBy)
    {
        var requestedAt = _timeProvider.GetUtcNow();
        var items = _manifest.BuildManifest()
            .Select(evidence => BuildItem(evidence, requestedAt, requestedBy))
            .ToArray();

        return new ZeroPhpBatchTwoWorkerDryRunExecutionReport(
            ZeroPhpBatchTwoWorkerReplacementCatalog.BatchNumber,
            items.Length,
            items.Count(item => string.Equals(item.Status, "dry-run-planned", StringComparison.OrdinalIgnoreCase)),
            ExactRouteOnly: true,
            ZeroPhpBatchTwoWorkerReplacementCatalog.PhpFallbackRequired,
            items);
    }

    private ZeroPhpBatchTwoWorkerDryRunExecutionItem BuildItem(
        ZeroPhpBatchTwoWorkerDryRunEvidence evidence,
        DateTimeOffset requestedAt,
        string requestedBy)
    {
        var result = _runner.PlanRun(new ZeroPhpBatchTwoWorkerReplacementRunRequest(
            evidence.LegacyPhpEntry,
            requestedAt,
            requestedBy,
            DryRun: true));

        return new ZeroPhpBatchTwoWorkerDryRunExecutionItem(
            evidence.LegacyPhpEntry,
            result.Status,
            evidence.DryRunCommand,
            evidence.ParitySamplePath,
            evidence.RollbackCommand,
            result.Message);
    }
}
