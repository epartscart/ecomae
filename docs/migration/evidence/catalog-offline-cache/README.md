# Catalog Offline Cache Route Parity Evidence

Tracked PHP routes (offline/cache path):

- `api/v1/catalog.php?action=vin`
- `api/v1/catalog.php?action=engines`
- `api/v1/catalog.php?action=analogs`
- `api/v1/catalog.php?action=brands` (BrandRefinement by article — not supplier list)
- `api/v1/catalog.php?action=categories`
- `api/v1/catalog.php?action=products`

## ASP.NET implementation

- `/api/v1/catalog/vin?vin=...&language=en&region=WWW`
- `/api/v1/catalog/engines?section=passenger&mfa_id=...`
- `/api/v1/catalog/analogs?section=passenger&article=...&brand=...`
- `/api/v1/catalog/article-brands?section=passenger&article=...` (PHP `action=brands`)
- `/api/v1/catalog/categories?section=passenger&id=...`
- `/api/v1/catalog/products?section=passenger&category_id=...&id=...`
- Service: `CatalogOfflineCacheService`
- Repository: `DbCatalogOfflineCacheRepository` (read-only)
- Tables: `epc_umapi_vin_cache`, `epc_umapi_cache`
- Cache key: `UmapiCacheKeyBuilder` mirrors PHP `epc_cache_key` / `epc_normalize_vin`
- Auth: catalog API-key actions `vin` / `engines` / `analogs` / `brands` / `categories` / `products`
- Writes: zero
- Cache miss: HTTP 404; PHP/UMAPI remains authoritative for live fills

Note: ASP.NET `/api/v1/catalog/brands` remains the suppliers/brands table list (PHP `action=suppliers`). Article BrandRefinement is intentionally on `/article-brands`.

## Exact-route shadow examples

- `deploy/aspnet/nginx-catalog-vin-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-engines-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-analogs-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-article-brands-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-categories-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-products-shadow-example.conf`

## Staging smoke

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/vin?vin=WBAXG1103CDW29096&language=en&region=WWW"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/engines?section=passenger&mfa_id=10"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/analogs?section=passenger&article=0986424590&brand=BOSCH"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/article-brands?section=passenger&article=0986424590"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/categories?section=passenger&id=1"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/products?section=passenger&category_id=1&id=1"
```

Keep PHP fallback until artifacts are attached. Do not enable nginx shadows without staging smoke.
