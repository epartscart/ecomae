using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationProgressReporterTests
{
    [Fact]
    public void BuildReportShowsFoundationProgressAndPendingBusinessParity()
    {
        var report = new MigrationProgressReporter().BuildReport();

        Assert.Equal(60, report.OverallCompletePercent);
        Assert.Equal(40, report.OverallPendingPercent);
        Assert.Contains(report.Items, item => item.Area == "ASP.NET Core platform foundation" && item.CompletePercent == 100);
        Assert.Contains(report.Items, item => item.Area == "Platform ERP migration" && item.Status == "erp-parity-visible");
        Assert.Contains(report.Items, item => item.Area == "Tenant CP and tenant ERP migration" && item.Status == "tenant-workspace-parity-visible");
        Assert.Contains(report.Items, item => item.Area == "Storefront migration" && item.Status == "storefront-parity-visible");
        Assert.Contains(report.Items, item => item.Area == "Production cutover and PHP removal" && item.Status == "route-cutover-headers-started");
    }
}
