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
            new("CP, ERP, BOS, and tenant workflow parity", 25, 34, "shell-summaries-started", [
                "CP/ERP/BOS shells require admin backend-group sessions and expose identity + read-only dashboard summaries.",
                "Still need login UX, tenant admin, ERP workflows, BOS ops, and fine-grained role mapping."
            ]),
            new("Storefront and public API parity", 15, 95, "price-catalog-and-account-shell-started", [
                "Price/catalog cache routes including suppliers plus customer-gated storefront account shell/summary are wired.",
                "Live UMAPI proxy fills, HTML storefront parity, checkout, and staging smoke remain."
            ]),
            new("Background jobs and scheduled work", 10, 80, "dry-run-validators-started", [
                "Dry-run validators cover core cron/queue jobs including fulfillment, APAI sync/cleanup, SEO, tax, and platform jobs (writes blocked).",
                "Batch 1 still requires per-job parity samples and live smoke before schedule cutover."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 84, "auth-claims-wired-evidence-pending", [
                "API-key auth and admin backend-group claims are wired; customer sessions gate storefront account routes.",
                "UMAPI usage, platform-jobs, and CP/ERP/BOS/storefront summary diagnostics are read-only.",
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
