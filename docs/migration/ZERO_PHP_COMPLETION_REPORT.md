# Zero-PHP Completion Report

This report translates the current migration foundation into the remaining work required to reach production with ASP.NET Core as the primary platform, Python retained for workloads where it is stronger, and PHP fully removed.

## Current completion estimate

| Area | Current status | Completion |
| --- | --- | ---: |
| ASP.NET Core migration foundation | Platform, workers, diagnostics, cutover policy, runbooks, and tests are scaffolded | 100% |
| Python price/catalog sidecar foundation | Existing FastAPI `pyapi` supports price/search/read/ingest-adjacent flows | 70% |
| PHP route inventory and ownership | Inventory script exists, but every live route still needs final owner/parity status | 35% |
| CP/ERP/BOS business workflow replacement | Shells and parity reporters exist; real workflows still need ports | 15% |
| Storefront replacement | Foundation exists; real rendering/search/checkout parity still needs completion | 20% |
| Public API replacement | Catalog/price scaffolding exists; database-backed parity and API-key behavior need completion | 35% |
| Background jobs | Worker catalog/planner exists; production non-PHP job replacements need implementation | 25% |
| Production cutover evidence | Diagnostics-only posture exists; staged exact-route cutover evidence is pending | 25% |

Overall estimate: **about 35% complete toward true 0% PHP production**.

Remaining estimate: **about 65% left**.

## What 100% means

100% does not mean every line is C#. The target is:

- ASP.NET Core owns the public web app, CP, ERP, BOS, API gateway, auth/tenant policy, and production routing.
- Python owns approved sidecar workloads for price ingestion, supplier ETL, catalog enrichment, analytics, AI/ML, and high-volume async jobs.
- PHP owns nothing in production request handling or scheduled jobs.
- PHP-FPM, PHP cron entries, PHP route rewrites, PHP secrets, and PHP deployment dependencies can be removed after rollback approval.

## Remaining work by milestone

### Milestone 1 — make PR merge path clean

- Merge the latest final PR that contains the ASP.NET Core foundation and final handoff.
- Close/supersede older conflicting duplicate PRs.
- Keep the branch small after merge: future work should be route-by-route.

### Milestone 2 — route inventory hardening

- Generate the PHP route/job inventory.
- Assign every route/job an owner: ASP.NET Core, Python sidecar, or deleted.
- Mark every item as `php-only`, `aspnet-shadow`, `python-sidecar`, `parity-ready`, `live`, or `removed`.
- Block broad route proxying until there are zero unknown `php-only` routes.

### Milestone 3 — CP/ERP/BOS replacement

- Port CP login, dashboard, user admin, tenant admin, settings, and permissions.
- Port ERP finance, inventory, invoices, reports, chart of accounts, and tenant workflows.
- Port BOS command center and privileged admin actions.
- Validate permissions against legacy behavior before cutover.

### Milestone 4 — price/catalog hybrid replacement

- Keep Python for price ingestion, supplier feed parsing, normalization, search enrichment, and batch jobs.
- Put ASP.NET Core in front as the public/controller layer for catalog and price APIs.
- Add response parity tests between PHP and the ASP.NET Core/Python hybrid path.
- Retire PHP price endpoints only after live parity passes.

### Milestone 5 — storefront and public APIs

- Port storefront rendering and SEO-safe pages.
- Port checkout/cart/account flows.
- Port public API routes and API-key policy.
- Add live smoke tests for public traffic and tenant-specific behavior.

### Milestone 6 — non-PHP workers

- Replace PHP cron/setup scripts with ASP.NET Core workers or Python workers based on the ownership rule.
- Add idempotency, retry, dead-letter, telemetry, and alerting.
- Disable PHP cron only after the replacement has successful production runs.

### Milestone 7 — staged production cutover

- Start diagnostics-only.
- Move exact route groups one at a time.
- Keep PHP fallback enabled until every route group is live and stable.
- Run rollback drills before removing PHP.

### Milestone 8 — PHP removal

- Remove PHP rewrites and PHP-FPM dependencies.
- Archive PHP code and secrets.
- Remove PHP cron entries.
- Keep backup/restore evidence and rollback approval.

## Immediate next PR after the foundation

The next productive PR should not be another giant consolidation. It should be one of these small slices:

1. ASP.NET Core public price API delegates to Python `pyapi` and returns the same response shape as PHP.
2. Route inventory report writes a tracked JSON/Markdown artifact with ownership status.
3. CP login/session parity: ASP.NET Core validates the legacy session and renders a real authenticated CP dashboard shell.
4. Python worker hardening for price ingest: idempotency key, dead-letter table, and telemetry.

Recommended next slice: **ASP.NET Core price API + Python sidecar contract**, because price is already where Python is strongest today.
