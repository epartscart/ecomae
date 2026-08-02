using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchOneWorkerReplacementCatalogTests
{
    [Fact]
    public void CatalogKeepsBatchOneExactRouteAndFallbackGuardrails()
    {
        var catalog = new ZeroPhpBatchOneWorkerReplacementCatalog();

        Assert.Equal(1, ZeroPhpBatchOneWorkerReplacementCatalog.BatchNumber);
        Assert.Equal("exact-route-only", ZeroPhpBatchOneWorkerReplacementCatalog.CutoverMode);
        Assert.True(ZeroPhpBatchOneWorkerReplacementCatalog.PhpFallbackRequired);
        Assert.Equal(50, catalog.Replacements.Count);
        Assert.All(catalog.Replacements, replacement => Assert.Equal("worker-replacement", replacement.TargetSlice));
        Assert.All(catalog.Replacements, replacement => Assert.Equal("aspnet-worker-dry-run-required", replacement.Status));
        Assert.Contains(catalog.Replacements, replacement => replacement.LegacyPhpEntry == "cp/content/content/ajax_create_sitemap.php" && replacement.Risk == "high");
        Assert.Contains(catalog.Replacements, replacement => replacement.RequiredEvidence.Contains("live smoke", StringComparison.OrdinalIgnoreCase));
    }
}
