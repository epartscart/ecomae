# ASP.NET Target Stack Scaffolding Notes

Scaffolding-only guidance for Enterprise BOS target components. **Nothing here enables production cutover.**

## Current runtime (honest)

- ASP.NET Core platform on `127.0.0.1:5100` behind Nginx diagnostics/exact-shadow configs.
- Data access today: `MySqlConnector` via `ITenantDbConnectionFactory` (legacy MySQL/MariaDB).
- Auth today: PHP cookie bridge + API keys + backend-group / modules_access ACL probe.
- Workers: write-blocked dry-run validators only.

## EF Core 10 readiness

- Package: `Microsoft.EntityFrameworkCore` 10.0.0 is referenced by `EcomAE.Platform`.
- Scaffold type: `EcomAE.Platform.Data.Scaffolding.EcomAeScaffoldDbContext` with Catalog, TenantRegistry, and Identity stub entities.
- Unwired contracts: `ICatalogScaffoldRepository`, `ITenantRegistryScaffoldRepository` (no DI registration).
- **Not wired:** `Program.cs` must not call `AddDbContext` until repository cutover is approved.
- Bridge phase: keep read-only SQL digests via `MySqlConnector`; introduce DbContext per bounded context (Catalog, Identity, ERP, TenantRegistry) without dual-write.
- PostgreSQL 17 is the long-term primary SoR; do not claim PG live until migration + parity evidence exist.
- SQL Server 2025 remains the documented alternative, not the current path.

## YARP gateway design (not enabled)

- Nginx remains the production edge during Zero-PHP.
- Design example: `deploy/aspnet/yarp-exact-routes-example.json` (not loaded by `Program.cs`; `cutoverAllowed=false`).
- Future YARP cluster should only proxy **exact** approved routes already shadowed in `deploy/aspnet/nginx-*-shadow-example.conf`.
- Forbidden: catch-all `/api`, `/cp`, `/erp`, `/bos`, `/` locations.

## Redis 8 notes

- Intended for distributed cache, rate limits, and eventually session materialization.
- Until staging proves cookie parity, PHP `sessions` cookies remain authoritative.
- Do not store secrets in Redis; use Vault/Key Vault when introduced.

## OpenTelemetry ActivitySource names

Reserved in `EcomAE.Platform.Observability.EcomAeActivitySources`:

- `EcomAE.Platform`
- `EcomAE.Platform.Auth`
- `EcomAE.Platform.Surfaces`
- `EcomAE.Platform.Data`
- `EcomAE.Workers` (workers package may mirror later)

Surfaces activity is started on selected digests (e.g. cash-entries) for future OTEL wiring.
Exporters (OTLP → Prometheus/Grafana/Seq) are not registered in this scaffolding step.

## Messaging / search / storage (future)

- Kafka 4 primary (RabbitMQ alternative) for domain/integration events after worker parity.
- OpenSearch 3 for enterprise search/logs.
- Azure Blob / S3 / MinIO for documents and backups.

## AI boundary

- Python FastAPI sidecars for AI only.
- ASP.NET Core calls AI over REST/gRPC.
- Python must not own business transactions, permissions, or SoR writes.
