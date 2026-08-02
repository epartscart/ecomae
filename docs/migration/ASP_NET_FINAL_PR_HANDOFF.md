# ASP.NET Core Final PR Handoff

This handoff marks the consolidated ASP.NET Core migration foundation as ready for review and merge after the test SDK and price parity expectation fixes.

## Merge scope

This branch contains the complete ASP.NET Core migration foundation:

- ASP.NET Core platform and worker projects under `aspnet/`.
- Migration diagnostics, readiness, progress, parity, and cutover endpoints.
- Tenant routing, surface modules, legacy-session/API-key scaffolding, catalog/price contracts, and worker-job scaffolding.
- CloudPanel/Nginx diagnostics-only deployment artifacts, systemd units, preflight checks, rollback scripts, and production runbooks.
- PHP route-alias compatibility fixes for CP, ERP, and BOS handoff.
- Regression and foundation checks for .NET, PHP, shell scripts, and proxy guardrails.

## Final hotfixes included

- `Microsoft.NET.Test.Sdk` is pinned to `18.3.0` so restore does not fail when `18.0.2` is unavailable in the configured package feed.
- `PriceLookupParityReporterTests` expects the normalized sample article `044650K020`, matching the ASP.NET Core article normalization behavior that strips punctuation and uppercases alphanumeric characters.

## Production posture after merge

This PR does **not** approve zero-PHP cutover by itself. It keeps the safe production posture as diagnostics-only ASP.NET Core with PHP fallback required until each route group has parity evidence.

Keep these cutover flags in production until route-by-route parity is proven:

```ini
MigrationRouteCutover__RequirePhpFallback=true
MigrationRouteCutover__ApiShadowTrafficEnabled=false
MigrationRouteCutover__StorefrontAspNetEnabled=false
MigrationRouteCutover__AdminAspNetEnabled=false
```

Only expose the diagnostics routes to ASP.NET Core at first:

- `/health`
- `/migration/status`
- `/migration/readiness`
- `/migration/route-cutover`

Do not proxy broad `/`, `/api`, `/cp`, `/erp`, or `/bos` traffic to ASP.NET Core until the related parity reports and live smoke tests are green.

## Required verification commands

Run these before merging or immediately after pulling the branch:

```bash
dotnet restore aspnet/tests/EcomAE.Platform.Tests
dotnet test aspnet/tests/EcomAE.Platform.Tests
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/preflight_aspnet_production.sh
bash scripts/verify_aspnet_proxy_guardrails.sh
```

Expected minimum result:

- .NET tests: 58 passed, 0 failed.
- Detailed foundation checks: failed count is 0; live smoke may remain skipped unless explicitly enabled.
- Proxy guardrails: failed count is 0.

## Zero-PHP status

The migration foundation is complete, but true 100% ASP.NET Core / 0 PHP still requires replacing all PHP business routes, APIs, worker jobs, authentication/session flows, tenant workflows, storefront rendering, reports, uploads/downloads, and production smoke coverage before PHP fallback can be removed.
