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
        Assert.Contains(report.Items, item => item.Surface == "Public APIs" && item.CurrentStatus == "catalog-cache-routes-wired-awaiting-staging");
        Assert.Contains(report.Items, item => item.Surface == "Background jobs" && item.CurrentStatus == "dry-run-validator-layer-complete");
        Assert.Contains(report.ProductionCutoverGates, gate => gate.Contains("ensure→issue", StringComparison.OrdinalIgnoreCase)
            || gate.Contains("ensure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.ProductionCutoverGates, gate => gate.Contains("response parity", StringComparison.Ordinal));
    }
}
