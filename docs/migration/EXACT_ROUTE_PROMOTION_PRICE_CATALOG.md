# Exact-route promotion: price lookup + catalog status

PHP remains authoritative until each step below is green. Never enable broad `/api`.

## Preconditions

1. Authenticated smoke attached:
   - `docs/migration/evidence/decommission/staging-smoke/price-lookup-aspnet.json`
   - `docs/migration/evidence/decommission/staging-smoke/catalog-status-aspnet.json`
2. Optional dual PHP↔ASP.NET compare green:
   - `python3 scripts/compare_price_lookup_parity.py <php.json> <aspnet.json>`
   - `python3 scripts/compare_catalog_status_parity.py <php.json> <aspnet.json>`
3. `GET /migration/php-decommission-readiness` does not need to be fully green for a single shadow, but smoke artifacts must exist for that route.

## Promote one path at a time

### Price lookup (already live on many hosts)

Example: `deploy/aspnet/nginx-price-lookup-shadow-example.conf`

```bash
# On CloudPanel
sudo cp /opt/ecomae-aspnet-source/deploy/aspnet/nginx-price-lookup-shadow-example.conf \
  /etc/nginx/sites-enabled/ecomae-price-lookup-shadow.conf   # or include path used by the site
sudo nginx -t && sudo systemctl reload nginx
curl -sS -o /tmp/price.json -w '%{http_code}\n' \
  -H "X-API-Key: $ECOMAE_PRICE_LOOKUP_API_KEY" \
  "https://www.ecomae.com/api/v1/price/lookup?brand=TOYOTA&article=04465-0K020"
```

Expect HTTP 200 JSON (not PHP HTML).

### Catalog status

Example: `deploy/aspnet/nginx-api-shadow-example.conf`  
Must forward `X-API-Key` and `Authorization` (same as price shadow).

```bash
sudo cp /opt/ecomae-aspnet-source/deploy/aspnet/nginx-api-shadow-example.conf \
  /etc/nginx/sites-enabled/ecomae-catalog-status-shadow.conf
sudo nginx -t && sudo systemctl reload nginx
curl -sS -o /tmp/catalog.json -w '%{http_code}\n' \
  -H "X-API-Key: $ECOMAE_CATALOG_API_KEY" \
  "https://www.ecomae.com/api/v1/catalog/status"
```

Expect HTTP 200 JSON with `connected` / `counts` / `source` (not PHP HTML 404).

## Rollback

```bash
sudo rm -f /etc/nginx/sites-enabled/ecomae-catalog-status-shadow.conf
sudo rm -f /etc/nginx/sites-enabled/ecomae-price-lookup-shadow.conf
sudo nginx -t && sudo systemctl reload nginx
# Or: bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback
```

Traffic returns to PHP. Do not remove PHP-FPM/cron/rewrites from this runbook.
