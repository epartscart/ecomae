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


    [Fact]
    public void SeoSitemapWarmDryRunValidatesUrlsWithoutWarms()
    {
        var executor = new SeoSitemapWarmDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "seo-sitemap-warm");
        var request = new MigrationWorkerJobRunRequest(
            "seo-sitemap-warm",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_urls"] = "https://www.ecomae.com/sitemap.xml\nnot-a-url"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["warms"]);
        Assert.Equal("1", output.Metrics["valid_urls"]);
    }

    [Fact]
    public void UaeTaxLegislationDryRunValidatesDocsWithoutWrites()
    {
        var executor = new UaeTaxLegislationDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "uae-tax-legislation");
        var request = new MigrationWorkerJobRunRequest(
            "uae-tax-legislation",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_docs"] = "vat-guide,VAT Guide\nbad"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("1", output.Metrics["valid_docs"]);
    }

    [Fact]
    public void ApaiBackgroundJobsDryRunValidatesSampleWithoutClaims()
    {
        var executor = new ApaiBackgroundJobsDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "apai-background-jobs");
        var request = new MigrationWorkerJobRunRequest(
            "apai-background-jobs",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_jobs"] = "crawl_hourly,pending\ncrawl_daily,done\nbad"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["claims"]);
        Assert.Equal("1", output.Metrics["pending"]);
    }


    [Fact]
    public void FulfillmentQueueDryRunValidatesSampleWithoutClaims()
    {
        var executor = new FulfillmentQueueDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "fulfillment-queue");
        var request = new MigrationWorkerJobRunRequest(
            "fulfillment-queue",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_orders"] = "1001,queued\n1002,done\nbad"
            });

        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["claims"]);
        Assert.Equal("1", output.Metrics["queued"]);
    }

    [Fact]
    public void ApaiSyncCategoriesDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ApaiSyncCategoriesDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "apai-sync-categories");
        var request = new MigrationWorkerJobRunRequest(
            "apai-sync-categories",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_categories"] = "10,Brakes\nbad"
            });

        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("1", output.Metrics["valid_categories"]);
    }

    [Fact]
    public void IntegrationsCleanupDryRunValidatesSampleWithoutDeletes()
    {
        var executor = new IntegrationsCleanupDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "integrations-cleanup");
        var request = new MigrationWorkerJobRunRequest(
            "integrations-cleanup",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_integrations"] = "old_feed,90\nnew_feed,7\nbad"
            });

        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["deletes"]);
        Assert.Equal("1", output.Metrics["stale_candidates"]);
    }


    [Fact]
    public void ProductExistLimitDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ProductExistLimitDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "product-exist-limit");
        var request = new MigrationWorkerJobRunRequest(
            "product-exist-limit",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["limit"] = "1",
                ["sample_products"] = "A-1,3\nB-2,1\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("1", output.Metrics["over_limit"]);
    }

    [Fact]
    public void CacheWarmupDryRunValidatesKeysWithoutWarms()
    {
        var executor = new CacheWarmupDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "cache-warmup");
        var request = new MigrationWorkerJobRunRequest(
            "cache-warmup",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_keys"] = "epc_catalog_vin\nbad key"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["warms"]);
        Assert.Equal("1", output.Metrics["valid_keys"]);
    }

    [Fact]
    public void CatalogMissFillDryRunSimulatesWithoutOutboundOrWrites()
    {
        var executor = new CatalogMissFillDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "catalog-miss-fill");
        var request = new MigrationWorkerJobRunRequest(
            "catalog-miss-fill",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_actions"] = "engines,section=passenger&mfa_id=999999001\nvin,vin=ZZZMISS\narticles\nnot-an-action"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["outbound"]);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("0", output.Metrics["fills"]);
        Assert.Equal("2", output.Metrics["valid_actions"]);
        Assert.Equal("1", output.Metrics["invalid_actions"]);
        Assert.Equal("1", output.Metrics["always_live_rejected"]);
        Assert.Equal("false", output.Metrics["cutover_allowed"]);
    }

    [Fact]
    public void CatalogMissFillDryRunRefusesConfirmFlagsWithoutOutbound()
    {
        var executor = new CatalogMissFillDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "catalog-miss-fill");
        var request = new MigrationWorkerJobRunRequest(
            "catalog-miss-fill",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_actions"] = "engines",
                ["confirm_outbound"] = "true"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-confirm-refused", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("0", output.Metrics["outbound"]);
        Assert.Equal("0", output.Metrics["fills"]);
    }

    [Fact]
    public void ImportOrchestratorDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ImportOrchestratorDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "import-orchestrator");
        var request = new MigrationWorkerJobRunRequest(
            "import-orchestrator",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_imports"] = "uae_csv,120\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("120", output.Metrics["preview_rows"]);
    }

    [Fact]
    public void ApaiHourlyCrawlDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ApaiHourlyCrawlDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "apai-hourly-crawl");
        var request = new MigrationWorkerJobRunRequest(
            "apai-hourly-crawl",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_tenants"] = "demo,parts\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["crawls"]);
        Assert.Equal("1", output.Metrics["valid_rows"]);
    }

    [Fact]
    public void WebhooksProcessDryRunValidatesSampleWithoutSends()
    {
        var executor = new WebhooksProcessDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "webhooks-process");
        var request = new MigrationWorkerJobRunRequest(
            "webhooks-process",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_deliveries"] = "12,pending\n13,done\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["sends"]);
        Assert.Equal("1", output.Metrics["pending"]);
    }

    [Fact]
    public void OfflineResilienceWarmDryRunValidatesTargetsWithoutWarms()
    {
        var executor = new OfflineResilienceWarmDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "offline-resilience-warm");
        var request = new MigrationWorkerJobRunRequest(
            "offline-resilience-warm",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_targets"] = "/api/v1/catalog/status\nbad target"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["warms"]);
        Assert.Equal("1", output.Metrics["valid_targets"]);
    }

    [Fact]
    public void ApaiWeeklyPlatformSyncDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ApaiWeeklyPlatformSyncDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "apai-weekly-platform-sync");
        var request = new MigrationWorkerJobRunRequest(
            "apai-weekly-platform-sync",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_sources"] = "tecdoc,500\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("500", output.Metrics["preview_rows"]);
    }

    [Fact]
    public void ApaiDailySourceExpandDryRunValidatesSampleWithoutWrites()
    {
        var executor = new ApaiDailySourceExpandDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "apai-daily-source-expand");
        var request = new MigrationWorkerJobRunRequest(
            "apai-daily-source-expand",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_expansions"] = "brands,2\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["writes"]);
        Assert.Equal("1", output.Metrics["valid_rows"]);
    }

    [Fact]
    public void ApiClientPingDryRunValidatesSampleWithoutPings()
    {
        var executor = new ApiClientPingDryRunExecutor();
        var job = new MigrationWorkerJobCatalog().Jobs.First(item => item.Key == "api-client-ping");
        var request = new MigrationWorkerJobRunRequest(
            "api-client-ping",
            DateTimeOffset.UnixEpoch,
            "test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_clients"] = "7,/health\nbad"
            });
        var output = executor.Execute(job, request);
        Assert.Equal("dry-run-validated", output.Status);
        Assert.Equal("0", output.Metrics["pings"]);
        Assert.Equal("1", output.Metrics["valid_rows"]);
    }

}
