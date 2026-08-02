using Xunit;
using EcomAE.Workers;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchOneWorkerParityReporterTests
{
    [Fact]
    public void BuildReportKeepsPhpFallbackUntilDryRunAndParityEvidencePass()
    {
        var reporter = new ZeroPhpBatchOneWorkerParityReporter(new ZeroPhpBatchOneWorkerReplacementCatalog());

        var report = reporter.BuildReport();

        Assert.Equal(1, report.BatchNumber);
        Assert.Equal("exact-route-only", report.CutoverMode);
        Assert.True(report.PhpFallbackRequired);
        Assert.Equal(50, report.TotalReplacements);
        Assert.Equal(50, report.DryRunRequired);
        Assert.Equal(0, report.ReadyForShadow);
        Assert.Equal(0, report.ReadyForLive);
        Assert.False(report.ReadyToRemovePhpFallback);
        Assert.Contains("dry-run", report.NextAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact-route shadow", report.NextAction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(report.RequiredEvidence, evidence => evidence.Contains("PHP-vs-ASP.NET parity sample", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RequiredEvidence, evidence => evidence.Contains("live smoke", StringComparison.OrdinalIgnoreCase));
    }
}
