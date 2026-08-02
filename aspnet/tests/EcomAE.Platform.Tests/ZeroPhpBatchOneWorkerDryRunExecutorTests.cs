using EcomAE.Workers;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpBatchOneWorkerDryRunExecutorTests
{
    [Fact]
    public void PlanAllCreatesDryRunPlanForEveryBatchOneWorkerWithoutRemovingPhpFallback()
    {
        var timeProvider = TimeProvider.System;
        var catalog = new ZeroPhpBatchOneWorkerReplacementCatalog();
        var executor = new ZeroPhpBatchOneWorkerDryRunExecutor(
            new ZeroPhpBatchOneWorkerDryRunEvidenceManifest(catalog),
            new ZeroPhpBatchOneWorkerReplacementRunner(catalog, timeProvider),
            timeProvider);

        var report = executor.PlanAll("migration-operator");

        Assert.Equal(1, report.BatchNumber);
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
