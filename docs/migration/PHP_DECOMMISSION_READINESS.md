# PHP Decommission Readiness

This document tracks the final Zero-PHP gate. It does **not** authorize PHP removal.

## Status

`blocked-not-ready-for-php-removal`

Live diagnostic: `GET /migration/php-decommission-readiness`

Weighted completion remains **95% / 5% pending**. The residual 5% is PHP runtime decommission only.

## Operator kit

```bash
bash scripts/run_zero_php_final_gate_checklist.sh
bash scripts/probe_live_surface_stack.sh
bash scripts/run_php_decommission_area_tests.sh
```

When (and only when) `/migration/php-decommission-readiness` shows `readyToRemovePhp=true`:

```bash
ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission.sh
```

Live operator URL catalog: `docs/migration/LIVE_SURFACE_LINKS.md` and `GET /migration/live-surface-links`.

On CloudPanel after deploy (loads keys from `/etc/ecomae-aspnet/platform.env` when present):

**Until PR #599 is merged, do NOT reset to `main`** — main still lacks `cloudpanel_validate_final_gate_env.sh` / health wait. Paste this one-liner as root:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/smoke-preflight-public-probes-7b3b/scripts/cloudpanel_redeploy_final_gate_branch.sh)"
```

That script redeploys the final-gate branch, waits for `:5100/health`, validates env (no secret print), then captures. It exits with `BLOCKED` until keys/cookie are set.

Required in `/etc/ecomae-aspnet/platform.env` (server-only, never commit):

```bash
ECOMAE_PRICE_LOOKUP_API_KEY=epc_pricepro_...
ECOMAE_CATALOG_API_KEY=epc_catalog_...
ECOMAE_ADMIN_COOKIE_HEADER=admin_session=...; admin_u_id=123
```

Helpers (no secret print):

```bash
# Preferred on CloudPanel — writes keys (+ admin cookie if a live CP session exists):
ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh
bash scripts/cloudpanel_prepare_smoke_secrets.sh
```

Common CloudPanel failures:
- `cloudpanel_validate_final_gate_env.sh: No such file` → you are on stale `main`; use the one-liner above (PR #599 branch).
- `Failed to connect to 127.0.0.1 port 5100` right after restart → wait for health (automatic on the PR branch).
- `ECOMAE_*_API_KEY: MISSING` → keys empty; issue/copy plaintext `epc_pricepro_` / `epc_catalog_` keys (DB stores hashes only).
- `ECOMAE_ADMIN_COOKIE_HEADER/JAR: BAD_FORMAT` or probe `kind:0` → cookie missing both `admin_session` + numeric `admin_u_id`, or not logged in as Super CP.
- Probe success is `kind:2` (or `"Admin"`) with `isAuthenticated:true`.

Exact-route promotion (after smoke only): `docs/migration/EXACT_ROUTE_PROMOTION_PRICE_CATALOG.md`

Deploy packs `docs/migration/evidence/decommission` into the ASP.NET release ContentRoot so `/migration/php-decommission-readiness` can see attached git artifacts on the server (checklist should show public probes/scripts present after redeploy; authenticated smoke + approval still required).

Evidence pack: `docs/migration/evidence/decommission/`  
Public probes (no secrets) are already attached under `public-probes/`.  
Authenticated surface smoke now requires at least one CP/ERP/BOS digest HTTP **200** (`ok:true` alone with only 401s is rejected).

Opt-in staging smoke:

- `tests/live_smoke/run_price_lookup_exact_route_smoke.sh`
- `tests/live_smoke/run_catalog_status_exact_route_smoke.sh`
- `tests/live_smoke/run_surface_digest_exact_route_smoke.sh`
- `scripts/cloudpanel_commit_final_gate_smoke.sh` (push real artifacts only)

Exact-route shadow examples (all four are packed into the ASP.NET ContentRoot on deploy):

- `deploy/aspnet/nginx-price-lookup-shadow-example.conf`
- `deploy/aspnet/nginx-api-shadow-example.conf` (catalog status)
- `deploy/aspnet/nginx-surface-digests-shadow-example.conf`
- `deploy/aspnet/nginx-storefront-digests-shadow-example.conf`

One-path extract helper (writes a **disabled** snippet; never auto-enables traffic):

- `bash scripts/cloudpanel_extract_exact_route_shadow.sh /api/v1/catalog/status`

## Why the last 5% remains

1. Every tracked route/job needs green PHP-vs-ASP.NET parity evidence
2. Exact-route staging smoke artifacts must be attached under `docs/migration/evidence/decommission/staging-smoke/`
3. Only approved `location =` nginx shadows may be promoted
4. Release-owner written approval must exist (`RELEASE_OWNER_APPROVAL.md` with `APPROVED_TO_REMOVE_PHP_FALLBACK`)
5. Rollback commands must be validated

## Must remain until the final gate

- PHP-FPM
- PHP cron / schedulers
- PHP rewrites and docroot PHP entrypoints
- PHP source dependencies used as authoritative fallback

## Forbidden shortcuts

- Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront nginx cutover
- Claiming 100% Zero-PHP because dry-run catalogs or checklists exist
- Deleting PHP before parity/shadow/live evidence is complete
