# PHP Route and Job Ownership Plan Summary

Generated from `scripts/plan_zero_php_ownership.py`. This moves the zero-PHP inventory from a raw `php-only` baseline into a target-owner plan. It does not declare parity complete; every item is still `owner-assigned-pending-parity` until implementation, tests, live smoke, and rollback evidence are attached.

## Assignment totals

- Total assignments: 3049.
- aspnet-core: 2755.
- aspnet-with-python-ai-helper: 294.

## Work slices

| Slice | Assignments |
| --- | ---: |
| ai-service-contract | 289 |
| bos-admin-port | 9 |
| cp-workflow-port | 568 |
| erp-workflow-port | 413 |
| platform-route-port | 587 |
| public-api-port | 49 |
| storefront-port | 989 |
| worker-replacement | 145 |

## Risk

| Risk | Assignments |
| --- | ---: |
| high | 1204 |
| low | 587 |
| medium | 1258 |

## Rules

- ASP.NET Core owns every public route, API, auth decision, database transaction, business workflow, job orchestration, and final response.
- Python is allowed only for stateless AI-service helper work behind ASP.NET Core.
- PHP remains fallback only until exact-route parity, rollback evidence, and production smoke checks pass.
- No broad proxy cutover is allowed from this plan.

## Immediate execution order

1. Worker replacement: start with job-like imports/refresh/sync scripts because they are high-risk and easy to run in dry-run mode.
2. API and price/catalog facade: ASP.NET Core owns API/auth/database and delegates only AI enrichment to Python when needed.
3. CP login/session/dashboard parity: unlock admin migration and permission proof.
4. ERP finance/inventory exact-route parity.
5. Storefront SEO/cart/account parity.
