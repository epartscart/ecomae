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
