using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantWorkspaceParityReporterTests
{
    [Fact]
    public void BuildReportNamesTenantModesAndProductionRegistryGap()
    {
        var report = new TenantWorkspaceParityReporter().BuildReport();

        Assert.Equal("Tenant CP and tenant ERP workspaces", report.Surface);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("ERP-only", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("ensure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("production MySQL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("RequirePhpFallback=true", StringComparison.Ordinal));
    }
}
