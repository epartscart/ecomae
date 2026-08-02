namespace EcomAE.Workers;

public sealed record ZeroPhpCutoverBatchRunRequest(
    int BatchNumber,
    string LegacyPhpEntry,
    DateTimeOffset RequestedAt,
    string RequestedBy,
    bool DryRun = true);

public sealed record ZeroPhpCutoverBatchRunResult(
    int BatchNumber,
    string LegacyPhpEntry,
    string Status,
    bool DryRun,
    string Message,
    DateTimeOffset RequestedAt,
    DateTimeOffset CompletedAt,
    string RequiredEvidence);

public sealed record ZeroPhpCutoverBatchDryRunEvidence(
    int BatchNumber,
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    bool ExactRouteOnly,
    bool PhpFallbackRequired);

public sealed record ZeroPhpCutoverBatchDryRunExecutionItem(
    int BatchNumber,
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    string PlannerMessage);

public sealed record ZeroPhpCutoverBatchDryRunExecutionReport(
    int BatchNumber,
    int TotalItems,
    int PlannedItems,
    bool ExactRouteOnly,
    bool PhpFallbackRequired,
    IReadOnlyCollection<ZeroPhpCutoverBatchDryRunExecutionItem> Items);

public sealed record ZeroPhpCutoverBatchParityReport(
    int BatchNumber,
    string CutoverMode,
    bool PhpFallbackRequired,
    int TotalAssignments,
    int DryRunRequired,
    int ReadyForShadow,
    int ReadyForLive,
    bool ReadyToRemovePhpFallback,
    string NextAction,
    IReadOnlyCollection<string> RequiredEvidence);

public sealed record ZeroPhpCutoverFleetParityReport(
    int FirstBatch,
    int LastBatch,
    int TotalBatches,
    int TotalAssignments,
    int DryRunRequired,
    int ReadyForShadow,
    int ReadyForLive,
    bool PhpFallbackRequired,
    bool ReadyToRemovePhpFallback,
    string NextAction);

public sealed class ZeroPhpCutoverBatchRunner
{
    private readonly ZeroPhpCutoverBatchCatalog _catalog;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpCutoverBatchRunner(ZeroPhpCutoverBatchCatalog catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public ZeroPhpCutoverBatchRunResult PlanRun(ZeroPhpCutoverBatchRunRequest request)
    {
        var completedAt = _timeProvider.GetUtcNow();
        var assignment = _catalog.Assignments.FirstOrDefault(item =>
            item.BatchNumber == request.BatchNumber
            && string.Equals(item.LegacyPhpEntry, request.LegacyPhpEntry, StringComparison.OrdinalIgnoreCase));

        if (assignment is null)
        {
            return new ZeroPhpCutoverBatchRunResult(
                request.BatchNumber,
                request.LegacyPhpEntry,
                "not-found",
                request.DryRun,
                $"No batch {request.BatchNumber} cutover assignment is registered for '{request.LegacyPhpEntry}'.",
                request.RequestedAt,
                completedAt,
                "Add the legacy entry to an exact-route batch before planning execution.");
        }

        if (!request.DryRun)
        {
            return new ZeroPhpCutoverBatchRunResult(
                assignment.BatchNumber,
                assignment.LegacyPhpEntry,
                "manual-approval-required",
                request.DryRun,
                $"Execution is blocked for {assignment.LegacyPhpEntry}; batch {assignment.BatchNumber} currently allows dry-run planning only and requires PHP fallback.",
                request.RequestedAt,
                completedAt,
                assignment.RequiredEvidence);
        }

        return new ZeroPhpCutoverBatchRunResult(
            assignment.BatchNumber,
            assignment.LegacyPhpEntry,
            "dry-run-planned",
            request.DryRun,
            $"Dry run accepted for batch {assignment.BatchNumber} {assignment.TargetSlice} replacement; keep {ZeroPhpCutoverBatchCatalog.CutoverMode} and PHP fallback until parity evidence passes.",
            request.RequestedAt,
            completedAt,
            assignment.RequiredEvidence);
    }
}

public sealed class ZeroPhpCutoverBatchDryRunEvidenceManifest
{
    private readonly ZeroPhpCutoverBatchCatalog _catalog;

    public ZeroPhpCutoverBatchDryRunEvidenceManifest(ZeroPhpCutoverBatchCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyCollection<ZeroPhpCutoverBatchDryRunEvidence> BuildManifest(int? batchNumber = null)
    {
        var items = batchNumber is null
            ? _catalog.Assignments
            : _catalog.GetBatch(batchNumber.Value);

        return items
            .Select(item => new ZeroPhpCutoverBatchDryRunEvidence(
                item.BatchNumber,
                item.LegacyPhpEntry,
                "parity-sample-required",
                $"dotnet run --project aspnet/src/EcomAE.Workers -- --batch {item.BatchNumber} --legacy-entry {Quote(item.LegacyPhpEntry)} --dry-run",
                $"docs/migration/parity/batch-{item.BatchNumber:D3}/{NormalizeEvidenceName(item.LegacyPhpEntry)}.json",
                $"bash scripts/rollback_aspnet_foundation.sh --route {Quote(item.LegacyPhpEntry)} --keep-php-fallback",
                ExactRouteOnly: true,
                ZeroPhpCutoverBatchCatalog.PhpFallbackRequired))
            .ToArray();
    }

    private static string NormalizeEvidenceName(string legacyPhpEntry)
    {
        return legacyPhpEntry
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace("_", "-", StringComparison.Ordinal)
            .Replace(".php", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }
}

public sealed class ZeroPhpCutoverBatchDryRunExecutor
{
    private readonly ZeroPhpCutoverBatchDryRunEvidenceManifest _manifest;
    private readonly ZeroPhpCutoverBatchRunner _runner;
    private readonly TimeProvider _timeProvider;

    public ZeroPhpCutoverBatchDryRunExecutor(
        ZeroPhpCutoverBatchDryRunEvidenceManifest manifest,
        ZeroPhpCutoverBatchRunner runner,
        TimeProvider timeProvider)
    {
        _manifest = manifest;
        _runner = runner;
        _timeProvider = timeProvider;
    }

    public ZeroPhpCutoverBatchDryRunExecutionReport PlanBatch(int batchNumber, string requestedBy)
    {
        var requestedAt = _timeProvider.GetUtcNow();
        var items = _manifest.BuildManifest(batchNumber)
            .Select(evidence => BuildItem(evidence, requestedAt, requestedBy))
            .ToArray();

        return new ZeroPhpCutoverBatchDryRunExecutionReport(
            batchNumber,
            items.Length,
            items.Count(item => string.Equals(item.Status, "dry-run-planned", StringComparison.OrdinalIgnoreCase)),
            ExactRouteOnly: true,
            ZeroPhpCutoverBatchCatalog.PhpFallbackRequired,
            items);
    }

    private ZeroPhpCutoverBatchDryRunExecutionItem BuildItem(
        ZeroPhpCutoverBatchDryRunEvidence evidence,
        DateTimeOffset requestedAt,
        string requestedBy)
    {
        var result = _runner.PlanRun(new ZeroPhpCutoverBatchRunRequest(
            evidence.BatchNumber,
            evidence.LegacyPhpEntry,
            requestedAt,
            requestedBy,
            DryRun: true));

        return new ZeroPhpCutoverBatchDryRunExecutionItem(
            evidence.BatchNumber,
            evidence.LegacyPhpEntry,
            result.Status,
            evidence.DryRunCommand,
            evidence.ParitySamplePath,
            evidence.RollbackCommand,
            result.Message);
    }
}

public sealed class ZeroPhpCutoverBatchParityReporter
{
    private readonly ZeroPhpCutoverBatchCatalog _catalog;

    public ZeroPhpCutoverBatchParityReporter(ZeroPhpCutoverBatchCatalog catalog)
    {
        _catalog = catalog;
    }

    public ZeroPhpCutoverBatchParityReport BuildReport(int batchNumber)
    {
        var assignments = _catalog.GetBatch(batchNumber);
        var dryRunRequired = assignments.Count(item => string.Equals(item.Status, "aspnet-worker-dry-run-required", StringComparison.OrdinalIgnoreCase));
        var evidence = assignments
            .Select(item => item.RequiredEvidence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ZeroPhpCutoverBatchParityReport(
            batchNumber,
            ZeroPhpCutoverBatchCatalog.CutoverMode,
            ZeroPhpCutoverBatchCatalog.PhpFallbackRequired,
            assignments.Count,
            dryRunRequired,
            ReadyForShadow: 0,
            ReadyForLive: 0,
            ReadyToRemovePhpFallback: false,
            NextAction: $"Execute ASP.NET Core dry-run replacements for every batch {batchNumber} PHP entry, compare PHP-vs-ASP.NET parity samples, then approve exact-route shadow before any live cutover.",
            evidence);
    }

    public ZeroPhpCutoverFleetParityReport BuildFleetReport()
    {
        var assignments = _catalog.Assignments;
        var dryRunRequired = assignments.Count(item => string.Equals(item.Status, "aspnet-worker-dry-run-required", StringComparison.OrdinalIgnoreCase));

        return new ZeroPhpCutoverFleetParityReport(
            ZeroPhpCutoverBatchCatalog.FirstGeneratedBatch,
            ZeroPhpCutoverBatchCatalog.LastGeneratedBatch,
            _catalog.BatchNumbers.Count,
            assignments.Count,
            dryRunRequired,
            ReadyForShadow: 0,
            ReadyForLive: 0,
            ZeroPhpCutoverBatchCatalog.PhpFallbackRequired,
            ReadyToRemovePhpFallback: false,
            NextAction: "All batches 3-61 have dry-run scaffolding only. Attach parity samples and staging smoke before any exact-route shadow or PHP fallback removal.");
    }
}
