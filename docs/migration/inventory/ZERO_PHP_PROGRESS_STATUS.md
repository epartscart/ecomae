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
| All 127 CP/ERP/BOS digests | Wired; live unauth **401** after ASP.NET republish |
| Storefront digests | **4 / 6 live** unauth **401** (search/cart wired, awaiting shadow install) |

**Catalog exact-route progress:** **18 / 18**

**Surface digest exact-route progress:** **133 / 133** wired (incl. `/erp/process-flow-tasks` + report-center + aging + stock-movements; live www until republish + shadow install)

**Storefront digest exact-route progress:** **4 / 6** (wired 6; live shadows still 4)

### Still 100% PHP on public www (blocks Zero-PHP)

- Product chrome: `/`, `/CP/`, `/ERP/`, `/BOS/` (and aliases) — **full page presentation, fonts, analytics**
- Interactive modules: ~405 CP features, ~160 ERP tabs, ~116 BOS modules, storefront cart/checkout/search
- Dual-sample PHP↔ASP.NET parity attachments for promoted digests
- Presentation recheck pass (`scripts/cloudpanel_probe_php_presentation_parity.sh`)
- Module function test evidence (`MODULE_FUNCTION_TEST_PASS.md`)
- Human `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`
- PHP-FPM / cron / rewrite removal (gated script only)

**Honest recheck:** `docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md` — live ASP.NET shells are not PHP-like; do not remove PHP.

### Improvements shipped

- Blazor SSR Zero-PHP operator console at `/migration/console` (interim ops UI; Enterprise BOS still targets Angular/React for product chrome)
- Batch installers for surface + storefront digests (one nginx reload each)
- Digest dual-sample capture helper: `scripts/cloudpanel_capture_digest_dual_samples.sh`
- Hybrid chrome strengthen: `/cp|/erp|/bos|/storefront/{app,login}` with PHP-linked nav + opt-in PHP-compatible login bridge (`EcomAE__SecretSuccession`); gap matrix `docs/migration/CHROME_PARITY_GAP_MATRIX.md`


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
- **Surface digests wired: 127/127** (live www ~30/127 until ASP.NET republish). Storefront digests: **4/6 live** (wired 6).
- Blazor SSR migration console (ops improvement).
- No broad PHP cutover; route/job parity/shadow metrics remain 0%.
- Digest dual-sample contract parity attached (migration baseline + live ASP.NET; `failed=0`, `cutoverAllowed=false`).
- PHP decommission readiness: smoke + dual contract samples present; removal blocked on human `RELEASE_OWNER_APPROVAL.md` (chrome still PHP).

## Path to 100% / Remaining 5% (PHP runtime decommission only)

100% on the weighted meter requires approved exact-route shadows where promoted, and **human** release-owner approval to remove PHP-FPM/cron/rewrites/source.

**Confirmed live (2026-08-03 ops):** catalog **18/18**; surface digests **30/30**; storefront digests **4/6 live** (wired 6); Blazor `/migration/console` **200**; digest dual-sample contract compare **pairsChecked=19 failed=0** (`docs/migration/evidence/surface-parity/digest-dual-sample-contract-result.json`). Chrome still PHP.

**Practically still pending before approval is honest:**

1. Keep product chrome on PHP until intentional shell cutover (Blazor console is **not** chrome cutover).
2. Optional: commit live `aspnet-*.json` samples from CloudPanel into git for archival.
3. Human `RELEASE_OWNER_APPROVAL.md` — then gated PHP decommission only.

## Next execution order

- Fail-closed parity: `bash scripts/verify_pre_php_removal_parity.sh`
- Confirm readiness: `curl -sS https://www.ecomae.com/migration/php-decommission-readiness` (8/9; approval missing).
- Human creates `RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK` only after that approval.
- When `readyToRemovePhp=true`: `ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh`
- Do **not** remove PHP until then.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
