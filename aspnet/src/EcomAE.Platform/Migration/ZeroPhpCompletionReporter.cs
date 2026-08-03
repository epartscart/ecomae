namespace EcomAE.Platform.Migration;

public sealed class ZeroPhpCompletionReporter : IZeroPhpCompletionReporter
{
    private readonly IPhpDecommissionReadinessReporter _decommission;

    public ZeroPhpCompletionReporter(IPhpDecommissionReadinessReporter decommission)
    {
        _decommission = decommission;
    }

    public ZeroPhpCompletionReport BuildReport()
    {
        var gate = _decommission.BuildReport();
        var decommissionComplete = gate.ReadyToRemovePhp ? 100 : 0;
        var decommissionStatus = gate.ReadyToRemovePhp ? "ready-for-php-removal" : "blocked";

        ZeroPhpCompletionArea[] areas =
        [
            new("Foundation, deployment, and diagnostics", 20, 100, "complete", [
                "Keep ASP.NET Core hosted on 127.0.0.1:5100 behind Nginx.",
                "Keep /health and allowlisted /migration/* public while production routes remain on PHP.",
                "Track Enterprise BOS architecture compliance; do not invent cutover infra."
            ]),
            new("Route inventory and cutover ownership", 10, 100, "all-61-batches-dry-run-scaffolded-execution-pending", [
                "Inventory ownership remains complete for 3049 PHP files / 61 batches.",
                "All 61 batches have ASP.NET dry-run scaffolding; parity/shadow/live remain 0%.",
                "Broad /, /api, /cp, /erp, and /bos cutovers stay blocked."
            ]),
            new("CP, ERP, BOS, and tenant workflow parity", 25, 100, "po-stock-currencies-api-clients-wired", [
                "CP digests include menus/pages/sessions/storages/currencies/api-clients metadata.",
                "ERP digests include COA/warehouses/sales-orders/purchase-orders/inventory-stock KPIs.",
                "BOS digests include fleet readiness and audit-log.",
                "Still need login UX, tenant admin writes, and full workflow ports before parity claims."
            ]),
            new("Storefront and public API parity", 15, 100, "account-profile-garage-wired", [
                "Catalog/price routes plus customer-gated account/orders/garage/profile digests are wired.",
                "Live UMAPI proxy fills, HTML storefront/SPA, checkout, and staging smoke remain."
            ]),
            new("Background jobs and scheduled work", 10, 100, "dry-run-validator-layer-complete", [
                "Tracked worker dry-run validators cover cataloged cron/queue jobs (writes blocked).",
                "All 61 cutover batches still require per-entry parity samples and live smoke before schedule cutover."
            ]),
            new("Data, auth, observability, and rollback evidence", 15, 100, "nested-acl-otel-scaffold-wired", [
                "Admin sessions expose capabilities plus nested modules_access ACL; ActivitySource names are reserved.",
                "EF Core stub entities exist but DbContext is not registered; PG17/YARP/Redis/Kafka remain not live.",
                "Staging smoke artifacts and live rollback approvals remain pending before PHP removal."
            ]),
            new("PHP runtime decommission", 5, decommissionComplete, decommissionStatus, gate.ReadyToRemovePhp
                ?
                [
                    "Final-gate checklist is complete and ReadyToRemovePhp is true.",
                    "Run ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh on CloudPanel only.",
                    "Keep exact-route shadows; do not enable broad tree cutovers."
                ]
                :
                [
                    $"Final-gate checklist {gate.ChecklistCompleteCount}/{gate.ChecklistTotalCount} ({gate.ChecklistCompletePercent}%).",
                    "Authenticated staging smoke + RELEASE_OWNER_APPROVAL.md are still required.",
                    "See /migration/php-decommission-readiness for the explicit blocker list."
                ])
        ];

        var complete = areas.Sum(area => area.WeightPercent * area.CompletePercent) / 100;
        return new ZeroPhpCompletionReport(
            complete,
            100 - complete,
            gate.ReadyToRemovePhp ? "ready-for-php-removal" : "not-ready-for-php-removal",
            areas,
            gate.ReadyToRemovePhp
                ?
                [
                    "ReadyToRemovePhp is true — run the gated CloudPanel decommission script with explicit confirmation.",
                    "Keep rollback available and avoid broad /api /cp /erp /bos /storefront cutover."
                ]
                :
                [
                    "Redeploy main: bash scripts/cloudpanel_redeploy_final_gate_branch.sh (or git reset --hard origin/main && bash scripts/cloudpanel_find_and_redeploy.sh).",
                    "Diagnose smoke DB: cloudpanel_diagnose_smoke_db.sh (TenantRegistry vs PHP app db, redacted).",
                    "If CREATE denied: cloudpanel_apply_epc_api_clients_ddl.sh (debian.cnf) or align TenantRegistry Database= via cloudpanel_align_tenant_registry_to_php_db.sh.",
                    "Ensure table + issue smoke creds: cloudpanel_ensure_epc_api_clients_table.sh → ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES cloudpanel_issue_smoke_credentials.sh (never invent keys).",
                    "Validate env (redacted): cloudpanel_validate_final_gate_env.sh / cloudpanel_prepare_smoke_secrets.sh.",
                    "Capture/commit staging-smoke for price lookup, catalog status, and surface digests.",
                    "Optional: ECOMAE_CUSTOMER_COOKIE_HEADER for storefront digests (not required for ReadyToRemovePhp).",
                    "Promote one location = shadow at a time; compare_catalog_status_parity.py / compare_catalog_list_parity.py before more catalog paths.",
                    "Follow ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md for EF Core/PG17/YARP/OTel tracks without broad cutover.",
                    "The remaining 5% is PHP runtime decommission only — run scripts/run_zero_php_final_gate_checklist.sh, attach staging smoke/parity artifacts, then release-owner approval before PHP removal."
                ]);
    }
}
