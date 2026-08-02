# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.

## Current percentage

- True zero-PHP completion: 52.0%.
- Pending to 100%: 48.0%.
- Foundation/planning floor: 35.0%.
- Route/job implementation started (not parity-ready): price lookup + catalog cache/DB routes + worker dry-run validators + admin session DB check.
- Route/job parity-ready: 0.0%.
- Route/job shadow-or-better: 0.0%.

## Inventory

- Total PHP files: 3049.
- Job-like PHP files: 140.

| Surface | PHP files |
| --- | ---: |
| api | 53 |
| bos | 9 |
| cp | 638 |
| erp | 431 |
| platform | 812 |
| storefront | 1106 |

## Planning progress

- Ownership assigned: 3049 (100.0%).
- Batch assignments: 3049 (100.0%).
- Total exact-route batches: 61.

## Concrete implementation progress (honest)

- `/api/v1/price/lookup`: DB repository + API-key auth + evidence pack + exact-route shadow example. Staging smoke still required.
- `/api/v1/catalog/status`: DB status reader + catalog API-key auth + evidence pack. Staging smoke still required.
- `/api/v1/catalog/models|/modifications|/brands`: DB cache readers + auth + shadow examples.
- `/api/v1/catalog/manufacturers`: DB cache reader + catalog API-key auth + exact-route shadow example. Staging smoke still required.
- `/api/v1/catalog/vin|/engines|/analogs|/article-brands|/categories|/products|/engine-search|/article-links`: offline DB/cache readers + auth + shadow examples (cache-miss keeps PHP authoritative).
- `/api/v1/catalog/brand-parts`: DB stock reader from `shop_docpart_prices_data` + auth + shadow example.
- Admin session DB check: `DbBackedLegacySessionValidator` against `sessions.type=1` when TenantRegistry DB is configured.
- `/migration/umapi-usage`: read-only UMAPI usage summary diagnostic (not an external catalog API).
- Worker dry-run validators (writes blocked): price-import, sitemap, backups, notifications, erp-reports.
- Production deploy helpers: `scripts/cloudpanel_production_deploy_foundation.sh`, `scripts/cloudpanel_find_and_redeploy.sh`, `scripts/cloudpanel_bootstrap_from_github.sh` (diagnostics/foundation only; no broad PHP cutover).

## Next execution order

- Redeploy by refreshing `/opt/ecomae-aspnet-source` to `origin/main` first (stale checkouts miss new scripts). Do **not** use `/var/www/ecomae`.
- Run exact-route staging smoke for price lookup and catalog routes with real API keys; attach artifacts.
- Enable only approved `location =` nginx shadows after smoke passes.
- Continue batch-by-batch worker/route replacements with parity evidence.
- Remove PHP only after every tracked item is live/removed with rollback approval.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
