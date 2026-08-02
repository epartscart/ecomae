using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationWorkerBatchDryRunReporterTests
{
    [Fact]
    public void BuildReportCoversEveryBatchOneWorkerAndKeepsFallback()
    {
        var catalog = new MigrationWorkerJobCatalog();
        var reporter = new MigrationWorkerBatchDryRunReporter(catalog, new MigrationWorkerDryRunEvidenceProvider());

        var report = reporter.BuildReport(DateTimeOffset.UnixEpoch, "migration-test");

        Assert.Equal("batch-1-worker-dry-run-replacements", report.BatchKey);
        Assert.Equal("dry-run-evidence-ready", report.Status);
        Assert.Equal(catalog.Jobs.Count, report.TotalJobs);
        Assert.Equal(catalog.Jobs.Count, report.DryRunEvidenceReadyJobs);
        Assert.True(report.PhpFallbackRequired);
        Assert.All(report.EvidenceItems, item => Assert.True(item.PhpFallbackRequired));
        Assert.Contains(report.EvidenceItems, item => item.JobKey == "price-import");
        Assert.Contains(report.EvidenceItems, item => item.JobKey == "erp-reports");
        Assert.Contains("production smoke has not run", report.RemainingBlockers);
        Assert.Contains("PHP schedulers remain authoritative fallback", report.RemainingBlockers);
    }
}
