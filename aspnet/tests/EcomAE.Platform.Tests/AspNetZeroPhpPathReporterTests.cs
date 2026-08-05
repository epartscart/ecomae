using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class AspNetZeroPhpPathReporterTests
{
    [Fact]
    public void BuildReportTargetsAspNetPrimaryPhpReferenceWithoutInventingCutover()
    {
        var report = new AspNetZeroPhpPathReporter().BuildReport();
        Assert.Equal("100%-aspnet-core-live-php-reference-kept", report.TargetEndState);
        Assert.Equal("building-toward-aspnet-primary-php-reference", report.Status);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.InRange(report.HonestCompletionPct, 1, 99);
        Assert.Contains(report.Phases, p => p.Id == "6-php-traffic-fallback-removal");
        Assert.Contains(report.Phases, p => p.Detail.Contains("reference", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Notes, n => n.Contains("PHP project is retained as reference", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Phases, p => p.Id == "3-presentation-parity" && p.Status == "in-progress");
        Assert.NotEmpty(report.NextBuilds);
    }
}
