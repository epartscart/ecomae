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
        Assert.NotNull(result.Evidence);
        Assert.Equal("price-import", result.Evidence.JobKey);
        Assert.True(result.Evidence.PhpFallbackRequired);
        Assert.Contains("PHP baseline", result.Evidence.PhpBaselineSample, StringComparison.Ordinal);
        Assert.Contains("disable ASP.NET worker flag", result.Evidence.RollbackCommand, StringComparison.Ordinal);
        Assert.NotNull(result.DryRunOutput);
        Assert.Equal("dry-run-needs-sample", result.DryRunOutput.Status);
        Assert.True(result.DryRunOutput.WritesBlocked);
    }

    [Fact]
    public void PlanRunExecutesPriceImportDryRunSampleWithoutWrites()
    {
        var runner = new MigrationWorkerJobRunner(new MigrationWorkerJobCatalog(), TimeProvider.System);
        var request = new MigrationWorkerJobRunRequest(
            "price-import",
            DateTimeOffset.UnixEpoch,
            "migration-test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_csv"] = "sku,price,currency\nSKU-1,12.50,AED"
            });

        var result = runner.PlanRun(request);

        Assert.Equal("dry-run-planned", result.Status);
        Assert.NotNull(result.DryRunOutput);
        Assert.Equal("dry-run-validated", result.DryRunOutput.Status);
        Assert.Equal("1", result.DryRunOutput.Metrics["valid_rows"]);
        Assert.Equal("0", result.DryRunOutput.Metrics["writes"]);
    }

    [Fact]
    public void PlanRunBlocksNonDryRunUntilConcreteImplementationExists()
    {
        var runner = new MigrationWorkerJobRunner(new MigrationWorkerJobCatalog(), TimeProvider.System);
        var request = new MigrationWorkerJobRunRequest("sitemap", DateTimeOffset.UnixEpoch, "migration-test", DryRun: false);

        var result = runner.PlanRun(request);

        Assert.Equal("manual-approval-required", result.Status);
        Assert.Contains("concrete implementation", result.Message, StringComparison.Ordinal);
        Assert.Null(result.Evidence);
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
