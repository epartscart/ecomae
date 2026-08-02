# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.

## Current percentage

- True zero-PHP completion: 77.0%.
- Pending to 100%: 23.0%.
- Foundation/planning floor: 35.0%.
- Route/job implementation started (not parity-ready): catalog/price APIs + session capabilities/module ACL + surface digests + worker dry-runs.
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

- Catalog/price API routes with DB/cache readers + API-key auth.
- Admin backend-group claims with surface capabilities and modules_access ACL probe.
- CP digests: dashboard, tenants, users, groups.
- ERP digests: dashboard/accounts KPIs, suppliers, purchases.
- Storefront customer account/orders/garage digests.
- Broad set of write-blocked worker dry-run validators.
- Migration diagnostics only; no broad PHP cutover.

## Next execution order

- Redeploy by refreshing `/opt/ecomae-aspnet-source` to `origin/main` first. Do **not** use `/var/www/ecomae`.
- Run exact-route staging smoke; attach artifacts; enable only approved `location =` shadows.
- Continue batch-by-batch replacements with parity evidence.
- Remove PHP only after every tracked item is live/removed with rollback approval.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
