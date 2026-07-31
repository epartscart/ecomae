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
check 'legacy session parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/ILegacySessionParityReporter.cs"
check 'legacy session parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/auth/session/parity'
check 'program maps legacy session parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.LegacySessionParity'
check 'program registers legacy session parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacySessionParityReporter, LegacySessionParityReporter'
check 'legacy session parity tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacySessionParityReporterTests.cs"
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
check 'catalog parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/ICatalogParityReporter.cs"
check 'catalog parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/parity'
check 'api module maps catalog parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'EcomAeRoutes.CatalogParity'
check 'program registers catalog parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogParityReporter, CatalogParityReporter'
check 'catalog parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/CatalogParityReporterTests.cs"
check 'api module maps price lookup' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'EcomAeRoutes.PriceLookup'
check 'price lookup tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/PriceLookupServiceTests.cs"
check 'price parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/IPriceLookupParityReporter.cs"
check 'price parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/price/parity'
check 'api module maps price parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'EcomAeRoutes.PriceLookupParity'
check 'program registers price parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IPriceLookupParityReporter, PriceLookupParityReporter'
check 'price parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/PriceLookupParityReporterTests.cs"
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
check 'progress report exposes 100 percent foundation complete' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationProgressReporterTests.cs" 'Assert.Equal(100, report.OverallCompletePercent)'
check 'progress report keeps cutover approval gated' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationProgressReporter.cs" 'release-owner cutover approval'
check 'cutover validation reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/ICutoverValidationReporter.cs"
check 'cutover validation route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/cutover-validation'
check 'program maps cutover validation endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationCutoverValidation'
check 'program registers cutover validation reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICutoverValidationReporter, CutoverValidationReporter'
check 'cutover validation reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/CutoverValidationReporterTests.cs"
check 'data parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IDataParityReporter.cs"
check 'data parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/data-parity'
check 'program maps data parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationDataParity'
check 'program registers data parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IDataParityReporter, DataParityReporter'
check 'data parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/DataParityReporterTests.cs"
check 'surface parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/surface-parity'
check 'surface parity reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReporter.cs"
check 'program maps surface parity' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.SurfaceParity'
check 'surface parity tests cover ERP-only tenants' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/SurfaceParityReporterTests.cs" 'ERP-only tenant'
check 'surface parity report names fifty percent gate' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReport.cs" 'RequiredBeforeFiftyPercent'
check 'surface shell catalog exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Surfaces/MigrationSurfaceShellCatalog.cs"
check 'CP parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IControlPanelParityReporter.cs"
check 'CP parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/parity'
check 'CP module maps parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs" 'EcomAeRoutes.ControlPanelParity'
check 'program registers CP parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IControlPanelParityReporter, ControlPanelParityReporter'
check 'CP parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/ControlPanelParityReporterTests.cs"
check 'CP module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs" 'ISurfaceShellCatalog'
check 'ERP parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IErpParityReporter.cs"
check 'ERP parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/parity'
check 'ERP module maps parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs" 'EcomAeRoutes.ErpParity'
check 'program registers ERP parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IErpParityReporter, ErpParityReporter'
check 'ERP parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/ErpParityReporterTests.cs"
check 'BOS parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IBosParityReporter.cs"
check 'BOS parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/parity'
check 'BOS module maps parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs" 'EcomAeRoutes.BosParity'
check 'program registers BOS parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IBosParityReporter, BosParityReporter'
check 'BOS parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/BosParityReporterTests.cs"
check 'ERP module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs" 'ISurfaceShellCatalog'
check 'BOS module uses surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs" 'ISurfaceShellCatalog'
check 'program registers surface shell catalog' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ISurfaceShellCatalog, MigrationSurfaceShellCatalog'
check 'CP aliases include lowercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/cp/"'
check 'ERP aliases include uppercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/ERP/"'
check 'BOS aliases include lowercase trailing slash' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '"/bos/"'
check 'surface route alias tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/SurfaceRouteAliasTests.cs"
check 'live smoke script exists' test -x "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh"
check 'live smoke redacts passwords' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'Secrets: redacted'
check 'live smoke supports super username alias' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'ECOMAE_SUPER_USERNAME'
check 'live smoke checks login page marker' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'check_login_page'
check 'live smoke can post super login without printing secrets' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'check_super_auth_post'
check 'live smoke checks CloudPanel dashboard path' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'ECOMAE_CLOUDPANEL_DASHBOARD_PATH'
check 'live smoke reports proxy tunnel failures' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'blocked by outbound proxy CONNECT tunnel'
check 'live smoke requires opt-in' contains "$ROOT/tests/live_smoke/run_ecomae_surface_smoke.sh" 'RUN_LIVE_ECOMAE_SMOKE=1'
check 'detailed foundation test runner exists' test -x "$ROOT/tests/aspnet_migration/run_detailed_foundation_tests.sh"
check 'detailed foundation test runner includes PHP lint' contains "$ROOT/tests/aspnet_migration/run_detailed_foundation_tests.sh" 'php -l'
check 'detailed foundation test runner handles missing dotnet' contains "$ROOT/tests/aspnet_migration/run_detailed_foundation_tests.sh" 'dotnet SDK is not installed'
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
check 'legacy API client parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/ILegacyApiClientParityReporter.cs"
check 'legacy API client parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/auth/api-client/parity'
check 'program maps legacy API client parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.LegacyApiClientParity'
check 'program registers legacy API client parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiClientParityReporter, LegacyApiClientParityReporter'
check 'legacy API client parity tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacyApiClientParityReporterTests.cs"
check 'legacy API client policy checks quota' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientPolicy.cs" 'QuotaAvailable'
check 'legacy API usage log SQL exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiUsageLogSql.cs"
check 'legacy API usage log SQL maps epc_umapi_usage_log' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiUsageLogSql.cs" 'epc_umapi_usage_log'
check 'legacy API usage logger exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/MigrationLegacyApiUsageLogger.cs"
check 'program registers legacy API usage logger' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiUsageLogger, MigrationLegacyApiUsageLogger'
check 'PR range rebase script exists' test -x "$ROOT/scripts/rebase_conflicted_pr_range.sh"
check 'PR range rebase script covers PR 500 default' contains "$ROOT/scripts/rebase_conflicted_pr_range.sh" 'START_PR="${1:-500}"'
check 'PR range rebase script covers PR 508 default' contains "$ROOT/scripts/rebase_conflicted_pr_range.sh" 'END_PR="${2:-508}"'
check 'PR rebase runbook exists' test -f "$ROOT/docs/migration/PR_REBASE_CONFLICT_RUNBOOK.md"
check 'storefront parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IStorefrontParityReporter.cs"
check 'storefront parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/parity'
check 'storefront module maps parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs" 'EcomAeRoutes.StorefrontParity'
check 'program registers storefront parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IStorefrontParityReporter, StorefrontParityReporter'
check 'storefront parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/StorefrontParityReporterTests.cs"
check 'tenant workspace parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/ITenantWorkspaceParityReporter.cs"
check 'tenant workspace parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/tenant/workspace/parity'
check 'program maps tenant workspace parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.TenantWorkspaceParity'
check 'program registers tenant workspace parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ITenantWorkspaceParityReporter, TenantWorkspaceParityReporter'
check 'tenant workspace parity reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/TenantWorkspaceParityReporterTests.cs"
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
check 'route cutover policy interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/IMigrationRouteCutoverPolicy.cs"
check 'route cutover policy keeps PHP fallback' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationRouteCutoverPolicy.cs" 'aspnet-shadow-with-php-fallback'
check 'program registers route cutover policy' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IMigrationRouteCutoverPolicy, MigrationRouteCutoverPolicy'
check 'route cutover policy tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationRouteCutoverPolicyTests.cs"
check 'route cutover route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/route-cutover'
check 'program maps route cutover endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.MigrationRouteCutover'
check 'route cutover endpoint uses tenant context' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'policy.Decide(tenant)' 
check 'route cutover options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Configuration/MigrationRouteCutoverOptions.cs"
check 'appsettings configures route cutover' contains "$ROOT/aspnet/src/EcomAE.Platform/appsettings.json" 'MigrationRouteCutover'
check 'program binds route cutover options' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'MigrationRouteCutoverOptions.SectionName'
check 'route cutover policy consumes options' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationRouteCutoverPolicy.cs" 'IOptions<MigrationRouteCutoverOptions>'
check 'route cutover tests cover disabled API shadow traffic' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationRouteCutoverPolicyTests.cs" 'ApiCanBeDisabledByConfiguration' 
check 'route cutover middleware exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Middleware/RouteCutoverDecisionMiddleware.cs"
check 'route cutover middleware emits target runtime header' contains "$ROOT/aspnet/src/EcomAE.Platform/Middleware/RouteCutoverDecisionMiddleware.cs" 'X-EcomAE-Target-Runtime'
check 'route cutover middleware emits PHP fallback header' contains "$ROOT/aspnet/src/EcomAE.Platform/Middleware/RouteCutoverDecisionMiddleware.cs" 'X-EcomAE-PHP-Fallback'
check 'program wires route cutover middleware' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'RouteCutoverDecisionMiddleware'
check 'route cutover middleware tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/RouteCutoverDecisionMiddlewareTests.cs"
check 'ASP.NET Core modern stack confirmation exists' test -f "$ROOT/docs/migration/ASP_NET_CORE_MODERN_STACK.md"
check 'ASP.NET Core modern stack excludes legacy ASP.NET' contains "$ROOT/docs/migration/ASP_NET_CORE_MODERN_STACK.md" 'not legacy ASP.NET'
check 'ASP.NET Core modern stack excludes System.Web' contains "$ROOT/docs/migration/ASP_NET_CORE_MODERN_STACK.md" 'System.Web'
check 'ASP.NET Core advanced architecture roadmap exists' test -f "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md"
check 'ASP.NET Core roadmap covers CQRS' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'CQRS'
check 'ASP.NET Core roadmap covers zero allocation' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Zero-allocation'
check 'ASP.NET Core roadmap covers Kubernetes' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Kubernetes'
check 'ASP.NET Core roadmap covers sharding' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Database sharding'
check 'ASP.NET Core roadmap covers zero trust' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Zero-trust'
check 'ASP.NET Core production runbook exists' test -f "$ROOT/deploy/aspnet/PRODUCTION_DEPLOYMENT_RUNBOOK.md"
check 'ASP.NET Core platform systemd unit exists' test -f "$ROOT/deploy/aspnet/ecomae-platform.service"
check 'ASP.NET Core worker systemd unit exists' test -f "$ROOT/deploy/aspnet/ecomae-workers.service"
check 'ASP.NET Core production env template keeps PHP fallback' contains "$ROOT/deploy/aspnet/platform.env.example" 'MigrationRouteCutover__RequirePhpFallback=true'
check 'ASP.NET Core diagnostics-only nginx config allowlists migration routes' contains "$ROOT/deploy/aspnet/nginx-diagnostics-only.conf" 'allow YOUR_OFFICE_IP'
check 'ASP.NET Core exact API shadow example exists' contains "$ROOT/deploy/aspnet/nginx-api-shadow-example.conf" '/api/v1/catalog/status'
check 'ASP.NET Core deploy script exists' test -x "$ROOT/scripts/deploy_aspnet_foundation.sh"
check 'ASP.NET Core deploy script runs detailed foundation tests' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'run_detailed_foundation_tests.sh'
check 'ASP.NET Core rollback script exists' test -x "$ROOT/scripts/rollback_aspnet_foundation.sh"
check 'ASP.NET Core production preflight script exists' test -x "$ROOT/scripts/preflight_aspnet_production.sh"
check 'ASP.NET Core production preflight checks PHP fallback' contains "$ROOT/scripts/preflight_aspnet_production.sh" 'MigrationRouteCutover__RequirePhpFallback=true'
check 'CloudPanel include template avoids broad cutover' contains "$ROOT/deploy/aspnet/cloudpanel-site-include.template.conf" 'intentionally avoids /cp, /erp, /bos, /api'
check 'ASP.NET Core go-live checklist exists' test -f "$ROOT/deploy/aspnet/GO_LIVE_CHECKLIST.md"
check 'ASP.NET Core go-live checklist requires exact-match proxy' contains "$ROOT/deploy/aspnet/GO_LIVE_CHECKLIST.md" 'exact-match only'
check 'ASP.NET Core proxy guardrail script exists' test -x "$ROOT/scripts/verify_aspnet_proxy_guardrails.sh"
check 'ASP.NET Core proxy guardrail script blocks broad API' contains "$ROOT/scripts/verify_aspnet_proxy_guardrails.sh" 'contains a broad API location'
check 'ASP.NET Core remote deploy script exists' test -x "$ROOT/scripts/remote_aspnet_foundation_deploy.sh"
check 'ASP.NET Core remote deploy is dry-run by default' contains "$ROOT/scripts/remote_aspnet_foundation_deploy.sh" 'ECOMAE_RUN_REMOTE_DEPLOY:-0'
check 'ASP.NET Core remote deploy env example exists' test -f "$ROOT/deploy/aspnet/remote-deploy.env.example"
check 'ASP.NET Core remote deploy env keeps remote execution disabled' contains "$ROOT/deploy/aspnet/remote-deploy.env.example" 'ECOMAE_RUN_REMOTE_DEPLOY=0'
check 'CloudPanel quick start exists' test -f "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md"
check 'CloudPanel quick start explains repo root' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'run from the repository root'
check 'CloudPanel quick start has paste-safe finder' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'Paste-safe repo finder'
check 'CloudPanel quick start warns about literal placeholder path' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'Do not paste the example path'
check 'CloudPanel missing repo recovery exists' test -f "$ROOT/deploy/aspnet/CLOUDPANEL_MISSING_REPO_RECOVERY.md"
check 'CloudPanel missing repo recovery requires real git URL' contains "$ROOT/deploy/aspnet/CLOUDPANEL_MISSING_REPO_RECOVERY.md" 'Set ECOMAE_GIT_URL to the real repository URL first'
check 'production runbook troubleshoots missing script' contains "$ROOT/deploy/aspnet/PRODUCTION_DEPLOYMENT_RUNBOOK.md" 'No such file or directory'
check 'Codex PR cleanup script exists' test -x "$ROOT/scripts/cleanup_codex_prs.sh"
check 'Codex PR cleanup script is dry-run by default' contains "$ROOT/scripts/cleanup_codex_prs.sh" 'RUN_CLOSE:-0'
check 'open PR consolidation runbook exists' test -f "$ROOT/docs/migration/OPEN_PR_CONSOLIDATION_RUNBOOK.md"
check 'Cursor handoff status exists' test -f "$ROOT/docs/migration/CURSOR_HANDOFF_STATUS.md"
check 'Cursor handoff status records foundation complete' contains "$ROOT/docs/migration/CURSOR_HANDOFF_STATUS.md" 'Repository foundation | 100%'
check 'Cursor handoff status blocks broad cutover' contains "$ROOT/docs/migration/CURSOR_HANDOFF_STATUS.md" 'Do not proxy broad'
check 'consolidated PR push script exists' test -x "$ROOT/scripts/push_consolidated_pr_update.sh"
check 'migration plan documents zero PHP final state' contains "$ROOT/docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md" 'zero PHP files'

echo "----------------------------"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
