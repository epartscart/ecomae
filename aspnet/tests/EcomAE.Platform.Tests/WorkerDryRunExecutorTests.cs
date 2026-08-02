using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WorkerDryRunExecutorTests
{
    [Fact]
    public void SitemapDryRunValidatesUrlsWithoutWrites()
    {
        var executor = new SitemapDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "sitemap");
        var request = new MigrationWorkerJobRunRequest(
            "sitemap",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_urls"] = "https://www.ecomae.com/\nnot-a-url\nhttp://tenant.example/parts"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("2", output.Metrics["valid_urls"]);
        Assert.Equal("1", output.Metrics["invalid_urls"]);
    }

    [Fact]
    public void BackupDryRunValidatesTargetsWithoutArchives()
    {
        var executor = new BackupDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "backups");
        var request = new MigrationWorkerJobRunRequest(
            "backups",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["retention_days"] = "14",
                ["targets"] = "database,files"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["archives_created"]);
    }

    [Fact]
    public void NotificationsDryRunBlocksSend()
    {
        var executor = new NotificationsDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "notifications");
        var request = new MigrationWorkerJobRunRequest(
            "notifications",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_recipients"] = "ops@ecomae.com,bad-address"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["sent"]);
        Assert.Equal("1", output.Metrics["valid_recipients"]);
    }

    [Fact]
    public void ErpReportsDryRunBlocksGeneration()
    {
        var executor = new ErpReportsDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "erp-reports");
        var request = new MigrationWorkerJobRunRequest(
            "erp-reports",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["report_key"] = "vat",
                ["period"] = "2026-08"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["reports_generated"]);
    }

    [Fact]
    public void CurrencyLiveRatesDryRunValidatesSampleWithoutWrites()
    {
        var executor = new CurrencyLiveRatesDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "currency-live-rates");
        var request = new MigrationWorkerJobRunRequest(
            "currency-live-rates",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_rates"] = "USD,3.6725\nEUR,4.01\nbad-row"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("2", output.Metrics["valid_rates"]);
        Assert.Equal("1", output.Metrics["invalid_rates"]);
    }

    [Fact]
    public void DemoExpireDryRunValidatesSampleWithoutDeletes()
    {
        var executor = new DemoExpireDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "demo-expire");
        var request = new MigrationWorkerJobRunRequest(
            "demo-expire",
            DateTimeOffset.FromUnixTimeSeconds(1_720_000_000),
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_tenants"] = "demo-acme,1710000000\ndemo-live,0\nbad-row"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["deletes"]);
        Assert.Equal("1", output.Metrics["expired_candidates"]);
        Assert.Equal("1", output.Metrics["active_candidates"]);
    }

    [Fact]
    public void PlatformJobsDryRunValidatesSampleWithoutClaims()
    {
        var executor = new PlatformJobsDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "platform-jobs");
        var request = new MigrationWorkerJobRunRequest(
            "platform-jobs",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_jobs"] = "seo_warm,queued\nseo_warm,running\nbad"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["claims"]);
        Assert.Equal("1", output.Metrics["queued"]);
        Assert.Equal("1", output.Metrics["running"]);
    }

    [Fact]
    public void SeoSitemapPingDryRunValidatesUrlsWithoutPings()
    {
        var executor = new SeoSitemapPingDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "seo-sitemap-ping");
        var request = new MigrationWorkerJobRunRequest(
            "seo-sitemap-ping",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_urls"] = "https://www.ecomae.com/sitemap.xml\nnot-a-url"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["pings"]);
        Assert.Equal("1", output.Metrics["valid_urls"]);
        Assert.Equal("1", output.Metrics["invalid_urls"]);
    }

}
