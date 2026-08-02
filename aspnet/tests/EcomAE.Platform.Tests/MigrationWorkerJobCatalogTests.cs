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
        Assert.Contains(catalog.Jobs, job => job.Key == "currency-live-rates"
            && job.LegacyPhpEntry.Contains("epc-currency-live-rates-cron.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "demo-expire"
            && job.LegacyPhpEntry.Contains("epc-demo-expire-cron.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "platform-jobs"
            && job.LegacyPhpEntry.Contains("epc-platform-jobs-cron.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "seo-sitemap-ping"
            && job.LegacyPhpEntry.Contains("epc-seo-sitemap-ping.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "seo-sitemap-warm"
            && job.LegacyPhpEntry.Contains("epc-seo-sitemap-warm.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "uae-tax-legislation"
            && job.LegacyPhpEntry.Contains("epc-uae-tax-legislation-cron.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "apai-background-jobs"
            && job.LegacyPhpEntry.Contains("epc-apai-background-jobs-cron.php", StringComparison.Ordinal));
        Assert.Contains(catalog.Jobs, job => job.Key == "fulfillment-queue");
        Assert.Contains(catalog.Jobs, job => job.Key == "apai-sync-categories");
        Assert.Contains(catalog.Jobs, job => job.Key == "integrations-cleanup");
    }
}
