namespace EcomAE.Platform.Migration;

public sealed class ZeroPhpCompletionReporter : IZeroPhpCompletionReporter
{
    public ZeroPhpCompletionReport BuildReport()
    {
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
            new("CP, ERP, BOS, and tenant workflow parity", 25, 48, "tenant-fleet-digests-started", [
                "CP/ERP/BOS shells are session-gated with capabilities, dashboard summaries, tenant digests, and ERP KPI SQL parity.",
                "Still need login UX, tenant admin writes, full ERP/BOS workflows, and module ACL."
            ]),
            new("Storefront and public API parity", 15, 97, "account-orders-digest-started", [
                "Catalog/price routes plus customer-gated account shell/summary/orders digests are wired.",
                "Live UMAPI proxy fills, HTML storefront parity, checkout, and staging smoke remain."
            ]),
            new("Background jobs and scheduled work", 10, 88, "dry-run-validators-started", [
                "Dry-run validators cover core cron/queue jobs including product-exist-limit, cache-warmup, and import-orchestrator (writes blocked).",
                "Batch 1 still requires per-job parity samples and live smoke before schedule cutover."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 90, "session-capabilities-wired-evidence-pending", [
                "Admin backend-group claims expose surface capabilities on probe/shells; customer sessions gate account/orders digests.",
                "ERP digests use epc_erp_* cash/supplier mirrors; tenant fleet digests are read-only.",
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
                "Redeploy with git reset --hard origin/main then scripts/cloudpanel_find_and_redeploy.sh (or cloudpanel_bootstrap_from_github.sh).",
                "Run exact-route staging smoke for /api/v1/price/lookup and /api/v1/catalog/status with real API keys.",
                "Attach smoke artifacts, then enable only approved location = exact-route nginx shadows.",
                "Repeat exact-route/job cutovers through all 61 batches until zero PHP-only routes/jobs remain."
            ]);
    }
}
