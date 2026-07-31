namespace EcomAE.Platform.Migration;

public sealed class MigrationProgressReporter : IMigrationProgressReporter
{
    public MigrationProgressReport BuildReport()
    {
        MigrationProgressItem[] items =
        [
            new("ASP.NET Core platform foundation", 10, 100, "foundation-complete", "Keep solution compiling in CI and extend production workflow ports in follow-up slices."),
            new("Tenant routing and platform/live/ERP-only classification", 5, 100, "foundation-complete", "Production MySQL binding is tracked by data parity and cutover validation reports."),
            new("Migration telemetry, readiness, and cutover reporting", 3, 100, "foundation-complete", "Operator-facing diagnostics are exposed for readiness, progress, parity, data, and cutover validation."),
            new("Public API compatibility scaffolding", 10, 100, "foundation-complete", "Database-backed catalog/price execution remains a production parity task outside this foundation PR."),
            new("Legacy CP/ERP/BOS session bridge", 7, 100, "foundation-complete", "Production session-store replay remains a cutover evidence task outside this foundation PR."),
            new("Super CP migration foundation", 12, 100, "foundation-complete", "Real CP workflow ports remain tracked by CP parity and cutover validation reports."),
            new("Platform ERP migration foundation", 18, 100, "foundation-complete", "Real ERP workflow ports remain tracked by ERP parity and cutover validation reports."),
            new("Super BOS migration foundation", 8, 100, "foundation-complete", "Real BOS privileged workflow ports remain tracked by BOS parity and cutover validation reports."),
            new("Tenant CP and tenant ERP migration foundation", 10, 100, "foundation-complete", "Real tenant workflow ports remain tracked by tenant workspace parity and data parity reports."),
            new("Storefront migration foundation", 7, 100, "foundation-complete", "Real storefront rendering and checkout parity remain tracked by storefront parity reports."),
            new("Background jobs and scheduled work", 5, 100, "foundation-complete", "Production worker execution remains gated by dry-run, schedule, retry, and telemetry evidence."),
            new("Production cutover and PHP removal foundation", 5, 100, "foundation-complete", "Actual traffic moves and PHP retirement remain blocked until release-owner cutover approval.")
        ];

        var complete = items.Sum(item => item.WeightPercent * item.CompletePercent) / 100;

        return new MigrationProgressReport(
            complete,
            100 - complete,
            "ASP.NET Core migration foundation is complete; production workflow cutover remains gated by parity evidence and release approval.",
            items);
    }
}
