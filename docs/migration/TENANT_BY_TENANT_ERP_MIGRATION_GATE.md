# Tenant-by-tenant ERP migration gate

This is the operator gate for moving ERP ownership from PHP to ASP.NET per
tenant. It is a gate contract, not evidence that any named production tenant
has passed. The current machine-readable board is
`docs/migration/evidence/tenant-by-tenant-erp-migration-gate.json`.

## Promotion law

Each tenant must independently pass every `ErpTenantAcceptanceCatalog`
promotion gate:

1. PHP route parity
2. Functional happy path
3. Validation and denial
4. Physical tenant isolation
5. Persisted SQL and audit readback
6. Authenticated browser parity
7. Production recovery and ownership rollback
8. UAT and production release-owner approval

The result is fail-closed:

```text
ready(tenant) =
  every_required_gate_has_direct_evidence(tenant)
  && production_backup_restore_verified(tenant)
  && release_owner_approval_recorded(tenant)
```

No tenant may be promoted because another tenant passed, because a weighted
percentage increased, or because the service health endpoint returned 200.

## Required evidence bundle

For each tenant, retain a dated bundle containing:

- resolved host, tenant registry identity, database identity, and active
  release SHA;
- PHP-vs-ASP.NET route/field/action/permission comparison;
- happy-path, invalid, denial, duplicate/retry, and audit results;
- direct SQL readback for business rows, related ledger/allocation rows,
  sequences, and audit records;
- two-physical-database isolation evidence for representative ERP processes;
- authenticated desktop and mobile browser captures/readback;
- backup, restore, rollback, and post-rollback smoke results;
- UAT owner, date, scope, and production release-owner approval.

Evidence must identify the tenant. A throwaway rehearsal is labelled as
rehearsal evidence and cannot satisfy production backup/restore, rollback, UAT,
or release approval by implication.

## Safe operating sequence

1. Freeze the tenant's current PHP ownership and record the active release SHA.
2. Capture the PHP baseline and database backup metadata.
3. Run the ASP.NET dry-run and denial suite with zero-write verification.
4. Run confirmed writes only on approved throwaway or scheduled tenant data.
5. Compare authenticated UI and direct SQL/audit readback.
6. Verify physical isolation against a second database.
7. Rehearse restore and ownership rollback; confirm PHP fallback remains usable.
8. Obtain tenant-specific UAT and production release-owner approval.
9. Promote only the exact approved routes for that tenant.
10. Re-run health, release-SHA, route, browser, and rollback smoke checks.

Until all five named tenants have their own complete bundles, keep
`cutoverAllowed=false`, `readyForPhpRemoval=false`, PHP/PHP-FPM available, and
broad `/cp`, `/erp`, `/bos`, API, and storefront cutover disabled.
