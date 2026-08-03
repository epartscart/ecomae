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
| `/health`, `/migration/*` | ASP.NET diagnostics + Blazor SSR console `/migration/console` |
| `/api/v1/price/lookup` | Live |
| `/api/v1/catalog/status` … `/brand-parts` | **All 18/18 wired catalog API paths live** |
| All 30 CP/ERP/BOS digests | Live unauth **401 unauthorized** |
| All 4 storefront digests | Live unauth **401 unauthorized** (customer cookie for 200) |

**Catalog exact-route progress:** **18 / 18**

**Surface digest exact-route progress:** **30 / 30**

**Storefront digest exact-route progress:** **4 / 4**

### Still 100% PHP on public www (blocks Zero-PHP)

- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/` (and aliases)
- Dual-sample PHP↔ASP.NET parity attachments for promoted digests
- Human `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
- PHP-FPM / cron / rewrite removal (gated script only)

### Improvements shipped

- Blazor SSR Zero-PHP operator console at `/migration/console` (interim ops UI; Enterprise BOS still targets Angular/React for product chrome)
- Batch installers for surface + storefront digests (one nginx reload each)
- Digest dual-sample capture helper: `scripts/cloudpanel_capture_digest_dual_samples.sh`

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
- **Surface digests live: 30/30.** Storefront digests: **4/4.**
- Blazor SSR migration console (ops improvement).
- No broad PHP cutover; route/job parity/shadow metrics remain 0%.
- PHP decommission readiness: smoke present; removal blocked on dual samples + human `RELEASE_OWNER_APPROVAL.md`.

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% on the weighted meter requires approved exact-route shadows where promoted, and **human** release-owner approval to remove PHP-FPM/cron/rewrites/source.

**Confirmed live (2026-08-03 ops):** storefront digests public probe **PASS=4**; surface digests **PASS=30**; catalog **18/18**. Blazor console needs antiforgery fix redeploy (`UseAntiforgery`).

**Practically still pending before approval is honest:**

1. Redeploy antiforgery fix so `/migration/console` returns 200 (not 500).
2. Re-run dual-sample capture+compare (seeds migration contract baselines when PHP JSON is no longer public).
3. Keep product chrome on PHP until intentional shell cutover (Blazor console is **not** chrome cutover).
4. Human `RELEASE_OWNER_APPROVAL.md` — then gated PHP decommission only.

## Next execution order

- Pull + redeploy: `bash scripts/cloudpanel_find_and_redeploy.sh` (ships Blazor `UseAntiforgery` fix).
- Confirm console: `curl -sS https://www.ecomae.com/migration/console | head`
- Dual samples: `bash scripts/cloudpanel_capture_digest_dual_samples.sh` (auto-compares; migration baseline + live ASP.NET).
- Fail-closed parity: `bash scripts/verify_pre_php_removal_parity.sh`
- Confirm readiness: `curl -sS https://www.ecomae.com/migration/php-decommission-readiness` (8/9; approval missing).
- Human creates `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK` only after that approval.
- Do **not** remove PHP until then.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
