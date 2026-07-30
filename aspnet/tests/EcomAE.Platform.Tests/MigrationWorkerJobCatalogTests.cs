using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationWorkerJobCatalogTests
{
    [Fact]
    public void CatalogTracksPlannedPhpCronReplacements()
    {
        var catalog = new MigrationWorkerJobCatalog();

        Assert.Contains(catalog.Jobs, job => job.Key == "price-import" && job.LegacyPhpEntry.Contains("upload_price.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "sitemap" && job.Schedule == "daily");
        Assert.Contains(catalog.Jobs, job => job.Key == "erp-reports" && job.RequiredParity.Contains("ERP PHP totals", StringComparison.Ordinal));
    }
}
