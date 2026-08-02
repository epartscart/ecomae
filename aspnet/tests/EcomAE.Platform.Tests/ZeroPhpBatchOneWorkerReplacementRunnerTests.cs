using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchOneWorkerReplacementRunnerTests
{
    [Fact]
    public void PlanRunAcceptsDryRunForBatchOneReplacement()
    {
        var runner = new ZeroPhpBatchOneWorkerReplacementRunner(new ZeroPhpBatchOneWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchOneWorkerReplacementRunRequest("cp/content/content/ajax_create_sitemap.php", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("dry-run-planned", result.Status);
        Assert.True(result.DryRun);
        Assert.Contains("exact-route-only", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PHP fallback", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parity", result.RequiredEvidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanRunBlocksNonDryRunForBatchOneReplacement()
    {
        var runner = new ZeroPhpBatchOneWorkerReplacementRunner(new ZeroPhpBatchOneWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchOneWorkerReplacementRunRequest("cp/content/content/ajax_create_sitemap.php", DateTimeOffset.UnixEpoch, "migration-test", DryRun: false);

        var result = runner.PlanRun(request);

        Assert.Equal("manual-approval-required", result.Status);
        Assert.False(result.DryRun);
        Assert.Contains("dry-run planning only", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanRunReportsUnknownBatchOneReplacement()
    {
        var runner = new ZeroPhpBatchOneWorkerReplacementRunner(new ZeroPhpBatchOneWorkerReplacementCatalog(), TimeProvider.System);
        var request = new ZeroPhpBatchOneWorkerReplacementRunRequest("unknown.php", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("not-found", result.Status);
    }
}
