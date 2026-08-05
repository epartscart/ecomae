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
                "Live smoke runner is opt-in and redacts secrets before external reachability checks.",
                "CloudPanel ensure→issue→validate→capture→commit path issues epc_pricepro_/epc_catalog_ keys without inventing secrets.",
                "Dual-sample compare_*_parity.py scripts and cloudpanel_extract_exact_route_shadow.sh gate one-path promotion."
            ],
            [
                "Confirmed model: ASP.NET Core becomes live primary; PHP project stays available as reference for previous results / gap-finding (see /migration/php-reference-mode).",
                "Keep PHP authoritative on product chrome until route flags are approved per surface.",
                "Require shadow-read evidence before disabling PHP fallback for any route prefix.",
                "Preserve uppercase CP/ERP/BOS aliases and BOS handoff behavior for operator rollback.",
                "After human approval: exact-route CloudPanel promote may set StorefrontAspNetEnabled/AdminAspNetEnabled=true while RequirePhpFallback=true.",
                "Record rollback owner, timestamp, tenant scope, and expected runtime before each traffic move.",
                "Rollback via bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback.",
                "Reference mode ≠ PHP source deletion — keep docroot until a separate decommission approval."
            ],
            [
                "Authenticated staging-smoke artifacts for price/catalog/surface digests are attached (never invented).",
                "All required parity reports are green for the target surface (compare_*_parity.py match=true).",
                "Production-like data replay has matching payloads and latency budget evidence.",
                "Access-denial and audit-log behavior matches PHP for privileged CP/ERP/BOS workflows.",
                "Only approved location = exact-route shadows are enabled (never broad /api /cp /erp /bos /storefront).",
                "Human RELEASE_OWNER_APPROVAL.md present (APPROVED_TO_REMOVE_PHP_FALLBACK + KeepPhpProjectAvailable); execute via cloudpanel_execute_aspnet_primary_cutover_operator.sh.",
                "After ASP.NET exact-route primary, PHP reference host remains reachable for dual-sample / human compare (read-only preferred)."
            ]);
    }
}
