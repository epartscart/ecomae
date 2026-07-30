namespace EcomAE.Platform.Migration;

public sealed class MigrationCutoverPlanner : IMigrationCutoverPlanner
{
    public MigrationCutoverPlan BuildPlan()
    {
        MigrationCutoverStep[] steps =
        [
            new(1, "/migration/*", "ASP.NET Core", "ASP.NET Core", "diagnostics only; no customer traffic", true),
            new(2, "/api/v1/catalog/status", "PHP", "ASP.NET Core", "schema parity and monitoring enabled", false),
            new(3, "/api/v1/price/lookup", "PHP", "ASP.NET Core", "catalog database parity and response-time parity", false),
            new(4, "ecomae.com/CP", "PHP", "ASP.NET Core", "login, users, permissions, tenant admin, and settings parity", false),
            new(5, "ecomae.com/ERP", "PHP", "ASP.NET Core", "finance, inventory, invoice, report, and audit parity", false),
            new(6, "ecomae.com/BOS", "PHP", "ASP.NET Core", "privileged BOS action parity and audit logging", false),
            new(7, "tenant.com/CP", "PHP", "ASP.NET Core", "tenant-scoped CP auth and permission parity", false),
            new(8, "tenant.com/ERP", "PHP", "ASP.NET Core", "live-tenant and ERP-only tenant workflow parity", false),
            new(9, "PHP cron/setup scripts", "PHP", "EcomAE.Workers", "job telemetry and retry policy parity", false)
        ];

        return new MigrationCutoverPlan(
            "feature-flagged-reverse-proxy-cutover-with-php-fallback",
            steps,
            [
                "Disable the ASP.NET Core route flag for the affected surface.",
                "Route the affected path back to the existing PHP upstream.",
                "Keep database schema backward compatible until both runtimes are fully cut over.",
                "Review parity telemetry before re-enabling ASP.NET Core traffic."
            ]);
    }
}
