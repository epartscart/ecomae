using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosParityReporterTests
{
    [Fact]
    public void BuildReportNamesCaseInsensitiveAliasesAndPrivilegedGaps()
    {
        var report = new BosParityReporter().BuildReport();

        Assert.Equal("Super BOS / BOC", report.Surface);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("case-insensitive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("privileged operations", StringComparison.OrdinalIgnoreCase));
    }
}
