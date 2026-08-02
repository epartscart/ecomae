using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpDecommissionReadinessReporterTests
{
    [Fact]
    public void BuildReportBlocksPhpRemovalAndListsEvidence()
    {
        var report = new PhpDecommissionReadinessReporter().BuildReport();

        Assert.Equal("blocked-not-ready-for-php-removal", report.Status);
        Assert.False(report.ReadyToRemovePhp);
        Assert.True(report.BlockerCount >= 5);
        Assert.Contains(report.Blockers, item => item.Contains("parity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RequiredEvidence, item => item.Contains("release-owner", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextActions, item => item.Contains("Keep PHP authoritative", StringComparison.OrdinalIgnoreCase));
    }
}
