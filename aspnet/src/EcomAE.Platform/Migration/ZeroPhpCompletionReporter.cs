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
            new("Route inventory and cutover ownership", 10, 25, "pending-inventory", [
                "Inventory every PHP entrypoint, rewrite, cron target, and API route.",
                "Assign route owners and define exact ASP.NET Core cutover candidates.",
                "Keep broad /, /api, /cp, /erp, and /bos cutovers blocked until unknown routes are zero."
            ]),
            new("CP, ERP, BOS, and tenant workflow parity", 25, 10, "foundation-only", [
                "Port CP login, users, tenant administration, settings, and dashboards.",
                "Port ERP accounting, inventory, invoices, reports, and permission checks.",
                "Port BOS privileged operations and validate tenant CP/ERP host behavior."
            ]),
            new("Storefront and public API parity", 15, 10, "scaffold-only", [
                "Replace storefront shell responses with production rendering and checkout behavior.",
                "Replace catalog/price scaffolds with database-backed implementations.",
                "Run PHP-vs-ASP.NET response parity checks for each public API before cutover."
            ]),
            new("Background jobs and scheduled work", 10, 5, "placeholder", [
                "Move import, sitemap, notification, backup, cleanup, and maintenance jobs into EcomAE.Workers.",
                "Run worker dry-runs with retry, idempotency, and telemetry evidence before enabling production schedules."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 25, "evidence-required", [
                "Validate tenant registry, legacy sessions, API keys, permissions, and audit behavior against PHP.",
                "Add live smoke, error-rate, latency, rollback, and parity evidence for each route.",
                "Confirm data read/write parity for platform, live tenant, and ERP-only tenant hosts."
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
                "Freeze the diagnostics-only Nginx exposure that is now live.",
                "Build a complete PHP route/job inventory and choose one exact low-risk route for the first parity cutover.",
                "Implement that route fully in ASP.NET Core, compare PHP-vs-ASP.NET responses, then cut over only that exact route.",
                "Repeat exact-route cutovers until zero PHP-only routes and zero PHP-backed jobs remain."
            ]);
    }
}
