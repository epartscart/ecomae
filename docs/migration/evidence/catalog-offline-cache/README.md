# Catalog Offline Cache Route Parity Evidence

Tracked PHP routes (offline/cache path):

- `api/v1/catalog.php?action=vin`
- `api/v1/catalog.php?action=engines`
- `api/v1/catalog.php?action=analogs`

## ASP.NET implementation

- `/api/v1/catalog/vin?vin=...&language=en&region=WWW`
- `/api/v1/catalog/engines?section=passenger&mfa_id=...`
- `/api/v1/catalog/analogs?section=passenger&article=...&brand=...`
- Service: `CatalogOfflineCacheService`
- Repository: `DbCatalogOfflineCacheRepository` (read-only)
- Tables: `epc_umapi_vin_cache`, `epc_umapi_cache`
- Cache key: `UmapiCacheKeyBuilder` mirrors PHP `epc_cache_key` / `epc_normalize_vin`
- Auth: catalog API-key actions `vin` / `engines` / `analogs`
- Writes: zero
- Cache miss: HTTP 404; PHP/UMAPI remains authoritative for live fills

## Exact-route shadow examples

- `deploy/aspnet/nginx-catalog-vin-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-engines-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-analogs-shadow-example.conf`

## Staging smoke

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/vin?vin=WBAXG1103CDW29096&language=en&region=WWW"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/engines?section=passenger&mfa_id=10"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/analogs?section=passenger&article=0986424590&brand=BOSCH"
```

Keep PHP fallback until artifacts are attached. Do not enable nginx shadows without staging smoke.
