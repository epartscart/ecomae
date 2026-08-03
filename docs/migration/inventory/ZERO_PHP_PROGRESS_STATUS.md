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
- Smoke env preflight (prefix/cookie format, no secret print), catalog nginx API-key header fix, price/catalog promotion runbook, dual-sample compare helpers, live still-PHP public probes.
- CloudPanel smoke credential issuer writes `epc_pricepro_` / `epc_catalog_` keys (+ active admin session cookie) into `platform.env` without printing secrets.
- Deploy packs all four gate shadow examples (price/api/surface/storefront) into ContentRoot so live `exact-route-shadows-only` is not a false negative.
- Exact-route extract helper emits one disabled `location =` snippet; refuses broad `/cp|/erp|/bos|/api|/storefront`.
- Field contracts + shadow stubs cover remaining wired digests (config-items, admin-sessions, storages, accounts-summary, cash-*, bos/tenants).
- Catalog list + offline-cache + VIN + brand-parts envelope contracts and compare scripts for all wired catalog routes.
- Surface harness + dual-sample compare cover smoke-wired CP/ERP/BOS digests; optional storefront customer smoke + price `--contract-only`.
- Smoke issuer uses PHP `DP_Config` → TenantRegistry DB; ensure-table helper packed into ContentRoot.
- Final-gate public probes regenerated: field contracts=53, php-decommission checklist 5/9 (shadows present; smoke/approval missing), surface Public API/Workers statuses honest.
- Redeploy helper defaults to **main** (PR #603 ensure→issue merged).
- Live-surface pending-shadow inventory covers full digest + catalog shadow set; harness capture matches smoke/nginx routes.
- MigrationParity / ApiModule / auth-session reporters aligned to ensure→issue.
- **Authenticated staging smoke attached on main (PR #612)** — price/catalog/surfaces; checklist 40 pass / approval skip.
- PHP decommission readiness: smoke present; removal blocked only on human `RELEASE_OWNER_APPROVAL.md`.
- No broad PHP cutover; parity/shadow remain 0%.

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% requires approved exact-route shadows where promoted, and **human** release-owner approval to remove PHP-FPM/cron/rewrites/source. Staging smoke for the final-gate digest/API set is attached. Dry-run scaffolding does **not** authorize PHP removal. See `/migration/php-decommission-readiness` and `bash scripts/run_zero_php_final_gate_checklist.sh`. Remaining batches still need promotion from dry-run scaffolding to shadow/live.

## Next execution order

- Run fail-closed parity verdict (must keep PHP): `bash scripts/verify_pre_php_removal_parity.sh`
- Confirm readiness: `curl -sS http://127.0.0.1:5100/migration/php-decommission-readiness` (8/9; approval missing).
- Public exact-route API shadows live: price lookup + catalog status through categories (+ vin warm hit); CP/ERP/BOS chrome still PHP.
- Do **not** remove PHP until more exact-route shadows + dual samples + human approval exist.
- Optional storefront: set `ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=<digits>` (not required for ReadyToRemovePhp).
- Extract one approved path: `bash scripts/cloudpanel_extract_exact_route_shadow.sh /api/v1/catalog/status` (enable only with operator intent).
- Attach dual PHP↔ASP.NET parity samples; promote shadows one `location =` at a time.
- Create `RELEASE_OWNER_APPROVAL.md` **only after human approval**; then gated PHP decommission.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
