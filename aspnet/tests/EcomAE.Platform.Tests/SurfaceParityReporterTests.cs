using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceParityReporterTests
{
    [Fact]
    public void BuildReportTracksEveryProductionSurfaceBeforeFiftyPercentGate()
    {
        var report = new SurfaceParityReporter().BuildReport();

        Assert.Equal("parity-not-yet-reached", report.Status);
        Assert.Contains(report.Items, item => item.Surface == "Login" && item.Status == "bridge-started");
        Assert.Contains(report.Items, item => item.Surface == "Super CP" && item.AspNetRoute == "/CP");
        Assert.Contains(report.Items, item => item.Surface == "Platform ERP" && item.RequiredEvidence.Contains("chart of accounts", StringComparison.Ordinal));
        Assert.Contains(report.Items, item => item.Surface == "Super BOS" && item.LegacyRoute == "ecomae.com/BOS");
        Assert.Contains(report.Items, item => item.Surface == "Tenant ERP" && item.RequiredEvidence.Contains("ERP-only tenant", StringComparison.Ordinal));
        Assert.Contains(report.RequiredBeforeFiftyPercent, gate => gate.Contains("customer-facing shell", StringComparison.Ordinal));
    }
}
