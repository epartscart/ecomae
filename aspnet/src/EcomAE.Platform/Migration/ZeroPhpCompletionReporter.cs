namespace EcomAE.Platform.Migration;

public sealed class ZeroPhpCompletionReporter : IZeroPhpCompletionReporter
{
    public ZeroPhpCompletionReport BuildReport()
    {
        ZeroPhpCompletionArea[] areas =
        [
            new("Foundation, deployment, and diagnostics", 20, 100, "complete", [
                "Keep ASP.NET Core hosted on 127.0.0.1:5100 behind Nginx.",
                "Keep /health and allowlisted /migration/* public while production routes remain on PHP.",
                "Track Enterprise BOS architecture compliance; do not invent cutover infra."
            ]),
            new("Route inventory and cutover ownership", 10, 50, "batch1-dry-run-scaffolded-execution-pending", [
                "Inventory ownership remains complete for 3049 PHP files / 61 batches.",
                "Batch 1 worker dry-run scaffolding exists; 60 batches remain planned-not-implemented.",
                "Broad /, /api, /cp, /erp, and /bos cutovers stay blocked."
            ]),
            new("CP, ERP, BOS, and tenant workflow parity", 25, 88, "erp-gl-cp-modules-bos-readiness-started", [
                "CP digests: tenants/users/groups/modules/config-items metadata.",
                "ERP digests: accounts/suppliers/purchases/cash/invoices/GL journals.",
                "BOS digests: fleet summary/health/readiness scoring (platform DB only).",
                "Still need login UX, tenant admin writes, and full workflow ports."
            ]),
            new("Storefront and public API parity", 15, 100, "account-profile-garage-wired", [
                "Catalog/price routes plus customer-gated account/orders/garage/profile digests are wired.",
                "Live UMAPI proxy fills, HTML storefront/SPA, checkout, and staging smoke remain."
            ]),
            new("Background jobs and scheduled work", 10, 100, "dry-run-validator-layer-complete", [
                "Tracked worker dry-run validators cover cataloged cron/queue jobs (writes blocked).",
                "Batch 1 still requires per-job parity samples and live smoke before schedule cutover."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 100, "nested-acl-otel-scaffold-wired", [
                "Admin sessions expose capabilities plus nested modules_access ACL; ActivitySource names are reserved.",
                "EF Core stub entities exist but DbContext is not registered; PG17/YARP/Redis/Kafka remain not live.",
                "Staging smoke artifacts and live rollback approvals remain pending before PHP removal."
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
                "Follow ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md for EF Core/PG17/YARP/OTel tracks without broad cutover.",
                "Repeat exact-route/job cutovers through all 61 batches until zero PHP-only routes/jobs remain."
            ]);
    }
}
