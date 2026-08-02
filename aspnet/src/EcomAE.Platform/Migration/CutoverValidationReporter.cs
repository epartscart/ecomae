namespace EcomAE.Platform.Migration;

public sealed class CutoverValidationReporter : ICutoverValidationReporter
{
    public CutoverValidationReport BuildReport()
    {
        return new CutoverValidationReport(
            "validation-plan-ready-traffic-cutover-blocked",
            [
                "RouteCutoverDecisionMiddleware emits target-runtime and PHP-fallback headers for every resolved tenant.",
                "Parity endpoints expose CP, ERP, BOS, storefront, tenant workspace, catalog, price, auth, session, data, readiness, and progress status.",
                "Foundation checks assert route constants, DI registrations, surface modules, worker planners, and PHP alias compatibility.",
                "Live smoke runner is opt-in and redacts secrets before external reachability checks."
            ],
            [
                "Keep PHP authoritative until route flags are approved per surface.",
                "Require shadow-read evidence before disabling PHP fallback for any route prefix.",
                "Preserve uppercase CP/ERP/BOS aliases and BOS handoff behavior for operator rollback.",
                "Record rollback owner, timestamp, tenant scope, and expected runtime before each traffic move."
            ],
            [
                "All required parity reports are green for the target surface.",
                "Production-like data replay has matching payloads and latency budget evidence.",
                "Access-denial and audit-log behavior matches PHP for privileged CP/ERP/BOS workflows.",
                "A manual release owner approves feature-flag enforcement and rollback monitoring."
            ]);
    }
}
