using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCutoverBatchParityReporterTests
{
    [Fact]
    public void FleetReportKeepsPhpFallbackAcrossAllGeneratedBatches()
    {
        var reporter = new ZeroPhpCutoverBatchParityReporter(new ZeroPhpCutoverBatchCatalog());

        var fleet = reporter.BuildFleetReport();
        var batch3 = reporter.BuildReport(3);

        Assert.Equal(3, fleet.FirstBatch);
        Assert.Equal(61, fleet.LastBatch);
        Assert.Equal(59, fleet.TotalBatches);
        Assert.Equal(2949, fleet.TotalAssignments);
        Assert.Equal(2949, fleet.DryRunRequired);
        Assert.Equal(0, fleet.ReadyForShadow);
        Assert.False(fleet.ReadyToRemovePhpFallback);
        Assert.True(fleet.PhpFallbackRequired);
        Assert.Equal(50, batch3.TotalAssignments);
        Assert.Contains("exact-route shadow", batch3.NextAction, StringComparison.OrdinalIgnoreCase);
    }
}
