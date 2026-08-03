using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CatalogParityReporterTests
{
    [Fact]
    public void BuildReportNamesLegacyCatalogAndGaps()
    {
        var report = new CatalogParityReporter().BuildReport();

        Assert.True(report.ReadyForShadowTraffic);
        Assert.Contains("api/v1/catalog.php", report.LegacySource, StringComparison.Ordinal);
        Assert.Contains("manufacturers", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vin", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("article", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("articles", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("brand-parts", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("suppliers", report.AspNetSource, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("catalog-cache-routes-live-miss-fill-php", report.Status);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("ensure_epc_api_clients_table.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("--contract-only", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("UMAPI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("cloudpanel_probe_catalog_miss_path.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("compare_catalog_miss_dual_samples", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("cutoverAllowed=false", StringComparison.Ordinal));
    }
}
