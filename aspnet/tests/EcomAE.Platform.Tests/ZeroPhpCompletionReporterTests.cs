using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCompletionReporterTests
{
    [Fact]
    public void BuildReportQuantifiesRemainingWorkAndBlocksPhpRemoval()
    {
        var report = new ZeroPhpCompletionReporter().BuildReport();

        Assert.Equal(35, report.OverallCompletePercent);
        Assert.Equal(65, report.OverallPendingPercent);
        Assert.Equal("not-ready-for-php-removal", report.Status);
        Assert.Contains(report.Areas, area => area.Name == "Foundation, deployment, and diagnostics" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "PHP runtime decommission" && area.CompletePercent == 0 && area.Status == "blocked");
        Assert.Contains(report.NextActions, action => action.Contains("one exact low-risk route", StringComparison.Ordinal));
    }
}
