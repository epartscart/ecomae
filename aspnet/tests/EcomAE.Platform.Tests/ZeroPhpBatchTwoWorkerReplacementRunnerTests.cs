using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchTwoWorkerReplacementRunnerTests
{
    [Fact]
    public void PlanRunAcceptsDryRunForBatchTwoReplacement()
    {
        var runner = new ZeroPhpBatchTwoWorkerReplacementRunner(new ZeroPhpBatchTwoWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchTwoWorkerReplacementRunRequest("sitemap-pages.php", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("dry-run-planned", result.Status);
        Assert.True(result.DryRun);
        Assert.Contains("exact-route-only", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PHP fallback", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parity", result.RequiredEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanRunBlocksNonDryRunForBatchTwoReplacement()
    {
        var runner = new ZeroPhpBatchTwoWorkerReplacementRunner(new ZeroPhpBatchTwoWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchTwoWorkerReplacementRunRequest("sitemap-pages.php", DateTimeOffset.UnixEpoch, "migration-test", DryRun: false);

        var result = runner.PlanRun(request);

        Assert.Equal("manual-approval-required", result.Status);
        Assert.False(result.DryRun);
        Assert.Contains("dry-run planning only", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanRunReportsUnknownBatchTwoReplacement()
    {
        var runner = new ZeroPhpBatchTwoWorkerReplacementRunner(new ZeroPhpBatchTwoWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchTwoWorkerReplacementRunRequest("unknown.php", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("not-found", result.Status);
    }
}
