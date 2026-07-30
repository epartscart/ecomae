using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationReadinessReporterTests
{
    [Fact]
    public void BuildReportKeepsPhpRemovalBlockedUntilParityIsComplete()
    {
        var report = new MigrationReadinessReporter().BuildReport();

        Assert.False(report.PhpRemovalReady);
        Assert.Equal("not-ready-for-php-removal", report.OverallStatus);
        Assert.Contains(report.Items, item => item.Surface == "Super CP" && item.BlocksPhpRemoval);
        Assert.Contains(report.Items, item => item.Surface == "Platform ERP" && item.BlocksPhpRemoval);
        Assert.Contains(report.Items, item => item.Surface == "Super BOS" && item.BlocksPhpRemoval);
        Assert.Contains(report.Items, item => item.Surface == "Tenant ERP" && item.CorrectiveAction.Contains("ERP-only tenant modes", StringComparison.Ordinal));
        Assert.Contains(report.ProductionCutoverGates, gate => gate.Contains("response parity", StringComparison.Ordinal));
    }
}
