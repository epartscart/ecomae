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
            new("Public API compatibility scaffolding", 10, 95, "data-parity-contracts-visible", "Replace placeholder price lookup with database-backed catalog services and parity tests."),
            new("Legacy CP/ERP/BOS session bridge", 7, 100, "session-and-api-client-contracts-ready", "Validate PHP session cookie semantics and permission mapping against production login flows."),
            new("Super CP migration", 12, 45, "cp-cutover-validation-visible", "Port login, users, tenant administration, settings, and dashboards."),
            new("Platform ERP migration", 18, 40, "erp-cutover-validation-visible", "Port finance, inventory, sales, purchase, invoice, reporting, and audit modules."),
            new("Super BOS migration", 8, 50, "bos-cutover-validation-visible", "Port command center, privileged operations, and audit trails."),
            new("Tenant CP and tenant ERP migration", 10, 45, "tenant-cutover-validation-visible", "Port tenant-scoped CP/ERP workflows for live and ERP-only tenants."),
            new("Storefront migration", 7, 55, "storefront-cutover-validation-visible", "Port storefront rendering, catalog browsing, cart, checkout, and customer account flows."),
            new("Background jobs and scheduled work", 5, 100, "schedule-planner-ready", "Move PHP cron/setup scripts to EcomAE.Workers with retry and telemetry."),
            new("Production cutover and PHP removal", 5, 90, "cutover-validation-plan-ready", "Complete parity, route feature flags, rollback validation, telemetry, and then remove PHP files/runtime.")
        ];

        var complete = items.Sum(item => item.WeightPercent * item.CompletePercent) / 100;

        return new MigrationProgressReport(
            complete,
            100 - complete,
            "ASP.NET Core migration foundation is in place, but business-surface parity and PHP removal are still pending.",
            items);
    }
}
