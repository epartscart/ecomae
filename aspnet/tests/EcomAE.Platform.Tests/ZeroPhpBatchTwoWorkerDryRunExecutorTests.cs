using Xunit;
using EcomAE.Workers;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchTwoWorkerDryRunExecutorTests
{
    [Fact]
    public void PlanAllCreatesDryRunPlanForEveryBatchTwoWorkerWithoutRemovingPhpFallback()
    {
        var timeProvider = TimeProvider.System;
        var catalog = new ZeroPhpBatchTwoWorkerReplacementCatalog();
        var executor = new ZeroPhpBatchTwoWorkerDryRunExecutor(
            new ZeroPhpBatchTwoWorkerDryRunEvidenceManifest(catalog),
            new ZeroPhpBatchTwoWorkerReplacementRunner(catalog, timeProvider),
            timeProvider);

        var report = executor.PlanAll("migration-operator");

        Assert.Equal(2, report.BatchNumber);
        Assert.Equal(50, report.TotalItems);
        Assert.Equal(50, report.PlannedItems);
        Assert.True(report.ExactRouteOnly);
        Assert.True(report.PhpFallbackRequired);
        Assert.All(report.Items, item => Assert.Equal("dry-run-planned", item.Status));
        Assert.All(report.Items, item => Assert.Contains("--dry-run", item.DryRunCommand, StringComparison.OrdinalIgnoreCase));
        Assert.All(report.Items, item => Assert.Contains("--keep-php-fallback", item.RollbackCommand, StringComparison.OrdinalIgnoreCase));
        Assert.All(report.Items, item => Assert.Contains("PHP fallback", item.PlannerMessage, StringComparison.OrdinalIgnoreCase));
    }
}
