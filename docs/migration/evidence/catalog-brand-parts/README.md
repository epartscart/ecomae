# Catalog Brand Parts Route Parity Evidence

Tracked PHP route: `api/umapi_proxy.php?action=brand_parts` (`epc_brand_parts_payload`).

## ASP.NET implementation

- Route: `/api/v1/catalog/brand-parts?brand=BOSCH&limit=100&offset=0`
- Service: `CatalogBrandPartsService`
- Repository: `DbCatalogBrandPartsRepository` (read-only)
- Table: `shop_docpart_prices_data`
- Auth: catalog API-key action `products` (closest external allowlisted action)
- Writes: zero
- Storefront price redaction is not applied yet; PHP remains authoritative for public storefront visibility rules.

## Exact-route shadow example

`deploy/aspnet/nginx-catalog-brand-parts-shadow-example.conf`

## Staging smoke

```bash
curl -sS -H "X-API-Key: epc_catalog_REAL_KEY" \
  "$ECOMAE_ASPNET_BASE_URL/api/v1/catalog/brand-parts?brand=BOSCH&limit=20"
```

Keep PHP fallback until artifacts are attached.
