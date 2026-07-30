namespace EcomAE.Workers;

public sealed class MigrationWorkerJobCatalog
{
    public IReadOnlyCollection<MigrationWorkerJob> Jobs { get; } =
    [
        new("price-import", "api/prices/upload_price.php and supplier import scripts", "EcomAE.Workers.PriceImport", "supplier-triggered and scheduled", "planned", "Imported row counts, validation errors, and price visibility match PHP imports."),
        new("sitemap", "PHP sitemap generation scripts", "EcomAE.Workers.Sitemap", "daily", "planned", "Generated URLs, lastmod values, and tenant scope match PHP sitemap output."),
        new("notifications", "PHP notification cron scripts", "EcomAE.Workers.Notifications", "queue-driven", "planned", "Email/SMS recipients, templates, retries, and audit rows match PHP behavior."),
        new("backups", "PHP backup scripts", "EcomAE.Workers.Backups", "daily", "planned", "Backup files, retention, encryption, and restore checks match operations requirements."),
        new("erp-reports", "ERP scheduled report PHP scripts", "EcomAE.Workers.ErpReports", "scheduled", "planned", "Generated finance reports match ERP PHP totals and delivery schedule.")
    ];
}
