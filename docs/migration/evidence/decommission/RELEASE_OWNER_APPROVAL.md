# Release-owner approval (HUMAN CONFIRMED)

Approver: eparts cart \<epartscart@gmail.com\>  
Date (UTC): 2026-08-05  
Confirmed in Cursor cloud agent session (human instruction: approve traffic cutover path + this approval file).

## Marker

```text
APPROVED_TO_REMOVE_PHP_FALLBACK
```

## Scope

- Exact-route ASP.NET Core promotion on **www.ecomae.com** (and later named tenants only after same-to-same dual-sample).
- PHP **traffic/fallback** removal for promoted exact routes after CloudPanel shadow closeout + dual-sample evidence.
- **Keep PHP project as reference** (`KeepPhpProjectAvailable=true`) for previous-result compares and gap-finding.
- Does **not** authorize PHP source deletion, PHP-FPM/cron wipe, or broad `location /api|/cp|/erp|/bos|/storefront` trees.

## Architecture (retained)

- Live primary destination: **ASP.NET Core**
- PHP role: **reference until keep/delete** — see `docs/migration/PHP_AS_REFERENCE_MODE.md`
- Boards: `/migration/php-reference-mode`, `/migration/compare`, `/migration/aspnet-zero-php-path`

## Rollback

- On-call: release owner / CloudPanel operator
- Command: `bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback`
- PHP project/docroot remains installed for reference compares

## Preconditions still required on CloudPanel before disabling PHP fallback per route

1. Staging smoke under `staging-smoke/` (price/catalog/surface digests)
2. Parity samples under `parity-samples/`
3. Exact-route nginx shadows validated (not broad trees)
4. Rollback tested with `--keep-php-fallback`
