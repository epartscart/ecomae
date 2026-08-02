namespace EcomAE.Workers;

public sealed record ZeroPhpBatchTwoWorkerDryRunEvidence(
    string LegacyPhpEntry,
    string Status,
    string DryRunCommand,
    string ParitySamplePath,
    string RollbackCommand,
    bool ExactRouteOnly,
    bool PhpFallbackRequired);

public sealed class ZeroPhpBatchTwoWorkerDryRunEvidenceManifest
{
    private readonly ZeroPhpBatchTwoWorkerReplacementCatalog _catalog;

    public ZeroPhpBatchTwoWorkerDryRunEvidenceManifest(ZeroPhpBatchTwoWorkerReplacementCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyCollection<ZeroPhpBatchTwoWorkerDryRunEvidence> BuildManifest()
    {
        return _catalog.Replacements
            .Select(item => new ZeroPhpBatchTwoWorkerDryRunEvidence(
                item.LegacyPhpEntry,
                "parity-sample-required",
                $"dotnet run --project aspnet/src/EcomAE.Workers -- --batch {ZeroPhpBatchTwoWorkerReplacementCatalog.BatchNumber} --legacy-entry {Quote(item.LegacyPhpEntry)} --dry-run",
                $"docs/migration/parity/batch-002/{NormalizeEvidenceName(item.LegacyPhpEntry)}.json",
                $"bash scripts/rollback_aspnet_foundation.sh --route {Quote(item.LegacyPhpEntry)} --keep-php-fallback",
                ExactRouteOnly: true,
                ZeroPhpBatchTwoWorkerReplacementCatalog.PhpFallbackRequired))
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
