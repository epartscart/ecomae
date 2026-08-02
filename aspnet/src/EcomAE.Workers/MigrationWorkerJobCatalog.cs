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
        new("seo-sitemap-ping", "epc-seo-sitemap-ping.php", "EcomAE.Workers.SeoSitemapPing", "scheduled", "planned", "Validated sitemap/ping URLs match PHP targets; no outbound ping until parity smoke.")
    ];
}
