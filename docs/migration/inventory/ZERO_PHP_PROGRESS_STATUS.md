# Zero-PHP Progress Status

This status is generated from the tracked inventory, ownership plan, and exact-route cutover batches. It reports true production progress separately from planning progress so we do not overstate 0% PHP readiness.

Enterprise BOS target stack tracking lives in `docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md` and must not be confused with Zero-PHP completion.

## Current percentage

- True zero-PHP completion: 87.0%.
- Pending to 100%: 13.0%.
- Foundation/planning floor: 35.0%.
- Route/job implementation started (not parity-ready): digests + nested ACL + worker dry-run layer + batch-1 dry-run scaffolding.
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
- Batch statuses: batch 1 `aspnet-dry-run-scaffolded`; batches 2–61 `planned-not-implemented`.

## Concrete implementation progress (honest)

- Catalog/price API routes with DB/cache readers + API-key auth.
- Admin nested modules_access ACL + surface capabilities.
- CP digests: dashboard, tenants, users, groups, modules, config-items metadata.
- ERP digests: accounts, suppliers, purchases, cash accounts/entries, invoices, GL journals.
- BOS digests: fleet summary/health/readiness (platform DB only).
- Storefront account/orders/garage/profile digests.
- Tracked write-blocked worker dry-run validator layer + batch-1 dry-run scaffolding.
- EF Core stub entities (unwired); Enterprise BOS compliance docs.
- No broad PHP cutover; parity/shadow remain 0%; PHP decommission blocked.

## Path to 100% (honest)

100% requires all 61 batches live/removed with parity evidence, staging smoke, approved exact-route shadows, and release-owner approval to remove PHP. This PR does **not** claim 100%.

## Next execution order

- Redeploy by refreshing `/opt/ecomae-aspnet-source` to `origin/main` first.
- Run exact-route staging smoke; attach artifacts; enable only approved `location =` shadows.
- Continue batch-by-batch replacements with parity evidence.
- Remove PHP only after every tracked item is live/removed with rollback approval.

## Guardrail

Do not report 100% until every tracked PHP route/job is `live` or `removed`, PHP fallback removal has rollback approval, and production smoke checks pass. Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront cutover remains forbidden.
