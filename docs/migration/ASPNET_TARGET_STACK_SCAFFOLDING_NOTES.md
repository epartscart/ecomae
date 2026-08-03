# ASP.NET Target Stack Scaffolding Notes

Scaffolding-only guidance for Enterprise BOS target components. **Nothing here enables production cutover.**

## Current runtime (honest)

- ASP.NET Core platform on `127.0.0.1:5100` behind Nginx diagnostics/exact-shadow configs.
- Data access today: `MySqlConnector` via `ITenantDbConnectionFactory` (legacy MySQL/MariaDB).
- Auth today: PHP cookie bridge + API keys + backend-group / modules_access ACL probe.
- Workers: write-blocked dry-run validators only.

## EF Core 10 readiness

- Package: `Microsoft.EntityFrameworkCore` 10.0.0 is referenced by `EcomAE.Platform`.
- Scaffold type: `EcomAE.Platform.Data.Scaffolding.EcomAeScaffoldDbContext` with Catalog, TenantRegistry, Identity, and ERP cash stub entities.
- Unwired contracts: `ICatalogScaffoldRepository`, `ITenantRegistryScaffoldRepository`, `IErpScaffoldRepository` (no DI registration).
- **Not wired:** `Program.cs` must not call `AddDbContext` until repository cutover is approved.
- Bridge phase: keep read-only SQL digests via `MySqlConnector`; introduce DbContext per bounded context (Catalog, Identity, ERP, TenantRegistry) without dual-write.
- PostgreSQL 17 is the long-term primary SoR: `EcomAePostgresScaffoldOptions` + `IPostgresMigrationScaffold` (`ReplaceMysqlBridge=false`).
- Do not claim PG live until migration + parity evidence exist.
- SQL Server 2025 remains the documented alternative, not the current path.

## YARP gateway design (not enabled)

- Nginx remains the production edge during Zero-PHP.
- Design example: `deploy/aspnet/yarp-exact-routes-example.json` (not loaded by `Program.cs`; `cutoverAllowed=false`).
- Regenerate: `python3 scripts/generate_yarp_exact_routes_example.py` (presentation + surface digests allowlists).
- Outputs: `yarp-exact-routes-example.json`, `yarp-surface-digests-example.json`.
- Future YARP cluster should only proxy **exact** approved routes already shadowed in `deploy/aspnet/nginx-*-shadow-example.conf`.
- Forbidden: catch-all `/api`, `/cp`, `/erp`, `/bos`, `/` locations.

## Redis 8 notes

- Scaffold types: `EcomAE.Platform.Caching.EcomAeRedisScaffoldOptions`, `IDistributedCacheScaffold` (no DI registration; `Enabled=false` by default).
- Intended for distributed cache, rate limits, and eventually session materialization.
- Until staging proves cookie parity, PHP `sessions` cookies remain authoritative (`ReplacePhpSessionCookies` must stay false).
- Do not store secrets in Redis; use Vault/Key Vault when introduced.

## OpenTelemetry ActivitySource names

Reserved in `EcomAE.Platform.Observability.EcomAeActivitySources`:

- `EcomAE.Platform`
- `EcomAE.Platform.Auth`
- `EcomAE.Platform.Surfaces`
- `EcomAE.Platform.Data`
- `EcomAE.Workers` (workers package may mirror later)

Auth activity starts on DB-backed session validate; Surfaces activity starts on selected digests (cash-entries, ERP/BOS dashboard summaries).
Exporters (OTLP → Prometheus/Grafana/Seq) are not registered in this scaffolding step.

## Messaging / search / storage (future)

- Kafka 4 primary: `EcomAeKafkaScaffoldOptions` + `IDomainEventPublisherScaffold` (`Enabled=false`, `AllowPublish=false`).
- RabbitMQ 4 alternative: `EcomAeRabbitMqScaffoldOptions` (`AllowPublish=false`).
- OpenSearch 3: `EcomAeOpenSearchScaffoldOptions` + `IEnterpriseSearchScaffold` (`ReplacePhpSearch=false`).
- Azure Blob / S3 / MinIO: `EcomAeObjectStorageScaffoldOptions` + `IObjectStorageScaffold` (`ReplaceLocalFilePaths=false`).
- Do not register producers/clients in `Program.cs` until dry-run parity evidence exists.

## Serilog / OTLP sinks (not registered)

- Scaffold options: `EcomAeSerilogScaffoldOptions` (`RegisterExporters=false`).
- Workers mirror: `EcomAE.Workers.Observability.EcomAeWorkerActivitySources`.
- Do not call `UseSerilog` / OTLP exporter registration until staging sink approval.

## AI boundary

- Python FastAPI sidecars for AI only.
- ASP.NET Core calls AI over REST/gRPC.
- Python must not own business transactions, permissions, or SoR writes.

## Vault / Key Vault (not bound)

- Scaffold types: `EcomAeVaultScaffoldOptions`, `ISecretStoreScaffold` (`ReplaceEnvFileSecrets=false`).
- CloudPanel env files remain the current secret source.
- Never commit credentials or paste secrets into PR comments.

## Helm / K8s (design only)

- Example chart: `deploy/aspnet/helm-ecomae-platform-example/` (`cutoverAllowed=false`, CloudPanel VM remains current).
- Do not apply this chart for production cutover or PHP removal.

## OAuth / SPA targets (not bound)

- OAuth/MFA: `EcomAeOAuthScaffoldOptions` + `IModernIdentityScaffold` (`ReplacePhpCookieBridge=false`).
- SPA: `EcomAeSpaScaffoldOptions` (`ReplaceBlazorHybridPresentation=false`; APIs via ASP.NET Core only).
- Consolidated example: `deploy/aspnet/ecomae-scaffold-options.example.json`.

## Polly resilience (not registered)

- Scaffold types: `EcomAePollyScaffoldOptions`, `IResiliencePipelineScaffold` (`RegisterPipelines=false`).
- Do not register pipelines in `Program.cs` until staging policy composition is approved.

## Hybrid UI dual-sample operator helper

- `bash scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh`
- Asserts compare-result keeps `cutoverAllowed=false`.
