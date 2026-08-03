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
| `/api/v1/catalog/status` … `/brand-parts` | **All 18/18 wired catalog API paths live** |
| `/cp/dashboard-summary` | Live unauth **401 unauthorized** (admin cookie for 200) |
| `/cp/tenants` | Live unauth **401 unauthorized** (admin CP capability for 200) |
| `/cp/users` | Live unauth **401 unauthorized** (CDN may briefly cache prior PHP HTML 200 — re-probe) |
| `/cp/groups` | Live unauth **401 unauthorized** |

**Catalog exact-route progress:** **18 / 18** — wired catalog API exact-route set complete on www.

**Surface digest exact-route progress:** **4 / 30** (from `nginx-surface-digests-shadow-example.conf`).

### Surface digest exact-routes still pending

Next: `/cp/modules` → `/cp/menus` → … → ERP/BOS digests (one `location =` at a time). Never broad `/cp|/erp|/bos`.

### Still 100% PHP on public www (blocks Zero-PHP)

- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/` (and aliases)
- Remaining CP / ERP / BOS digest exact-routes (26/30) — loopback ASP.NET only until each `location =` is installed
- Storefront digests (`/storefront/*`) — optional; not required for `ReadyToRemovePhp`
- Dual-sample PHP↔ASP.NET parity attachments for promoted routes
- Human `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
- PHP-FPM / cron / rewrite removal (gated script only)

### Known ops gaps (not missing nginx locations)

- Offline-cache routes return ASP.NET `404 cache_miss` when probe params ≠ warm `epc_umapi_cache` key (PHP/UMAPI still fills live)
- Local nginx `--resolve` probes may hit wrong `default_server` HTML while public URL returns ASP.NET JSON
- Installer exact-match required for prefix-colliding paths (`article` vs `article-links`, `engine` vs `engines`)
- Digest shadows need Cookie proxy (from `nginx-surface-digests-shadow-example.conf`); unauth gate is `401 unauthorized` (not `missing_api_key`)
- Digests that were real PHP pages (e.g. `/cp/users`) may show CDN-cached HTML 200 briefly after `location=` insert — installer retries cache-bust / soft-OK when loopback is ASP.NET

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
- CP/ERP/BOS/storefront digests scaffolded on loopback + staging smoke attached (PR #612).
- **Public exact-route catalog/price shadows complete (18/18 catalog + price + health/migration).**
- **Surface digests live:** `/cp/dashboard-summary`, `/cp/tenants`, `/cp/users`, `/cp/groups` (4/30).
- No broad PHP cutover; route/job parity/shadow metrics remain 0%.
- PHP decommission readiness: smoke present; removal blocked on human `RELEASE_OWNER_APPROVAL.md` after more digest shadows + dual samples.

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% on the weighted meter requires approved exact-route shadows where promoted, and **human** release-owner approval to remove PHP-FPM/cron/rewrites/source.

**Practically still pending before approval is honest:**

1. Promote remaining CP/ERP/BOS digest exact-routes one `location =` at a time (next `/cp/modules`).
2. Attach dual PHP↔ASP.NET parity samples for promoted routes.
3. Keep product chrome on PHP until intentional shell cutover.
4. Human `RELEASE_OWNER_APPROVAL.md` — then gated PHP decommission only.

## Next execution order

- Next install: `ECOMAE_CONFIRM_INSTALL_EXACT_ROUTE_SHADOW=YES bash scripts/cloudpanel_install_exact_route_shadow.sh /cp/modules`
- Expect public **401** ASP.NET JSON `unauthorized` (admin cookie required); Cookie header must be proxied.
- Continue digests from `deploy/aspnet/nginx-surface-digests-shadow-example.conf` one path at a time.
- Run fail-closed parity verdict (chrome must keep PHP): `bash scripts/verify_pre_php_removal_parity.sh`
- Confirm readiness: `curl -sS http://127.0.0.1:5100/migration/php-decommission-readiness` (8/9; approval missing).
- Do **not** remove PHP until digest shadows + dual samples + human approval exist.
- Create `RELEASE_OWNER_APPROVAL.md` **only after human approval**; then gated PHP decommission.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
