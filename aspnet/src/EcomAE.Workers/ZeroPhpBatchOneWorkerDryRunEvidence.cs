namespace EcomAE.Workers;

public sealed record ZeroPhpBatchOneWorkerDryRunEvidence(
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    bool ExactRouteOnly,
    bool PhpFallbackRequired);

public sealed class ZeroPhpBatchOneWorkerDryRunEvidenceManifest
{
    private readonly ZeroPhpBatchOneWorkerReplacementCatalog _catalog;

    public ZeroPhpBatchOneWorkerDryRunEvidenceManifest(ZeroPhpBatchOneWorkerReplacementCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyCollection<ZeroPhpBatchOneWorkerDryRunEvidence> BuildManifest()
    {
        return _catalog.Replacements
            .Select(item => new ZeroPhpBatchOneWorkerDryRunEvidence(
                item.LegacyPhpEntry,
                "parity-sample-required",
                $"dotnet run --project aspnet/src/EcomAE.Workers -- --batch {ZeroPhpBatchOneWorkerReplacementCatalog.BatchNumber} --legacy-entry {Quote(item.LegacyPhpEntry)} --dry-run",
                $"docs/migration/parity/batch-001/{NormalizeEvidenceName(item.LegacyPhpEntry)}.json",
                $"bash scripts/rollback_aspnet_foundation.sh --route {Quote(item.LegacyPhpEntry)} --keep-php-fallback",
                ExactRouteOnly: true,
                ZeroPhpBatchOneWorkerReplacementCatalog.PhpFallbackRequired))
            .ToArray();
    }

    private static string NormalizeEvidenceName(string legacyPhpEntry)
    {
        return legacyPhpEntry
            .Replace('/', '-', StringComparison.Ordinal)
            .Replace('_', '-', StringComparison.Ordinal)
            .Replace(".php", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
    }

    private static string Quote(string value)
    {
        return $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }
}
