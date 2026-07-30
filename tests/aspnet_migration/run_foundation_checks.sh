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
check 'program maps CP placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.ControlPanel'
check 'program maps ERP placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.Erp'
check 'program maps BOS placeholder' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'EcomAeRoutes.Bos'
check 'configuration tenant registry exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Services/ConfigurationTenantRegistry.cs"
check 'tenant registry interface exists' test -f "$ROOT/aspnet/src/EcomAE.Platform/Services/ITenantRegistry.cs"
check 'appsettings seeds platform tenant' contains "$ROOT/aspnet/src/EcomAE.Platform/appsettings.json" 'www.ecomae.com'
check 'appsettings seeds ERP-only tenant' contains "$ROOT/aspnet/src/EcomAE.Platform/appsettings.json" 'ErpOnlyTenant'
check 'authorization policies exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Security/EcomAePolicies.cs"
check 'route constants exist' test -f "$ROOT/aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"
check 'program registers tenant registry' contains "$ROOT/aspnet/src/EcomAE.Platform/Program.cs" 'AddSingleton<ITenantRegistry, ConfigurationTenantRegistry>'
check 'unit tests classify tenant ERP-only mode' contains "$ROOT/aspnet/tests/EcomAE.Platform.Tests/TenantResolutionTests.cs" 'TenantMode.ErpOnlyTenant'
check 'migration plan documents zero PHP final state' contains "$ROOT/docs/migration/ASP_NET_CORE_MIGRATION_PLAN.md" 'zero PHP files'

echo "----------------------------"
echo "Passed: $pass  Failed: $fail"
exit $(( fail > 0 ? 1 : 0 ))
