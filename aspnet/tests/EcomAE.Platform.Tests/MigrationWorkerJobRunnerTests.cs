using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationWorkerJobRunnerTests
{
    [Fact]
    public void PlanRunAcceptsDryRunForKnownJob()
    {
        var runner = new MigrationWorkerJobRunner(new MigrationWorkerJobCatalog(), TimeProvider.System);
        var request = new MigrationWorkerJobRunRequest("PRICE-IMPORT", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("price-import", result.JobKey);
        Assert.True(result.DryRun);
        Assert.Equal("dry-run-planned", result.Status);
        Assert.Contains("EcomAE.Workers.PriceImport", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanRunBlocksNonDryRunUntilConcreteImplementationExists()
    {
        var runner = new MigrationWorkerJobRunner(new MigrationWorkerJobCatalog(), TimeProvider.System);
        var request = new MigrationWorkerJobRunRequest("sitemap", DateTimeOffset.UnixEpoch, "migration-test", DryRun: false);

        var result = runner.PlanRun(request);

        Assert.Equal("manual-approval-required", result.Status);
        Assert.Contains("concrete implementation", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanRunReportsUnknownJob()
    {
        var runner = new MigrationWorkerJobRunner(new MigrationWorkerJobCatalog(), TimeProvider.System);
        var request = new MigrationWorkerJobRunRequest("unknown", DateTimeOffset.UnixEpoch, "migration-test");

        var result = runner.PlanRun(request);

        Assert.Equal("not-found", result.Status);
    }
}
