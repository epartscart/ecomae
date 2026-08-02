# Price Lookup Route Parity Evidence

Tracked PHP route/job: `api/v1/price/lookup` backed by `shop_docpart_prices_data`.

## ASP.NET implementation

- Route: `/api/v1/price/lookup`.
- Service: `RepositoryPriceLookupService`.
- Production-intent read repository: `DbPriceOfferRepository` executes `LegacyPriceLookupSql.LookupOffers` against `shop_docpart_prices_data` with tenant database scoping via `ITenantDbConnectionFactory` / `MySqlTenantDbConnectionFactory`.
- Offline parity repository: `CsvPriceOfferRepository` for captured PHP/staging baseline CSV exports (`PriceLookup:FixtureCsvPath`), preserving legacy normalization, positive-price filtering, ascending price ordering, and the legacy limit of 25 rows.
- DI selection order: CSV fixture path → configured MySQL connection → `MigrationPriceOfferRepository` noop fallback.
- Writes: not applicable for this read-only route; the route performs zero writes.
- Remaining gaps before exact-route shadow: live staging smoke artifacts, legacy API-key/quota enforcement against `epc_api_clients`, and PHP vs ASP.NET response comparison with real staging URLs.

## PHP baseline sample

- `docs/migration/evidence/price-lookup/php-baseline-sample.json`
- CSV export fixture: `tests/fixtures/price_lookup/php-baseline.csv`

## ASP.NET output sample

- `docs/migration/evidence/price-lookup/aspnet-output-sample.json`

## Automated parity comparison

```bash
python3 scripts/compare_price_lookup_parity.py
```

Expected output:

```text
PRICE LOOKUP PARITY PASSED: 2 offer(s) matched
```

## Staging smoke command

```bash
RUN_PRICE_LOOKUP_SMOKE=1 \
ECOMAE_ASPNET_BASE_URL="https://REAL-ASPNET-STAGING-URL" \
ECOMAE_PHP_BASE_URL="https://REAL-PHP-STAGING-URL" \
bash tests/live_smoke/run_price_lookup_exact_route_smoke.sh
```

Local repository evidence: the smoke script is exact-route only and defaults to skipped unless staging URLs are provided. Production/staging execution must attach the generated `/tmp/ecomae-aspnet-price-lookup.json` and optional `/tmp/ecomae-php-price-lookup.json` artifacts before promotion.

## Rollback

Disable only the exact ASP.NET price lookup route shadow/proxy and keep PHP fallback:

```bash
sudo rm -f /etc/nginx/conf.d/ecomae-price-lookup-shadow.conf
sudo nginx -t
sudo systemctl reload nginx
```

## Cutover boundary

Cutover remains exact-route only for `/api/v1/price/lookup`. Do not proxy broad `/api`, `/cp`, `/erp`, `/bos`, or storefront traffic to ASP.NET for this evidence item.
