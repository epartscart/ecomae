using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchTwoWorkerReplacementCatalogTests
{
    [Fact]
    public void CatalogKeepsBatchTwoExactRouteAndFallbackGuardrails()
    {
        var catalog = new ZeroPhpBatchTwoWorkerReplacementCatalog();

        Assert.Equal(2, ZeroPhpBatchTwoWorkerReplacementCatalog.BatchNumber);
        Assert.Equal("exact-route-only", ZeroPhpBatchTwoWorkerReplacementCatalog.CutoverMode);
        Assert.True(ZeroPhpBatchTwoWorkerReplacementCatalog.PhpFallbackRequired);
        Assert.Equal(50, catalog.Replacements.Count);
        Assert.All(catalog.Replacements, replacement => Assert.Equal("worker-replacement", replacement.TargetSlice));
        Assert.All(catalog.Replacements, replacement => Assert.Equal("aspnet-worker-dry-run-required", replacement.Status));
        Assert.Contains(catalog.Replacements, replacement => replacement.LegacyPhpEntry == "sitemap-pages.php" && replacement.Risk == "high");
        Assert.Contains(catalog.Replacements, replacement => replacement.RequiredEvidence.Contains("live smoke", StringComparison.OrdinalIgnoreCase));
    }
}
