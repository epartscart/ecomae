namespace EcomAE.Workers;

public sealed class MigrationWorkerJobCatalog
{
    public IReadOnlyCollection<MigrationWorkerJob> Jobs { get; } =
    [
        new("price-import", "api/prices/upload_price.php and supplier import scripts", "EcomAE.Workers.PriceImport", "supplier-triggered and scheduled", "planned", "Imported row counts, validation errors, and price visibility match PHP imports."),
        new("sitemap", "PHP sitemap generation scripts", "EcomAE.Workers.Sitemap", "daily", "planned", "Generated URLs, lastmod values, and tenant scope match PHP sitemap output."),
        new("notifications", "PHP notification cron scripts", "EcomAE.Workers.Notifications", "queue-driven", "planned", "Email/SMS recipients, templates, retries, and audit rows match PHP behavior."),
        new("backups", "PHP backup scripts", "EcomAE.Workers.Backups", "daily", "planned", "Backup files, retention, encryption, and restore checks match operations requirements."),
        new("erp-reports", "ERP scheduled report PHP scripts", "EcomAE.Workers.ErpReports", "scheduled", "planned", "Generated finance reports match ERP PHP totals and delivery schedule."),
        new("currency-live-rates", "epc-currency-live-rates-cron.php", "EcomAE.Workers.CurrencyLiveRates", "scheduled", "planned", "Previewed ISO/rate rows match PHP dry=1 output; no shop_currencies writes until parity smoke."),
        new("demo-expire", "epc-demo-expire-cron.php", "EcomAE.Workers.DemoExpire", "scheduled", "planned", "Previewed expired demo tenants match PHP dry preview; no deletes/reminders until parity smoke."),
        new("platform-jobs", "epc-platform-jobs-cron.php", "EcomAE.Workers.PlatformJobs", "scheduled", "planned", "Previewed queued/running rows match PHP claim set; no claim/complete until parity smoke."),
        new("seo-sitemap-ping", "epc-seo-sitemap-ping.php", "EcomAE.Workers.SeoSitemapPing", "scheduled", "planned", "Validated sitemap/ping URLs match PHP targets; no outbound ping until parity smoke."),
        new("seo-sitemap-warm", "epc-seo-sitemap-warm.php", "EcomAE.Workers.SeoSitemapWarm", "scheduled", "planned", "Validated warm URLs match PHP targets; no HTTP warm fetches until parity smoke."),
        new("uae-tax-legislation", "epc-uae-tax-legislation-cron.php", "EcomAE.Workers.UaeTaxLegislation", "scheduled", "planned", "Validated legislation doc rows match PHP dry preview; no KB writes until parity smoke."),
        new("apai-background-jobs", "epc-apai-background-jobs-cron.php", "EcomAE.Workers.ApaiBackgroundJobs", "scheduled", "planned", "Previewed pending APAI jobs match PHP claim set; no claims/writes until parity smoke."),
        new("fulfillment-queue", "content/shop/finance/epc_fulfillment_queue.php", "EcomAE.Workers.FulfillmentQueue", "queue-driven", "planned", "Previewed queued fulfillment orders match PHP queue; no claim/complete until parity smoke."),
        new("apai-sync-categories", "epc-apai-sync-categories-all.php", "EcomAE.Workers.ApaiSyncCategories", "scheduled", "planned", "Validated category rows match PHP sync preview; no category writes until parity smoke."),
        new("integrations-cleanup", "epc-integrations-cleanup.php", "EcomAE.Workers.IntegrationsCleanup", "scheduled", "planned", "Previewed stale integrations match PHP cleanup candidates; no deletes until parity smoke."),
        new("product-exist-limit", "content/cron/product_exist_limit.php", "EcomAE.Workers.ProductExistLimit", "scheduled", "planned", "Previewed over-limit product rows match PHP dry preview; no product writes until parity smoke."),
        new("cache-warmup", "epc-cache-warmup.php / epc-cache-warm-tenants.php", "EcomAE.Workers.CacheWarmup", "scheduled", "planned", "Validated cache keys match PHP warm targets; no cache writes until parity smoke."),
        new("import-orchestrator", "content/general_pages/epc_import_orchestrator.php", "EcomAE.Workers.ImportOrchestrator", "queue-driven", "planned", "Previewed import sources/rows match PHP orchestrator; no import writes until parity smoke."),
        new("apai-hourly-crawl", "epc-apai-hourly-crawl.php", "EcomAE.Workers.ApaiHourlyCrawl", "scheduled", "planned", "Validated tenant/source crawl targets match PHP preview; no crawl writes until parity smoke."),
        new("webhooks-process", "epc-webhooks-process.php", "EcomAE.Workers.WebhooksProcess", "queue-driven", "planned", "Previewed pending webhook deliveries match PHP queue; no sends/retries until parity smoke."),
        new("offline-resilience-warm", "epc-offline-resilience-warm.php", "EcomAE.Workers.OfflineResilienceWarm", "scheduled", "planned", "Validated warm targets match PHP preview; no HTTP/cache writes until parity smoke.")
    ];
}
