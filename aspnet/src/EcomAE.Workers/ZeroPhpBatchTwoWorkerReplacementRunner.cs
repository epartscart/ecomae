namespace EcomAE.Workers;

public sealed record ZeroPhpBatchTwoWorkerReplacementRunRequest(
    string LegacyPhpEntry,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    bool DryRun = true);

public sealed record ZeroPhpBatchTwoWorkerReplacementRunResult(
    string LegacyPhpEntry,
    string Status,
    bool DryRun,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt,
    string RequiredEvidence);

public sealed class ZeroPhpBatchTwoWorkerReplacementRunner
{
    private readonly ZeroPhpBatchTwoWorkerReplacementCatalog _catalog;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpBatchTwoWorkerReplacementRunner(ZeroPhpBatchTwoWorkerReplacementCatalog catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public ZeroPhpBatchTwoWorkerReplacementRunResult PlanRun(ZeroPhpBatchTwoWorkerReplacementRunRequest request)
    {
        var completedAt = _timeProvider.GetUtcNow();
        var replacement = _catalog.Replacements.FirstOrDefault(item => string.Equals(item.LegacyPhpEntry, request.LegacyPhpEntry, StringComparison.OrdinalIgnoreCase));

        if (replacement is null)
        {
            return new ZeroPhpBatchTwoWorkerReplacementRunResult(
                request.LegacyPhpEntry,
                "not-found",
                request.DryRun,
                $"No batch 2 worker replacement is registered for '{request.LegacyPhpEntry}'.",
                request.RequestedAt,
                completedAt,
                "Add the legacy entry to an exact-route batch before planning execution.");
        }

        if (!request.DryRun)
        {
            return new ZeroPhpBatchTwoWorkerReplacementRunResult(
                replacement.LegacyPhpEntry,
                "manual-approval-required",
                request.DryRun,
                $"Execution is blocked for {replacement.LegacyPhpEntry}; batch 2 currently allows dry-run planning only and requires PHP fallback.",
                request.RequestedAt,
                completedAt,
                replacement.RequiredEvidence);
        }

        return new ZeroPhpBatchTwoWorkerReplacementRunResult(
            replacement.LegacyPhpEntry,
            "dry-run-planned",
            request.DryRun,
            $"Dry run accepted for batch {ZeroPhpBatchTwoWorkerReplacementCatalog.BatchNumber} {replacement.TargetSlice} replacement; keep {ZeroPhpBatchTwoWorkerReplacementCatalog.CutoverMode} and PHP fallback until parity evidence passes.",
            request.RequestedAt,
            completedAt,
            replacement.RequiredEvidence);
    }
}
