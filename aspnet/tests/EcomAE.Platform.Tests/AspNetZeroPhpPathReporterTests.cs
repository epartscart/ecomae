using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class AspNetZeroPhpPathReporterTests
{
    [Fact]
    public void BuildReportTargetsZeroPhpWithoutInventingCutover()
    {
        var report = new AspNetZeroPhpPathReporter().BuildReport();
        Assert.Equal("100%-aspnet-core-0-php", report.TargetEndState);
        Assert.Equal("building-toward-zero-php", report.Status);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.InRange(report.HonestCompletionPct, 1, 99);
        Assert.Contains(report.Phases, p => p.Id == "6-php-removal");
        Assert.Contains(report.Phases, p => p.Id == "3-presentation-parity" && p.Status == "in-progress");
        Assert.NotEmpty(report.NextBuilds);
    }
}
