# Catalog Manufacturers Route Parity Evidence

Tracked PHP route: `api/v1/catalog.php?action=manufacturers` (cached from `epc_umapi_manufacturers`).

## ASP.NET implementation

- Route: `/api/v1/catalog/manufacturers?section=passenger`
- Service: `CatalogManufacturerService`
- Repository: `DbCatalogManufacturerRepository` (read-only)
- Auth: `LegacyApiClientAuthenticator` product `catalog`, action `manufacturers`
- Writes: zero

## Exact-route shadow example

`deploy/aspnet/nginx-catalog-manufacturers-shadow-example.conf`

## Staging smoke (manual)

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_STAGING_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/manufacturers?section=passenger" | python3 -m json.tool
```

Keep PHP fallback until artifacts are attached and approved.
