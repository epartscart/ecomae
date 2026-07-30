using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationWorkerSchedulePlannerTests
{
    [Fact]
    public void BuildPlanCreatesLockAndRetryPolicyForPriceImports()
    {
        var planner = new MigrationWorkerSchedulePlanner(new MigrationWorkerJobCatalog());

        var plan = planner.BuildPlan("PRICE-IMPORT");

        Assert.Equal("price-import", plan.JobKey);
        Assert.Equal("ecomae:worker:price-import", plan.LockKey);
        Assert.Equal("bounded-retry-with-import-audit", plan.RetryPolicy);
        Assert.True(plan.RequiresDistributedLock);
        Assert.False(plan.ReadyForExecution);
    }

    [Fact]
    public void BuildPlanUsesDeadLetterPolicyForQueueDrivenJobs()
    {
        var planner = new MigrationWorkerSchedulePlanner(new MigrationWorkerJobCatalog());

        var plan = planner.BuildPlan("notifications");

        Assert.Equal("queue-driven", plan.Schedule);
        Assert.Equal("exponential-backoff-with-dead-letter", plan.RetryPolicy);
    }

    [Fact]
    public void BuildPlanReportsUnknownJobsAsNotReady()
    {
        var planner = new MigrationWorkerSchedulePlanner(new MigrationWorkerJobCatalog());

        var plan = planner.BuildPlan("missing-job");

        Assert.Equal("unknown", plan.Schedule);
        Assert.False(plan.ReadyForExecution);
        Assert.Contains("No planned PHP job replacement", plan.ReadinessReason, StringComparison.Ordinal);
    }
}
