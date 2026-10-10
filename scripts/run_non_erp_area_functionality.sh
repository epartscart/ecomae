#!/usr/bin/env bash
# Detailed Cursor-owned area functionality run (storefront / CP / BOS / auth / tenants).
# ERP finance stays Devin and is not in these filters.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-/opt/cursor/artifacts/non_erp_area_functionality.log}"
mkdir -p "$(dirname "$OUT")"
export PATH="${PATH}:/home/ubuntu/.dotnet"
cd "$ROOT/aspnet"
{
  echo "non-ERP area functionality $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "branch=$(git -C "$ROOT" rev-parse --abbrev-ref HEAD)"
  echo "sha=$(git -C "$ROOT" rev-parse --short HEAD)"
  echo
} | tee "$OUT"

run_area() {
  local name="$1"
  local filter="$2"
  echo "=== AREA ${name} filter=${filter} ===" | tee -a "$OUT"
  set +e
  dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo --no-restore \
    --filter "$filter" \
    --logger "console;verbosity=minimal" 2>&1 | tee -a "$OUT"
  local rc=${PIPESTATUS[0]}
  set -e
  echo "=== AREA ${name} exit=${rc} ===" | tee -a "$OUT"
  echo | tee -a "$OUT"
  return 0
}

dotnet test tests/EcomAE.Platform.Tests/EcomAE.Platform.Tests.csproj --nologo -v q >/dev/null
run_area "auth-social-otp" "FullyQualifiedName~PhpPlanQ1HullParityTests|FullyQualifiedName~PhpPlanQ1KeelParityTests|FullyQualifiedName~NonErpAreaFunctionalityTests|FullyQualifiedName~AuthEmailOtpTests|FullyQualifiedName~AuthOtpVerifyLoginTests|FullyQualifiedName~StorefrontOAuthButtonsTests|FullyQualifiedName~StorefrontOtpModalTests|FullyQualifiedName~StorefrontLoginFormTests|FullyQualifiedName~StorefrontLoginPostTests"
run_area "storefront-commerce" "FullyQualifiedName~StorefrontCart|FullyQualifiedName~StorefrontCheckout|FullyQualifiedName~StorefrontMyOrder|FullyQualifiedName~StorefrontCatalogue|FullyQualifiedName~StorefrontProduct|FullyQualifiedName~StorefrontRegister|FullyQualifiedName~StorefrontRegForm|FullyQualifiedName~StorefrontProfile"
run_area "cp-bos" "FullyQualifiedName~CpPhp|FullyQualifiedName~CpTopMenu|FullyQualifiedName~BosCommand|FullyQualifiedName~PhpPlanQ1Slip"
run_area "tenants-jobs-social" "FullyQualifiedName~PhpPlanQ1Dock|FullyQualifiedName~PhpPlanQ1Quay|FullyQualifiedName~PhpPlanQ1Pier|FullyQualifiedName~PhpPlanQ1Cove|FullyQualifiedName~CpTenantHost"
run_area "planq1-all" "FullyQualifiedName~PhpPlanQ1"
echo "wrote $OUT"
