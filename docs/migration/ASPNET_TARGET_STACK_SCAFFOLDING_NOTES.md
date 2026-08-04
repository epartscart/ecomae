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
- Regenerate: `bash scripts/generate_all_yarp_design_examples.sh` (presentation + surface + storefront + catalog/api).
- Outputs: `yarp-exact-routes-example.json`, `yarp-surface-digests-example.json`, `yarp-storefront-digests-example.json`, `yarp-catalog-api-example.json`.
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

## Dual-sample operator helpers

- All families: `bash scripts/cloudpanel_run_all_dual_sample_operators.sh`
- Hybrid UI: `bash scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh`
- Login cookie (Batch 3): `bash scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh`
- Catalog miss (Batch 5): `bash scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh`
- Digest (cookie capture or migration contract-only): `bash scripts/cloudpanel_run_digest_dual_sample_operator.sh`
- Module-function inventory (`aspnetCompleteCount=0`): `bash scripts/cloudpanel_run_module_function_parity_operator.sh`
- Presentation recheck (cached/honest fail): `bash scripts/cloudpanel_run_presentation_recheck_operator.sh`
- Tenant same-to-same (cached pass + cutover false): `bash scripts/cloudpanel_run_tenant_safety_operator.sh`
- Operator index: `docs/migration/evidence/OPERATOR_VERIFY.md`
- Asserts compare-result keeps `cutoverAllowed=false`.

## GraphQL / gRPC (not exposed)

- GraphQL: `EcomAeGraphQlScaffoldOptions` + `IGraphQlScaffold` (`ExposePublicEndpoint=false`; REST default).
- gRPC: `EcomAeGrpcScaffoldOptions` + `IGrpcScaffold` (`ExposePublicEndpoint=false`).

## Blockchain integration (proof only)

- `EcomAeBlockchainScaffoldOptions` + `IBlockchainIntegrationScaffold` (`UseAsBusinessSourceOfRecord=false`).
- Business SoR remains app DB.

## Rate limiting (not registered)

- `EcomAeRateLimitScaffoldOptions` (`ReplaceLegacyApiClientThrottle=false`).

## GitOps / Argo CD (design only)

- Example Application: `deploy/aspnet/gitops-example/argocd-application.example.yaml` (`cutoverAllowed=false`).
- Workers chart: `deploy/aspnet/helm-ecomae-workers-example/` (`allowWorkerWrites=false`, dry-run).
- CloudPanel VM remains the current host; do not apply for production cutover.

## Native AOT (not required for platform host)

- `EcomAeNativeAotScaffoldOptions` (`RequireForPlatformHost=false`).
- Evaluate only for isolated services after trimming/reflection evidence.

## AI sidecar (Python FastAPI, AI-only)

- `EcomAeAiSidecarScaffoldOptions` + `IAiSidecarClientScaffold` (`AllowBusinessWrites=false`).
- ASP.NET Core calls AI over REST/gRPC; Python must not own permissions or SoR writes.

## Guardrails (must stay green)

- Consolidated options: `deploy/aspnet/ecomae-scaffold-options.example.json` — validate with `python3 scripts/validate_scaffold_options_example.py`.
- Evidence locks: `python3 scripts/validate_migration_evidence_cutover_locks.py` (no true cutover flags; no invented approval/pass files).
- Migration golden locks: `python3 scripts/validate_migration_golden_cutover_locks.py` (53 goldens declare cutoverAllowed=false + match generator).
- Allowlist sync: `python3 scripts/validate_presentation_hybrid_allowlist_sync.py` (nginx 47 ↔ hybrid 37 + shells/logins/auth ↔ installer expected ↔ YARP routeCount; hybrid digestRoute cross-lock).
- Digest allowlist sync: `python3 scripts/validate_surface_digest_allowlist_sync.py` (surface 30 + storefront 4 + orders-digest ↔ capture/compare/migration goldens ↔ YARP).
- Digest dual-sample contracts: 35 stems via `python3 scripts/compare_digest_dual_samples.py --contract-only`.
- Catalog/API allowlist sync: `python3 scripts/validate_catalog_api_allowlist_sync.py` (19 exact routes).
- Catalog/API contract floor: `bash scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh`.
- Full suite: `bash scripts/validate_enterprise_bos_scaffold_guardrails.sh` (Program.cs omits production wiring; YARP/Helm/Argo keep `cutoverAllowed=false`; regenerates YARP design packs).
- Disabled env key comments: `deploy/aspnet/platform.env.example` (`EcomAe__*` Replace/Allow/Register flags stay false).
- Never invent `RELEASE_OWNER_APPROVAL.md`.
