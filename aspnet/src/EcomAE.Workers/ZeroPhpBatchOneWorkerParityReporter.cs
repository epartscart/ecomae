namespace EcomAE.Workers;

public sealed record ZeroPhpBatchOneWorkerParityReport(
    int BatchNumber,
    string CutoverMode,
    bool PhpFallbackRequired,
    int TotalReplacements,
    int DryRunRequired,
    int ReadyForShadow,
    int ReadyForLive,
    bool ReadyToRemovePhpFallback,
    string NextAction,
    IReadOnlyCollection<string> RequiredEvidence);

public sealed class ZeroPhpBatchOneWorkerParityReporter
{
    private readonly ZeroPhpBatchOneWorkerReplacementCatalog _catalog;

    public ZeroPhpBatchOneWorkerParityReporter(ZeroPhpBatchOneWorkerReplacementCatalog catalog)
    {
        _catalog = catalog;
    }

    public ZeroPhpBatchOneWorkerParityReport BuildReport()
    {
        var replacements = _catalog.Replacements;
        var dryRunRequired = replacements.Count(item => string.Equals(item.Status, "aspnet-worker-dry-run-required", StringComparison.OrdinalIgnoreCase));
        var evidence = replacements
            .Select(item => item.RequiredEvidence)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ZeroPhpBatchOneWorkerParityReport(
            ZeroPhpBatchOneWorkerReplacementCatalog.BatchNumber,
            ZeroPhpBatchOneWorkerReplacementCatalog.CutoverMode,
            ZeroPhpBatchOneWorkerReplacementCatalog.PhpFallbackRequired,
            replacements.Count,
            dryRunRequired,
            ReadyForShadow: 0,
            ReadyForLive: 0,
            ReadyToRemovePhpFallback: false,
            NextAction: "Execute ASP.NET Core dry-run replacements for every batch 1 PHP worker, compare PHP-vs-ASP.NET parity samples, then approve exact-route shadow before any live cutover.",
            evidence);
    }
}
