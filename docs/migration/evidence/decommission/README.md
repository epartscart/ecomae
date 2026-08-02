# PHP Decommission Evidence Pack

This directory holds **operator-attached** artifacts for the final 5% Zero-PHP gate.

Nothing here authorizes PHP removal by itself. `GET /migration/php-decommission-readiness` stays `blocked-not-ready-for-php-removal` until every checklist item is present **and** release-owner approval is attached. The ASP.NET reporter still keeps `ReadyToRemovePhp=false` as a hard guardrail until that full gate is intentionally opened by humans.

## Layout

- `staging-smoke/` — copy JSON outputs from opt-in smoke runners
  - `price-lookup-aspnet.json`
  - `catalog-status-aspnet.json`
  - `surface-digests-aspnet.json`
- `parity-samples/` — PHP-vs-ASP.NET comparison JSON samples
- `RELEASE_OWNER_APPROVAL.example.md` — template only
- `RELEASE_OWNER_APPROVAL.md` — create only when a release owner approves (must contain `APPROVED_TO_REMOVE_PHP_FALLBACK`)

## Operator commands

```bash
# Local checklist (no PHP removal, live smoke opt-in)
bash scripts/run_zero_php_final_gate_checklist.sh

# Staging smoke examples (real keys/cookies required)
RUN_PRICE_LOOKUP_SMOKE=1 \
ECOMAE_ASPNET_BASE_URL="http://127.0.0.1:5100" \
ECOMAE_PRICE_LOOKUP_API_KEY="epc_pricepro_..." \
bash tests/live_smoke/run_price_lookup_exact_route_smoke.sh

RUN_CATALOG_STATUS_SMOKE=1 \
ECOMAE_ASPNET_BASE_URL="http://127.0.0.1:5100" \
ECOMAE_CATALOG_API_KEY="epc_catalog_..." \
bash tests/live_smoke/run_catalog_status_exact_route_smoke.sh

RUN_SURFACE_DIGEST_SMOKE=1 \
ECOMAE_ASPNET_BASE_URL="http://127.0.0.1:5100" \
ECOMAE_ADMIN_COOKIE_HEADER="admin_session=...; admin_u_id=..." \
bash tests/live_smoke/run_surface_digest_exact_route_smoke.sh
```

Copy `/tmp/ecomae-aspnet-*.json` artifacts into `staging-smoke/` with the filenames above after a green staging run.
