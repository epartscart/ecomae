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
        Assert.Equal("catalog-cache-routes-wired-awaiting-staging", report.Status);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("UMAPI", StringComparison.OrdinalIgnoreCase));
    }
}
