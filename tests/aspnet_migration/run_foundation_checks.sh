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
check 'worker placeholder names PHP job replacements' contains "$ROOT/aspnet/src/EcomAE.Workers/MigrationWorkerPlaceholder.cs" 'price import, sitemap, notifications, backups'
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
check 'progress report exposes 20 percent complete' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/MigrationProgressReporterTests.cs" 'Assert.Equal(20, report.OverallCompletePercent)'
check 'progress report blocks PHP removal' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/MigrationProgressReporter.cs" 'Production cutover and PHP removal'
check 'surface parity route constant exists' contains "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs" '/migration/surface-parity'
check 'surface parity reporter exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReporter.cs"
check 'program maps surface parity' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.SurfaceParity'
check 'surface parity tests cover ERP-only tenants' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/SurfaceParityReporterTests.cs" 'ERP-only tenant'
check 'surface parity report names fifty percent gate' contains "$ROOT/aspnet/src/EcomAE.Platform/Migration/SurfaceParityReport.cs" 'RequiredBeforeFiftyPercent'
check 'migration plan documents zero PHP final state' contains "$ROOT/docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md" 'zero PHP files'

echo "----------------------------"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
