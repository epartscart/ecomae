#!/usr/bin/env bash
set -u
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
pass=0
fail=0
check() {
  local label="$1"
  shift
  if "$@"; then
    pass=$((pass + 1))
    printf '  PASS  %s\n' "$label"
  else
    fail=$((fail + 1))
    printf '  FAIL  %s\n' "$label"
  fi
}
contains() {
  local file="$1"
  local needle="$2"
  grep -Fq "$needle" "$file"
}

echo "== ASP.NET Core migration foundation =="
check 'solution file exists' test -f "$ROOT/aspnet/EcomAE.AspNetCore.sln"
check 'platform project targets net10.0' contains "$ROOT/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" '<TargetFramework>net10.0</TargetFramework>'
check 'global.json pins .NET 10 SDK band' contains "$ROOT/aspnet/global.json" '10.0.100'
check 'tenant context model exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Services/TenantContext.cs"
check 'tenant resolver handles CP' contains "$ROOT/aspnet/src/EcomAE.Platform/Services/RouteTenantResolver.cs" '"cp" => TenantSurface.ControlPanel'
check 'tenant resolver handles ERP' contains "$ROOT/aspnet/src/EcomAE.Platform/Services/RouteTenantResolver.cs" '"erp" => TenantSurface.Erp'
check 'tenant resolver handles BOS' contains "$ROOT/aspnet/src/EcomAE.Platform/Services/RouteTenantResolver.cs" '"bos" => TenantSurface.Bos'
check 'middleware stores tenant context' contains "$ROOT/aspnet/src/EcomAE.Platform/Middleware/TenantResolutionMiddleware.cs" 'EcomAE.Tenant'
check 'program maps migration status' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationStatus'
check 'CP module maps CP placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs" 'EcomAeRoutes.ControlPanel'
check 'ERP module maps ERP placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs" 'EcomAeRoutes.Erp'
check 'BOS module maps BOS placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs" 'EcomAeRoutes.Bos'
check 'configuration tenant registry exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Services/ConfigurationTenantRegistry.cs"
check 'tenant registry interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Services/ITenantRegistry.cs"
check 'appsettings seeds platform tenant' contains "$ROOT/aspnet/src/EcomAE.Platform/appsettings.json" 'www.ecomae.com'
check 'appsettings seeds ERP-only tenant' contains "$ROOT/aspnet/src/EcomAE.Platform/appsettings.json" 'ErpOnlyTenant'
check 'authorization policies exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Security/EcomAePolicies.cs"
check 'route constants exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"
check 'program registers tenant registry' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>'
check 'unit tests classify tenant ERP-only mode' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/TenantResolutionTests.cs" 'TenantMode.ErpOnlyTenant'
check 'portal tenant SQL maps epc_portal_tenants' contains "$ROOT/aspnet/src/EcomAE.Platform/Data/PortalTenantSql.cs" 'epc_portal_tenants'
check 'portal tenant row maps ERP-only mode' contains "$ROOT/aspnet/src/EcomAE.Platform/Data/PortalTenantRow.cs" 'TenantMode.ErpOnlyTenant'
check 'legacy session validator exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/HttpLegacySessionValidator.cs"
check 'legacy session tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacySessionValidatorTests.cs"
check 'api key legacy sessions authenticate' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionContext.cs" 'Kind == LegacySessionKind.ApiKey'
check 'legacy session probe route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/auth/session/probe'
check 'program registers legacy session validator' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacySessionValidator, HttpLegacySessionValidator'
check 'surface module contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/ISurfaceModule.cs"
check 'CP module exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"
check 'ERP module exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"
check 'BOS module exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs"
check 'storefront module exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"
check 'API module exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs"
check 'migration parity reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationParityReporter.cs"
check 'program maps surface modules' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'MapEcomAeSurfaceModules'
check 'worker project exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj"
check 'worker catalog names PHP job replacements' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobCatalog.cs" 'price-import'
check 'solution includes worker project' contains "$ROOT/aspnet/EcomAE.AspNetCore.sln" 'EcomAE.Workers'
check 'catalog API request DTO exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/PriceLookupRequest.cs"
check 'price lookup service exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/IPriceLookupService.cs"
check 'price lookup route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/price/lookup'
check 'catalog status route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/status'
check 'api module maps price lookup' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'EcomAeRoutes.PriceLookup'
check 'price lookup tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/PriceLookupServiceTests.cs"
check 'migration readiness route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/readiness'
check 'migration readiness reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationReadinessReporter.cs"
check 'program maps migration readiness' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationReadiness'
check 'readiness report blocks PHP removal' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationReadinessReporter.cs" 'not-ready-for-php-removal'
check 'readiness tests cover Tenant ERP' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationReadinessReporterTests.cs" 'Tenant ERP'
check 'migration cutover plan route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/cutover-plan'
check 'migration cutover planner exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationCutoverPlanner.cs"
check 'program maps migration cutover plan' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationCutoverPlan'
check 'cutover plan keeps PHP fallback' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationCutoverPlanner.cs" 'php-fallback'
check 'cutover tests cover BOS route' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationCutoverPlannerTests.cs" 'ecomae.com/BOS'
check 'migration progress route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/progress'
check 'migration progress reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationProgressReporter.cs"
check 'program maps migration progress' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationProgress'
check 'progress report exposes 44 percent complete' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationProgressReporterTests.cs" 'Assert.Equal(44, report.OverallCompletePercent)'
check 'progress report blocks PHP removal' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationProgressReporter.cs" 'Production cutover and PHP removal'
check 'surface parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/surface-parity'
check 'surface parity reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReporter.cs"
check 'program maps surface parity' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.SurfaceParity'
check 'surface parity tests cover ERP-only tenants' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/SurfaceParityReporterTests.cs" 'ERP-only tenant'
check 'surface parity report names fifty percent gate' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReport.cs" 'RequiredBeforeFiftyPercent'
check 'surface shell catalog exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Surfaces/MigrationSurfaceShellCatalog.cs"
check 'CP module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs" 'ISurfaceShellCatalog'
check 'ERP module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs" 'ISurfaceShellCatalog'
check 'BOS module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs" 'ISurfaceShellCatalog'
check 'program registers surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ISurfaceShellCatalog, MigrationSurfaceShellCatalog'
check 'CP aliases include lowercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/cp/"'
check 'ERP aliases include uppercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/ERP/"'
check 'BOS aliases include lowercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/bos/"'
check 'surface route alias tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/SurfaceRouteAliasTests.cs"
check 'live smoke script exists' test -x "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh"
check 'live smoke redacts passwords' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'Secrets: redacted'
check 'live smoke requires opt-in' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'RUN_LIVE_ECOMAE_SMOKE=1'
check 'live smoke checks super BOS trailing slash' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'Super BOS trailing slash'
check 'consolidated PR script exists' test -x "$ROOT/scripts/prepare_consolidated_aspnet_pr.sh"
check 'consolidated PR script starts from origin main' contains "$ROOT/scripts/prepare_consolidated_aspnet_pr.sh" 'git checkout -B "$TARGET_BRANCH" "$BASE_REMOTE/$BASE_BRANCH"'
check 'consolidated PR script avoids merging old PR history' contains "$ROOT/scripts/prepare_consolidated_aspnet_pr.sh" 'instead of merging'
check 'consolidated PR script includes aspnet tree' contains "$ROOT/scripts/prepare_consolidated_aspnet_pr.sh" 'aspnet'
check 'legacy price SQL contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/LegacyPriceLookupSql.cs"
check 'legacy price SQL maps shop_docpart_prices_data' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/LegacyPriceLookupSql.cs" 'shop_docpart_prices_data'
check 'price offer DTO exposes lead time' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/PriceLookupResult.cs" 'LeadTime'
check 'price offer repository interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/IPriceOfferRepository.cs"
check 'repository price lookup service exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/RepositoryPriceLookupService.cs"
check 'program registers price offer repository' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IPriceOfferRepository, MigrationPriceOfferRepository'
check 'program registers repository price lookup service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IPriceLookupService, RepositoryPriceLookupService'
check 'legacy API key parser exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs"
check 'legacy API key parser supports catalog prefix' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs" 'epc_catalog_'
check 'legacy API key parser supports pricepro prefix' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs" 'epc_pricepro_'
check 'legacy session validator uses API key parser' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/HttpLegacySessionValidator.cs" 'LegacyApiClientKeyParser.Parse'
check 'legacy API client SQL contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientSql.cs"
check 'legacy API client SQL maps epc_api_clients' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientSql.cs" 'epc_api_clients'
check 'legacy API client policy exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientPolicy.cs"
check 'legacy API client policy checks quota' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientPolicy.cs" 'QuotaAvailable'
check 'legacy API usage log SQL exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiUsageLogSql.cs"
check 'legacy API usage log SQL maps epc_umapi_usage_log' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiUsageLogSql.cs" 'epc_umapi_usage_log'
check 'legacy API usage logger exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/MigrationLegacyApiUsageLogger.cs"
check 'program registers legacy API usage logger' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiUsageLogger, MigrationLegacyApiUsageLogger'
check 'PR range rebase script exists' test -x "$ROOT/scripts/rebase_conflicted_pr_range.sh"
check 'PR range rebase script covers PR 500 default' contains "$ROOT/scripts/rebase_conflicted_pr_range.sh" 'START_PR="${1:-500}"'
check 'PR range rebase script covers PR 508 default' contains "$ROOT/scripts/rebase_conflicted_pr_range.sh" 'END_PR="${2:-508}"'
check 'PR rebase runbook exists' test -f "$ROOT/docs/migration/PR_REBASE_CONFLICT_RUNBOOK.md"
check 'storefront module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs" 'ISurfaceShellCatalog'
check 'storefront shell maps catalog browsing' contains "$ROOT/aspnet/src/EcomAE.Platform/Surfaces/MigrationSurfaceShellCatalog.cs" 'Catalog browsing'
check 'worker job catalog exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobCatalog.cs"
check 'worker job catalog includes price import' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobCatalog.cs" 'price-import'
check 'worker program registers job catalog' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'MigrationWorkerJobCatalog'
check 'worker placeholder logs job count' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerPlaceholder.cs" 'JobCount'
check 'worker job runner interface exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/IMigrationWorkerJobRunner.cs"
check 'worker job runner dry-run status exists' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobRunner.cs" 'dry-run-planned'
check 'worker job runner blocks non-dry-run' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobRunner.cs" 'manual-approval-required'
check 'worker program registers job runner' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobRunner, MigrationWorkerJobRunner'
check 'worker job runner tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationWorkerJobRunnerTests.cs"
check 'worker schedule planner interface exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/IMigrationWorkerSchedulePlanner.cs"
check 'worker schedule planner has distributed lock readiness' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerSchedulePlanner.cs" 'RequiresDistributedLock: true'
check 'worker schedule planner includes dead-letter policy' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerSchedulePlanner.cs" 'exponential-backoff-with-dead-letter'
check 'worker program registers schedule planner' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerSchedulePlanner, MigrationWorkerSchedulePlanner'
check 'worker schedule planner tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationWorkerSchedulePlannerTests.cs"
check 'migration plan documents zero PHP final state' contains "$ROOT/docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md" 'zero PHP files'

echo "----------------------------"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
