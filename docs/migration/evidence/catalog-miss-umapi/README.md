# Catalog miss / UMAPI dual-sample evidence (Batch 5)

**Policy:** ASP.NET catalog exact-routes are **cache readers only** (18/18 live). On miss they return **404** with `error.code` = `cache_miss` or `vin_cache_miss` — they do **not** call UMAPI or write cache. Live fills remain PHP (`api/umapi_proxy.php` / `api/v1/catalog.php`). `cutoverAllowed` / `readyForPhpRemoval` stay **false**. Never invent `RELEASE_OWNER_APPROVAL.md`.

## What “miss” means

| Side | Behavior |
| --- | --- |
| ASP.NET shadowed `location = /api/v1/catalog/*` | Auth → DB/cache lookup → hit 200 / miss **404** (`cache_miss` / `vin_cache_miss`). No outbound UMAPI. |
| PHP `api/v1/catalog.php` + `api/umapi_proxy.php` | On miss may call live UMAPI and write `epc_umapi_cache` / VIN cache. |
| Nginx | Exact-route shadows always proxy to ASP.NET; there is **no** auto-fallback-to-PHP on 404. Clients that need fills still use PHP. |

## Still PHP-only

- All outbound UMAPI fills and cache write-on-miss / `refresh=1`
- Always-live (non-cacheable) actions: `articles`, `engine` (and any action not in PHP `epc_cacheable_action`)
- Storefront JS/PHP callers that hit `/api/umapi_proxy.php`
- Quota / live UMAPI usage accounting

## Capture / probe (CloudPanel)

```bash
set -a; source /etc/ecomae-aspnet/platform.env; set +a
cd /opt/ecomae-aspnet-source   # or repo root
# Unauth + optional auth miss probes (never prints API key):
bash scripts/cloudpanel_probe_catalog_miss_path.sh
# Contract stubs (default) or live capture when ECOMAE_CATALOG_API_KEY is set:
bash scripts/cloudpanel_capture_catalog_miss_dual_samples.sh
python3 scripts/compare_catalog_miss_dual_samples.py \
  --samples-dir docs/migration/evidence/catalog-miss-umapi \
  --out docs/migration/evidence/catalog-miss-umapi/compare-result.json
```

Without credentials the capture script writes **contract stubs** so foundation checks have a stable floor.

## Pass meaning

- ASP.NET miss samples: HTTP 404 (or documented 401 unauth), `ok=false`, `error.code` in `{cache_miss, vin_cache_miss}`
- Samples must not claim `cutoverAllowed` / `readyForPhpRemoval`
- Compare result always `"cutoverAllowed": false`
- PHP remaining authoritative for live fills is recorded as inventory, not a cutover signal

## Miss-fill dry-run (write-/outbound-blocked)

Worker job key: `catalog-miss-fill` (`CatalogMissFillDryRunExecutor`).

```text
# In-process / unit tests — parameters.sample_actions (one action per line; optional action,query)
engines,section=passenger&mfa_id=999999001
vin,vin=ZZZMISSNOFILLVIN01
```

- Always `WritesBlocked=true`, metrics `outbound=0` / `writes=0` / `fills=0`
- `confirm_outbound=true` or `confirm_writes=true` → `dry-run-confirm-refused` (still no outbound)
- Always-live `articles` / `engine` rejected as non-cacheable
- Evidence stub: `miss-fill-dry-run-report.json` (`cutoverAllowed=false`)

Live UMAPI fill is **not** implemented in ASP.NET by this dry-run.
