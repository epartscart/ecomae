# PHP Decommission Readiness

This document tracks the final Zero-PHP gate. It does **not** authorize PHP removal.

## Status

`blocked-not-ready-for-php-removal`

Live diagnostic: `GET /migration/php-decommission-readiness`

Weighted completion remains **95% / 5% pending**. The residual 5% is PHP runtime decommission only.

## Operator kit

```bash
bash scripts/run_zero_php_final_gate_checklist.sh
```

Evidence pack: `docs/migration/evidence/decommission/`

Opt-in staging smoke:

- `tests/live_smoke/run_price_lookup_exact_route_smoke.sh`
- `tests/live_smoke/run_catalog_status_exact_route_smoke.sh`
- `tests/live_smoke/run_surface_digest_exact_route_smoke.sh`

Exact-route shadow examples:

- `deploy/aspnet/nginx-price-lookup-shadow-example.conf`
- `deploy/aspnet/nginx-api-shadow-example.conf` (catalog status)
- `deploy/aspnet/nginx-surface-digests-shadow-example.conf`

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
