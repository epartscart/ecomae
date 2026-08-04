using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class OnPremisesParityReporterTests
{
    [Fact]
    public void BuildReportLocksCutoverAndSeparatesErpOnlyFromInstaller()
    {
        var report = new OnPremisesParityReporter().BuildReport();
        Assert.Equal("on-premises-erp-parity", report.Role);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.PhpAuthoritative);
        Assert.Contains(report.Tracks, t => t.Id == "erp-only-tenant");
        Assert.Contains(report.Tracks, t => t.Id == "on-prem-installer" && t.Status == "scaffold");
        Assert.Contains(report.Tracks, t => t.Id == "on-prem-installer" && t.Detail.Contains("on-premises-aspnet", StringComparison.Ordinal));
        Assert.Contains(report.PhpPaths, p => p.Contains("erp_tabs_on_premises.php", StringComparison.Ordinal));
        Assert.Contains(report.PhpPaths, p => p.Contains("deploy/on-premises/", StringComparison.Ordinal));
        Assert.Equal("/erp/on-premises-app", report.AspNetRouteHint);
    }
}
