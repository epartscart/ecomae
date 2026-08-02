# Zero-PHP Production Cutover Roadmap

Target: ASP.NET Core serves 100% of production traffic and PHP is fully decommissioned. This document defines the required path; it does not approve cutover by itself.

## Current status

- Repository ASP.NET Core migration foundation: complete.
- Diagnostics-only CloudPanel/Nginx deployment artifacts: complete.
- PHP authoritative fallback: still required until every route and job has parity evidence.
- Production zero-PHP status: not complete.

## Non-negotiable cutover rule

PHP can only be removed when all required application surfaces, APIs, background jobs, data contracts, authentication/session flows, tenant routing modes, observability, rollback procedures, and production smoke tests are green with dated evidence.

## Phase 1: Route inventory and ownership

- Export every production PHP route, rewrite, API endpoint, CP/ERP/BOS/storefront path, AJAX endpoint, webhook, file upload/download path, and scheduled job into a tracked inventory.
- Assign an ASP.NET Core owner and parity status to every route and job.
- Mark routes as one of: `php-only`, `aspnet-core-shadow`, `aspnet-core-parity-ready`, `aspnet-core-live`, or `php-removed`.
- Block broad `/api`, `/cp`, `/erp`, `/bos`, and storefront catch-all cutovers until the inventory has zero `php-only` unknowns.

## Phase 2: ASP.NET Core implementation completion

- Replace placeholder shell endpoints with real ASP.NET Core behavior for CP, ERP, BOS, storefront, and API surfaces.
- Implement catalog, pricing, orders, users, authentication/session, tenant administration, reports, uploads/downloads, and integrations using typed services and tested contracts.
- Replace PHP cron/setup scripts with ASP.NET Core worker jobs, idempotent job runners, distributed locks, dead-letter handling, and retry policies.
- Keep PHP read-only or shadow-only after ASP.NET Core parity is proven for each route.

## Phase 3: Data migration and multi-tenant readiness

- Validate every tenant mode, including ERP-only, BOS, storefront, API clients, and platform tenants.
- Prove database schema compatibility, migrations, indexes, query plans, sharding decisions, cache invalidation, and tenant isolation.
- Run read-after-write parity tests for all critical business flows.
- Freeze destructive PHP schema changes before final cutover.

## Phase 4: Security and compliance readiness

- Validate OAuth2/OIDC or approved legacy-session bridge behavior for all user and API flows.
- Prove dynamic authorization policies for tenant, company, department, role, feature entitlement, and risk context.
- Complete secrets audit, cookie/session hardening, CSRF/CORS validation, API-key rotation, audit logging, and least-privilege worker review.
- Produce threat-model notes for every externally reachable ASP.NET Core surface.

## Phase 5: Performance, reliability, and observability gates

- Establish production-like load tests for 1,000+ tenants and peak catalog/ERP/API usage.
- Add OpenTelemetry traces, metrics, structured logs, dashboards, and alerts for ASP.NET Core platform and workers.
- Prove Kestrel, GC, cache, database, queue, and worker behavior under load using benchmark/profiler evidence.
- Run chaos drills for database latency, Redis/cache outage, worker crash, pod/process restart, and network degradation in staging.

## Phase 6: Staged production rollout

- Start with diagnostics-only routes and health/parity endpoints.
- Move exact routes to ASP.NET Core one route group at a time using feature flags and rollback plans.
- Require successful live smoke checks after each group: unauthenticated storefront, authenticated CP, ERP, BOS, API key, upload/download, and background-job checks.
- Keep PHP fallback enabled until every route group is live and stable for the approved observation window.

## Phase 7: PHP decommission

- Disable PHP write paths after ASP.NET Core owns all writes and workers.
- Archive PHP code, cron definitions, web-server rewrites, PHP-FPM configuration, and legacy secrets after rollback window approval.
- Remove PHP runtime packages only after backup, restore, and rollback evidence is signed off.
- Final state: Nginx/CloudPanel routes all application traffic to ASP.NET Core platform/workers and no production request requires PHP.

## Evidence required for 100% production status

- Route inventory shows zero `php-only` and zero `aspnet-core-shadow` production routes.
- All ASP.NET Core unit/integration/e2e/live smoke tests pass in CI and production smoke.
- All worker jobs have ASP.NET Core replacements with idempotency and retry/dead-letter evidence.
- Observability dashboards and alerts are live for platform, workers, databases, queues, cache, and tenant errors.
- Rollback and restore drills are completed and documented.
- Business owner signs off CP, ERP, BOS, storefront, API, reports, and financial flows.

## Short status report

- Code foundation: 100% for migration scaffolding and guardrails.
- Production deployment automation: 80% because scripts/runbooks exist, but real server execution and live smoke remain pending.
- ASP.NET Core feature parity: not yet 100%; placeholder routes and scaffolds must be replaced with complete business logic.
- Zero-PHP production: 0% complete until all PHP routes/jobs are replaced, verified live, and PHP fallback is removed after approval.
- Overall zero-PHP target readiness from this repository alone: approximately 35% complete.
