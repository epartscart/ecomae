# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports **true production cutover** separately from the weighted completion meter so we do not overstate 0% PHP readiness.

Enterprise BOS target stack tracking lives in `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` and must not be confused with Zero-PHP completion.

## Current percentage (weighted meter)

- True zero-PHP completion meter: **95.0%**.
- Pending to 100% (PHP runtime decommission residual): **5.0%**.
- Final-gate unlock path is wired (`ReadyToRemovePhp` becomes true only with validated smoke + approval). PHP is **not** removed yet.
- Foundation/planning floor: 35.0%.
- Route/job implementation started (not parity-ready): digests + nested ACL + worker dry-run layer + all 61 batches dry-run scaffolding.
- Route/job parity-ready: 0.0%.
- Route/job shadow-or-better: 0.0%.

The **95% / 5%** meter is the historical weighted Zero-PHP score (scaffolding + gate wiring + attached loopback smoke). It is **not** “95% of public routes cut over to ASP.NET.”

## Live cutover scorecard (www.ecomae.com) — 2026-08-03

### Exact-route ASP.NET shadows live (public unauth → ASP.NET JSON 401)

| Path | Auth / warm notes |
| --- | --- |
| `/health`, `/migration/*` | ASP.NET diagnostics |
| `/api/v1/price/lookup` | Live |
| `/api/v1/catalog/status` | Live |
| `/api/v1/catalog/manufacturers` | Live; `section=passenger` |
| `/api/v1/catalog/models` | Live; needs `mfa_id>0`; warm e.g. `111` |
| `/api/v1/catalog/modifications` | Live; needs `ms_id>0`; warm e.g. `8541` |
| `/api/v1/catalog/brands` | Live; ~1314 rows |
| `/api/v1/catalog/suppliers` | Live; brands-table alias |
| `/api/v1/catalog/vin` | Live; warm `WBAXG1103CDW29096` → 200 |
| `/api/v1/catalog/engines` | Live; auth often `404 cache_miss` |
| `/api/v1/catalog/analogs` | Live; needs `article`+`brand` |
| `/api/v1/catalog/article-brands` | Live; UMAPI action=`brands` |
| `/api/v1/catalog/categories` | Live; warm-key / param mismatch common |
| `/api/v1/catalog/products` | Live; same warm-key pattern |
| `/api/v1/catalog/engine-search` | Live unauth 401; auth may **403 `action_not_allowed`** until smoke key allowlist includes `engine_search` |
| `/api/v1/catalog/article-links` | Live unauth 401 (PR #636 install; installer public FAIL can be CDN lag — re-probe). Auth uses action=`article` |
| `/api/v1/catalog/article` | Live unauth 401 (PR #638 exact-match install; public OK; local SNI may HTML) |
| `/api/v1/catalog/articles` | Live unauth 401 (public OK; local SNI may HTML) |
| `/api/v1/catalog/engine` | Live unauth 401 (exact-match install; not confused with engines/engine-search) |

**Catalog exact-route progress:** **17 / 18** wired catalog API paths shadowed on www.

### Catalog exact-routes still pending nginx `location =`

1. `/api/v1/catalog/brand-parts` ← **next** (last wired catalog API shadow)

### Still 100% PHP on public www (blocks Zero-PHP)

- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/` (and aliases)
- All CP / ERP / BOS digest exact-routes (dashboard, tenants, users, cash, fleet, …) — loopback ASP.NET only
- Storefront digests (`/storefront/*`) — optional; not required for `ReadyToRemovePhp`
- Dual-sample PHP↔ASP.NET parity attachments for promoted routes
- Human `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
- PHP-FPM / cron / rewrite removal (gated script only)

### Known ops gaps (not missing nginx locations)

- Smoke catalog key ACL missing `engine_search` / `article` until re-issue after updated `issue_final_gate_smoke_credentials.php`
- Offline-cache routes return ASP.NET `404 cache_miss` when probe params ≠ warm `epc_umapi_cache` key (PHP/UMAPI still fills live)
- Local nginx `--resolve` probes may hit wrong `default_server` HTML while public URL returns ASP.NET JSON
- Installer bug (fixed): substring `location = /api/v1/catalog/article` falsely matched `article-brands` / `article-links` as ALREADY PRESENT — re-run install after fix lands

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
- **Public exact-route catalog/price shadows through engine** (**17/18**; brand-parts pending).
- No broad PHP cutover; route/job parity/shadow metrics remain 0%.

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% on the weighted meter requires approved exact-route shadows where promoted, and **human** release-owner approval to remove PHP-FPM/cron/rewrites/source. Staging smoke for the final-gate digest/API set is attached. Dry-run scaffolding does **not** authorize PHP removal. See `/migration/php-decommission-readiness` and `bash scripts/run_zero_php_final_gate_checklist.sh`.

**Practically still pending before approval is honest:**

1. Finish last catalog exact-route: `brand-parts`.
2. Promote CP/ERP/BOS digest exact-routes one `location =` at a time after dual samples.
3. Smoke catalog ACL includes `engine_search` + `article` after re-issue (done on CloudPanel post-#637).
4. Attach dual PHP↔ASP.NET parity samples for promoted routes.
5. Human `RELEASE_OWNER_APPROVAL.md` — then gated PHP decommission only.

## Next execution order

- Next install: `ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW=YES bash scripts/cloudpanel_install_exact_route_shadow.sh /api/v1/catalog/brand-parts`
- Run fail-closed parity verdict (must keep PHP): `bash scripts/verify_pre_php_removal_parity.sh`
- Confirm readiness: `curl -sS http://127.0.0.1:5100/migration/php-decommission-readiness` (8/9; approval missing).
- Do **not** remove PHP until more exact-route shadows + dual samples + human approval exist.
- Optional storefront: set `ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=<digits>` (not required for ReadyToRemovePhp).
- Create `RELEASE_OWNER_APPROVAL.md` **only after human approval**; then gated PHP decommission.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
