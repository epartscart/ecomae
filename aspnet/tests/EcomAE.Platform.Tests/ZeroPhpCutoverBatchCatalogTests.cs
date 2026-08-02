using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCutoverBatchCatalogTests
{
    [Fact]
    public void CatalogCoversBatchesThreeThroughSixtyOneWithFallbackGuardrails()
    {
        var catalog = new ZeroPhpCutoverBatchCatalog();

        Assert.Equal("exact-route-only", ZeroPhpCutoverBatchCatalog.CutoverMode);
        Assert.True(ZeroPhpCutoverBatchCatalog.PhpFallbackRequired);
        Assert.Equal(3, ZeroPhpCutoverBatchCatalog.FirstGeneratedBatch);
        Assert.Equal(61, ZeroPhpCutoverBatchCatalog.LastGeneratedBatch);
        Assert.Equal(2949, catalog.Assignments.Count);
        Assert.Equal(59, catalog.BatchNumbers.Count);
        Assert.Equal(50, catalog.GetBatch(3).Count);
        Assert.All(catalog.Assignments, item => Assert.Equal("aspnet-worker-dry-run-required", item.Status));
        Assert.Contains(catalog.Assignments, item => item.BatchNumber == 3 && item.LegacyPhpEntry == "sitemap-wh-51.php");
        Assert.Contains(catalog.Assignments, item => item.BatchNumber == 61);
    }
}
