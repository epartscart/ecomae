# PHP Route and Job Inventory Summary

Generated from `scripts/inventory_php_routes.sh json`. This is the tracked zero-PHP baseline for assigning every legacy PHP route/job to ASP.NET Core, ASP.NET Core with a Python AI-service helper, deleted/removed, or temporary PHP fallback.

## Current baseline

- Total PHP files: 3049.
- Job-like PHP files: 140.
- Current generated migration status: `php-only` until each item is explicitly assigned an owner and parity state.

## Surface counts

| Surface | PHP files | Job-like files |
| --- | ---: | ---: |
| api | 53 | 0 |
| bos | 9 | 0 |
| cp | 638 | 12 |
| erp | 431 | 2 |
| storefront | 1106 | 7 |
| platform | 812 | 119 |

## Required owner states

Each item in `php-route-job-inventory.json` must move from `php-only` to one of:

- `aspnet-core`: ASP.NET Core owns route/API/job/business logic.
- `aspnet-with-python-ai-helper`: ASP.NET Core owns route/API/auth/database/persistence and calls Python only for stateless AI-service results.
- `removed`: dead/duplicate route or obsolete job removed from production.
- `php-fallback`: temporary fallback only, with parity and rollback evidence required before removal.

## Fast next slices

1. Price/catalog API facade: move public validation, auth, response shape, and database access to ASP.NET Core; Python may help only for AI enrichment/matching/anomaly results.
2. CP login/session parity: make one authenticated CP dashboard shell real in ASP.NET Core.
3. Worker replacement: move one import/refresh job from PHP cron to ASP.NET Core worker orchestration.
4. Storefront/API cutover: select exact routes only; no broad proxying.

## Guardrails

- No new PHP business features.
- No direct frontend calls to Python.
- No Python ownership of business CRUD, permissions, transactions, or final API responses.
- No broad CP/ERP/BOS/API/storefront proxy cutover until inventory status is assigned and parity evidence exists.
