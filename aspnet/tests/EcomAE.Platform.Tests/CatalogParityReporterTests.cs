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
        Assert.Contains("DbCatalogStatusRepository", report.AspNetSource, StringComparison.Ordinal);
        Assert.Equal("status-route-wired-awaiting-staging", report.Status);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("manufacturer", StringComparison.OrdinalIgnoreCase));
    }
}
