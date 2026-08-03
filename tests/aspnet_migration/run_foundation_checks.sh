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
  grep -Fq -- "$needle" "$file"
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
check 'DB-backed legacy session validator exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbBackedLegacySessionValidator.cs"
check 'DB legacy session store is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbLegacySessionStore.cs" 'Performs zero writes'
check 'legacy session SQL checks admin type' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionSql.cs" '`type` = 1'
check 'legacy session tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacySessionValidatorTests.cs"
check 'legacy session parity reporter interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/ILegacySessionParityReporter.cs"
check 'legacy session parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/auth/session/parity'
check 'program maps legacy session parity endpoint' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.LegacySessionParity'
check 'program registers legacy session parity reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacySessionParityReporter, LegacySessionParityReporter'
check 'legacy session parity tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacySessionParityReporterTests.cs"
check 'api key legacy sessions authenticate' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionContext.cs" 'Kind == LegacySessionKind.ApiKey'
check 'legacy session probe route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/auth/session/probe'
check 'program registers legacy session validator' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacySessionValidator, DbBackedLegacySessionValidator'
check 'program registers legacy session store' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacySessionStore'
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
check 'CSV price offer repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/CsvPriceOfferRepository.cs"
check 'CSV price offer repository filters positive price' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/CsvPriceOfferRepository.cs" 'price <= 0'
check 'CSV price offer repository preserves legacy limit' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/CsvPriceOfferRepository.cs" 'LegacyPriceLookupSql.DefaultLimit'
check 'DB price offer repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbPriceOfferRepository.cs"
check 'DB price offer repository uses legacy SQL' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbPriceOfferRepository.cs" 'LegacyPriceLookupSql.LookupOffers'
check 'DB price offer repository is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbPriceOfferRepository.cs" 'Performs zero writes'
check 'tenant DB connection factory exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/ITenantDbConnectionFactory.cs"
check 'MySQL tenant DB connection factory exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/MySqlTenantDbConnectionFactory.cs"
check 'platform references MySqlConnector' contains "$ROOT/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" 'MySqlConnector'
check 'program can register CSV price repository' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'PriceLookup:FixtureCsvPath'
check 'program can register DB price repository' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'DbPriceOfferRepository'
check 'program registers tenant DB connection factory' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ITenantDbConnectionFactory, MySqlTenantDbConnectionFactory'
check 'program keeps migration price repository fallback' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'MigrationPriceOfferRepository'
check 'price lookup fixture CSV exists' test -f "$ROOT/tests/fixtures/price_lookup/php-baseline.csv"
check 'price lookup parity script exists' test -x "$ROOT/scripts/compare_price_lookup_parity.py"
check 'price lookup PHP baseline sample exists' test -f "$ROOT/docs/migration/evidence/price-lookup/php-baseline-sample.json"
check 'price lookup ASP.NET output sample exists' test -f "$ROOT/docs/migration/evidence/price-lookup/aspnet-output-sample.json"
check 'price lookup evidence runbook names exact route' contains "$ROOT/docs/migration/evidence/price-lookup/README.md" '/api/v1/price/lookup'
check 'price lookup evidence documents DB repository' contains "$ROOT/docs/migration/evidence/price-lookup/README.md" 'DbPriceOfferRepository'
check 'price lookup smoke script exists' test -x "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh"
check 'price lookup smoke is opt-in' contains "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh" 'RUN_PRICE_LOOKUP_SMOKE=1'
check 'price lookup rollback keeps exact route only' contains "$ROOT/docs/migration/evidence/price-lookup/README.md" 'exact-route only'
check 'program registers repository price lookup service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IPriceLookupService, RepositoryPriceLookupService'
check 'DB price offer repository tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/DbPriceOfferRepositoryTests.cs"
check 'CSV price offer repository tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/CsvPriceOfferRepositoryTests.cs"
check 'legacy API key parser exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs"
check 'legacy API key parser supports catalog prefix' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs" 'epc_catalog_'
check 'legacy API key parser supports pricepro prefix' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientKeyParser.cs" 'epc_pricepro_'
check 'legacy session validator uses API key parser' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbBackedLegacySessionValidator.cs" 'LegacyApiClientKeyParser.Parse'
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
check 'DB legacy API usage logger exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbLegacyApiUsageLogger.cs"
check 'program registers legacy API usage logger' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiUsageLogger, DbLegacyApiUsageLogger'
check 'legacy API client authenticator exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientAuthenticator.cs"
check 'legacy API client store interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/ILegacyApiClientStore.cs"
check 'DB legacy API client store exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbLegacyApiClientStore.cs"
check 'program registers legacy API client store' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiClientStore, DbLegacyApiClientStore'
check 'program registers legacy API client authenticator' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ILegacyApiClientAuthenticator, LegacyApiClientAuthenticator'
check 'API module gates price lookup with price_pro auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'price_pro'
check 'API module price lookup auth is configurable' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'RequireApiClientAuth'
check 'price lookup options require API client auth by default' contains "$ROOT/aspnet/src/EcomAE.Platform/Configuration/PriceLookupOptions.cs" 'RequireApiClientAuth'
check 'legacy API client authenticator tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/LegacyApiClientAuthenticatorTests.cs"
check 'price lookup exact-route nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-price-lookup-shadow-example.conf"
check 'price lookup exact-route nginx shadow is exact match' contains "$ROOT/deploy/aspnet/nginx-price-lookup-shadow-example.conf" 'location = /api/v1/price/lookup'
check 'price lookup smoke requires API key' contains "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh" 'ECOMAE_PRICE_LOOKUP_API_KEY'
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
check 'worker program registers job runner' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'AddSingleton<IMigrationWorkerJobRunner>'
check 'worker job runner tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationWorkerJobRunnerTests.cs"
check 'worker dry-run executor interface exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/IMigrationWorkerJobDryRunExecutor.cs"
check 'price import dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/PriceImportDryRunExecutor.cs"
check 'price import dry-run executor blocks writes' contains "$ROOT/aspnet/src/EcomAE.Workers/PriceImportDryRunExecutor.cs" 'WritesBlocked: true'
check 'price import dry-run executor validates sku' contains "$ROOT/aspnet/src/EcomAE.Workers/PriceImportDryRunExecutor.cs" '"sku"'
check 'worker program registers price import dry-run executor' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobDryRunExecutor, PriceImportDryRunExecutor'
check 'worker program registers sitemap dry-run executor' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobDryRunExecutor, SitemapDryRunExecutor'
check 'worker program registers backup dry-run executor' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobDryRunExecutor, BackupDryRunExecutor'
check 'worker program registers notifications dry-run executor' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobDryRunExecutor, NotificationsDryRunExecutor'
check 'worker program registers erp-reports dry-run executor' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerJobDryRunExecutor, ErpReportsDryRunExecutor'
check 'sitemap dry-run executor blocks writes' contains "$ROOT/aspnet/src/EcomAE.Workers/SitemapDryRunExecutor.cs" 'WritesBlocked: true'
check 'backup dry-run executor blocks writes' contains "$ROOT/aspnet/src/EcomAE.Workers/BackupDryRunExecutor.cs" 'WritesBlocked: true'
check 'price import dry-run executor tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/PriceImportDryRunExecutorTests.cs"
check 'worker dry-run executor tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/WorkerDryRunExecutorTests.cs"
check 'DB catalog status repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogStatusRepository.cs"
check 'DB catalog status repository is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogStatusRepository.cs" 'Performs zero writes'
check 'program registers catalog status service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogStatusService, CatalogStatusService'
check 'API module gates catalog status with catalog auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" '"catalog", "status"'
check 'catalog status evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-status/README.md"
check 'CloudPanel production deploy script exists' test -x "$ROOT/scripts/cloudpanel_production_deploy_foundation.sh"
check 'CloudPanel production deploy refuses price-lookup auto-shadow' contains "$ROOT/scripts/cloudpanel_production_deploy_foundation.sh" 'refusing automatic price-lookup shadow enable'
check 'CloudPanel production deploy keeps PHP fallback' contains "$ROOT/scripts/cloudpanel_production_deploy_foundation.sh" 'PHP remains authoritative'
check 'zero PHP progress status remains below one hundred' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" 'True zero-PHP completion: 95.0%'
check 'legacy session SQL checks customer sessions without type filter' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionSql.cs" 'CountCustomerSession'
check 'legacy session DB evidence exists' test -f "$ROOT/docs/migration/evidence/legacy-session-db/README.md"
check 'catalog article route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/article'
check 'API module maps article offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupArticleAsync'
check 'catalog article nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-article-shadow-example.conf"
check 'currency live-rates dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/CurrencyLiveRatesDryRunExecutor.cs"
check 'worker program registers currency live-rates dry-run' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'CurrencyLiveRatesDryRunExecutor'
check 'UMAPI usage recent today SQL is select only' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacyUmapiUsageSql.cs" 'RecentToday'
check 'catalog articles route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/articles'
check 'catalog engine route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/engine'
check 'API module maps articles offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupArticlesAsync'
check 'API module maps engine offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupEngineAsync'
check 'catalog articles nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-articles-shadow-example.conf"
check 'catalog engine nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-engine-shadow-example.conf"
check 'CP shell requires admin session' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs" 'Admin session required'
check 'ERP shell requires admin session' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ErpModule.cs" 'Admin session required'
check 'BOS shell requires admin session' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/BosModule.cs" 'Admin session required'
check 'platform jobs migration route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/platform-jobs'
check 'platform jobs reporter is registered' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IPlatformJobsSummaryReporter, PlatformJobsSummaryReporter'
check 'platform jobs evidence exists' test -f "$ROOT/docs/migration/evidence/platform-jobs/README.md"
check 'demo-expire dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/DemoExpireDryRunExecutor.cs"
check 'platform-jobs dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/PlatformJobsDryRunExecutor.cs"
check 'seo-sitemap-ping dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/SeoSitemapPingDryRunExecutor.cs"
check 'admin backend group SQL exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionSql.cs" 'SelectBackendGroupIds'
check 'admin identity loaded by session store' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/ILegacySessionStore.cs" 'GetAdminIdentityAsync'
check 'CP dashboard summary route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/dashboard-summary'
check 'ERP dashboard summary route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/dashboard-summary'
check 'BOS fleet summary route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/fleet-summary'
check 'surface dashboard reporter registered' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ISurfaceDashboardSummaryReporter, SurfaceDashboardSummaryReporter'
check 'surface dashboard evidence exists' test -f "$ROOT/docs/migration/evidence/surface-dashboards/README.md"
check 'catalog suppliers route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/suppliers'
check 'catalog suppliers nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-suppliers-shadow-example.conf"
check 'seo-sitemap-warm dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/SeoSitemapWarmDryRunExecutor.cs"
check 'uae-tax-legislation dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/UaeTaxLegislationDryRunExecutor.cs"
check 'apai-background-jobs dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApaiBackgroundJobsDryRunExecutor.cs"
check 'storefront account route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/account'
check 'storefront account summary route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/account-summary'
check 'storefront account requires customer session' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs" 'Customer session required'
check 'fulfillment-queue dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/FulfillmentQueueDryRunExecutor.cs"
check 'apai-sync-categories dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApaiSyncCategoriesDryRunExecutor.cs"
check 'integrations-cleanup dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/IntegrationsCleanupDryRunExecutor.cs"
check 'CP tenants route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/tenants'
check 'BOS tenants route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/tenants'
check 'BOS fleet-health route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/fleet-health'
check 'ERP accounts-summary route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/accounts-summary'
check 'storefront orders route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/orders'
check 'ERP cash SQL uses epc_erp_cash_bank_accounts' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'epc_erp_cash_bank_accounts'
check 'session capabilities exposed on probe' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'capabilities = session.Capabilities'
check 'product-exist-limit dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ProductExistLimitDryRunExecutor.cs"
check 'cache-warmup dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/CacheWarmupDryRunExecutor.cs"
check 'import-orchestrator dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ImportOrchestratorDryRunExecutor.cs"
check 'CP users route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/users'
check 'CP groups route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/groups'
check 'ERP suppliers route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/suppliers'
check 'ERP purchases route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/purchases'
check 'storefront garage route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/garage'
check 'module ACL SQL exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionSql.cs" 'SelectModuleAccessForGroup'
check 'session probe exposes module_acl' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'module_acl = session.Modules'
check 'apai-hourly-crawl dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApaiHourlyCrawlDryRunExecutor.cs"
check 'webhooks-process dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/WebhooksProcessDryRunExecutor.cs"
check 'offline-resilience-warm dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/OfflineResilienceWarmDryRunExecutor.cs"
check 'ERP cash-accounts route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/cash-accounts'
check 'storefront profile route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/storefront/profile'
check 'nested ACL group parent SQL exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionSql.cs" 'SelectGroupParent'
check 'ActivitySource scaffolding exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Observability/EcomAeActivitySources.cs"
check 'Enterprise BOS architecture compliance doc exists' test -f "$ROOT/docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md"
check 'ASP.NET target stack scaffolding notes exist' test -f "$ROOT/docs/migration/ASPNET_TARGET_STACK_SCAFFOLDING_NOTES.md"
check 'Enterprise BOS instructions exist' test -f "$ROOT/docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md"
check 'Python migration doc defers to ASP.NET Core ownership' contains "$ROOT/docs/PYTHON_MIGRATION.md" 'ASP.NET Core 10'
check 'apai-weekly-platform-sync dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApaiWeeklyPlatformSyncDryRunExecutor.cs"
check 'apai-daily-source-expand dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApaiDailySourceExpandDryRunExecutor.cs"
check 'api-client-ping dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ApiClientPingDryRunExecutor.cs"
check 'Enterprise BOS instructions are canonical project law' contains "$ROOT/docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md" 'Canonical project law'
check 'Enterprise BOS forbids Java Node Go PHP backends' contains "$ROOT/docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md" 'Do not introduce Java Spring Boot, Node.js backend, Go backend, PHP'
check 'Enterprise BOS requires PostgreSQL 17' contains "$ROOT/docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md" 'PostgreSQL 17'
check 'Enterprise BOS requires Python AI-only' contains "$ROOT/docs/migration/PROJECT_ARCHITECTURE_INSTRUCTIONS.md" 'Use Python only for AI-related workloads'
check 'Enterprise BOS compliance marks PG17 not migrated' contains "$ROOT/docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md" '❌ not migrated'
check 'Enterprise BOS compliance marks Redis not wired' contains "$ROOT/docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md" 'Redis 8'
check 'EF Core 10 package referenced by platform' contains "$ROOT/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" 'Microsoft.EntityFrameworkCore'
check 'EF Core scaffold DbContext exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/EcomAeScaffoldDbContext.cs"
check 'EF Core scaffold is not registered in Program' bash -c '! grep -q "AddDbContext" "$ROOT/aspnet/src/EcomAE.Platform/Program.cs"'
check 'no Go module backend at repo root' bash -c '! test -f "$ROOT/go.mod"'
check 'no Maven pom Spring backend at repo root' bash -c '! test -f "$ROOT/pom.xml"'
check 'Python migration doc is superseded historical' contains "$ROOT/docs/PYTHON_MIGRATION.md" 'SUPERSEDED / HISTORICAL ONLY'
check 'hybrid roadmap marks pyapi business surface legacy' contains "$ROOT/docs/migration/ASP_NET_CORE_PYTHON_HYBRID_ROADMAP.md" 'temporary legacy'
check 'advanced architecture prefers Kafka primary' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Apache Kafka 4'
check 'blockchain doc keeps business SoR in ASP.NET' contains "$ROOT/docs/BLOCKCHAIN_BOS_ENTERPRISE.md" 'integration/proof layer only'
check 'tenant scale doc marks PHP MySQL as interim' contains "$ROOT/docs/TENANT_SCALE_1000.md" 'interim'
check 'ERP cash-entries route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/cash-entries'
check 'ERP invoices route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/invoices'
check 'ERP gl-journals route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/gl-journals'
check 'ERP coa-accounts route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/coa-accounts'
check 'ERP warehouses route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/warehouses'
check 'ERP sales-orders route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/sales-orders'
check 'CP modules route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/modules'
check 'CP config-items route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/config-items'
check 'CP menus route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/menus'
check 'CP pages route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/pages'
check 'CP admin-sessions route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/admin-sessions'
check 'CP storages route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/storages'
check 'BOS fleet-readiness route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/fleet-readiness'
check 'BOS audit-log route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/bos/audit-log'
check 'ERP purchase-orders route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/purchase-orders'
check 'ERP inventory-stock route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/erp/inventory-stock'
check 'CP currencies route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/currencies'
check 'CP api-clients route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/cp/api-clients'
check 'PHP decommission readiness route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/php-decommission-readiness'
check 'PHP decommission readiness doc exists' test -f "$ROOT/docs/migration/PHP_DECOMMISSION_READINESS.md"
check 'PHP decommission readiness blocks removal' contains "$ROOT/docs/migration/PHP_DECOMMISSION_READINESS.md" 'blocked-not-ready-for-php-removal'

check 'final gate checklist script exists' test -x "$ROOT/scripts/run_zero_php_final_gate_checklist.sh"
check 'CloudPanel final-gate capture script exists' test -x "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh"
check 'deploy packs decommission evidence into release' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'Packed decommission evidence'
check 'deploy copies public probe evidence directory' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'docs/migration/evidence/decommission'
check 'deploy packs price lookup gate shadow' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'nginx-price-lookup-shadow-example.conf'
check 'deploy packs catalog/api gate shadow' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'nginx-api-shadow-example.conf'
check 'deploy packs surface digests gate shadow' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'nginx-surface-digests-shadow-example.conf'
check 'deploy packs storefront digests gate shadow' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'nginx-storefront-digests-shadow-example.conf'
check 'deploy packs exact-route extract helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_extract_exact_route_shadow.sh'
check 'deploy packs ensure epc_api_clients helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_ensure_epc_api_clients_table.sh'
check 'deploy packs smoke secrets prepare helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_prepare_smoke_secrets.sh'
check 'deploy packs smoke cookie repair helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_repair_smoke_cookie_env.sh'
check 'deploy packs smoke commit helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_commit_final_gate_smoke.sh'
check 'deploy packs smoke credential issuer' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_issue_smoke_credentials.sh'
check 'smoke credential issuer exists' test -x "$ROOT/scripts/cloudpanel_issue_smoke_credentials.sh"
check 'smoke issuer uses PHP DP_Config bootstrap' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'DP_Config'
check 'smoke bootstrap prefers TenantRegistry DSN' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'ConnectionStrings__TenantRegistry user='
check 'smoke bootstrap refuses PHP DB mismatch' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'ECOMAE_SMOKE_ALLOW_PHP_DB_MISMATCH'
check 'smoke bootstrap bash-quotes env values' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'function smoke_bash_quote'
check 'smoke issuer writes bash-quoted cookie' contains "$ROOT/scripts/php/issue_final_gate_smoke_credentials.php" 'smoke_bash_quote($cookie)'
check 'smoke issuer persists ECOMAE_ADMIN_U_ID' contains "$ROOT/scripts/php/issue_final_gate_smoke_credentials.php" 'ECOMAE_ADMIN_U_ID'
check 'smoke cookie repair helper exists' test -f "$ROOT/scripts/cloudpanel_repair_smoke_cookie_env.sh"
check 'smoke env validator sources cookie repair helper' contains "$ROOT/scripts/cloudpanel_validate_final_gate_env.sh" 'cloudpanel_repair_smoke_cookie_env.sh'
check 'smoke capture sources cookie repair helper' contains "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" 'cloudpanel_repair_smoke_cookie_env.sh'
check 'smoke env validator repairs truncated cookie' contains "$ROOT/scripts/cloudpanel_validate_final_gate_env.sh" 'repaired from ECOMAE_ADMIN_U_ID'
check 'deploy packs smoke DB bootstrap PHP' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" '_smoke_db_bootstrap.php'
check 'deploy packs ensure table PHP' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'ensure_epc_api_clients_table.php'
check 'ensure epc_api_clients helper exists' test -x "$ROOT/scripts/cloudpanel_ensure_epc_api_clients_table.sh"
check 'epc_api_clients DDL SQL exists' test -f "$ROOT/scripts/sql/epc_api_clients.sql"
check 'print epc_api_clients DDL helper exists' test -x "$ROOT/scripts/cloudpanel_print_epc_api_clients_ddl.sh"
check 'apply epc_api_clients DDL helper exists' test -x "$ROOT/scripts/cloudpanel_apply_epc_api_clients_ddl.sh"
check 'smoke DB diagnose helper exists' test -x "$ROOT/scripts/cloudpanel_diagnose_smoke_db.sh"
check 'align TenantRegistry to PHP db helper exists' test -x "$ROOT/scripts/cloudpanel_align_tenant_registry_to_php_db.sh"
check 'use PHP DP_Config as TenantRegistry helper exists' test -x "$ROOT/scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh"
check 'apply DDL uses clpctl master credentials' contains "$ROOT/scripts/cloudpanel_apply_epc_api_clients_ddl.sh" 'db:show:master-credentials'
check 'apply DDL grants only existing mysql.user hosts' contains "$ROOT/scripts/cloudpanel_apply_epc_api_clients_ddl.sh" 'grant_existing_hosts'
check 'smoke export bundle helper exists' test -x "$ROOT/scripts/cloudpanel_export_final_gate_smoke_bundle.sh"
check 'smoke token push helper exists' test -x "$ROOT/scripts/cloudpanel_push_final_gate_smoke.sh"
check 'smoke token push requires GH_TOKEN' contains "$ROOT/scripts/cloudpanel_push_final_gate_smoke.sh" 'GH_TOKEN'
check 'smoke token push disables terminal prompt' contains "$ROOT/scripts/cloudpanel_push_final_gate_smoke.sh" 'GIT_TERMINAL_PROMPT=0'
check 'smoke token push rejects placeholder tokens' contains "$ROOT/scripts/cloudpanel_push_final_gate_smoke.sh" 'documentation placeholder'
check 'smoke commit preserves capture before rebase' contains "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" 'preserve_evidence'
check 'smoke commit restores capture after rebase' contains "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" 'restore_evidence'
check 'smoke commit commits refreshed probes' contains "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" 'Committing refreshed capture/probe artifacts'
check 'smoke commit recovers failed push auth' contains "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" 'cloudpanel_export_final_gate_smoke_bundle.sh'
check 'smoke commit prefers token push helper' contains "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" 'cloudpanel_push_final_gate_smoke.sh'
check 'redeploy prints live readiness snapshot' contains "$ROOT/scripts/cloudpanel_redeploy_final_gate_branch.sh" 'Live readiness snapshot'
check 'redeploy reminds not to invent approval at 8/9' contains "$ROOT/scripts/cloudpanel_redeploy_final_gate_branch.sh" 'do NOT invent RELEASE_OWNER_APPROVAL.md'
check 'pre-PHP-removal parity verdict helper exists' test -x "$ROOT/scripts/verify_pre_php_removal_parity.sh"
check 'pre-PHP-removal verdict never removes PHP' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'NEVER removes PHP'
check 'area tests validate attached staging smoke' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'attached-staging-smoke'
check 'area tests assert public digests not cut over' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'not-cutover'
check 'deploy packs pre-PHP-removal parity verdict helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'verify_pre_php_removal_parity.sh'
check 'deploy packs smoke token push helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_push_final_gate_smoke.sh'
check 'deploy packs print epc_api_clients DDL helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_print_epc_api_clients_ddl.sh'
check 'deploy packs apply epc_api_clients DDL helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_apply_epc_api_clients_ddl.sh'
check 'deploy packs smoke export bundle helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_export_final_gate_smoke_bundle.sh'
check 'deploy packs smoke DB diagnose helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_diagnose_smoke_db.sh'
check 'deploy packs align TenantRegistry helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_align_tenant_registry_to_php_db.sh'
check 'deploy packs use PHP DP_Config TenantRegistry helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_use_php_dp_config_as_tenant_registry.sh'
check 'deploy packs use_php_dp_config PHP' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'use_php_dp_config_as_tenant_registry.php'
check 'deploy packs epc_api_clients SQL' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'epc_api_clients.sql'
check 'deploy packs diagnose_smoke_db PHP' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'diagnose_smoke_db.php'
check 'deploy packs align_tenant_registry PHP' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'align_tenant_registry_to_php_db.php'
check 'smoke bootstrap prints CREATE recovery DDL' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'smoke_print_epc_api_clients_recovery'
check 'smoke bootstrap can sync admin session' contains "$ROOT/scripts/php/_smoke_db_bootstrap.php" 'smoke_sync_admin_session_to_tenant'
check 'smoke issuer supports SYNC_ADMIN_SESSION' contains "$ROOT/scripts/php/issue_final_gate_smoke_credentials.php" 'ECOMAE_CONFIRM_SYNC_ADMIN_SESSION'
check 'catalog list parity compare script exists' test -f "$ROOT/scripts/compare_catalog_list_parity.py"
check 'catalog offline-cache parity compare script exists' test -f "$ROOT/scripts/compare_catalog_offline_cache_parity.py"
check 'catalog vin parity compare script exists' test -f "$ROOT/scripts/compare_catalog_vin_parity.py"
check 'catalog brand-parts parity compare script exists' test -f "$ROOT/scripts/compare_catalog_brand_parts_parity.py"
check 'deploy packs catalog list compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_list_parity.py'
check 'deploy packs catalog offline-cache compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_offline_cache_parity.py'
check 'deploy packs catalog status compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_status_parity.py'
check 'deploy packs catalog vin compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_vin_parity.py'
check 'deploy packs catalog brand-parts compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_brand_parts_parity.py'
check 'deploy packs price lookup compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_price_lookup_parity.py'
check 'deploy packs digest dual-sample compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_digest_dual_samples.py'
check 'deploy packs surface payload compare script' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_surface_payload_parity.py'
check 'deploy packs surface parity harness' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'run_surface_parity_harness.sh'
check 'live surface stack probe exists' test -x "$ROOT/scripts/probe_live_surface_stack.sh"
check 'live surface stack probe covers data-parity' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/migration/data-parity'
check 'live surface stack probe covers cp parity' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/cp/parity'
check 'api client parity reporter mentions ensure' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyApiClientParityReporter.cs" 'ensure_epc_api_clients_table.sh'
check 'session parity reporter mentions issue smoke' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionParityReporter.cs" 'issue_smoke_credentials.sh'
check 'storefront digest exact-route smoke exists' test -x "$ROOT/tests/live_smoke/run_storefront_digest_exact_route_smoke.sh"
check 'price lookup compare supports contract-only' contains "$ROOT/scripts/compare_price_lookup_parity.py" '--contract-only'
check 'harness lists cp-config-items contract' contains "$ROOT/scripts/run_surface_parity_harness.sh" 'cp-config-items.json'
check 'harness lists bos-tenants contract' contains "$ROOT/scripts/run_surface_parity_harness.sh" 'bos-tenants.json'
check 'harness supports customer cookie capture' contains "$ROOT/scripts/run_surface_parity_harness.sh" 'ECOMAE_CUSTOMER_COOKIE_HEADER'
check 'capture wires optional storefront smoke' contains "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" 'run_storefront_digest_exact_route_smoke.sh'
check 'exact-route extract helper exists' test -x "$ROOT/scripts/cloudpanel_extract_exact_route_shadow.sh"
check 'exact-route extract helper refuses broad /api' contains "$ROOT/scripts/cloudpanel_extract_exact_route_shadow.sh" 'refusing broad surface cutover'
check 'surface digests shadow covers config-items' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /cp/config-items'
check 'surface digests shadow covers admin-sessions' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /cp/admin-sessions'
check 'surface digests shadow covers storages' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /cp/storages'
check 'surface digests shadow covers accounts-summary' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /erp/accounts-summary'
check 'surface digests shadow covers cash-accounts' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /erp/cash-accounts'
check 'surface digests shadow covers cash-entries' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /erp/cash-entries'
check 'surface digests shadow covers bos tenants' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /bos/tenants'
check 'CloudPanel final-gate capture never removes PHP' contains "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" 'never removes PHP'
check 'public decommission probes attached' test -f "$ROOT/docs/migration/evidence/decommission/public-probes/www-zero-php-completion.json"
check 'public decommission readiness probe attached' test -f "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'surface field parity public probe attached' test -f "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-field-parity.json"
check 'surface field parity probe tracks catalog vin contract' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-field-parity.json" '/api/v1/catalog/vin'
check 'surface field parity probe contractCount is current' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-field-parity.json" '"contractCount": 53'
check 'live surface links probe includes field parity route' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/migration/surface-field-parity'
check 'live surface links probe includes catalog brand-parts' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/api/v1/catalog/brand-parts'
# After #612 smoke attach: public probes should point at redeploy + human approval (not re-issue keys).
check 'zero php probe next actions mention main redeploy' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-zero-php-completion.json" 'cloudpanel_redeploy_final_gate_branch.sh'
check 'zero php probe next actions mention release-owner approval' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-zero-php-completion.json" 'RELEASE_OWNER_APPROVAL'
check 'php decommission probe next actions mention release-owner approval' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json" 'RELEASE_OWNER_APPROVAL'
check 'php decommission probe marks staging-smoke-price present' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); i=next(c for c in d["checklist"] if c["id"]=="staging-smoke-price"); assert i["status"]=="present"' "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'php decommission probe marks staging-smoke-catalog present' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); i=next(c for c in d["checklist"] if c["id"]=="staging-smoke-catalog"); assert i["status"]=="present"' "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'php decommission probe marks staging-smoke-surfaces present' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); i=next(c for c in d["checklist"] if c["id"]=="staging-smoke-surfaces"); assert i["status"]=="present"' "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'php decommission probe marks approval missing' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); i=next(c for c in d["checklist"] if c["id"]=="release-owner-approval"); assert i["status"]=="missing"; assert d.get("readyToRemovePhp") is False' "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'live surface links probe includes cp parity board' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/cp/parity'
check 'surface digest smoke covers erp gl-journals' contains "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh" '/erp/gl-journals'
check 'surface digest smoke covers cp groups' contains "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh" '/cp/groups'
check 'capture incomplete footer mentions ensure table' contains "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" 'cloudpanel_ensure_epc_api_clients_table.sh'
check 'redeploy BLOCKED footer mentions ensure table' contains "$ROOT/scripts/cloudpanel_redeploy_final_gate_branch.sh" 'cloudpanel_ensure_epc_api_clients_table.sh'
check 'API migration status is post-scaffold honest' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'catalog-cache-routes-wired-awaiting-staging'
check 'migration parity milestones mention ensure' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationParityReporter.cs" 'ensure'
check 'live surface links probe includes catalog article-brands' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/api/v1/catalog/article-brands'
check 'live surface links probe includes bos audit-log' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/bos/audit-log'
check 'live surface links probe includes session parity' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '/auth/session/parity'
check 'presentation parity probe mentions ensure path' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-presentation-parity.json" 'ensure'
check 'harness catalog capture includes brand-parts' contains "$ROOT/scripts/run_surface_parity_harness.sh" '/api/v1/catalog/brand-parts'
check 'harness admin capture includes bos audit-log' contains "$ROOT/scripts/run_surface_parity_harness.sh" '/bos/audit-log'
check 'CloudPanel quick start points to main redeploy' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'ecomae/main/scripts/cloudpanel_redeploy_final_gate_branch.sh'
check 'zero php progress JSON next order mentions release-owner approval' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" 'RELEASE_OWNER_APPROVAL.md'
check 'zero php progress JSON next order mentions redeploy packs smoke' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" 'cloudpanel_redeploy_final_gate_branch.sh'
check 'php decommission probe marks exact-route shadows present' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); i=next(c for c in d["checklist"] if c["id"]=="exact-route-shadows-only"); assert i["status"]=="present"' "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json"
check 'surface parity probe public API status is wired' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-parity.json" 'catalog-cache-routes-wired-awaiting-staging'
check 'redeploy final-gate defaults to main' contains "$ROOT/scripts/cloudpanel_redeploy_final_gate_branch.sh" 'ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"'
check 'final gate checklist never removes PHP' contains "$ROOT/scripts/run_zero_php_final_gate_checklist.sh" 'never removes PHP'
check 'catalog status smoke runner exists' test -x "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh"
check 'surface digest smoke runner exists' test -x "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh"
check 'surface digests shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf"
check 'surface digests shadow is exact-route only' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /cp/dashboard-summary'
check 'decommission evidence pack exists' test -f "$ROOT/docs/migration/evidence/decommission/README.md"
check 'release owner approval is example only by default' test -f "$ROOT/docs/migration/evidence/decommission/RELEASE_OWNER_APPROVAL.example.md"
check 'release owner approval marker not committed live' bash -c '! test -f "$ROOT/docs/migration/evidence/decommission/RELEASE_OWNER_APPROVAL.md"'
check 'parity sample template exists' test -f "$ROOT/docs/migration/parity/templates/exact-route-parity-sample.template.json"
check 'EF catalog scaffold repository interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/ICatalogScaffoldRepository.cs"
check 'zero PHP path-to-100 documents remaining batches' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" 'Path to 100%'
check 'batch statuses record dry-run scaffolding' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" 'aspnet-dry-run-scaffolded'

check 'CloudPanel bootstrap-from-github script exists' test -x "$ROOT/scripts/cloudpanel_bootstrap_from_github.sh"
check 'CloudPanel bootstrap resets hard to origin main' contains "$ROOT/scripts/cloudpanel_bootstrap_from_github.sh" 'git reset --hard'
check 'CloudPanel quick start documents stale checkout recovery' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'git reset --hard origin/main'
check 'UMAPI usage migration route exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/umapi-usage'
check 'program registers UMAPI usage reporter' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'IUmapiUsageSummaryReporter, UmapiUsageSummaryReporter'
check 'UMAPI usage reporter is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/UmapiUsageSummaryReporter.cs" 'Performs zero writes'
check 'UMAPI usage reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/UmapiUsageSummaryReporterTests.cs"
check 'UMAPI usage evidence exists' test -f "$ROOT/docs/migration/evidence/umapi-usage/README.md"
check 'zero PHP progress status blocks broad cutover' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" 'Broad `/api`, `/cp`, `/erp`, `/bos`'
check 'catalog manufacturers route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/manufacturers'
check 'DB catalog manufacturers repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogManufacturerRepository.cs"
check 'program registers catalog manufacturers service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogManufacturerService, CatalogManufacturerService'
check 'API module gates manufacturers with catalog auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" '"catalog", "manufacturers"'
check 'catalog manufacturers evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-manufacturers/README.md"
check 'catalog manufacturers nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-manufacturers-shadow-example.conf"
check 'catalog models nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-models-shadow-example.conf"
check 'catalog modifications nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-modifications-shadow-example.conf"
check 'catalog brands nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-brands-shadow-example.conf"
check 'catalog models route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/models'
check 'catalog modifications route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/modifications'
check 'catalog brands route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/brands'
check 'DB catalog vehicle cache repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogVehicleCacheRepository.cs"
check 'program registers catalog vehicle cache service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogVehicleCacheService, CatalogVehicleCacheService'
check 'catalog vehicle cache evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-vehicle-cache/README.md"
check 'catalog vin route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/vin'
check 'catalog engines route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/engines'
check 'catalog analogs route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/analogs'
check 'DB catalog offline cache repository exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogOfflineCacheRepository.cs"
check 'DB catalog offline cache repository is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogOfflineCacheRepository.cs" 'Performs zero writes'
check 'program registers catalog offline cache service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogOfflineCacheService, CatalogOfflineCacheService'
check 'API module gates vin with catalog auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "vin"'
check 'API module gates engines with catalog auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "engines"'
check 'API module gates analogs with catalog auth' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'AuthorizeCatalogAsync(httpContext, authenticator, usageLogger, options, "analogs"'
check 'catalog offline cache evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-offline-cache/README.md"
check 'catalog vin nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-vin-shadow-example.conf"
check 'catalog engines nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-engines-shadow-example.conf"
check 'catalog analogs nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-analogs-shadow-example.conf"
check 'catalog offline cache service tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/CatalogOfflineCacheServiceTests.cs"
check 'catalog article-brands route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/article-brands'
check 'API module maps article-brands offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupArticleBrandsAsync'
check 'catalog article-brands nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-article-brands-shadow-example.conf"
check 'catalog categories route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/categories'
check 'catalog products route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/products'
check 'API module maps categories offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupCategoriesAsync'
check 'API module maps products offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupProductsAsync'
check 'catalog categories nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-categories-shadow-example.conf"
check 'catalog products nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-products-shadow-example.conf"
check 'catalog engine-search route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/engine-search'
check 'catalog article-links route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/article-links'
check 'catalog brand-parts route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/api/v1/catalog/brand-parts'
check 'program registers catalog brand parts service' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'ICatalogBrandPartsService, CatalogBrandPartsService'
check 'DB catalog brand parts repository is read-only' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Catalog/DbCatalogBrandPartsRepository.cs" 'Performs zero writes'
check 'API module maps engine-search offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupEngineSearchAsync'
check 'API module maps article-links offline cache' contains "$ROOT/aspnet/src/EcomAE.Platform/Modules/ApiModule.cs" 'LookupArticleLinksAsync'
check 'catalog engine-search nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-engine-search-shadow-example.conf"
check 'catalog article-links nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-article-links-shadow-example.conf"
check 'catalog brand-parts nginx shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-catalog-brand-parts-shadow-example.conf"
check 'catalog brand-parts evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-brand-parts/README.md"
check 'CloudPanel find-and-redeploy script exists' test -x "$ROOT/scripts/cloudpanel_find_and_redeploy.sh"
check 'CloudPanel find-and-redeploy rejects var www ecomae assumption' contains "$ROOT/scripts/cloudpanel_find_and_redeploy.sh" '/var/www/ecomae is NOT a required path'
check 'CloudPanel quick start warns against var www ecomae' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'Do not use `/var/www/ecomae`'
check 'CloudPanel continue-after-env script exists' test -x "$ROOT/scripts/cloudpanel_continue_after_env.sh"
check 'CloudPanel quick start explains nano save/exit' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'Ctrl+O'
check 'platform.env example documents final-gate smoke keys' contains "$ROOT/deploy/aspnet/platform.env.example" 'ECOMAE_PRICE_LOOKUP_API_KEY'
check 'CloudPanel quick start keeps admin ASP.NET disabled by default' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'AdminAspNetEnabled=false'
check 'CloudPanel quick start points to final-gate capture' contains "$ROOT/deploy/aspnet/CLOUDPANEL_QUICK_START.md" 'cloudpanel_capture_final_gate_artifacts.sh'
check 'worker dry-run evidence provider exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerDryRunEvidenceProvider.cs"
check 'worker dry-run evidence keeps PHP fallback required' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerDryRunEvidenceProvider.cs" 'PhpFallbackRequired: true'
check 'worker dry-run evidence includes rollback command' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerDryRunEvidenceProvider.cs" 'disable ASP.NET worker flag'
check 'worker job runner attaches dry-run evidence' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobRunner.cs" 'BuildEvidence'
check 'worker dry-run evidence tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationWorkerDryRunEvidenceProviderTests.cs"
check 'worker batch dry-run reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerBatchDryRunReporter.cs"
check 'worker batch dry-run report keeps fallback blocker' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerBatchDryRunReporter.cs" 'PHP schedulers remain authoritative fallback'
check 'worker batch dry-run reporter tests exist' test -f "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationWorkerBatchDryRunReporterTests.cs"
check 'worker placeholder logs batch dry-run report' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerPlaceholder.cs" 'Batch worker dry-run report'
check 'worker placeholder logs dry-run blockers' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerPlaceholder.cs" 'RemainingBlockers'
check 'worker program registers batch dry-run reporter' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'IMigrationWorkerBatchDryRunReporter, MigrationWorkerBatchDryRunReporter'
check 'worker program keeps zero-php batch one catalog' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'ZeroPhpBatchOneWorkerReplacementCatalog'
check 'worker program keeps zero-php batch two catalog' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'ZeroPhpBatchTwoWorkerReplacementCatalog'
check 'worker program registers cutover batch catalog' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'ZeroPhpCutoverBatchCatalog'
check 'cutover batch catalog covers remaining batches' contains "$ROOT/aspnet/src/EcomAE.Workers/ZeroPhpCutoverBatchCatalog.cs" 'LastGeneratedBatch = 61'
check 'all batches marked dry-run scaffolded in progress json' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" '"aspnet-dry-run-scaffolded": 61'
check 'zero-php batch two catalog file exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ZeroPhpBatchTwoWorkerReplacement.cs"
check 'zero-php batch two dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/ZeroPhpBatchTwoWorkerDryRunExecutor.cs"
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
check 'ASP.NET Core roadmap covers Roslyn and IL' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Roslyn, IL'
check 'ASP.NET Core roadmap covers GC tuning' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Garbage Collector tuning'
check 'ASP.NET Core roadmap covers Kestrel transport internals' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Kestrel transport internals'
check 'ASP.NET Core roadmap covers SIMD vectorization' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'SIMD/vectorization'
check 'ASP.NET Core roadmap covers unmanaged interop' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Unmanaged interop'
check 'ASP.NET Core roadmap covers CPU cache design' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'CPU cache-aware design'
check 'ASP.NET Core roadmap covers Polly resilience' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Polly resilience pipelines'
check 'ASP.NET Core roadmap covers chaos engineering' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'Chaos experiments'
check 'ASP.NET Core roadmap covers OpenTelemetry' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'OpenTelemetry trace context'
check 'ASP.NET Core roadmap covers eBPF observability' contains "$ROOT/docs/migration/ASP_NET_CORE_ADVANCED_ARCHITECTURE_ROADMAP.md" 'eBPF observability'
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
check 'Cursor handoff status defers to Enterprise BOS law' contains "$ROOT/docs/migration/CURSOR_HANDOFF_STATUS.md" 'PROJECT_ARCHITECTURE_INSTRUCTIONS.md'
check 'Cursor handoff status blocks broad cutover' contains "$ROOT/docs/migration/CURSOR_HANDOFF_STATUS.md" 'Do not proxy broad'
check 'consolidated PR push script exists' test -x "$ROOT/scripts/push_consolidated_pr_update.sh"
check 'migration plan documents zero PHP final state' contains "$ROOT/docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md" 'zero PHP files'
check 'zero PHP production roadmap exists' test -f "$ROOT/docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md"
check 'zero PHP roadmap defines final state' contains "$ROOT/docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md" 'ASP.NET Core serves 100% of production traffic and PHP is fully decommissioned'
check 'zero PHP roadmap requires route inventory' contains "$ROOT/docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md" 'Route inventory shows zero `php-only`'
check 'zero PHP roadmap blocks broad cutover' contains "$ROOT/docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md" 'Block broad `/api`, `/cp`, `/erp`, `/bos`, and storefront catch-all cutovers'
check 'zero PHP roadmap reports readiness percent' contains "$ROOT/docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md" 'approximately 35% complete'
check 'zero PHP inventory script exists' test -x "$ROOT/scripts/inventory_php_routes.sh"
check 'zero PHP inventory script reports php-only status' contains "$ROOT/scripts/inventory_php_routes.sh" 'inventory-required-for-zero-php'
check 'zero PHP inventory script emits surface counts' contains "$ROOT/scripts/inventory_php_routes.sh" 'surfaceCounts'
check 'zero PHP inventory script excludes vendor dependencies' contains "$ROOT/scripts/inventory_php_routes.sh" "-not -path './vendor/*'"

echo "----------------------------"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
