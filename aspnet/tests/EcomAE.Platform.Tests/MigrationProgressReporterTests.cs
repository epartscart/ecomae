using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationProgressReporterTests
{
    [Fact]
    public void BuildReportShowsFoundationCompleteAndProductionCutoverStillGated()
    {
        var report = new MigrationProgressReporter().BuildReport();

        Assert.Equal(100, report.OverallCompletePercent);
        Assert.Equal(0, report.OverallPendingPercent);
        Assert.Contains(report.Items, item => item.Area == "ASP.NET Core platform foundation" && item.Status == "foundation-complete");
        Assert.Contains(report.Items, item => item.Area == "Platform ERP migration foundation" && item.Status == "foundation-complete");
        Assert.Contains(report.Items, item => item.Area == "Production cutover and PHP removal foundation" && item.NextMilestone.Contains("cutover approval", StringComparison.OrdinalIgnoreCase));
    }
}
