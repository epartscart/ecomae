using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationProgressReporterTests
{
    [Fact]
    public void BuildReportShowsFoundationProgressAndPendingBusinessParity()
    {
        var report = new MigrationProgressReporter().BuildReport();

        Assert.Equal(39, report.OverallCompletePercent);
        Assert.Equal(61, report.OverallPendingPercent);
        Assert.Contains(report.Items, item => item.Area == "ASP.NET Core platform foundation" && item.CompletePercent == 100);
        Assert.Contains(report.Items, item => item.Area == "Platform ERP migration" && item.Status == "shell-started");
        Assert.Contains(report.Items, item => item.Area == "Production cutover and PHP removal" && item.Status == "blocked");
    }
}
