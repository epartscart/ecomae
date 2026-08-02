using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCutoverBatchDryRunExecutorTests
{
    [Fact]
    public void PlanBatchCreatesDryRunPlanForBatchThreeWithoutRemovingPhpFallback()
    {
        var catalog = new ZeroPhpCutoverBatchCatalog();
        var timeProvider = TimeProvider.System;
        var executor = new ZeroPhpCutoverBatchDryRunExecutor(
            new ZeroPhpCutoverBatchDryRunEvidenceManifest(catalog),
            new ZeroPhpCutoverBatchRunner(catalog, timeProvider),
            timeProvider);

        var report = executor.PlanBatch(3, "migration-test");

        Assert.Equal(3, report.BatchNumber);
        Assert.Equal(50, report.TotalItems);
        Assert.Equal(50, report.PlannedItems);
        Assert.True(report.ExactRouteOnly);
        Assert.True(report.PhpFallbackRequired);
        Assert.All(report.Items, item => Assert.Equal("dry-run-planned", item.Status));
        Assert.All(report.Items, item => Assert.StartsWith("docs/migration/parity/batch-003/", item.ParitySamplePath, StringComparison.Ordinal));
    }
}
