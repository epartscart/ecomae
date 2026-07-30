# ASP.NET Core Migration Plan

## Objective

Move ECOM AE from the current PHP runtime to an ASP.NET Core platform while keeping production PHP online during the transition. The final state is zero PHP files, no PHP-FPM, and no PHP runtime dependency.

## Starting Scope

The first migration slice adds an ASP.NET Core foundation under `aspnet/` without deleting any PHP files. PHP remains the production runtime until each surface reaches parity.

## Target Surfaces

| Current surface | ASP.NET Core destination |
| --- | --- |
| Super CP `/CP` | `EcomAE.Platform` CP area/controllers |
| Platform ERP `/ERP` | ERP module endpoints and UI |
| Super BOS `/BOS` | BOS module endpoints and UI |
| Tenant CP `tenant.com/CP` | Tenant-aware CP module |
| Tenant ERP `tenant.com/ERP` | Tenant-aware ERP module |
| Storefront | Razor/MVC storefront |
| Public APIs | ASP.NET Core Web API |
| PHP cron/setup scripts | Worker services/jobs |

## Migration Rules

1. Keep PHP running until ASP.NET Core has tested parity for the route being cut over.
2. Build tenant resolution first; every later module depends on tenant context.
3. Keep existing MySQL schema initially; introduce .NET migrations only after parity.
4. Move one module at a time behind routing/proxy rules.
5. After all routes, APIs, jobs, and setup tasks are replaced, remove PHP files and PHP runtime.

## First Milestone Included Here

- ASP.NET Core solution skeleton.
- Tenant context model.
- Route-based tenant resolver.
- Tenant resolution middleware.
- Health and migration status endpoints.
- Placeholder CP, ERP, and BOS endpoints.
- Static foundation checks runnable without the .NET SDK.

## Next Milestones

1. Add MySQL tenant registry access.
2. Add authentication/authorization parity for CP, ERP, and BOS.
3. Port public API endpoints.
4. Port Super CP tenant hub.
5. Port ERP shell and accounting dashboard.
6. Port BOS command center.
7. Port storefront and SEO routes.
8. Port background jobs.
9. Remove PHP once all parity tests pass.

## Second Milestone Included Here

- Configuration-backed tenant registry abstraction.
- Seed tenant records for platform, live tenant, and ERP-only tenant examples.
- Permission constants and ASP.NET Core authorization policy names for CP, ERP, BOS, tenant, and API access.
- Shared route constants for `/CP`, `/ERP`, `/BOS`, `/health`, `/migration/status`, and `/tenant/context`.

## Authorization Migration Targets

| Current PHP control | ASP.NET Core target |
| --- | --- |
| Super CP session checks | `SuperCp` authorization policy |
| Super ERP access | `SuperErp` authorization policy |
| Super BOS access | `SuperBos` authorization policy |
| Tenant CP login/session | `TenantCp` authorization policy |
| Tenant ERP login/session | `TenantErp` authorization policy |
| API key/session checks | `Api` authorization policy |

The policies initially define the permission names and wiring only. Real authentication must be connected to the existing user/session tables or a new Identity store in a later milestone.

## Third Milestone Included Here

- SQL contract for reading active tenants from the existing `epc_portal_tenants` registry table.
- `PortalTenantRow` mapper that translates PHP registry columns into ASP.NET Core `TenantRegistryRecord` values.
- Legacy session bridge contracts that understand the current PHP cookie/header names (`admin_session`, `admin_u_id`, `session`, `u_id`, and `X-API-Key`).
- `/auth/session/probe` diagnostic route for validating the bridge during migration. This route is a temporary migration aid and must be removed or locked before production cutover.

## Existing Registry Table Mapping

The ASP.NET Core registry bridge is aligned with the PHP-created `epc_portal_tenants` table: `site_key`, `hostname`, `db_name`, `status`, `is_demo`, `erp_only_shared`, `is_active`, `dedicated_db`, and `scale_policy`.

## Authentication Bridge Rules

1. PHP remains the source of truth until ASP.NET Core Identity/session storage is complete.
2. ASP.NET Core reads legacy cookies/API headers only to classify the request during transition.
3. Real DB-backed session verification must replace the probe validator before any protected ASP.NET Core module is exposed publicly.
4. Once .NET auth parity is proven, PHP sessions can be retired surface by surface.

## Fourth Milestone Included Here

- Surface module contracts for CP, ERP, BOS, storefront, and API replacement work.
- Central module endpoint mapper so each surface can be ported independently.
- Migration parity reporter exposed through `/migration/status` to list final state, PHP runtime status, surface descriptors, and next milestones.
- Worker service skeleton for replacing PHP cron/setup jobs with ASP.NET Core hosted services.

## Surface Port Order

1. API module: catalog, price lookup, tenant, ERP, BOS, mobile, and webhook APIs.
2. CP module: login shell, tenant hub, menu, orders, pricing, settings, integrations.
3. ERP module: shell, accounting dashboard, chart of accounts, journal vouchers, treasury.
4. BOS module: command center, fleet health, tenant operations, audit log.
5. Storefront module: SEO routes, product pages, cart, checkout, CMS, sitemaps.
6. Worker module: price import, sitemap generation, notifications, backups, scheduled ERP reports.

## Fifth Milestone Included Here

- ASP.NET Core catalog/price API compatibility route scaffolding.
- `/api/v1/catalog/status` status endpoint for replacing `api/v1/catalog.php`.
- `/api/v1/price/lookup` placeholder endpoint for replacing `api/v1/price/lookup.php`.
- `IPriceLookupService` abstraction and migration placeholder implementation with brand/article normalization.

## API Migration Priority

1. Preserve current public route shapes before changing clients.
2. Add ASP.NET Core DTOs and service interfaces.
3. Connect services to existing MySQL/catalog tables.
4. Run old PHP endpoint and new ASP.NET endpoint in parity mode.
5. Cut traffic to ASP.NET Core after response schema and performance match.
6. Delete the PHP endpoint once production telemetry proves parity.


## Sixth Milestone Included Here

- Added `/migration/readiness` to report whether PHP can be removed.
- The readiness report intentionally returns `not-ready-for-php-removal` until CP, ERP, BOS, tenant surfaces, APIs, and worker jobs reach tested parity.
- Each readiness item names the legacy PHP entry, ASP.NET Core destination, blocking status, and corrective action needed before production cutover.
- Production should only route a surface to ASP.NET Core after response parity, auth parity, tenant-mode validation, and worker telemetry checks pass.


## Seventh Milestone Included Here

- Added `/migration/cutover-plan` to expose the production route cutover order.
- Customer-facing CP, ERP, BOS, tenant CP, tenant ERP, public API, and worker routes remain disabled by default until their gates pass.
- Cutover strategy is feature-flagged reverse proxy routing with PHP fallback so a surface can be rolled back immediately.
- Rollback actions require disabling the ASP.NET Core route flag, sending the path back to PHP, keeping schema compatibility, and reviewing parity telemetry before re-enable.


## Eighth Milestone Included Here

- Added `/migration/progress` to expose overall ASP.NET Core migration completion.
- Current measured completion is 20%: platform foundation, tenant routing, migration telemetry, and API scaffolding have started or completed.
- Current measured pending work is 80%: CP, ERP, BOS, tenant surfaces, storefront, workers, database-backed APIs, parity testing, deployment routing, and PHP removal remain.
- The progress report is intentionally conservative and should only increase when production-parity code and tests replace PHP behavior.


## Ninth Milestone Included Here

- Added `/migration/surface-parity` to track parity evidence required before the migration can honestly claim 50% completion.
- The parity matrix covers login, Super CP, Platform ERP, Super BOS, tenant CP, tenant ERP, storefront, public APIs, and worker jobs.
- The migration should not be marked 50% complete until at least one customer-facing shell moves beyond placeholder status, catalog/price APIs read the real database, legacy session parity is validated, and proxy rollback telemetry exists.
- This keeps progress reporting honest while making the next 50% gate explicit for reviewers and production operators.


## Tenth Milestone Included Here

- Replaced CP, ERP, and BOS placeholder responses with structured ASP.NET Core surface shell payloads.
- Added `ISurfaceShellCatalog` and `MigrationSurfaceShellCatalog` so `/CP`, `/ERP`, and `/BOS` return sections, legacy mappings, tenant mode, and next parity checks.
- Progress increased from 20% to 28% because the first CP/ERP/BOS ASP.NET shell layer now exists, but it is not 50% until database-backed modules, auth parity, and production proxy gates pass.
- Remaining route-level work to reach 50% is real login parity, ERP finance data parity, BOS audit parity, and catalog/price database reads.


## Eleventh Milestone Included Here

- ASP.NET Core CP, ERP, and BOS modules now map lowercase, uppercase, and trailing-slash aliases.
- `ecomae.com/cp`, `ecomae.com/cp/`, `ecomae.com/CP`, `ecomae.com/CP/`, plus matching ERP and BOS forms all route to the same shell payload.
- The canonical generated route is lowercase (`/cp`, `/erp`, `/bos`) while uppercase aliases remain accepted for operator-entered URLs and legacy bookmarks.
- Live login testing still requires a user-entered password; credentials should never be committed or placed in PR text.


## Twelfth Milestone Included Here

- Added `tests/live_smoke/run_ecomae_surface_smoke.sh` for opt-in live checks of Super CP, ERP, BOS, and tenant CP/ERP URLs.
- Live credentials must be supplied only through environment variables (`ECOMAE_SUPER_EMAIL`, `ECOMAE_SUPER_PASSWORD`, `ECOMAE_TENANT_EMAIL`, `ECOMAE_TENANT_PASSWORD`) and are never printed.
- The script is disabled by default and only performs network checks when `RUN_LIVE_ECOMAE_SMOKE=1` is set.
- This prepares safe login/surface validation without committing passwords or leaking secrets into PR descriptions.


## PR Conflict Resolution Procedure

When several ASP.NET migration PRs are open and all conflict, close the duplicate PRs and create one consolidated branch from latest `origin/main`:

```bash
scripts/prepare_consolidated_aspnet_pr.sh work aspnet-migration-consolidated
git push -u origin aspnet-migration-consolidated
```

The script checks out the final migration files from the source branch onto latest `origin/main` instead of merging old PR history. This avoids conflicts from duplicate PRs while keeping one reviewable PR for the complete migration foundation.


## Thirteenth Milestone Included Here

- Added `LegacyPriceLookupSql` to mirror the current PHP `/api/v1/price/lookup.php` query contract against `shop_docpart_prices_data`.
- Expanded ASP.NET price offer DTO shape to include brand, article, item name, stock hint, and lead time fields that exist in the PHP response.
- Progress increased from 28% to 30% because the price API now has an explicit SQL parity contract, but full API migration remains pending until it executes against the real database with auth and response parity tests.


## Fourteenth Milestone Included Here

- Added `IPriceOfferRepository`, `PriceOfferRow`, `MigrationPriceOfferRepository`, and `RepositoryPriceLookupService` to move the price API from static placeholder response toward a repository-backed pipeline.
- The ASP.NET price lookup service now normalizes brand/article input, asks a repository for legacy offer rows, and maps rows into the PHP-compatible offer DTO shape.
- Progress increased from 30% to 32%; the remaining API work is provider-backed database execution, API-key auth parity, and response comparison against PHP.


## Fifteenth Milestone Included Here

- Added `LegacyApiClientKeyParser` and `LegacyApiClientKey` to mirror PHP API key prefix rules for `epc_catalog_` and `epc_pricepro_` keys.
- The ASP.NET legacy session validator now accepts `X-API-Key` and Bearer authorization headers only when the key matches the legacy prefix contract.
- This moves auth parity forward; remaining work is validating hashed keys against `epc_api_clients`, enforcing products/actions, and consuming daily quota in the database.


## Sixteenth Milestone Included Here

- Added `LegacyApiClientRecord`, `LegacyApiClientSql`, and `LegacyApiClientPolicy` to mirror PHP `epc_api_clients` product, action, and daily quota semantics.
- ASP.NET now has testable policy logic for `catalog`, `price_pro`, `both`, wildcard actions, JSON/list action filters, and daily quota availability.
- Progress increased from 33% to 35%; the remaining auth work is executing these contracts against the database and logging usage parity.


## Seventeenth Milestone Included Here

- Added `LegacyApiUsageLogEntry`, `LegacyApiUsageLogSql`, `ILegacyApiUsageLogger`, and `MigrationLegacyApiUsageLogger` to mirror PHP `epc_umapi_usage_log` field limits and usage logging shape.
- ASP.NET now has a registered usage logger boundary for API client parity, with tests for truncation, default source, quota-blocked metadata, and table mapping.
- Progress increased from 35% to 36%; remaining work is database-backed insert execution and wiring logger calls into every API auth success/failure path.


## Eighteenth Milestone Included Here

- Added a structured ASP.NET storefront shell payload for home/CMS, catalog browsing, cart/checkout, and customer account migration areas.
- Updated `StorefrontModule` to use `ISurfaceShellCatalog`, matching the CP/ERP/BOS shell approach.
- Progress increased from 36% to 37%; storefront still requires rendered HTML parity, catalog integration, cart/session compatibility, and checkout validation before PHP storefront removal.


## Nineteenth Milestone Included Here

- Added `MigrationWorkerJobCatalog` and `MigrationWorkerJob` to enumerate PHP cron/job replacements for price imports, sitemap generation, notifications, backups, and ERP scheduled reports.
- The worker host now registers the job catalog and logs planned job keys at startup.
- Progress increased from 37% to 39%; workers still require executable job implementations, retry policies, monitoring, and production scheduling before PHP cron removal.


## Twentieth Milestone Included Here

- Added `IMigrationWorkerJobRunner`, `MigrationWorkerJobRunRequest`, and `MigrationWorkerJobRunResult` so planned worker jobs can be dry-run/validated before executable cron replacements are enabled.
- The worker host now registers the runner and `TimeProvider.System`, which creates a testable seam for future scheduling, locks, retries, telemetry, and manual approvals.
- Progress increased from 39% to 41%; worker execution remains intentionally blocked for non-dry-run requests until concrete job implementations and production safeguards are complete.


## Twenty First Milestone Included Here

- Added `IMigrationWorkerSchedulePlanner` and `MigrationWorkerJobSchedulePlan` to document per-job schedule, lock key, retry policy, distributed-lock requirements, and execution readiness.
- The worker host now registers the schedule planner, giving future worker implementations a shared contract for lock/retry behavior before PHP cron jobs are disabled.
- Progress increased to 44%; background-job scaffolding is now complete at the contract level, but production execution remains blocked until concrete job code, lock storage, telemetry, and rollout switches are implemented.


## Twenty Second Milestone Included Here

- Added `IMigrationRouteCutoverPolicy` and `MigrationRouteCutoverDecision` to make PHP-vs-ASP.NET runtime decisions explicit for API, storefront, CP, ERP, BOS, and unknown surfaces.
- API routes are now modeled as eligible for ASP.NET shadow traffic with PHP fallback, while CP/ERP/BOS/storefront remain PHP-primary until business parity is complete.
- Added `scripts/push_consolidated_pr_update.sh` to push the conflict-free consolidated branch from a GitHub-connected workstation and then close superseded PRs #500 through #508.
- Progress increased to 45%; the remaining 55% is the business migration work: login, permissions, data writes, reports, storefront checkout, database-backed API/auth execution, production telemetry, and final PHP removal.


## Twenty Third Milestone Included Here

- Exposed `/migration/route-cutover` so operators and smoke tests can inspect the resolved tenant/surface runtime decision for the current request.
- The endpoint uses the same tenant middleware context and `IMigrationRouteCutoverPolicy`, keeping route cutover behavior testable without switching production traffic away from PHP.
- Progress increased to 46%; the next production blockers remain real feature flags/proxy routing, per-surface business parity, and telemetry-backed rollback validation.

