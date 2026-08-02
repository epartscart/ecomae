namespace EcomAE.Platform.Migration;

public sealed class ZeroPhpCompletionReporter : IZeroPhpCompletionReporter
{
    public ZeroPhpCompletionReport BuildReport()
    {
        // Honest weighted progress only. Do not claim 90/100 without evidence for every tracked route/job.
        ZeroPhpCompletionArea[] areas =
        [
            new("Foundation, deployment, and diagnostics", 20, 100, "complete", [
                "Keep ASP.NET Core hosted on 127.0.0.1:5100 behind Nginx.",
                "Keep /health and allowlisted /migration/* public while production routes remain on PHP."
            ]),
            new("Route inventory and cutover ownership", 10, 40, "inventory-complete-execution-pending", [
                "Inventory and ownership assignment are complete for 3049 PHP files / 61 batches.",
                "Execution remains route-by-route; broad /, /api, /cp, /erp, and /bos cutovers stay blocked."
            ]),
            new("CP, ERP, BOS, and tenant workflow parity", 25, 15, "foundation-only", [
                "Port CP login, users, tenant administration, settings, and dashboards.",
                "Port ERP accounting, inventory, invoices, reports, and permission checks.",
                "Port BOS privileged operations and validate tenant CP/ERP host behavior."
            ]),
            new("Storefront and public API parity", 15, 58, "price-and-catalog-cache-routes-started", [
                "Price lookup plus catalog status/manufacturers/models/modifications/brands have DB readers + API-key auth.",
                "Still need article/VIN/live UMAPI endpoints, staging smoke artifacts, and exact-route shadows only after evidence."
            ]),
            new("Background jobs and scheduled work", 10, 40, "dry-run-validators-started", [
                "Dry-run validators exist for price-import, sitemap, backups, notifications, and erp-reports (writes blocked).",
                "Batch 1 still requires per-job parity samples and live smoke before schedule cutover."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 35, "auth-wired-evidence-pending", [
                "API-key auth/quota path is wired for price lookup and catalog status against epc_api_clients.",
                "Staging smoke artifacts, live rollback approvals, and full route evidence packs remain pending."
            ]),
            new("PHP runtime decommission", 5, 0, "blocked", [
                "Remove PHP-FPM, PHP cron, PHP rewrites, and PHP source dependencies only after every route and job has green parity evidence.",
                "Keep PHP fallback required until the final release-owner decommission approval."
            ])
        ];

        var complete = areas.Sum(area => area.WeightPercent * area.CompletePercent) / 100;

        return new ZeroPhpCompletionReport(
            complete,
            100 - complete,
            "not-ready-for-php-removal",
            areas,
            [
                "Deploy ASP.NET foundation on production with diagnostics-only exposure (scripts/cloudpanel_production_deploy_foundation.sh).",
                "Run exact-route staging smoke for /api/v1/price/lookup and /api/v1/catalog/status with real API keys.",
                "Attach smoke artifacts, then enable only approved location = exact-route nginx shadows.",
                "Repeat exact-route/job cutovers through all 61 batches until zero PHP-only routes/jobs remain."
            ]);
    }
}
