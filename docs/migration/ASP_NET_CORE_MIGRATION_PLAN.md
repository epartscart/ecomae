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
