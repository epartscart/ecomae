# Catalog Vehicle Cache Route Parity Evidence

Tracked PHP routes (offline/cache path):

- `api/v1/catalog.php?action=models`
- `api/v1/catalog.php?action=modifications`
- `api/v1/catalog.php?action=brands`

## ASP.NET implementation

- `/api/v1/catalog/models?section=passenger&mfa_id=...`
- `/api/v1/catalog/modifications?section=passenger&ms_id=...`
- `/api/v1/catalog/brands`
- Service: `CatalogVehicleCacheService`
- Repository: `DbCatalogVehicleCacheRepository` (read-only)
- Auth: catalog API-key actions `models` / `modifications` / `brands`
- Writes: zero

## Exact-route shadow examples

- `deploy/aspnet/nginx-catalog-models-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-modifications-shadow-example.conf`
- `deploy/aspnet/nginx-catalog-brands-shadow-example.conf`

## Staging smoke

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/models?section=passenger&mfa_id=10"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/modifications?section=passenger&ms_id=100"
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/brands"
```

Keep PHP fallback until artifacts are attached.
