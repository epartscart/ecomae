using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class DataParityReporterTests
{
    [Fact]
    public void BuildReportNamesProductionTablesAndShadowReplayGate()
    {
        var report = new DataParityReporter().BuildReport();

        Assert.Equal("contracts-ready-production-data-pending", report.Status);
        Assert.Contains(report.ProductionDataSources, source => source.Contains("shop_docpart_prices_data", StringComparison.Ordinal));
        Assert.Contains(report.RequiredBeforeCutover, gate => gate.Contains("shadow queries", StringComparison.OrdinalIgnoreCase));
    }
}
