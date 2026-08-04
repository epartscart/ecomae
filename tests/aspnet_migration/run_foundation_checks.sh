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
check 'price lookup dual-sample operator exists' test -f "$ROOT/scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh"
check 'price lookup dual-sample operator is executable' test -x "$ROOT/scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh"
check 'price lookup dual-sample operator asserts cutover false' contains "$ROOT/scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh" 'cutoverAllowed'
check 'price lookup OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/price-lookup/OPERATOR_VERIFY.md"
check 'price lookup compare supports --out' contains "$ROOT/scripts/compare_price_lookup_parity.py" '--out'
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
check 'zero PHP progress status remains below one hundred' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" 'True zero-PHP completion meter: **95.0%**.'
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
check 'catalog-miss-fill dry-run executor exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/CatalogMissFillDryRunExecutor.cs"
check 'worker catalog includes catalog-miss-fill' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerJobCatalog.cs" 'catalog-miss-fill'
check 'worker Program registers catalog-miss-fill dry-run' contains "$ROOT/aspnet/src/EcomAE.Workers/Program.cs" 'CatalogMissFillDryRunExecutor'
check 'catalog miss-fill dry-run evidence stub exists' test -f "$ROOT/docs/migration/evidence/catalog-miss-umapi/miss-fill-dry-run-report.json"
check 'catalog miss-fill dry-run evidence blocks cutover' contains "$ROOT/docs/migration/evidence/catalog-miss-umapi/miss-fill-dry-run-report.json" '"cutoverAllowed": false'
check 'catalog miss-fill dry-run evidence blocks outbound' contains "$ROOT/docs/migration/evidence/catalog-miss-umapi/miss-fill-dry-run-report.json" '"outboundBlocked": true'
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
check 'Enterprise BOS compliance keeps MySQL bridge for PG17' contains "$ROOT/docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md" 'ReplaceMysqlBridge=false'
check 'Enterprise BOS compliance marks Redis scaffold unwired' contains "$ROOT/docs/migration/ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md" 'ReplacePhpSessionCookies=false'
check 'EF Core 10 package referenced by platform' contains "$ROOT/aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj" 'Microsoft.EntityFrameworkCore'
check 'EF Core scaffold DbContext exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/EcomAeScaffoldDbContext.cs"
check 'EF tenant registry stub exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/TenantRegistryStub.cs"
check 'EF identity admin stub exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/IdentityAdminStub.cs"
check 'EF ERP cash account stub exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/ErpCashAccountStub.cs"
check 'EF ERP cash entry stub exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/ErpCashEntryStub.cs"
check 'EF ERP scaffold repository interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/IErpScaffoldRepository.cs"
check 'Redis scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Caching/EcomAeRedisScaffoldOptions.cs"
check 'Redis scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Caching/IDistributedCacheScaffold.cs"
check 'Redis scaffold defaults do not replace PHP cookies' contains "$ROOT/aspnet/src/EcomAE.Platform/Caching/EcomAeRedisScaffoldOptions.cs" 'ReplacePhpSessionCookies'
check 'Kafka scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Messaging/EcomAeKafkaScaffoldOptions.cs"
check 'Kafka publisher scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Messaging/IDomainEventPublisherScaffold.cs"
check 'Kafka scaffold defaults disallow publish' contains "$ROOT/aspnet/src/EcomAE.Platform/Messaging/EcomAeKafkaScaffoldOptions.cs" 'AllowPublish'
check 'OpenSearch scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Search/EcomAeOpenSearchScaffoldOptions.cs"
check 'OpenSearch scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Search/IEnterpriseSearchScaffold.cs"
check 'OpenSearch scaffold defaults do not replace PHP search' contains "$ROOT/aspnet/src/EcomAE.Platform/Search/EcomAeOpenSearchScaffoldOptions.cs" 'ReplacePhpSearch'
check 'Serilog scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Observability/EcomAeSerilogScaffoldOptions.cs"
check 'Serilog scaffold defaults do not register exporters' contains "$ROOT/aspnet/src/EcomAE.Platform/Observability/EcomAeSerilogScaffoldOptions.cs" 'RegisterExporters'
check 'Workers ActivitySource scaffolding exists' test -f "$ROOT/aspnet/src/EcomAE.Workers/Observability/EcomAeWorkerActivitySources.cs"
check 'Object storage scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Storage/EcomAeObjectStorageScaffoldOptions.cs"
check 'Object storage scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Storage/IObjectStorageScaffold.cs"
check 'Object storage scaffold defaults keep local paths' contains "$ROOT/aspnet/src/EcomAE.Platform/Storage/EcomAeObjectStorageScaffoldOptions.cs" 'ReplaceLocalFilePaths'
check 'Vault scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Security/Scaffolding/EcomAeVaultScaffoldOptions.cs"
check 'Vault secret store scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Security/Scaffolding/ISecretStoreScaffold.cs"
check 'Vault scaffold defaults keep env-file secrets' contains "$ROOT/aspnet/src/EcomAE.Platform/Security/Scaffolding/EcomAeVaultScaffoldOptions.cs" 'ReplaceEnvFileSecrets'
check 'Postgres scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/EcomAePostgresScaffoldOptions.cs"
check 'Postgres migration scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/IPostgresMigrationScaffold.cs"
check 'Postgres scaffold defaults keep MySQL bridge' contains "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/EcomAePostgresScaffoldOptions.cs" 'ReplaceMysqlBridge'
check 'OAuth scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/Scaffolding/EcomAeOAuthScaffoldOptions.cs"
check 'OAuth modern identity scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/Scaffolding/IModernIdentityScaffold.cs"
check 'OAuth scaffold defaults keep PHP cookie bridge' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/Scaffolding/EcomAeOAuthScaffoldOptions.cs" 'ReplacePhpCookieBridge'
check 'SPA scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Presentation/Scaffolding/EcomAeSpaScaffoldOptions.cs"
check 'SPA scaffold defaults keep Blazor hybrid' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/Scaffolding/EcomAeSpaScaffoldOptions.cs" 'ReplaceBlazorHybridPresentation'
check 'Helm design chart exists' test -f "$ROOT/deploy/aspnet/helm-ecomae-platform-example/Chart.yaml"
check 'Helm design values block cutover' contains "$ROOT/deploy/aspnet/helm-ecomae-platform-example/values.yaml" 'cutoverAllowed: false'
check 'Consolidated scaffold options example exists' test -f "$ROOT/deploy/aspnet/ecomae-scaffold-options.example.json"
check 'Consolidated scaffold options block cutover' contains "$ROOT/deploy/aspnet/ecomae-scaffold-options.example.json" '"cutoverAllowed": false'
check 'YARP surface digests design example exists' test -f "$ROOT/deploy/aspnet/yarp-surface-digests-example.json"
check 'YARP surface digests design blocks cutover' contains "$ROOT/deploy/aspnet/yarp-surface-digests-example.json" '"cutoverAllowed": false'
check 'YARP surface digests routeCount is 128' contains "$ROOT/deploy/aspnet/yarp-surface-digests-example.json" '"routeCount": 128'
check 'RabbitMQ scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Messaging/EcomAeRabbitMqScaffoldOptions.cs"
check 'RabbitMQ scaffold defaults disallow publish' contains "$ROOT/aspnet/src/EcomAE.Platform/Messaging/EcomAeRabbitMqScaffoldOptions.cs" 'AllowPublish'
check 'Polly scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Resilience/EcomAePollyScaffoldOptions.cs"
check 'Polly scaffold contract exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Resilience/IResiliencePipelineScaffold.cs"
check 'Polly scaffold defaults do not register pipelines' contains "$ROOT/aspnet/src/EcomAE.Platform/Resilience/EcomAePollyScaffoldOptions.cs" 'RegisterPipelines'
check 'hybrid UI dual-sample operator helper exists' test -f "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh"
check 'hybrid UI dual-sample operator asserts cutover false' contains "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh" 'cutoverAllowed'
check 'login-cookie dual-sample operator helper exists' test -f "$ROOT/scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh"
check 'login-cookie dual-sample operator is executable' test -x "$ROOT/scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh"
check 'login-cookie dual-sample operator asserts cutover false' contains "$ROOT/scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh" 'cutoverAllowed'
check 'catalog-miss dual-sample operator helper exists' test -f "$ROOT/scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh"
check 'catalog-miss dual-sample operator is executable' test -x "$ROOT/scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh"
check 'catalog-miss dual-sample operator asserts cutover false' contains "$ROOT/scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh" 'cutoverAllowed'
check 'digest dual-sample operator helper exists' test -f "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"
check 'digest dual-sample operator is executable' test -x "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"
check 'digest dual-sample operator supports contract-only without cookie' contains "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh" 'contract-only'
check 'all dual-sample operators helper exists' test -f "$ROOT/scripts/cloudpanel_run_all_dual_sample_operators.sh"
check 'all dual-sample operators helper is executable' test -x "$ROOT/scripts/cloudpanel_run_all_dual_sample_operators.sh"
check 'catalog miss compare skips dry-run report' contains "$ROOT/scripts/compare_catalog_miss_dual_samples.py" 'miss-fill-dry-run-report.json'
check 'digest compare supports --out result path' contains "$ROOT/scripts/compare_digest_dual_samples.py" '--out'
check 'YARP storefront digests design example exists' test -f "$ROOT/deploy/aspnet/yarp-storefront-digests-example.json"
check 'YARP storefront digests design blocks cutover' contains "$ROOT/deploy/aspnet/yarp-storefront-digests-example.json" '"cutoverAllowed": false'
check 'YARP storefront digests routeCount is 7' contains "$ROOT/deploy/aspnet/yarp-storefront-digests-example.json" '"routeCount": 7'
check 'YARP catalog-api design example exists' test -f "$ROOT/deploy/aspnet/yarp-catalog-api-example.json"
check 'YARP catalog-api design blocks cutover' contains "$ROOT/deploy/aspnet/yarp-catalog-api-example.json" '"cutoverAllowed": false'
check 'YARP catalog-api design blocks PHP removal' contains "$ROOT/deploy/aspnet/yarp-catalog-api-example.json" '"readyForPhpRemoval": false'
check 'YARP catalog-api routeCount is 19' contains "$ROOT/deploy/aspnet/yarp-catalog-api-example.json" '"routeCount": 19'
check 'YARP all-packs generator hard-floors catalog routeCount 19' contains "$ROOT/scripts/generate_all_yarp_design_examples.sh" 'yarp-catalog-api-example.json": 19'
check 'catalog allowlist sync mirrors live surface probe' contains "$ROOT/scripts/validate_catalog_api_allowlist_sync.py" 'probe_live_surface_stack.sh'
check 'catalog allowlist sync mirrors decommission area tests' contains "$ROOT/scripts/validate_catalog_api_allowlist_sync.py" 'run_php_decommission_area_tests.sh'
check 'catalog allowlist sync mirrors pre-php-removal parity' contains "$ROOT/scripts/validate_catalog_api_allowlist_sync.py" 'verify_pre_php_removal_parity.sh'
check 'presentation exact-route inventory exists' test -f "$ROOT/docs/migration/evidence/presentation/presentation-exact-routes.json"
check 'presentation exact-route inventory routeCount is 184' contains "$ROOT/docs/migration/evidence/presentation/presentation-exact-routes.json" '"routeCount": 184'
check 'presentation exact-route inventory blocks cutover' contains "$ROOT/docs/migration/evidence/presentation/presentation-exact-routes.json" '"cutoverAllowed": false'
check 'presentation allowlist sync mirrors inventory' contains "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py" 'presentation-exact-routes.json'
check 'live surface probe references presentation inventory' contains "$ROOT/scripts/probe_live_surface_stack.sh" 'presentation/presentation-exact-routes.json'
check 'decommission area tests reference presentation inventory' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'presentation/presentation-exact-routes.json'
check 'pre-php-removal parity references presentation inventory' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'presentation/presentation-exact-routes.json'
check 'cp menu tree inventory exists' test -f "$ROOT/docs/migration/evidence/surface-parity/cp-menu-tree-inventory.json"
check 'cp menu tree inventory blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/cp-menu-tree-inventory.json" '"cutoverAllowed": false'
check 'cp menu tree inventory omits raw structure' contains "$ROOT/docs/migration/evidence/surface-parity/cp-menu-tree-inventory.json" '"rawStructureReturned": false'
check 'cp menus SQL selects structure column' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'AS structure'
check 'cp menus digest contract requires nodeCount' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" 'nodeCount'
check 'cp menus migration golden includes structure summary' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/cp-menus.json" '"structurePresent": true'
check 'cp menus migration golden includes nodeCount' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/cp-menus.json" '"nodeCount": 3'
check 'cp menus generator emits structure sentinel' contains "$ROOT/scripts/generate_migration_digest_contract_samples.py" 'structurePresent'
check 'digest dual-sample locks cp-menus item fields' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'LIST_ITEM_FIELDS'
check 'digest dual-sample requires nonempty cp-menus migration' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'LIST_NONEMPTY_MIGRATION'
check 'cp menus item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/cp-menus-item-field-floor.json"
check 'cp menus item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/cp-menus-item-field-floor.json" '"cutoverAllowed": false'
check 'cp menus item-field floor omits raw structure' contains "$ROOT/docs/migration/evidence/surface-parity/cp-menus-item-field-floor.json" '"rawStructureReturned": false'
check 'cp menus item-field floor requires nodeCount' contains "$ROOT/docs/migration/evidence/surface-parity/cp-menus-item-field-floor.json" '"nodeCount"'
check 'list digest item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/list-digest-item-field-floor.json"
check 'list digest item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/list-digest-item-field-floor.json" '"cutoverAllowed": false'
check 'list digest item-field floor tracks 26 stems' contains "$ROOT/docs/migration/evidence/surface-parity/list-digest-item-field-floor.json" '"listStemCount": 26'
check 'list digest item-field floor requires nonempty sentinels' contains "$ROOT/docs/migration/evidence/surface-parity/list-digest-item-field-floor.json" '"requireNonemptyMigrationSentinel": true'
check 'digest dual-sample locks all list item fields' python3 -c 'from pathlib import Path; t=Path("scripts/compare_digest_dual_samples.py").read_text(); assert "LIST_NONEMPTY_MIGRATION = frozenset(LIST_ITEM_FIELDS)" in t'
check 'cp tenants migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/cp-tenants.json" '"siteKey": "www"'
check 'erp coa migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/erp-coa-accounts.json" '"code": "1000"'
check 'storefront garage migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/storefront-garage.json" '"vin": "MIGRATIONVIN000001"'
check 'cp menu structure analyzer exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/CpMenuStructureAnalyzer.cs"
check 'php module catalog deeplink floor validator exists' test -f "$ROOT/scripts/validate_php_module_catalog_deeplink_floor.py"
check 'php module catalog deeplink floor validator is executable' test -x "$ROOT/scripts/validate_php_module_catalog_deeplink_floor.py"
check 'php module catalog deeplink floor passes' python3 "$ROOT/scripts/validate_php_module_catalog_deeplink_floor.py"
check 'php full catalog deeplink floor evidence exists' test -f "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/php-full-catalog-deeplink-floor.json"
check 'php full catalog deeplink floor blocks cutover' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/php-full-catalog-deeplink-floor.json" '"cutoverAllowed": false'
check 'php full catalog deeplink floor tracks 726' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/php-full-catalog-deeplink-floor.json" '"totalTracked": 726'
check 'php catalog coverage board builder exists' test -f "$ROOT/scripts/build_surface_field_catalog_coverage_board.py"
check 'php catalog coverage board builder is executable' test -x "$ROOT/scripts/build_surface_field_catalog_coverage_board.py"
check 'php catalog coverage board passes' python3 "$ROOT/scripts/build_surface_field_catalog_coverage_board.py"
check 'php catalog coverage board evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json"
check 'php catalog coverage board blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"cutoverAllowed": false'
check 'php catalog coverage board blocks PHP removal' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"readyForPhpRemoval": false'
check 'php catalog coverage board tracks 726' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"totalTracked": 726'
check 'php catalog coverage board missingCount is zero' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"missingCount": 0'
check 'php catalog coverage board keeps interactive complete at zero' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"aspNetInteractiveComplete": 0'
check 'php catalog coverage board uses digest-contract status' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"digest-contract"'
check 'php catalog coverage board uses php-only-deeplink status' contains "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json" '"php-only-deeplink"'
check 'hybrid directory full catalog floor validator exists' test -f "$ROOT/scripts/validate_hybrid_directory_full_catalog_floor.py"
check 'hybrid directory full catalog floor validator is executable' test -x "$ROOT/scripts/validate_hybrid_directory_full_catalog_floor.py"
check 'hybrid directory full catalog floor passes' python3 "$ROOT/scripts/validate_hybrid_directory_full_catalog_floor.py"
check 'hybrid directory full catalog floor evidence exists' test -f "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json"
check 'hybrid directory full catalog floor blocks cutover' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json" '"cutoverAllowed": false'
check 'hybrid directory full catalog floor tracks 726' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json" '"totalTracked": 726'
check 'ERP dashboard lists ErpCategories directory' contains "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor" 'PhpModuleCatalog.ErpCategories'
check 'BOS fleet lists BosSections directory' contains "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosFleetApp.razor" 'PhpModuleCatalog.BosSections'
check 'PhpModuleCatalog summary exposes directoryCoverage' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/PhpModuleCatalog.cs" 'directoryCoverage'
check 'PhpModuleCatalog summary floors full catalog at 725' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/PhpModuleCatalog.cs" 'fullCatalogFloor"] = 725'
check 'php module catalog generator uniquifies duplicate ids' contains "$ROOT/scripts/generate_php_module_catalog.py" 'uniquify_ids'
check 'module-function coverage consistency validator exists' test -f "$ROOT/scripts/validate_module_function_coverage_consistency.py"
check 'module-function coverage consistency validator is executable' test -x "$ROOT/scripts/validate_module_function_coverage_consistency.py"
check 'module-function coverage consistency passes' python3 "$ROOT/scripts/validate_module_function_coverage_consistency.py"
check 'module-function coverage consistency evidence exists' test -f "$ROOT/docs/migration/evidence/module-function-parity/coverage-consistency.json"
check 'module-function coverage consistency matches 726' contains "$ROOT/docs/migration/evidence/module-function-parity/coverage-consistency.json" '"matchedIds": 726'
check 'module-function coverage consistency blocks cutover' contains "$ROOT/docs/migration/evidence/module-function-parity/coverage-consistency.json" '"cutoverAllowed": false'
check 'surface-field operator rebuilds php catalog coverage board' contains "$ROOT/scripts/cloudpanel_run_surface_field_parity_operator.sh" 'build_surface_field_catalog_coverage_board.py'
check 'surface-field operator floors contracts at 54' contains "$ROOT/scripts/cloudpanel_run_surface_field_parity_operator.sh" 'expected >=54'
check 'surface-field board contractCount is 153' contains "$ROOT/docs/migration/evidence/surface-parity/www-surface-field-parity.json" '"contractCount": 153'
check 'surface-field board includes orders-digest' contains "$ROOT/docs/migration/evidence/surface-parity/www-surface-field-parity.json" '"/cp/orders-digest"'
check 'PhpModuleCatalog rejects aspnet preview deeplinks' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/PhpModuleCatalog.cs" 'IsAllowedPhpDeeplink'
check 'surface-digest exact-route inventory exists' test -f "$ROOT/docs/migration/evidence/surface-parity/surface-digest-exact-routes.json"
check 'surface-digest exact-route inventory routeCount is 136' contains "$ROOT/docs/migration/evidence/surface-parity/surface-digest-exact-routes.json" '"routeCount": 136'
check 'surface-digest exact-route inventory blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/surface-digest-exact-routes.json" '"cutoverAllowed": false'
check 'surface digest allowlist sync mirrors inventory' contains "$ROOT/scripts/validate_surface_digest_allowlist_sync.py" 'surface-digest-exact-routes.json'
check 'live surface probe references surface-digest inventory' contains "$ROOT/scripts/probe_live_surface_stack.sh" 'surface-parity/surface-digest-exact-routes.json'
check 'decommission area tests reference surface-digest inventory' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'surface-parity/surface-digest-exact-routes.json'
check 'pre-php-removal parity references surface-digest inventory' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'surface-parity/surface-digest-exact-routes.json'
check 'YARP all-packs generator helper exists' test -f "$ROOT/scripts/generate_all_yarp_design_examples.sh"
check 'GraphQL scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Scaffolding/EcomAeGraphQlScaffoldOptions.cs"
check 'GraphQL scaffold defaults not public' contains "$ROOT/aspnet/src/EcomAE.Platform/Api/Scaffolding/EcomAeGraphQlScaffoldOptions.cs" 'ExposePublicEndpoint'
check 'gRPC scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Api/Scaffolding/EcomAeGrpcScaffoldOptions.cs"
check 'Blockchain scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Integrations/Scaffolding/EcomAeBlockchainScaffoldOptions.cs"
check 'Blockchain scaffold forbids SoR use' contains "$ROOT/aspnet/src/EcomAE.Platform/Integrations/Scaffolding/EcomAeBlockchainScaffoldOptions.cs" 'UseAsBusinessSourceOfRecord'
check 'Rate-limit scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Security/Scaffolding/EcomAeRateLimitScaffoldOptions.cs"
check 'Rate-limit scaffold keeps legacy throttle' contains "$ROOT/aspnet/src/EcomAE.Platform/Security/Scaffolding/EcomAeRateLimitScaffoldOptions.cs" 'ReplaceLegacyApiClientThrottle'
check 'GitOps Argo CD example exists' test -f "$ROOT/deploy/aspnet/gitops-example/argocd-application.example.yaml"
check 'GitOps Argo CD example blocks cutover' contains "$ROOT/deploy/aspnet/gitops-example/argocd-application.example.yaml" 'ecomae.cutoverAllowed: "false"'
check 'Workers Helm design chart exists' test -f "$ROOT/deploy/aspnet/helm-ecomae-workers-example/Chart.yaml"
check 'Workers Helm design disables writes' contains "$ROOT/deploy/aspnet/helm-ecomae-workers-example/values.yaml" 'allowWorkerWrites: false'
check 'Native AOT scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Hosting/Scaffolding/EcomAeNativeAotScaffoldOptions.cs"
check 'Native AOT scaffold does not require platform host' contains "$ROOT/aspnet/src/EcomAE.Platform/Hosting/Scaffolding/EcomAeNativeAotScaffoldOptions.cs" 'RequireForPlatformHost'
check 'AI sidecar scaffold options exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Integrations/Scaffolding/EcomAeAiSidecarScaffoldOptions.cs"
check 'AI sidecar scaffold forbids business writes' contains "$ROOT/aspnet/src/EcomAE.Platform/Integrations/Scaffolding/EcomAeAiSidecarScaffoldOptions.cs" 'AllowBusinessWrites'
check 'scaffold options example validator exists' test -f "$ROOT/scripts/validate_scaffold_options_example.py"
check 'scaffold options example validator is executable' test -x "$ROOT/scripts/validate_scaffold_options_example.py"
check 'scaffold options example validator passes' python3 "$ROOT/scripts/validate_scaffold_options_example.py"
check 'migration evidence cutover locks validator exists' test -f "$ROOT/scripts/validate_migration_evidence_cutover_locks.py"
check 'migration evidence cutover locks validator is executable' test -x "$ROOT/scripts/validate_migration_evidence_cutover_locks.py"
check 'migration evidence cutover locks pass' python3 "$ROOT/scripts/validate_migration_evidence_cutover_locks.py"
check 'cutover locks require decommission public-probe tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'decommission/public-probes/*.json'
check 'cutover locks require decommission parity-sample tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'decommission/parity-samples/*.json'
check 'cutover locks require hybrid-ui dual-sample tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'hybrid-ui-dual-samples/*.json'
check 'cutover locks require login-session-bridge tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'login-session-bridge/*.json'
check 'cutover locks require catalog-miss-umapi tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'catalog-miss-umapi/*.json'
check 'cutover locks require price-lookup tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'price-lookup/*.json'
check 'cutover locks require presentation tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'presentation/*.json'
check 'cutover locks require module-function-parity tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'module-function-parity/*.json'
check 'cutover locks require catalog-api tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'catalog-api/*.json'
check 'cutover locks require tenant-safety tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'tenant-safety/*.json'
check 'cutover locks require surface-parity top-level tree' contains "$ROOT/scripts/validate_migration_evidence_cutover_locks.py" 'surface-parity/*.json'
check 'price-lookup php sample blocks cutover' contains "$ROOT/docs/migration/evidence/price-lookup/php-baseline-sample.json" '"cutoverAllowed": false'
check 'price-lookup aspnet sample blocks cutover' contains "$ROOT/docs/migration/evidence/price-lookup/aspnet-output-sample.json" '"cutoverAllowed": false'
check 'php decommission probe blocks cutover' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json" '"cutoverAllowed": false'
check 'php decommission probe blocks PHP removal' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-php-decommission-readiness.json" '"readyForPhpRemoval": false'
check 'zero-php completion probe blocks cutover' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-zero-php-completion.json" '"cutoverAllowed": false'
check 'live surface links probe blocks cutover' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-live-surface-links.json" '"cutoverAllowed": false'
check 'migration golden cutover locks validator exists' test -f "$ROOT/scripts/validate_migration_golden_cutover_locks.py"
check 'migration golden cutover locks validator is executable' test -x "$ROOT/scripts/validate_migration_golden_cutover_locks.py"
check 'migration golden cutover locks pass' python3 "$ROOT/scripts/validate_migration_golden_cutover_locks.py"
check 'migration digest generator stamps cutoverAllowed false' contains "$ROOT/scripts/generate_migration_digest_contract_samples.py" 'payload["cutoverAllowed"] = False'
check 'deploy packs YARP exact-routes design example' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'yarp-exact-routes-example.json'
check 'deploy packs YARP catalog-api design example' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'yarp-catalog-api-example.json'
check 'deploy packs OPERATOR_VERIFY docs' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'OPERATOR_VERIFY.md'
check 'deploy packs migration golden cutover locks validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_migration_golden_cutover_locks.py'
check 'presentation/hybrid allowlist sync validator exists' test -f "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py"
check 'presentation/hybrid allowlist sync validator is executable' test -x "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py"
check 'presentation/hybrid allowlist sync passes' python3 "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py"
check 'surface/storefront digest allowlist sync validator exists' test -f "$ROOT/scripts/validate_surface_digest_allowlist_sync.py"
check 'surface/storefront digest allowlist sync validator is executable' test -x "$ROOT/scripts/validate_surface_digest_allowlist_sync.py"
check 'surface/storefront digest allowlist sync passes' python3 "$ROOT/scripts/validate_surface_digest_allowlist_sync.py"
check 'catalog/API allowlist sync validator exists' test -f "$ROOT/scripts/validate_catalog_api_allowlist_sync.py"
check 'catalog/API allowlist sync validator is executable' test -x "$ROOT/scripts/validate_catalog_api_allowlist_sync.py"
check 'catalog/API allowlist sync passes' python3 "$ROOT/scripts/validate_catalog_api_allowlist_sync.py"
check 'catalog/API contract floor helper exists' test -f "$ROOT/scripts/compare_catalog_api_contract_floor.py"
check 'catalog/API dual-sample operator exists' test -f "$ROOT/scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh"
check 'catalog/API dual-sample operator is executable' test -x "$ROOT/scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh"
check 'catalog/API OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/catalog-api/OPERATOR_VERIFY.md"
check 'catalog/API compare-result blocks cutover' contains "$ROOT/docs/migration/evidence/catalog-api/compare-result.json" '"cutoverAllowed": false'
check 'login-session OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/login-session-bridge/OPERATOR_VERIFY.md"
check 'catalog-miss OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/catalog-miss-umapi/OPERATOR_VERIFY.md"
check 'migration digest generator includes cp-orders-digest' contains "$ROOT/scripts/generate_migration_digest_contract_samples.py" 'cp-orders-digest.json'
check 'digest dual-sample contracts cover full allowlist' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'storefront-profile'
check 'digest dual-sample capture covers cp-users' contains "$ROOT/scripts/cloudpanel_capture_digest_dual_samples.sh" 'cp-users'
check 'digest dual-sample capture covers storefront-profile' contains "$ROOT/scripts/cloudpanel_capture_digest_dual_samples.sh" 'storefront-profile'
check 'digest compare-result reports 134 contracts' contains "$ROOT/docs/migration/evidence/surface-parity/digest-compare-result.json" '"contractsRegistered": 134'
check 'enterprise BOS scaffold guardrails script exists' test -f "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh"
check 'enterprise BOS scaffold guardrails is executable' test -x "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh"
check 'enterprise BOS scaffold guardrails pass' bash "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh"
check 'scaffold guardrails forbid AddKafka in Program.cs' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'AddKafka'
check 'scaffold guardrails forbid AddOtlpExporter in Program.cs' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'AddOtlpExporter'
check 'scaffold guardrails forbid AddSerilog in Program.cs' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'AddSerilog'
check 'scaffold guardrails forbid AddDistributedCache in Program.cs' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'AddDistributedCache'
check 'scaffold guardrails forbid Yarp.ReverseProxy in Program.cs' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'Yarp.ReverseProxy'
check 'scaffold guardrails check workers Program.cs needle loop' contains "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh" 'workers:$WORKERS_PROGRAM'
check 'platform.env.example documents disabled scaffold env keys' contains "$ROOT/deploy/aspnet/platform.env.example" 'EcomAe__AiSidecar__AllowBusinessWrites=false'
check 'platform.env.example documents Postgres ReplaceMysqlBridge false' contains "$ROOT/deploy/aspnet/platform.env.example" 'EcomAe__Postgres__ReplaceMysqlBridge=false'
check 'platform.env.example documents OAuth RequireMfa false' contains "$ROOT/deploy/aspnet/platform.env.example" 'EcomAe__OAuth__RequireMfa=false'
check 'platform.env.example documents NativeAot isolated evaluation true' contains "$ROOT/deploy/aspnet/platform.env.example" 'EcomAe__NativeAot__AllowIsolatedServiceEvaluation=true'
check 'platform.env scaffold key parity validator exists' test -f "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"
check 'platform.env scaffold key parity validator is executable' test -x "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"
check 'platform.env scaffold key parity passes' python3 "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"
check 'offline migration gate exists' test -f "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh"
check 'offline migration gate is executable' test -x "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh"
check 'offline migration gate runs dual-sample suite' contains "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh" 'cloudpanel_run_all_dual_sample_operators.sh'
check 'offline migration gate runs surface-field parity' contains "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh" 'cloudpanel_run_surface_field_parity_operator.sh'
check 'offline migration gate runs presentation recheck' contains "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh" 'cloudpanel_run_presentation_recheck_operator.sh'
check 'offline migration gate runs tenant safety' contains "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh" 'cloudpanel_run_tenant_safety_operator.sh'
check 'offline migration gate runs scaffold guardrails' contains "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh" 'validate_enterprise_bos_scaffold_guardrails.sh'
check 'surface-field parity operator exists' test -f "$ROOT/scripts/cloudpanel_run_surface_field_parity_operator.sh"
check 'surface-field parity operator is executable' test -x "$ROOT/scripts/cloudpanel_run_surface_field_parity_operator.sh"
check 'surface-field board blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/www-surface-field-parity.json" '"cutoverAllowed": false'
check 'surface-field board blocks PHP removal' contains "$ROOT/docs/migration/evidence/surface-parity/www-surface-field-parity.json" '"readyForPhpRemoval": false'
check 'platform.env.example documents dual-sample operator helper' contains "$ROOT/deploy/aspnet/platform.env.example" 'cloudpanel_run_hybrid_ui_dual_sample_operator.sh'
check 'platform.env.example documents offline migration gate' contains "$ROOT/deploy/aspnet/platform.env.example" 'cloudpanel_run_offline_migration_gate.sh'
check 'YARP generator script exists' test -f "$ROOT/scripts/generate_yarp_exact_routes_example.py"
check 'YARP design example routeCount matches presentation shadows' contains "$ROOT/deploy/aspnet/yarp-exact-routes-example.json" '"routeCount": 184'
check 'EF tenant registry scaffold repository interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Data/Scaffolding/ITenantRegistryScaffoldRepository.cs"
check 'YARP exact-routes design example exists' test -f "$ROOT/deploy/aspnet/yarp-exact-routes-example.json"
check 'YARP design example blocks cutover' contains "$ROOT/deploy/aspnet/yarp-exact-routes-example.json" '"cutoverAllowed": false'
check 'ActivitySource Data name reserved' contains "$ROOT/aspnet/src/EcomAE.Platform/Observability/EcomAeActivitySources.cs" 'EcomAE.Platform.Data'
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
check 'exact-route shadow installer exists' test -x "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh"
check 'exact-route shadow installer refuses broad paths' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'refusing broad path'
check 'exact-route shadow installer inserts before location /' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'immediately before location /'
check 'exact-route shadow installer probes local nginx bypassing CDN' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" '--resolve www.ecomae.com:443:127.0.0.1'
check 'exact-route installer uses re.escape exact location match' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 're.escape(route)'
check 'exact-route location match regression test exists' test -x "$ROOT/tests/aspnet_migration/test_exact_route_location_match.sh"
check 'exact-route location match regression passes' bash "$ROOT/tests/aspnet_migration/test_exact_route_location_match.sh"
check 'deploy packs exact-route shadow installer' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_install_exact_route_shadow.sh'
check 'surface digest batch installer exists' test -x "$ROOT/scripts/cloudpanel_install_surface_digest_shadows.sh"
check 'surface digest batch probe exists' test -x "$ROOT/scripts/cloudpanel_probe_surface_digest_shadows.sh"
check 'surface digest batch installer refuses without confirm' contains "$ROOT/scripts/cloudpanel_install_surface_digest_shadows.sh" 'ECOMAE_CONFIRM_INSTALL_SURFACE_DIGEST_SHADOWS'
check 'surface digest batch installer refuses broad paths' contains "$ROOT/scripts/cloudpanel_install_surface_digest_shadows.sh" 'refusing broad path'
check 'surface digest batch installer expects 128 routes' contains "$ROOT/scripts/cloudpanel_install_surface_digest_shadows.sh" 'expected 128 digest locations'
check 'surface digest batch probe expects PASS=128' contains "$ROOT/scripts/cloudpanel_probe_surface_digest_shadows.sh" 'expected 128 digest routes'
check 'deploy packs surface digest batch installer' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_install_surface_digest_shadows.sh'
check 'deploy packs surface digest batch probe' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_probe_surface_digest_shadows.sh'
check 'storefront digest batch installer exists' test -x "$ROOT/scripts/cloudpanel_install_storefront_digest_shadows.sh"
check 'storefront digest batch probe exists' test -x "$ROOT/scripts/cloudpanel_probe_storefront_digest_shadows.sh"
check 'storefront digest batch installer refuses without confirm' contains "$ROOT/scripts/cloudpanel_install_storefront_digest_shadows.sh" 'ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS'
check 'storefront digest batch installer expects 7 routes' contains "$ROOT/scripts/cloudpanel_install_storefront_digest_shadows.sh" 'expected 7 storefront digest locations'
check 'deploy packs storefront digest batch installer' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_install_storefront_digest_shadows.sh'
check 'deploy packs storefront digest batch probe' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_probe_storefront_digest_shadows.sh'
check 'digest dual-sample capture helper exists' test -x "$ROOT/scripts/cloudpanel_capture_digest_dual_samples.sh"
check 'deploy packs digest dual-sample capture helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_capture_digest_dual_samples.sh'
check 'Blazor migration console route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/console'
check 'Program maps Blazor Razor components' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'MapRazorComponents'
check 'Program enables antiforgery for Blazor SSR' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'UseAntiforgery'
check 'Blazor Zero-PHP console page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ZeroPhpConsole.razor"
check 'Blazor CP command centre app exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"
check 'Blazor ERP BOS dashboard app exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"
check 'Blazor BOS fleet app exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosFleetApp.razor"
check 'Blazor storefront preview app exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor"
check 'presentation app shadow example exists' test -f "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf"
check 'presentation app installer exists' test -x "$ROOT/scripts/cloudpanel_install_presentation_app_shadows.sh"
check 'CP stylesheets include command dashboard CSS' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs" 'epc_cp_command_dashboard_css.php'
check 'presentation parity mentions /cp/app preview' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/PresentationParityReporter.cs" '/cp/app'
check 'presentation parity mentions login bridge' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/PresentationParityReporter.cs" '/cp/login'
check 'chrome parity gap matrix exists' test -f "$ROOT/docs/migration/CHROME_PARITY_GAP_MATRIX.md"
check 'detailed PHP vs ASP.NET recheck doc exists' test -f "$ROOT/docs/migration/PHP_VS_ASPNET_DETAILED_RECHECK.md"
check 'module function parity inventory exists' test -f "$ROOT/docs/migration/inventory/MODULE_FUNCTION_PARITY_STATUS.md"
check 'module function parity evidence dir exists' test -d "$ROOT/docs/migration/evidence/module-function-parity"
check 'module function parity inventory JSON exists' test -f "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json"
check 'module function parity inventory blocks cutover' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"cutoverAllowed": false'
check 'module function parity inventory keeps aspnet-complete at zero' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"aspnetCompleteCount": 0'
check 'module function parity inventory tracks php catalog counts' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"phpCatalogCounts"'
check 'module function parity inventory floors CP brochure features' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"cpBrochureFeatures": 405'
check 'module function parity inventory floors ERP tabs' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"erpTabs": 154'
check 'module function parity inventory floors BOS modules' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"bosModules": 99'
check 'module function compare enforces php catalog floors' contains "$ROOT/scripts/compare_module_function_parity.py" 'PHP_CATALOG_FLOORS'
check 'module function compare enforces full catalog module floor' contains "$ROOT/scripts/compare_module_function_parity.py" 'MIN_FULL_MODULE_COUNT'
check 'module function inventory enumerates cp-feature rows' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"kind": "cp-feature"'
check 'module function inventory enumerates erp-tab rows' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"kind": "erp-tab"'
check 'module function inventory enumerates bos-module rows' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"kind": "bos-module"'
check 'module function inventory moduleCount covers full catalog' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); assert d["moduleCount"]>=725, d["moduleCount"]' "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json"
check 'module function inventory enumerates bos-section rows' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"kind": "bos-section"'
check 'module function inventory floors BOS sections' contains "$ROOT/docs/migration/evidence/module-function-parity/module-function-inventory.json" '"bosSections": 11'
check 'presentation php_module_catalog evidence has erpAreas list' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); assert isinstance(d.get("erpAreas"), list) and len(d["erpAreas"])>=35' "$ROOT/docs/migration/evidence/presentation/php_module_catalog.json"
check 'presentation php_module_catalog evidence has cp features list' python3 -c 'import json,sys; from pathlib import Path; d=json.loads(Path(sys.argv[1]).read_text()); assert isinstance(d.get("cpBrochureFeatures"), list) and len(d["cpBrochureFeatures"])>=405' "$ROOT/docs/migration/evidence/presentation/php_module_catalog.json"
check 'generate_php_module_catalog writes evidence catalog' contains "$ROOT/scripts/generate_php_module_catalog.py" 'evidence_catalog_path'
check 'module function parity compare helper exists' test -f "$ROOT/scripts/compare_module_function_parity.py"
check 'module function parity compare is executable' test -x "$ROOT/scripts/compare_module_function_parity.py"
check 'module function parity operator exists' test -f "$ROOT/scripts/cloudpanel_run_module_function_parity_operator.sh"
check 'module function parity operator is executable' test -x "$ROOT/scripts/cloudpanel_run_module_function_parity_operator.sh"
check 'module function parity operator asserts complete zero' contains "$ROOT/scripts/cloudpanel_run_module_function_parity_operator.sh" 'aspnetCompleteCount'
check 'module function parity human pass file absent' bash -c '! test -f "$ROOT/docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md"'
check 'presentation recheck operator exists' test -f "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh"
check 'presentation recheck operator is executable' test -x "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh"
check 'presentation recheck operator asserts removal false' contains "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh" 'readyForPhpRemoval'
check 'presentation recheck evidence blocks cutover' contains "$ROOT/docs/migration/evidence/presentation/php-vs-aspnet-recheck.json" '"cutoverAllowed": false'
check 'presentation recheck evidence keeps honest fail' contains "$ROOT/docs/migration/evidence/presentation/php-vs-aspnet-recheck.json" '"status": "fail"'
check 'operator verify index exists' test -f "$ROOT/docs/migration/evidence/OPERATOR_VERIFY.md"
check 'presentation OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/presentation/OPERATOR_VERIFY.md"
check 'hybrid-ui OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/OPERATOR_VERIFY.md"
check 'digest OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/surface-parity/OPERATOR_VERIFY.md"
check 'module-function OPERATOR_VERIFY exists' test -f "$ROOT/docs/migration/evidence/module-function-parity/OPERATOR_VERIFY.md"
check 'presentation parity probe script exists' test -x "$ROOT/scripts/cloudpanel_probe_php_presentation_parity.sh"
check 'presentation compare helper exists' test -f "$ROOT/scripts/compare_php_aspnet_presentation.py"
check 'login bridge service exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/DbLegacyAdminLoginService.cs"
check 'login session token factory exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionTokenFactory.cs"
check 'storefront piston CSS wired in presentation assets' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs" 'epc_automotive_spareparts.css'
check 'hub logo CSS wired in login stylesheets' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs" 'epc_ecomae_hub_logo_css.php'
check 'BOS login shell JS wired' contains "$ROOT/aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs" 'epc_bos_shell.js'
check 'piston banner component exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpAspPistonBanner.razor"
check 'hub logo component exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeHubLogo.razor"
check 'storefront app uses piston banner' contains "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor" 'PhpAspPistonBanner'
check 'BOS login emits particle host' contains "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosLoginApp.razor" 'bosParticles'
check 'plan requires graphical hero/animation parity' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'Graphical presentation is in scope'
check 'customer session insert includes last_activiti_time' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacyAdminLoginSql.cs" 'last_activiti_time'
check 'customer token formula uses userId' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionTokenFactory.cs" 'CustomerSessionToken'
check 'login cookie dual-sample compare exists' test -x "$ROOT/scripts/compare_login_cookie_dual_samples.py"
check 'login cookie dual-sample capture exists' test -x "$ROOT/scripts/cloudpanel_capture_login_cookie_dual_samples.sh"
check 'secret succession verify helper exists' test -x "$ROOT/scripts/cloudpanel_verify_secret_succession_configured.sh"
check 'login session bridge evidence dir exists' test -d "$ROOT/docs/migration/evidence/login-session-bridge"
check 'login bridge keeps BOS PHP-authoritative' contains "$ROOT/docs/migration/evidence/login-session-bridge/README.md" 'PHP-authoritative'
check 'catalog miss dual-sample compare exists' test -x "$ROOT/scripts/compare_catalog_miss_dual_samples.py"
check 'catalog miss dual-sample capture exists' test -x "$ROOT/scripts/cloudpanel_capture_catalog_miss_dual_samples.sh"
check 'catalog miss path probe exists' test -x "$ROOT/scripts/cloudpanel_probe_catalog_miss_path.sh"
check 'Wave B write dry-run probe exists' test -x "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh"
check 'Wave B write dry-run probe refuses cutover' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" 'cutoverAllowed=false'
check 'Wave B write dry-run probe covers cart delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/cart/delete'
check 'Wave B write dry-run probe covers OMS status' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/set-item-status'
check 'Wave B write dry-run probe covers ERP amend' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-entries/amend'
check 'Wave B write dry-run probe covers ERP void' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-entries/void'
check 'Wave B write dry-run probe covers GL manual' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/gl-journals/manual'
check 'Wave B write dry-run probe covers cart add' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/cart/add'
check 'Wave B write dry-run probe covers OMS send-message' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/send-message'
check 'Wave B write dry-run probe covers OMS set-courier' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/set-courier'
check 'Wave B write dry-run probe covers GL reverse' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/gl-journals/reverse'
check 'Wave B write dry-run probe covers purchase void' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/void'
check 'Wave B write dry-run probe covers invoice cancel' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/invoices/cancel'
check 'Wave B write dry-run probe covers garage notepad-add' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/garage/notepad-add'
check 'Wave B write dry-run probe covers quote submit' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/quotes/submit'
check 'Wave B write dry-run probe covers quote accept' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/quotes/accept'
check 'Wave B write dry-run probe covers garage set-active' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/garage/set-active'
check 'Wave B write dry-run probe covers OMS add-comment' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/add-comment'
check 'Wave B write dry-run probe covers storefront order send-message' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/orders/send-message'
check 'Wave B write dry-run probe covers OMS set-viewed' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/set-viewed'
check 'Wave B write dry-run probe covers quote add-item' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/quotes/add-item'
check 'Wave B write dry-run probe covers garage delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/garage/delete'
check 'Wave B write dry-run probe covers checkout create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/checkout/create'
check 'Wave B write dry-run probe covers ERP cash create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-entries/create'
check 'Wave B write dry-run probe covers ERP receipt voucher' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-entries/receipt-voucher'
check 'Wave B write dry-run probe covers ERP payment voucher' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-entries/payment-voucher'
check 'Wave B write dry-run probe covers ERP supplier create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/suppliers/create'
check 'Wave B write dry-run probe covers ERP purchase create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/create'
check 'Wave B write dry-run probe covers ERP purchase delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/delete'
check 'Wave B write dry-run probe covers ERP invoice delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/invoices/delete'
check 'Wave B write dry-run probe covers ERP cash account create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/cash-accounts/create'
check 'Wave B write dry-run probe covers ERP COA create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/coa-accounts/create'
check 'Wave B write dry-run probe covers OMS update-items' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/update-items'
check 'Wave B write dry-run probe covers OMS update-item' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/update-item'
check 'Wave B write dry-run probe covers OMS pay-refund' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/pay-refund'
check 'Wave B write dry-run probe covers quote add-manual' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/quotes/add-manual'
check 'Wave B write dry-run probe covers garage check-car' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/storefront/garage/check-car'
check 'Wave B write dry-run probe covers OMS fulfillment-set-stage' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/fulfillment-set-stage'
check 'Wave B write dry-run probe covers OMS fulfillment-advance' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/fulfillment-advance'
check 'Wave B write dry-run probe covers ERP purchase amend' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/amend'
check 'Wave B write dry-run probe covers ERP SO delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/sales-orders/delete'
check 'Wave B write dry-run probe covers ERP customer master-save' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/customers/master-save'
check 'Wave B write dry-run probe covers ERP RMA create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/aftersales/rma-create'
check 'Wave B write dry-run probe covers OMS refresh-item-cost' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/refresh-item-cost'
check 'Wave B write dry-run probe covers ERP purchase from-order' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/from-order'
check 'Wave B write dry-run probe covers ERP currency set-rate' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/currency/set-rate'
check 'Wave B write dry-run probe covers ERP period soft-close' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/periods/soft-close'
check 'Wave B write dry-run probe covers ERP period lock' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/periods/lock'
check 'Wave B write dry-run probe covers ERP customer settlement' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/customers/settlement'
check 'Wave B write dry-run probe covers ERP supplier settlement' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/suppliers/settlement'
check 'Wave B write dry-run probe covers ERP fiscal set-lock' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/fiscal/set-lock'
check 'Wave B write dry-run probe covers ERP period reopen' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/periods/reopen'
check 'Wave B write dry-run probe covers ERP purchase adjust' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchases/adjust'
check 'Wave B write dry-run probe covers ERP order settlement' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/orders/settlement'
check 'Wave B write dry-run probe covers ERP suppliers sync' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/suppliers/sync'
check 'Wave B write dry-run probe covers ERP GL post-sales' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/gl-journals/post-sales'
check 'Wave B write dry-run probe covers ERP GL sync-unposted' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/gl-journals/sync-unposted'
check 'Wave B write dry-run probe covers ERP workflow status' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/workflow/status'
check 'Wave B write dry-run probe covers ERP workflow create' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/workflow/create'
check 'Wave B write dry-run probe covers on-premises health' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/on-premises/health-dry-run'
check 'Wave B write dry-run probe covers on-premises license activate' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/on-premises/license-activate-dry-run'
check 'on-premises parity board reporter exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/OnPremisesParityReporter.cs" 'cutoverAllowed'
check 'on-premises parity route wired' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'MigrationOnPremisesParity'
check 'on-premises license activate dry-run route wired' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpOnPremisesLicenseActivateDryRun'
check 'on-premises licenses digest route wired' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpOnPremisesLicenses'
check 'on-premises licenses SQL omits notes' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'SelectOnPremisesLicenses'
check 'ASP.NET zero-PHP path mentions on-premises' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/AspNetZeroPhpPathReporter.cs" 'on-premises'
check 'ASP.NET zero-PHP path doc mentions on-premises' contains "$ROOT/docs/migration/ASPNET_ZERO_PHP_PATH.md" 'On-premises ERP'
check 'Wave B write dry-run probe covers SO cancel' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/sales-orders/cancel'
check 'Wave B write dry-run probe covers OMS delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/cp/orders/delete'
check 'Wave B write dry-run probe covers PO delete' contains "$ROOT/scripts/cloudpanel_probe_write_dryruns.sh" '/erp/purchase-orders/delete'
check 'Wave B write dry-run dual-sample operator exists' test -x "$ROOT/scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh"
check 'Wave B write dry-run dual-sample operator refuses cutover' contains "$ROOT/scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh" 'cutoverAllowed'
check 'all dual-sample operators include write-dryrun' contains "$ROOT/scripts/cloudpanel_run_all_dual_sample_operators.sh" 'write-dryrun'
check 'write dry-run evidence README keeps PHP authoritative' contains "$ROOT/docs/migration/evidence/write-dryruns/README.md" 'PHP ajax endpoints remain authoritative'
check 'catalog miss evidence dir exists' test -d "$ROOT/docs/migration/evidence/catalog-miss-umapi"
check 'catalog miss evidence keeps PHP fill authoritative' contains "$ROOT/docs/migration/evidence/catalog-miss-umapi/README.md" 'Live fills remain PHP'
check 'catalog miss compare refuses cutover' contains "$ROOT/scripts/compare_catalog_miss_dual_samples.py" 'cutoverAllowed=false'
check 'catalog miss probe refuses cutover claim' contains "$ROOT/scripts/cloudpanel_probe_catalog_miss_path.sh" 'cutoverAllowed=false'
check 'deploy packs catalog miss compare' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_catalog_miss_dual_samples.py'
check 'deploy packs catalog miss probe' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_probe_catalog_miss_path.sh'
check 'Batch 5 plan documents miss harness' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'miss-path probe'
check 'Batch 5 plan documents miss-fill dry-run' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'catalog-miss-fill'
check 'hybrid UI dual-sample compare exists' test -x "$ROOT/scripts/compare_hybrid_ui_dual_samples.py"
check 'hybrid UI dual-sample capture exists' test -x "$ROOT/scripts/cloudpanel_capture_hybrid_ui_dual_samples.sh"
check 'hybrid UI evidence dir exists' test -d "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples"
check 'hybrid UI evidence keeps PHP authoritative' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/README.md" 'PHP-authoritative'
check 'hybrid UI compare refuses cutover' contains "$ROOT/scripts/compare_hybrid_ui_dual_samples.py" 'cutoverAllowed=false'
check 'hybrid UI compare result blocks cutover' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/compare-result.json" '"cutoverAllowed": false'
check 'hybrid UI inventory blocks tenant chrome cutover' contains "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/php-hybrid-authoritative-inventory.json" '"tenantChromePhp": true'
check 'deploy packs hybrid UI compare' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_hybrid_ui_dual_samples.py'
check 'deploy packs hybrid UI capture' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_capture_hybrid_ui_dual_samples.sh'
check 'deploy packs hybrid UI dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_hybrid_ui_dual_sample_operator.sh'
check 'deploy packs login-cookie dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_login_cookie_dual_sample_operator.sh'
check 'deploy packs catalog-miss dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_catalog_miss_dual_sample_operator.sh'
check 'deploy packs digest dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_digest_dual_sample_operator.sh'
check 'deploy packs all dual-sample operators helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_all_dual_sample_operators.sh'
check 'deploy packs module-function parity operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_module_function_parity_operator.sh'
check 'deploy packs module-function parity compare' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'compare_module_function_parity.py'
check 'deploy packs presentation recheck operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_presentation_recheck_operator.sh'
check 'deploy packs price-lookup dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_price_lookup_dual_sample_operator.sh'
check 'deploy packs catalog-api dual-sample operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_catalog_api_dual_sample_operator.sh'
check 'deploy packs catalog/API allowlist sync validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_catalog_api_allowlist_sync.py'
check 'deploy packs scaffold options example' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'ecomae-scaffold-options.example.json'
check 'deploy packs scaffold options validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_scaffold_options_example.py'
check 'deploy packs enterprise BOS scaffold guardrails' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_enterprise_bos_scaffold_guardrails.sh'
check 'deploy packs evidence cutover locks validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_migration_evidence_cutover_locks.py'
check 'deploy packs presentation/hybrid allowlist sync validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_presentation_hybrid_allowlist_sync.py'
check 'deploy packs surface digest allowlist sync validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_surface_digest_allowlist_sync.py'
check 'deploy packs YARP all-packs generator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'generate_all_yarp_design_examples.sh'
check 'parity plan documents hybrid UI dual-sample packs' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'dual-sample evidence packs for hybrid UIs'
check 'tenant safety law documents same-to-same' contains "$ROOT/docs/migration/TENANT_MIGRATION_SAFETY.md" 'Same-to-same / invisible migration'
check 'tenant safety law digests never replace UX' contains "$ROOT/docs/migration/TENANT_MIGRATION_SAFETY.md" 'never** replace tenant product chrome'
check 'parity plan hard rule same-to-same' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'Same-to-same / invisible migration'
check 'parity plan blocks Batch 6 premature cutover' contains "$ROOT/docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md" 'blocked / premature'
check 'same-to-same tenant verify script exists' test -x "$ROOT/scripts/cloudpanel_verify_tenant_hosts_still_php.sh"
check 'same-to-same verify refuses cutover' contains "$ROOT/scripts/cloudpanel_verify_tenant_hosts_still_php.sh" 'cutoverAllowed=false'
check 'tenant chrome probe rejects Batch 4 Blazor markers' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'StorefrontCartApp'
check 'tenant chrome probe rejects storefront orders-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'StorefrontOrdersApp'
check 'tenant chrome probe rejects storefront garage-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'StorefrontGarageApp'
check 'tenant chrome probe rejects storefront profile-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'StorefrontProfileApp'
check 'tenant chrome probe rejects storefront account-summary-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'StorefrontAccountSummaryApp'
check 'deploy packs same-to-same tenant verify' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_verify_tenant_hosts_still_php.sh'
check 'deploy packs tenant-safety operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_tenant_safety_operator.sh'
check 'deploy packs offline migration gate' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_offline_migration_gate.sh'
check 'deploy packs surface-field parity operator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_run_surface_field_parity_operator.sh'
check 'deploy packs platform.env scaffold key parity validator' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'validate_platform_env_scaffold_key_parity.py'
check 'deploy packs live tenant chrome probe' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_probe_live_tenant_php_chrome.sh'
check 'tenant safety operator verify note exists' test -f "$ROOT/docs/migration/evidence/tenant-safety/OPERATOR_VERIFY.md"
check 'tenant safety operator exists' test -f "$ROOT/scripts/cloudpanel_run_tenant_safety_operator.sh"
check 'tenant safety operator is executable' test -x "$ROOT/scripts/cloudpanel_run_tenant_safety_operator.sh"
check 'tenant chrome evidence blocks cutover' contains "$ROOT/docs/migration/evidence/tenant-safety/live-tenant-php-chrome.json" '"cutoverAllowed": false'
check 'tenant chrome evidence blocks PHP removal' contains "$ROOT/docs/migration/evidence/tenant-safety/live-tenant-php-chrome.json" '"readyForPhpRemoval": false'
check 'same-to-same verify evidence exists' test -f "$ROOT/docs/migration/evidence/tenant-safety/same-to-same-verify.json"
check 'same-to-same verify evidence blocks cutover' contains "$ROOT/docs/migration/evidence/tenant-safety/same-to-same-verify.json" '"cutoverAllowed": false'
check 'live tenant probe writes cutoverAllowed false' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" '"cutoverAllowed": False'
check 'presentation parity states digests not tenant UX' contains "$ROOT/docs/migration/PRESENTATION_PARITY.md" 'not** tenant product chrome'
check 'session parity reporter Batch 3 status' contains "$ROOT/aspnet/src/EcomAE.Platform/Auth/LegacySessionParityReporter.cs" 'login-bridge-hybrid-batch3-hardened'
check 'presentation nginx includes login routes' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/login'
check 'presentation installer expects login+OMS+CP meta+audit-log+ERP+sf+bos fleet family routes' contains "$ROOT/scripts/cloudpanel_install_presentation_app_shadows.sh" 'expected = 147'
check 'tenant chrome probe rejects erp cash-entries-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'ErpCashEntriesApp'
check 'presentation nginx includes /erp/cash-entries-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/cash-entries-app'
check 'erp cash-entries-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpCashEntriesApp'
check 'erp cash-entries Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor"
check 'tenant chrome probe rejects bos fleet-summary-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'BosFleetSummaryApp'
check 'presentation nginx includes /bos/fleet-summary-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /bos/fleet-summary-app'
check 'bos fleet-summary-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'BosFleetSummaryApp'
check 'bos fleet-summary Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"
check 'tenant chrome probe rejects erp dashboard-summary-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'ErpDashboardSummaryApp'
check 'presentation nginx includes /erp/dashboard-summary-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/dashboard-summary-app'
check 'erp dashboard-summary-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpDashboardSummaryApp'
check 'erp dashboard-summary Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpDashboardSummaryApp.razor"
check 'tenant chrome probe rejects cp dashboard-summary-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'CpDashboardSummaryApp'
check 'presentation nginx includes /cp/dashboard-summary-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/dashboard-summary-app'
check 'cp dashboard-summary-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelDashboardSummaryApp'
check 'cp dashboard-summary Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpDashboardSummaryApp.razor"
check 'tenant chrome probe rejects erp accounts-summary-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'ErpAccountsSummaryApp'
check 'presentation nginx includes /erp/accounts-summary-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/accounts-summary-app'
check 'erp accounts-summary-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpAccountsSummaryApp'
check 'erp accounts-summary Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpAccountsSummaryApp.razor"
check 'tenant chrome probe rejects erp inventory-stock-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'ErpInventoryStockApp'
check 'tenant chrome probe rejects bos tenants-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'BosTenantsApp'
check 'tenant chrome probe rejects bos fleet-health-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'BosFleetHealthApp'
check 'tenant chrome probe rejects bos fleet-readiness-app marker' contains "$ROOT/scripts/cloudpanel_probe_live_tenant_php_chrome.sh" 'BosFleetReadinessApp'
check 'presentation nginx includes /erp/inventory-stock-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/inventory-stock-app'
check 'presentation nginx includes /bos/tenants-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /bos/tenants-app'
check 'presentation nginx includes /bos/fleet-health-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /bos/fleet-health-app'
check 'presentation nginx includes /bos/fleet-readiness-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /bos/fleet-readiness-app'
check 'erp inventory-stock-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpInventoryStockApp'
check 'bos tenants-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'BosTenantsApp'
check 'bos fleet-health-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'BosFleetHealthApp'
check 'bos fleet-readiness-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'BosFleetReadinessApp'
check 'erp inventory-stock Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryStockApp.razor"
check 'bos tenants Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosTenantsApp.razor"
check 'bos fleet-health Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosFleetHealthApp.razor"
check 'bos fleet-readiness Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosFleetReadinessApp.razor"
check 'presentation installer treats example conf as allowlist' contains "$ROOT/scripts/cloudpanel_install_presentation_app_shadows.sh" 'Example conf is the allowlist'
check 'presentation nginx includes /storefront/search-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/search-app'
check 'presentation nginx includes /storefront/cart-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/cart-app'
check 'presentation nginx includes /storefront/orders-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/orders-app'
check 'presentation nginx includes /storefront/garage-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/garage-app'
check 'presentation nginx includes /storefront/profile-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/profile-app'
check 'presentation nginx includes /storefront/account-summary-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /storefront/account-summary-app'
check 'storefront search-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontSearchApp'
check 'storefront cart-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontCartApp'
check 'storefront orders-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontOrdersApp'
check 'storefront garage-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontGarageApp'
check 'storefront profile-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontProfileApp'
check 'storefront account-summary-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontAccountSummaryApp'
check 'storefront search Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"
check 'storefront cart Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor"
check 'storefront orders Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"
check 'storefront garage Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageApp.razor"
check 'storefront profile Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProfileApp.razor"
check 'storefront account-summary Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAccountSummaryApp.razor"
check 'storefront part search SQL uses prices_data' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'SelectStorefrontPartSearch'
check 'storefront cart SQL uses shop_carts' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'SelectStorefrontCartLines'
check 'CP users-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelUsersApp'
check 'CP groups-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelGroupsApp'
check 'CP users Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpUsersApp.razor"
check 'CP groups Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpGroupsApp.razor"
check 'presentation nginx includes /cp/users-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/users-app'
check 'presentation nginx includes /cp/groups-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/groups-app'
check 'CP orders route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelOrders'
check 'CP orders digest SQL selects shop_orders' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs" 'SelectCpShopOrders'
check 'CP abandoned-carts Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpAbandonedCartsApp.razor"
check 'presentation nginx includes /cp/abandoned-carts-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/abandoned-carts-app'
check 'surface digest nginx includes /cp/abandoned-carts' contains "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" 'location = /cp/abandoned-carts'
check 'abandoned-carts digest contract catalogued' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" '/cp/abandoned-carts'
check 'CP orders Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor"
check 'presentation nginx includes /cp/orders' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/orders'
check 'presentation nginx includes /erp/sales-orders-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/sales-orders-app'
check 'presentation nginx includes /erp/purchase-orders-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/purchase-orders-app'
check 'presentation nginx includes /erp/invoices-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/invoices-app'
check 'presentation nginx includes /erp/cash-accounts-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/cash-accounts-app'
check 'presentation nginx includes /erp/coa-accounts-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/coa-accounts-app'
check 'presentation nginx includes /erp/gl-journals-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/gl-journals-app'
check 'presentation nginx includes /erp/warehouses-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/warehouses-app'
check 'presentation nginx includes /erp/suppliers-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/suppliers-app'
check 'presentation nginx includes /erp/purchases-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /erp/purchases-app'
check 'presentation nginx includes /cp/modules-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/modules-app'
check 'presentation nginx includes /cp/pages-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/pages-app'
check 'presentation nginx includes /cp/menus-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/menus-app'
check 'presentation nginx includes /cp/tenants-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/tenants-app'
check 'presentation nginx includes /cp/currencies-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/currencies-app'
check 'presentation nginx includes /cp/storages-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/storages-app'
check 'presentation nginx includes /cp/admin-sessions-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/admin-sessions-app'
check 'presentation nginx includes /cp/api-clients-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/api-clients-app'
check 'presentation nginx includes /cp/config-items-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /cp/config-items-app'
check 'presentation nginx includes /bos/audit-log-app' contains "$ROOT/deploy/aspnet/nginx-presentation-app-shadow-example.conf" 'location = /bos/audit-log-app'
check 'CP modules-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelModulesApp'
check 'CP pages-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelPagesApp'
check 'CP menus-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelMenusApp'
check 'CP tenants-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelTenantsApp'
check 'CP currencies-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelCurrenciesApp'
check 'CP storages-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelStoragesApp'
check 'CP admin-sessions-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelAdminSessionsApp'
check 'CP api-clients-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelApiClientsApp'
check 'CP config-items-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ControlPanelConfigItemsApp'
check 'BOS audit-log-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'BosAuditLogApp'
check 'CP modules Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpModulesApp.razor"
check 'CP pages Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpPagesApp.razor"
check 'CP menus Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpMenusApp.razor"
check 'CP tenants Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpTenantsApp.razor"
check 'CP currencies Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpCurrenciesApp.razor"
check 'CP storages Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpStoragesApp.razor"
check 'CP admin-sessions Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpAdminSessionsApp.razor"
check 'CP api-clients Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpApiClientsApp.razor"
check 'CP config-items Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/CpConfigItemsApp.razor"
check 'BOS audit-log Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/BosAuditLogApp.razor"
check 'ERP sales-orders-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpSalesOrdersApp'
check 'ERP purchase-orders-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpPurchaseOrdersApp'
check 'ERP invoices-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpInvoicesApp'
check 'ERP cash-accounts-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpCashAccountsApp'
check 'ERP coa-accounts-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpCoaAccountsApp'
check 'ERP gl-journals-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpGlJournalsApp'
check 'ERP warehouses-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpWarehousesApp'
check 'ERP suppliers-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpSuppliersApp'
check 'ERP purchases-app route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'ErpPurchasesApp'
check 'ERP purchase-orders Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor"
check 'ERP invoices Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor"
check 'ERP cash-accounts Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpCashAccountsApp.razor"
check 'ERP coa-accounts Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpCoaAccountsApp.razor"
check 'ERP gl-journals Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpGlJournalsApp.razor"
check 'ERP warehouses Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpWarehousesApp.razor"
check 'ERP suppliers Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpSuppliersApp.razor"
check 'ERP purchases Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchasesApp.razor"
check 'ERP sales-orders Blazor page exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"
check 'orders digest contract catalogued' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" '/cp/orders-digest'
check 'cp orders item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/cp-orders-item-field-floor.json"
check 'cp orders item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/cp-orders-item-field-floor.json" '"cutoverAllowed": false'
check 'cp orders migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/cp-orders-digest.json" '"orderSum": 0.0'
check 'digest dual-sample locks cp-orders hybrid list items' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'HYBRID_LIST_ITEM_FIELDS'
check 'digest dual-sample locks bos-fleet-health sampleTenants' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'bos-fleet-health'
check 'bos fleet-health sampleTenants floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/bos-fleet-health-sample-tenants-floor.json"
check 'bos fleet-health sampleTenants floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/bos-fleet-health-sample-tenants-floor.json" '"cutoverAllowed": false'
check 'bos fleet-health migration golden has sample tenant sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/bos-fleet-health.json" '"siteKey": "www"'
check 'storefront search item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/storefront-search-item-field-floor.json"
check 'storefront search item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/storefront-search-item-field-floor.json" '"cutoverAllowed": false'
check 'storefront search migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/storefront-search.json" '"articleShow": "0 986 424 590"'
check 'storefront cart item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/surface-parity/storefront-cart-item-field-floor.json"
check 'storefront cart item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/surface-parity/storefront-cart-item-field-floor.json" '"cutoverAllowed": false'
check 'storefront cart migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/storefront-cart.json" '"countNeed": 1.0'
check 'digest dual-sample locks storefront-search list items' contains "$ROOT/scripts/compare_digest_dual_samples.py" '"storefront-search"'
check 'digest dual-sample locks storefront-cart hybrid list items' contains "$ROOT/scripts/compare_digest_dual_samples.py" '"storefront-cart"'
check 'storefront search digest route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontSearch ='
check 'storefront cart digest route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" 'StorefrontCart ='
check 'catalog manufacturers contract requires MFA_ID' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" 'MFA_ID'
check 'catalog manufacturers migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-manufacturers.json" '"MFA_ID": 1'
check 'catalog api contract floor locks manufacturer item fields' contains "$ROOT/scripts/compare_catalog_api_contract_floor.py" 'LIST_ITEM_FIELDS'
check 'price lookup contract-only requires nonempty offers' contains "$ROOT/scripts/compare_price_lookup_parity.py" 'non-empty for contract floor sentinel'
check 'catalog manufacturers item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-api/manufacturers-item-field-floor.json"
check 'catalog manufacturers item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/catalog-api/manufacturers-item-field-floor.json" '"cutoverAllowed": false'
check 'catalog list item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-api/list-item-field-floor.json"
check 'catalog list item-field floor tracks 6 stems' contains "$ROOT/docs/migration/evidence/catalog-api/list-item-field-floor.json" '"listStemCount": 6'
check 'catalog models migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-models.json" '"model_series": "Migration Series"'
check 'catalog brands migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-brands.json" '"sup_id": 1'
check 'catalog brand-parts migration golden has item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-brand-parts.json" '"article_show": "0 986 479 001"'
check 'catalog models contract requires model_series' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" 'model_series'
check 'catalog brand-parts contract requires article_show' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" 'article_show'
check 'catalog VIN contract requires manufacturer envelope field' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs" 'manufacturer", "model_label", "payload"'
check 'catalog VIN migration golden includes manufacturer' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-vin.json" '"manufacturer"'
check 'catalog VIN envelope floor evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-api/vin-envelope-floor.json"
check 'catalog VIN envelope floor blocks cutover' contains "$ROOT/docs/migration/evidence/catalog-api/vin-envelope-floor.json" '"cutoverAllowed": false'
check 'catalog api contract floor locks offline-cache object data' contains "$ROOT/scripts/compare_catalog_api_contract_floor.py" 'OFFLINE_CACHE_OBJECT_DATA'
check 'catalog api contract floor locks offline-cache nested list items' contains "$ROOT/scripts/compare_catalog_api_contract_floor.py" 'OFFLINE_CACHE_NESTED_LIST_ITEM_FIELDS'
check 'catalog offline-cache nested item-field floor evidence exists' test -f "$ROOT/docs/migration/evidence/catalog-api/offline-cache-nested-item-field-floor.json"
check 'catalog offline-cache nested item-field floor blocks cutover' contains "$ROOT/docs/migration/evidence/catalog-api/offline-cache-nested-item-field-floor.json" '"cutoverAllowed": false'
check 'catalog offline-cache nested item-field floor tracks 7 nested list stems' contains "$ROOT/docs/migration/evidence/catalog-api/offline-cache-nested-item-field-floor.json" '"nestedListStemCount": 7'
check 'catalog offline-cache nested item-field floor covers all 10 object stems' contains "$ROOT/docs/migration/evidence/catalog-api/offline-cache-nested-item-field-floor.json" '"offlineCacheObjectDataStemCount": 10'
check 'catalog engines migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-engines.json" '"ENGINE_CODE": "N47D20"'
check 'catalog analogs migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-analogs.json" '"ARTICLE_NR": "0986424590"'
check 'catalog article-brands migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-article-brands.json" '"SEARCH_NUMBER": "0986424590"'
check 'catalog categories migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-categories.json" '"CATEGORY_NAME": "Migration category"'
check 'catalog products migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-products.json" '"ART_PRODUCT_NAME": "Migration product"'
check 'catalog articles migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-articles.json" '"ART_ARTICLE_NR": "0986424590"'
check 'catalog engine-search migration golden has nested item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-engine-search.json" '"code": "N47D20"'
check 'catalog article migration golden has object item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-article.json" '"TITLE": "Migration article"'
check 'catalog engine migration golden has object item sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-engine.json" '"ENG_ID": 1'
check 'catalog article-links migration golden has PC fitment sentinel' contains "$ROOT/docs/migration/evidence/surface-parity/samples/migration/api-catalog-article-links.json" '"CI_FROM": 0'
check 'catalog api contract floor locks offline-cache object item fields' contains "$ROOT/scripts/compare_catalog_api_contract_floor.py" 'OFFLINE_CACHE_OBJECT_ITEM_FIELDS'
check 'catalog api contract floor locks offline-cache section list items' contains "$ROOT/scripts/compare_catalog_api_contract_floor.py" 'OFFLINE_CACHE_SECTION_LIST_ITEM_FIELDS'
check 'orders digest migration golden exists' test -f "$ROOT/docs/migration/evidence/surface-parity/samples/migration/cp-orders-digest.json"
check 'digest dual compare accepts migration baseline' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'migrationBaselinePairs'
check 'digest dual compare detects seeded migration baseline' contains "$ROOT/scripts/compare_digest_dual_samples.py" 'migration-contract-golden'
check 'digest dual capture cleans seeded php baselines' contains "$ROOT/scripts/cloudpanel_capture_digest_dual_samples.sh" 'CLEAN seeded baseline'
check 'progress status reports storefront digests 4/6' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" '4 / 6'
check 'progress json reports storefrontDigestExactRoutesWired 6' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" '"storefrontDigestExactRoutesWired": 6'
check 'pre-PHP-removal parity verdict helper exists' test -x "$ROOT/scripts/verify_pre_php_removal_parity.sh"
check 'pre-PHP-removal verdict never removes PHP' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'NEVER removes PHP'
check 'pre-PHP-removal verdict skips nested heavy area suite' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'ECOMAE_AREA_SKIP_HEAVY=1'
check 'area tests honor ECOMAE_AREA_SKIP_HEAVY' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'ECOMAE_AREA_SKIP_HEAVY'
check 'area tests validate attached staging smoke' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'attached-staging-smoke'
check 'area tests require live surface digests from example' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'nginx-surface-digests-shadow-example.conf'
check 'area tests require 128 digest inventory' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" 'expected 128 digest routes'
check 'live surface links mark CP dashboard digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "CP dashboard digest"'
check 'live surface links mark CP tenants digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "CP tenants digest"'
check 'live surface links mark CP users digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "CP users digest"'
check 'live surface links mark CP groups digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "CP groups digest"'
check 'live surface links mark CP modules digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "CP modules digest"'
check 'live surface links mark ERP dashboard digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "ERP dashboard digest"'
check 'live surface links mark BOS audit-log digest shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "BOS audit-log digest"'
check 'progress status reports surface digests 128 wired' contains "$ROOT/docs/migration/inventory/ZERO_PHP_PROGRESS_STATUS.md" '128 / 128'
check 'progress json reports surfaceDigestExactRoutesWired 128' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" '"surfaceDigestExactRoutesWired": 128'
check 'exact-route installer retries CDN-cached PHP HTML' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'cache-bust'
check 'exact-route installer soft-OK when loopback ASP.NET' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'treating as soft-OK'
check 'area tests probe catalog status exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/status'
check 'area tests probe manufacturers exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/manufacturers'
check 'area tests probe models exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/models'
check 'area tests probe modifications exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/modifications'
check 'area tests probe brands exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/brands'
check 'area tests probe suppliers exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/suppliers'
check 'area tests probe vin exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/vin'
check 'area tests probe engines exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/engines'
check 'area tests probe analogs exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/analogs'
check 'area tests probe article-brands exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/article-brands'
check 'area tests probe categories exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/categories'
check 'area tests probe products exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/products'
check 'area tests probe engine-search exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/engine-search'
check 'pre-removal verdict loops catalog labels for aspnet-json' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" '("brand-parts", brand_parts)'
check 'pre-removal verdict allows digest aspnet-json exact-route' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'Digests may be exact-route shadowed'
check 'pre-removal stack probe wants brand-parts' contains "$ROOT/scripts/verify_pre_php_removal_parity.sh" 'api/v1/catalog/brand-parts'
check 'live surface stack probe includes brand-parts' contains "$ROOT/scripts/probe_live_surface_stack.sh" 'api/v1/catalog/brand-parts'
check 'live surface links mark manufacturers shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live'
check 'live surface links mark models shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog models'
check 'live surface links mark modifications shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog modifications'
check 'live surface links mark brands shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog brands'
check 'live surface links mark suppliers shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog suppliers'
check 'live surface links mark vin shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog VIN'
check 'live surface links mark engines shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog engines'
check 'live surface links mark analogs shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog analogs'
check 'live surface links mark article-brands shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog article-brands'
check 'live surface links mark categories shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog categories'
check 'live surface links mark products shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog products'
check 'live surface links mark engine-search shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog engine-search'
check 'live surface links mark article-links shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog article-links'
check 'live surface links mark article shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog article",'
check 'live surface links mark articles shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog articles"'
check 'live surface links mark engine (singular) shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog engine",'
check 'live surface links mark brand-parts shadow live' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/LiveSurfaceLinkReporter.cs" 'aspnet-exact-route-shadow-live", "Catalog brand-parts"'
check 'area tests probe article-links and article exact-routes' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/article-links /api/v1/catalog/article'
check 'area tests probe articles exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/article /api/v1/catalog/articles'
check 'area tests probe engine singular exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/articles /api/v1/catalog/engine'
check 'area tests probe brand-parts exact-route' contains "$ROOT/scripts/run_php_decommission_area_tests.sh" '/api/v1/catalog/engine /api/v1/catalog/brand-parts'
check 'exact-route installer accepts digest unauthorized gate' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'unauthorized'
check 'exact-route installer fallback proxies Cookie' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'proxy_set_header Cookie'
check 'stack probe includes manufacturers' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/manufacturers'
check 'stack probe includes models' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/models'
check 'stack probe includes modifications' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/modifications'
check 'stack probe includes brands' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/brands'
check 'stack probe includes suppliers' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/suppliers'
check 'stack probe includes vin' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/vin'
check 'stack probe includes engines' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/engines'
check 'stack probe includes analogs' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/analogs'
check 'stack probe includes article-brands' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/article-brands'
check 'stack probe includes categories' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/categories'
check 'stack probe includes products' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/products'
check 'stack probe includes engine-search' contains "$ROOT/scripts/probe_live_surface_stack.sh" '/api/v1/catalog/engine-search'
check 'warm vehicle ids helper lists vin cache' contains "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" 'epc_umapi_vin_cache'
check 'warm vehicle ids helper lists umapi cache' contains "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" 'epc_umapi_cache'
check 'catalog vehicle chain probe exists' test -x "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh"
check 'catalog vehicle chain probe reads MFA_ID' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" '("MFA_ID", "mfa_id")'
check 'catalog vehicle chain probe walks MFA_IDs' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'ECOMAE_VEHICLE_CHAIN_MAX_MFA'
check 'catalog vehicle chain probe hints epc_umapi_models' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'epc_umapi_models'
check 'catalog vehicle chain probe exports platform.env' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'set -a'
check 'catalog vehicle chain probe avoids printf -- options' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'Avoid `printf --stuff`'
check 'catalog vehicle chain probe falls back to loopback' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'ECOMAE_ASPNET_LOOPBACK'
check 'catalog vehicle chain probe prefers warm MFA list' contains "$ROOT/scripts/cloudpanel_probe_catalog_vehicle_chain.sh" 'cloudpanel_list_warm_catalog_models_mfa.sh'
check 'warm models MFA list helper exists' test -x "$ROOT/scripts/cloudpanel_list_warm_catalog_models_mfa.sh"
check 'warm vehicle ids helper lists modifications' contains "$ROOT/scripts/cloudpanel_list_warm_catalog_vehicle_ids.sh" 'epc_umapi_modifications'
check 'deploy packs warm models MFA list helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_list_warm_catalog_models_mfa.sh'
check 'deploy packs warm vehicle ids helper' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_list_warm_catalog_vehicle_ids.sh'
check 'exact-route installer accepts public ASP.NET when local SNI HTML' contains "$ROOT/scripts/cloudpanel_install_exact_route_shadow.sh" 'public URL serves ASP.NET JSON gate'
check 'deploy packs catalog vehicle chain probe' contains "$ROOT/scripts/deploy_aspnet_foundation.sh" 'cloudpanel_probe_catalog_vehicle_chain.sh'
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
check 'surface field parity probe contractCount is current' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-field-parity.json" '"contractCount": 149'
check 'surface field parity probe includes orders-digest' contains "$ROOT/docs/migration/evidence/decommission/public-probes/www-surface-field-parity.json" '"/cp/orders-digest"'
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
check 'zero php progress JSON next order mentions pre-removal parity verdict' contains "$ROOT/docs/migration/inventory/zero-php-progress-status.json" 'verify_pre_php_removal_parity.sh'
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
