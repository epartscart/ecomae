namespace EcomAE.Platform.Migration;

public sealed class MigrationProgressReporter : IMigrationProgressReporter
{
    public MigrationProgressReport BuildReport()
    {
        MigrationProgressItem[] items =
        [
            new("ASP.NET Core platform foundation", 10, 100, "complete", "Keep solution compiling in CI and extend modules incrementally."),
            new("Tenant routing and platform/live/ERP-only classification", 5, 100, "complete", "Connect tenant registry to production MySQL source."),
            new("Migration telemetry, readiness, and cutover reporting", 3, 100, "complete", "Expose reports to operators after ASP.NET Core hosting is enabled."),
            new("Public API compatibility scaffolding", 10, 60, "repository-pipeline-started", "Replace placeholder price lookup with database-backed catalog services and parity tests."),
            new("Legacy CP/ERP/BOS session bridge", 7, 71, "api-client-usage-log-parity", "Validate PHP session cookie semantics and permission mapping against production login flows."),
            new("Super CP migration", 12, 25, "shell-started", "Port login, users, tenant administration, settings, and dashboards."),
            new("Platform ERP migration", 18, 22, "shell-started", "Port finance, inventory, sales, purchase, invoice, reporting, and audit modules."),
            new("Super BOS migration", 8, 25, "shell-started", "Port command center, privileged operations, and audit trails."),
            new("Tenant CP and tenant ERP migration", 10, 0, "pending", "Port tenant-scoped CP/ERP workflows for live and ERP-only tenants."),
            new("Storefront migration", 7, 28, "shell-started", "Port storefront rendering, catalog browsing, cart, checkout, and customer account flows."),
            new("Background jobs and scheduled work", 5, 100, "schedule-planner-ready", "Move PHP cron/setup scripts to EcomAE.Workers with retry and telemetry."),
            new("Production cutover and PHP removal", 5, 40, "route-cutover-endpoint-started", "Complete parity, route feature flags, rollback validation, telemetry, and then remove PHP files/runtime.")
        ];

        var complete = items.Sum(item => item.WeightPercent * item.CompletePercent) / 100;

        return new MigrationProgressReport(
            complete,
            100 - complete,
            "ASP.NET Core migration foundation is in place, but business-surface parity and PHP removal are still pending.",
            items);
    }
}
