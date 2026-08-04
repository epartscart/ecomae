# Enterprise BOS Architecture Compliance Tracker

Tracks adherence to `PROJECT_ARCHITECTURE_INSTRUCTIONS.md` (Enterprise BOS Cloud Platform Technology & Architecture Instructions) — the **canonical project law**.

This is a **compliance tracker**, not a claim that production already runs the full target stack.

## Superseded / interim docs (do not follow for ownership)

| Doc | Status |
| --- | --- |
| `docs/PYTHON_MIGRATION.md` | Historical. Python business APIs must not expand; AI-only going forward. |
| `docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md` | Valid for Zero-PHP sequencing; MySQL is **bridge SoR**, target is PG17 + EF Core 10. |
| `docs/BLOCKCHAIN_BOS_ENTERPRISE.md` | Blockchain remains integration/proof only; business SoR moves to ASP.NET Core. |
| `docs/TENANT_SCALE_1000.md` | Interim PHP/MySQL scale; destination is ASP.NET workers + PG17 tenancy. |
| `docs/migration/CURSOR_HANDOFF_STATUS.md` | Operational handoff; progress % deferred to Zero-PHP status + this tracker. |

## Decision guide (mandatory)

| Concern | Owner |
| --- | --- |
| Business logic, APIs, authz, workflows, finance, multi-tenancy | ASP.NET Core 10 |
| Database access / EF Core | ASP.NET Core 10 |
| AI / ML / OCR / LLM / vision / data science | Python 3.13+ FastAPI sidecars only |
| Smart contracts / immutable proofs | Blockchain integration layer only |
| Frontend | Angular 20 or React 19 via ASP.NET Core APIs only |

Forbidden unless explicitly requested: Java Spring Boot, Node.js backend, Go backend, new PHP backends.

## Current vs target

| Requirement | Status | Notes |
| --- | --- | --- |
| .NET 10 / ASP.NET Core 10 / C# | ✅ in repo | `net10.0` platform + workers |
| ASP.NET Core owns enterprise app | 🔶 migration in progress | Exact-route ASP.NET ownership; PHP temporary fallback |
| No new non-.NET backends | ✅ | No Java/Node/Go introduced |
| Python AI-only | 🔶 options scaffold | `EcomAeAiSidecarScaffoldOptions` (`AllowBusinessWrites=false`); legacy `pyapi` business surface remains temporary |
| EF Core 10 primary ORM | 🔶 package + bounded-context stubs | `Microsoft.EntityFrameworkCore` 10.0.0 referenced; Catalog/TenantRegistry/Identity/ERP stubs on `EcomAeScaffoldDbContext` (not registered in `Program.cs`); production still uses `MySqlConnector` ADO |
| PostgreSQL 17 primary SoR | 🔶 options scaffold | `EcomAePostgresScaffoldOptions` + `IPostgresMigrationScaffold` unwired (`ReplaceMysqlBridge=false`); MySQL/MariaDB remains bridge SoR |
| Redis 8 | 🔶 options scaffold | `EcomAeRedisScaffoldOptions` + `IDistributedCacheScaffold` unwired; PHP cookies remain authoritative (`ReplacePhpSessionCookies=false`) |
| Kafka 4 (or RabbitMQ) | 🔶 options scaffold | Kafka + RabbitMQ options unwired (`AllowPublish=false`); dry-run workers only |
| OpenSearch 3 | 🔶 options scaffold | `EcomAeOpenSearchScaffoldOptions` + `IEnterpriseSearchScaffold` unwired (`ReplacePhpSearch=false`) |
| Object storage (Blob/S3/MinIO) | 🔶 options scaffold | `EcomAeObjectStorageScaffoldOptions` + `IObjectStorageScaffold` unwired (`ReplaceLocalFilePaths=false`) |
| YARP / Kong gateway | 🔶 design example | Nginx edge today; YARP JSON for presentation/surface/storefront/catalog-api (`generate_all_yarp_design_examples.sh`); not loaded; never catch-all |
| OpenTelemetry / Serilog | 🔶 scaffolding | ActivitySources + `EcomAeSerilogScaffoldOptions` (`RegisterExporters=false`); Workers ActivitySource mirror; exporters/sinks not registered |
| Polly resilience pipelines | 🔶 options scaffold | `EcomAePollyScaffoldOptions` + `IResiliencePipelineScaffold` unwired (`RegisterPipelines=false`) |
| GraphQL / gRPC | 🔶 options scaffold | GraphQL/gRPC options unwired (`ExposePublicEndpoint=false`); REST remains default |
| Rate limiting | 🔶 options scaffold | `EcomAeRateLimitScaffoldOptions` unwired (`ReplaceLegacyApiClientThrottle=false`) |
| Native AOT | 🔶 options scaffold | `EcomAeNativeAotScaffoldOptions` (`RequireForPlatformHost=false`; isolated evaluation only) |
| Vault / Key Vault | 🔶 options scaffold | `EcomAeVaultScaffoldOptions` + `ISecretStoreScaffold` unwired (`ReplaceEnvFileSecrets=false`); CloudPanel env files remain current |
| K8s / Helm / GitOps | 🔶 design chart | Platform + workers Helm examples + Argo CD Application example (`cutoverAllowed=false`); CloudPanel VM is current host |
| Angular 20 / React 19 | 🔶 options scaffold | `EcomAeSpaScaffoldOptions` unwired (`ReplaceBlazorHybridPresentation=false`); interim UI remains Blazor SSR hybrid |
| Blazor SSR hybrid presentation | 🔶 in progress | `/cp|erp|bos|storefront/*-app` www previews under PHP chrome shells; not tenant product chrome |
| Blockchain as integration only | 🔶 options scaffold | `EcomAeBlockchainScaffoldOptions` (`UseAsBusinessSourceOfRecord=false`); business SoR remains app DB |
| Modular monolith first | ✅ direction | Surface modules under `EcomAE.Platform`; extract microservices later |
| Zero Trust / MFA / OAuth 2.1 | 🔶 options scaffold | `EcomAeOAuthScaffoldOptions` + `IModernIdentityScaffold` unwired (`ReplacePhpCookieBridge=false`); PHP cookies remain authoritative |

## Zero-PHP relationship

- ASP.NET Core is the **destination** enterprise platform; PHP is temporary until exact-route/job parity + rollback approval.
- **Same-to-same tenant law:** live product chrome (`/`, `/CP/`, `/ERP/`, `/BOS/`, tenant hosts) stays PHP until exact-route shadows + dual-sample parity + human `RELEASE_OWNER_APPROVAL.md`. Digests/Blazor previews on www are scaffolding only — tenants must not feel PHP→ASP.NET.
- Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront nginx cutover remains forbidden.
- Do not remove PHP-FPM/cron/source until every tracked item is live/removed with evidence.
- Do not invent `RELEASE_OWNER_APPROVAL.md`.

## Scaffold guardrails

- `scripts/validate_scaffold_options_example.py` — fails if dangerous Replace/Allow/Register flags are true.
- `scripts/validate_migration_evidence_cutover_locks.py` — evidence JSON cannot claim cutover/PHP removal; approval/pass files must stay absent.
- `scripts/validate_presentation_hybrid_allowlist_sync.py` — presentation nginx, hybrid TARGETS, installer expected, and YARP routeCount stay aligned.
- `scripts/validate_enterprise_bos_scaffold_guardrails.sh` — Program.cs must omit production clients; YARP/Helm/Argo stay `cutoverAllowed=false`.
- `deploy/aspnet/platform.env.example` documents disabled `EcomAe__*` scaffold keys only.

## Next architecture tracks (ordered)

1. CloudPanel operator: live dual-sample captures after presentation shadows (`bash scripts/cloudpanel_run_all_dual_sample_operators.sh`; `cutoverAllowed=false` always).
2. Introduce EF Core 10 against current DB bridge (register DbContext only after approved repository cutover), then plan PostgreSQL 17 cutover.
3. Wire OpenTelemetry exporters + Serilog sinks; keep ActivitySource names stable.
4. Optionally place YARP behind Nginx for approved exact routes only (regenerate design JSON from nginx allowlist; not enabled).
5. Redis cache/rate-limit sidecar after cookie parity — keep `ReplacePhpSessionCookies=false` until evidence.
6. Kafka domain events for workers after dry-run parity samples.
7. Object storage + Vault secret materialization after staging parity (keep Replace* flags false until evidence).
8. SPA admin/storefront (Angular 20 or React 19) against ASP.NET Core APIs only — after Blazor hybrid parity evidence.
