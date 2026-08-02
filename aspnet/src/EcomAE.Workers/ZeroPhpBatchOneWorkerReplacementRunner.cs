namespace EcomAE.Workers;

public sealed record ZeroPhpBatchOneWorkerReplacementRunRequest(
    string LegacyPhpEntry,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    bool DryRun = true);

public sealed record ZeroPhpBatchOneWorkerReplacementRunResult(
    string LegacyPhpEntry,
    string Status,
    bool DryRun,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt,
    string RequiredEvidence);

public sealed class ZeroPhpBatchOneWorkerReplacementRunner
{
    private readonly ZeroPhpBatchOneWorkerReplacementCatalog _catalog;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpBatchOneWorkerReplacementRunner(ZeroPhpBatchOneWorkerReplacementCatalog catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public ZeroPhpBatchOneWorkerReplacementRunResult PlanRun(ZeroPhpBatchOneWorkerReplacementRunRequest request)
    {
        var completedAt = _timeProvider.GetUtcNow();
        var replacement = _catalog.Replacements.FirstOrDefault(item => string.Equals(item.LegacyPhpEntry, request.LegacyPhpEntry, StringComparison.OrdinalIgnoreCase));

        if (replacement is null)
        {
            return new ZeroPhpBatchOneWorkerReplacementRunResult(
                request.LegacyPhpEntry,
                "not-found",
                request.DryRun,
                $"No batch 1 worker replacement is registered for '{request.LegacyPhpEntry}'.",
                request.RequestedAt,
                completedAt,
                "Add the legacy entry to an exact-route batch before planning execution.");
        }

        if (!request.DryRun)
        {
            return new ZeroPhpBatchOneWorkerReplacementRunResult(
                replacement.LegacyPhpEntry,
                "manual-approval-required",
                request.DryRun,
                $"Execution is blocked for {replacement.LegacyPhpEntry}; batch 1 currently allows dry-run planning only and requires PHP fallback.",
                request.RequestedAt,
                completedAt,
                replacement.RequiredEvidence);
        }

        return new ZeroPhpBatchOneWorkerReplacementRunResult(
            replacement.LegacyPhpEntry,
            "dry-run-planned",
            request.DryRun,
            $"Dry run accepted for batch {ZeroPhpBatchOneWorkerReplacementCatalog.BatchNumber} {replacement.TargetSlice} replacement; keep {ZeroPhpBatchOneWorkerReplacementCatalog.CutoverMode} and PHP fallback until parity evidence passes.",
            request.RequestedAt,
            completedAt,
            replacement.RequiredEvidence);
    }
}
