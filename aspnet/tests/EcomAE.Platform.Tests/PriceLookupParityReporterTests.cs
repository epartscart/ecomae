using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PriceLookupParityReporterTests
{
    [Fact]
    public async Task BuildReportUsesRepositoryServiceAndNamesRemainingGaps()
    {
        var reporter = new PriceLookupParityReporter(new RepositoryPriceLookupService(new MigrationPriceOfferRepository()));

        var report = await reporter.BuildReportAsync();

        Assert.Equal("TOYOTA", report.SampleBrand);
        Assert.Equal("044650K020", report.SampleArticle);
        Assert.True(report.ReadyForShadowTraffic);
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("ensure_epc_api_clients_table.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("--contract-only", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("X-API-Key", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("location = /api/v1/price/lookup", StringComparison.Ordinal));
    }
}
