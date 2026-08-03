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
| Python AI-only | 🔶 partial | Hybrid roadmap states AI-only; legacy `docs/PYTHON_MIGRATION.md` is superseded for business APIs |
| EF Core 10 primary ORM | 🔶 package + empty scaffold | `Microsoft.EntityFrameworkCore` 10.0.0 referenced; `EcomAeScaffoldDbContext` not registered in `Program.cs`; production still uses `MySqlConnector` ADO |
| PostgreSQL 17 primary SoR | ❌ not migrated | Legacy MySQL/MariaDB remains SoR during Zero-PHP; PG17 is target after parity |
| Redis 8 | ❌ not wired | Documented in scaffolding notes; PHP cookies remain authoritative |
| Kafka 4 (or RabbitMQ) | ❌ not wired | Event architecture planned; not claimed live |
| OpenSearch 3 | ❌ not wired | Future search track |
| Object storage (Blob/S3/MinIO) | ❌ not wired | Future file track |
| YARP / Kong gateway | ❌ not wired | Nginx + diagnostics-only proxy today; YARP design notes only |
| OpenTelemetry / Serilog | 🔶 scaffolding | `EcomAeActivitySources` names reserved; exporters not registered |
| Vault / Key Vault | ❌ not wired | Env files used in CloudPanel deploy today |
| K8s / Helm / GitOps | 🔶 roadmap | Advanced architecture roadmap exists; CloudPanel VM is current host |
| Angular 20 / React 19 | ❌ not started | Target SPA later; interim UI is Blazor SSR hybrid chrome on exact-routes only |
| Blazor SSR hybrid presentation | 🔶 in progress | `/cp|erp|bos|storefront/*-app` www previews under PHP chrome shells; not tenant product chrome |
| Blockchain as integration only | ✅ policy | Business SoR remains app DB; blockchain docs treat it as proof layer |
| Modular monolith first | ✅ direction | Surface modules under `EcomAE.Platform`; extract microservices later |
| Zero Trust / MFA / OAuth 2.1 | 🔶 partial | Legacy session + API-key bridges; modern identity pending |

## Zero-PHP relationship

- ASP.NET Core is the **destination** enterprise platform; PHP is temporary until exact-route/job parity + rollback approval.
- **Same-to-same tenant law:** live product chrome (`/`, `/CP/`, `/ERP/`, `/BOS/`, tenant hosts) stays PHP until exact-route shadows + dual-sample parity + human `RELEASE_OWNER_APPROVAL.md`. Digests/Blazor previews on www are scaffolding only — tenants must not feel PHP→ASP.NET.
- Broad `/api`, `/cp`, `/erp`, `/bos`, or storefront nginx cutover remains forbidden.
- Do not remove PHP-FPM/cron/source until every tracked item is live/removed with evidence.
- Do not invent `RELEASE_OWNER_APPROVAL.md`.

## Next architecture tracks (ordered)

1. Finish exact-route Zero-PHP digests/parity with PHP fallback (storefront account family: orders → garage/profile → dual-sample evidence packs).
2. Introduce EF Core 10 against current DB bridge, then plan PostgreSQL 17 cutover.
3. Wire OpenTelemetry exporters + Serilog sinks; keep ActivitySource names stable.
4. Add YARP edge design behind Nginx for approved exact routes only.
5. Redis session/cache sidecar after cookie parity evidence.
6. Kafka domain events for workers after dry-run parity samples.
7. SPA admin/storefront (Angular 20 or React 19) against ASP.NET Core APIs only — after Blazor hybrid parity evidence.
