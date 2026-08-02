# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.

Enterprise BOS target stack tracking lives in `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` and must not be confused with Zero-PHP completion.

## Current percentage

- True zero-PHP completion: 95.0%.
- Pending to 100%: 5.0%.
- Final-gate unlock path is wired (`ReadyToRemovePhp` becomes true only with validated smoke + approval). PHP is **not** removed yet.
- Foundation/planning floor: 35.0%.
- Route/job implementation started (not parity-ready): digests + nested ACL + worker dry-run layer + all 61 batches dry-run scaffolding.
- Route/job parity-ready: 0.0%.
- Route/job shadow-or-better: 0.0%.

## Inventory

- Total PHP files: 3049.
- Job-like PHP files: 140.

| Surface | PHP files |
| --- | ---: |
| api | 53 |
| bos | 9 |
| cp | 638 |
| erp | 431 |
| platform | 812 |
| storefront | 1106 |

## Planning progress

- Ownership assigned: 3049 (100.0%).
- Batch assignments: 3049 (100.0%).
- Total exact-route batches: 61.
- Batch statuses: all 61 batches `aspnet-dry-run-scaffolded`.

## Concrete implementation progress (honest)

- Catalog/price API routes with DB/cache readers + API-key auth.
- Admin nested modules_access ACL + surface capabilities.
- CP digests: dashboard, tenants, users, groups, modules, config-items, menus, pages, admin-sessions, storages, currencies, api-clients metadata.
- ERP digests: accounts, suppliers, purchases, cash, invoices, GL, COA, warehouses, sales-orders, purchase-orders, inventory-stock KPIs.
- BOS digests: fleet summary/health/readiness + audit-log.
- Storefront account/orders/garage/profile digests.
- Tracked write-blocked worker dry-run validator layer + batches 1–61 dry-run scaffolding.
- Presentation-preserving CP/ERP/BOS/storefront HTML shells (reuse PHP CSS assets; JSON default for tooling).
- Live Super CP / tenant / ERP / frontend link catalog + stack probe (`/migration/live-surface-links`).
- Field/function/presentation parity contracts + harness (`/migration/surface-field-parity`, `scripts/run_surface_parity_harness.sh`).
- Migration-mode digest contract validator + golden samples (no secrets); live final-gate checklist ~5/9 until authenticated smoke is attached from CloudPanel.
- Final-gate smoke hardening: authenticated digest HTTP 200 required; catalog + storefront shadow examples in gate; CloudPanel commit helper for real smoke only.
- PHP decommission readiness reporter documents blockers; removal remains blocked.
- No broad PHP cutover; parity/shadow remain 0%.

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% requires green parity evidence for every tracked route/job, staging smoke, approved exact-route shadows, and release-owner approval to remove PHP-FPM/cron/rewrites/source. Dry-run scaffolding does **not** authorize PHP removal. See `/migration/php-decommission-readiness` and `bash scripts/run_zero_php_final_gate_checklist.sh`. Remaining batches still need promotion from dry-run scaffolding to shadow/live.

## Next execution order

- Redeploy by refreshing `/opt/ecomae-aspnet-source` to `origin/main` first.
- Run `bash scripts/run_zero_php_final_gate_checklist.sh`.
- On CloudPanel: add smoke keys to `/etc/ecomae-aspnet/platform.env`, then `bash scripts/cloudpanel_capture_final_gate_artifacts.sh`.
- Run opt-in staging smoke with real `epc_pricepro_` / `epc_catalog_` keys and admin cookies; copy JSON into `docs/migration/evidence/decommission/staging-smoke/`.
- Attach parity samples; enable only approved `location =` shadows.
- Create `RELEASE_OWNER_APPROVAL.md` only after human approval; then remove PHP.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
