namespace EcomAE.Workers;

public sealed record ZeroPhpBatchOneWorkerDryRunExecutionItem(
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    string PlannerMessage);

public sealed record ZeroPhpBatchOneWorkerDryRunExecutionReport(
    int BatchNumber,
    int TotalItems,
    int PlannedItems,
    bool ExactRouteOnly,
    bool PhpFallbackRequired,
    IReadOnlyCollection<ZeroPhpBatchOneWorkerDryRunExecutionItem> Items);

public sealed class ZeroPhpBatchOneWorkerDryRunExecutor
{
    private readonly ZeroPhpBatchOneWorkerDryRunEvidenceManifest _manifest;
    private readonly ZeroPhpBatchOneWorkerReplacementRunner _runner;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpBatchOneWorkerDryRunExecutor(
        ZeroPhpBatchOneWorkerDryRunEvidenceManifest manifest,
        ZeroPhpBatchOneWorkerReplacementRunner runner,
        TimeProvider timeProvider)
    {
        _manifest = manifest;
        _runner = runner;
        _timeProvider = timeProvider;
    }

    public ZeroPhpBatchOneWorkerDryRunExecutionReport PlanAll(string requestedBy)
    {
        var requestedAt = _timeProvider.GetUtcNow();
        var items = _manifest.BuildManifest()
            .Select(evidence => BuildItem(evidence, requestedAt, requestedBy))
            .ToArray();

        return new ZeroPhpBatchOneWorkerDryRunExecutionReport(
            ZeroPhpBatchOneWorkerReplacementCatalog.BatchNumber,
            items.Length,
            items.Count(item => string.Equals(item.Status, "dry-run-planned", StringComparison.OrdinalIgnoreCase)),
            ExactRouteOnly: true,
            ZeroPhpBatchOneWorkerReplacementCatalog.PhpFallbackRequired,
            items);
    }

    private ZeroPhpBatchOneWorkerDryRunExecutionItem BuildItem(
        ZeroPhpBatchOneWorkerDryRunEvidence evidence,
        DateTimeOffset requestedAt,
        string requestedBy)
    {
        var result = _runner.PlanRun(new ZeroPhpBatchOneWorkerReplacementRunRequest(
            evidence.LegacyPhpEntry,
            requestedAt,
            requestedBy,
            DryRun: true));

        return new ZeroPhpBatchOneWorkerDryRunExecutionItem(
            evidence.LegacyPhpEntry,
            result.Status,
            evidence.DryRunCommand,
            evidence.ParitySamplePath,
            evidence.RollbackCommand,
            result.Message);
    }
}
