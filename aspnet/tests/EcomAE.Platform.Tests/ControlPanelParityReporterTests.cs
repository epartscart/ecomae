using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ControlPanelParityReporterTests
{
    [Fact]
    public void BuildReportNamesCpAliasesAndRemainingAdminGaps()
    {
        var report = new ControlPanelParityReporter().BuildReport();

        Assert.Equal("Control Panel / Super CP", report.Surface);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("route aliases", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("backend-group session", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("presentation-shell-scaffolded-awaiting-staging", report.Status);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("presentation-preserving HTML", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("user management", StringComparison.OrdinalIgnoreCase));
    }
}
