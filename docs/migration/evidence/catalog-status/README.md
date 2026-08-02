# Catalog Status Route Parity Evidence

Tracked PHP route: `api/v1/catalog.php?action=status` backed by `epc_umapi_sync_status` and UMAPI count tables.

## ASP.NET implementation

- Route: `/api/v1/catalog/status`
- Service: `CatalogStatusService`
- Repository: `DbCatalogStatusRepository` (read-only) executing `LegacyCatalogStatusSql`
- Auth: `LegacyApiClientAuthenticator` with product `catalog`, action `status`
- Writes: zero

## Exact-route shadow example

Use the existing catalog status example only after staging smoke:

`deploy/aspnet/nginx-api-shadow-example.conf` → `location = /api/v1/catalog/status`

## Staging smoke (manual)

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_STAGING_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/status" | python3 -m json.tool
```

Attach ASP.NET and PHP responses before enabling nginx shadow. Keep PHP fallback.

## Cutover boundary

Exact-route only. Do not proxy broad `/api`.
