using EcomAE.Platform.Migration;
using EcomAE.Platform.Modules;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationParityReporterTests
{
    [Fact]
    public void BuildReportTracksPostScaffoldMilestonesAndKeepsPhpFallback()
    {
        var report = new MigrationParityReporter(Array.Empty<ISurfaceModule>()).BuildReport();

        Assert.Contains(report.NextMilestones, m => m.Contains("ensure→issue", StringComparison.OrdinalIgnoreCase)
            || m.Contains("ensure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextMilestones, m => m.Contains("compare_", StringComparison.Ordinal));
        Assert.Contains(report.NextMilestones, m => m.Contains("location=", StringComparison.Ordinal)
            || m.Contains("location =", StringComparison.Ordinal));
        Assert.Contains(report.NextMilestones, m => m.Contains("APPROVED_TO_REMOVE_PHP_FALLBACK", StringComparison.Ordinal)
            || m.Contains("ReadyToRemovePhp", StringComparison.Ordinal));
        Assert.DoesNotContain(report.NextMilestones, m => m.Contains("Replace placeholder CP login", StringComparison.Ordinal));
        Assert.DoesNotContain(report.NextMilestones, m => m.Contains("Port catalog/price APIs", StringComparison.Ordinal));
    }
}
