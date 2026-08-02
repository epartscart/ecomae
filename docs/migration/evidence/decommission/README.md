# PHP Decommission Evidence Pack

This directory holds **operator-attached** artifacts for the final 5% Zero-PHP gate.

Nothing here authorizes PHP removal by itself. `GET /migration/php-decommission-readiness` stays `blocked-not-ready-for-php-removal` until every checklist item is present with validated smoke JSON **and** release-owner approval is attached. `ReadyToRemovePhp` becomes true only when that full gate is green; `scripts/cloudpanel_php_decommission.sh` still requires `ECOMAE_CONFIRM_PHP_DECOMMISSION=YES`.

## Layout

- `public-probes/` — no-secret production diagnostic captures (already attached)
- `staging-smoke/` — copy JSON outputs from opt-in smoke runners
  - `price-lookup-aspnet.json`
  - `catalog-status-aspnet.json`
  - `surface-digests-aspnet.json`
- `parity-samples/` — PHP-vs-ASP.NET comparison JSON samples
- `RELEASE_OWNER_APPROVAL.example.md` — template only
- `RELEASE_OWNER_APPROVAL.md` — create only when a release owner approves (must contain `APPROVED_TO_REMOVE_PHP_FALLBACK`)

## CloudPanel one-shot

```bash
cd /opt/ecomae-aspnet-source
# platform.env must contain ECOMAE_PRICE_LOOKUP_API_KEY, ECOMAE_CATALOG_API_KEY,
# and ECOMAE_ADMIN_COOKIE_HEADER (or COOKIE_JAR) or staging-smoke/ stays empty.
source /etc/ecomae-aspnet/platform.env
bash scripts/cloudpanel_capture_final_gate_artifacts.sh
# When all three smoke JSON files exist:
bash scripts/cloudpanel_commit_final_gate_smoke.sh
```

Surface smoke is accepted only when `ok=true` **and** at least one non-migration digest returns HTTP 200.

## Operator commands

```bash
Live operator URL catalog: `docs/migration/LIVE_SURFACE_LINKS.md`  
Public stack probe: `bash scripts/probe_live_surface_stack.sh`

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
