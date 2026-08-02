using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCompletionReporterTests
{
    [Fact]
    public void BuildReportQuantifiesRemainingWorkAndBlocksPhpRemoval()
    {
        var report = new ZeroPhpCompletionReporter().BuildReport();

        Assert.Equal(82, report.OverallCompletePercent);
        Assert.Equal(18, report.OverallPendingPercent);
        Assert.Equal("not-ready-for-php-removal", report.Status);
        Assert.Contains(report.Areas, area => area.Name == "Foundation, deployment, and diagnostics" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "PHP runtime decommission" && area.CompletePercent == 0 && area.Status == "blocked");
        Assert.Contains(report.Areas, area => area.Name == "CP, ERP, BOS, and tenant workflow parity" && area.CompletePercent == 72);
        Assert.Contains(report.Areas, area => area.Name == "Storefront and public API parity" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Background jobs and scheduled work" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Data, auth, observability, and rollback evidence" && area.CompletePercent == 100);
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_bootstrap_from_github.sh", StringComparison.Ordinal)
            || action.Contains("cloudpanel_find_and_redeploy.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md", StringComparison.Ordinal));
    }
}
