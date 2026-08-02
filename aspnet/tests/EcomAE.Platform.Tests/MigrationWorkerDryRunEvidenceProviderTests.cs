using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationWorkerDryRunEvidenceProviderTests
{
    [Fact]
    public void BuildEvidenceKeepsPhpFallbackAndRollbackVisible()
    {
        var job = new MigrationWorkerJob(
            "notifications",
            "PHP notification cron scripts",
            "EcomAE.Workers.Notifications",
            "queue-driven",
            "planned",
            "Email/SMS recipients, templates, retries, and audit rows match PHP behavior.");
        var request = new MigrationWorkerJobRunRequest("notifications", DateTimeOffset.UnixEpoch, "migration-test");

        var evidence = new MigrationWorkerDryRunEvidenceProvider().BuildEvidence(job, request);

        Assert.Equal("notifications", evidence.JobKey);
        Assert.True(evidence.PhpFallbackRequired);
        Assert.Equal("not-run-production-smoke", evidence.ProductionSmokeStatus);
        Assert.Contains("PHP baseline", evidence.PhpBaselineSample, StringComparison.Ordinal);
        Assert.Contains("Dry-run only", evidence.AspNetDryRunSample, StringComparison.Ordinal);
        Assert.Contains("Email/SMS recipients", evidence.ParityComparison, StringComparison.Ordinal);
        Assert.Contains("PHP scheduler active", evidence.RollbackCommand, StringComparison.Ordinal);
        Assert.Contains("release-owner", evidence.RequiredApprovals);
    }
}
