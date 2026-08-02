using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpParityReporterTests
{
    [Fact]
    public void BuildReportNamesFinanceFixturesAndErpOnlyTenantGaps()
    {
        var report = new ErpParityReporter().BuildReport();

        Assert.Equal("Platform ERP", report.Surface);
        Assert.Equal("purchase-orders-inventory-stock-session-gated-awaiting-staging", report.Status);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("admin session", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("ERP-only tenants", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("chart-of-accounts", StringComparison.OrdinalIgnoreCase));
    }
}
