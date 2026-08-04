#!/usr/bin/env bash
# Enterprise BOS scaffolding guardrails.
# Ensures design-only artifacts stay disabled and Program.cs does not wire production clients.
# Never invents RELEASE_OWNER_APPROVAL.md. Always expects cutoverAllowed=false.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
FAIL=0

pass() { printf '  PASS  %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1"; FAIL=1; }

check_file() {
  local label="$1" path="$2"
  if [[ -f "$path" ]]; then pass "$label"; else fail "$label (missing $path)"; fi
}

check_contains() {
  local label="$1" path="$2" needle="$3"
  if [[ -f "$path" ]] && grep -qF -- "$needle" "$path"; then
    pass "$label"
  else
    fail "$label"
  fi
}

check_not_contains() {
  local label="$1" path="$2" needle="$3"
  if [[ -f "$path" ]] && ! grep -qF -- "$needle" "$path"; then
    pass "$label"
  else
    fail "$label"
  fi
}

printf '== Enterprise BOS scaffold guardrails ==\n'

# Program.cs must not wire scaffolding clients/exporters.
PLATFORM_PROGRAM="$ROOT/aspnet/src/EcomAE.Platform/Program.cs"
WORKERS_PROGRAM="$ROOT/aspnet/src/EcomAE.Workers/Program.cs"
PROGRAM_FORBIDDEN_NEEDLES=(
  AddDbContext
  AddDbContextPool
  AddNpgsql
  AddReverseProxy
  MapReverseProxy
  Yarp.ReverseProxy
  UseSerilog
  AddSerilog
  AddOpenTelemetry
  UseOpenTelemetry
  AddOtlpExporter
  UseOtlpExporter
  AddMeterProvider
  AddTracerProvider
  AddStackExchangeRedis
  AddStackExchangeRedisCache
  AddDistributedCache
  AddKafka
  Confluent
  MapGraphQL
  AddGrpc
  MapGrpcService
  AddRateLimiter
  EnableRateLimiting
  PublishAot
  EcomAeRedisScaffoldOptions
  EcomAeKafkaScaffoldOptions
  EcomAePostgresScaffoldOptions
)
for program_label in "platform:$PLATFORM_PROGRAM" "workers:$WORKERS_PROGRAM"; do
  label="${program_label%%:*}"
  program_path="${program_label#*:}"
  for needle in "${PROGRAM_FORBIDDEN_NEEDLES[@]}"; do
    check_not_contains "$label Program.cs omits $needle" "$program_path" "$needle"
  done
done

# Design packs keep cutover false.
for path in \
  "$ROOT/deploy/aspnet/ecomae-scaffold-options.example.json" \
  "$ROOT/deploy/aspnet/yarp-exact-routes-example.json" \
  "$ROOT/deploy/aspnet/yarp-surface-digests-example.json" \
  "$ROOT/deploy/aspnet/yarp-storefront-digests-example.json" \
  "$ROOT/deploy/aspnet/yarp-catalog-api-example.json"
do
  check_contains "$(basename "$path") blocks cutover" "$path" '"cutoverAllowed": false'
  check_contains "$(basename "$path") blocks PHP removal" "$path" '"readyForPhpRemoval": false'
done

check_contains "Helm platform values block cutover" \
  "$ROOT/deploy/aspnet/helm-ecomae-platform-example/values.yaml" "cutoverAllowed: false"
check_contains "Helm workers values disable writes" \
  "$ROOT/deploy/aspnet/helm-ecomae-workers-example/values.yaml" "allowWorkerWrites: false"
check_contains "Argo CD example blocks cutover" \
  "$ROOT/deploy/aspnet/gitops-example/argocd-application.example.yaml" 'ecomae.cutoverAllowed: "false"'

# Consolidated options validator.
if python3 "$ROOT/scripts/validate_scaffold_options_example.py" \
  --path "$ROOT/deploy/aspnet/ecomae-scaffold-options.example.json"; then
  pass "scaffold options example validator"
else
  fail "scaffold options example validator"
fi

# Evidence + allowlist sync validators.
if python3 "$ROOT/scripts/validate_migration_evidence_cutover_locks.py"; then
  pass "migration evidence cutover locks"
else
  fail "migration evidence cutover locks"
fi
if python3 "$ROOT/scripts/validate_presentation_hybrid_allowlist_sync.py"; then
  pass "presentation/hybrid allowlist sync"
else
  fail "presentation/hybrid allowlist sync"
fi
if python3 "$ROOT/scripts/validate_surface_digest_allowlist_sync.py"; then
  pass "surface/storefront digest allowlist sync"
else
  fail "surface/storefront digest allowlist sync"
fi
if python3 "$ROOT/scripts/validate_catalog_api_allowlist_sync.py"; then
  pass "catalog/API allowlist sync"
else
  fail "catalog/API allowlist sync"
fi
if python3 "$ROOT/scripts/validate_migration_golden_cutover_locks.py"; then
  pass "migration golden cutover locks"
else
  fail "migration golden cutover locks"
fi
if python3 "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"; then
  pass "platform.env scaffold key parity"
else
  fail "platform.env scaffold key parity"
fi
if python3 "$ROOT/scripts/validate_php_module_catalog_deeplink_floor.py"; then
  pass "php module catalog deeplink floor"
else
  fail "php module catalog deeplink floor"
fi
if python3 "$ROOT/scripts/build_surface_field_catalog_coverage_board.py"; then
  pass "php catalog surface-field coverage board"
else
  fail "php catalog surface-field coverage board"
fi
if python3 "$ROOT/scripts/validate_hybrid_directory_full_catalog_floor.py"; then
  pass "hybrid directory full catalog floor"
else
  fail "hybrid directory full catalog floor"
fi

# YARP regenerator still green.
if bash "$ROOT/scripts/generate_all_yarp_design_examples.sh" >/tmp/ecomae-yarp-guardrails.log 2>&1; then
  pass "generate_all_yarp_design_examples.sh"
else
  fail "generate_all_yarp_design_examples.sh (see /tmp/ecomae-yarp-guardrails.log)"
fi

# Operator helpers exist.
check_file "hybrid UI dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh"
check_file "login-cookie dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh"
check_file "catalog-miss dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh"
check_file "digest dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"
check_file "all dual-sample operators helper" \
  "$ROOT/scripts/cloudpanel_run_all_dual_sample_operators.sh"
check_file "module-function parity operator helper" \
  "$ROOT/scripts/cloudpanel_run_module_function_parity_operator.sh"
check_file "module-function parity compare helper" \
  "$ROOT/scripts/compare_module_function_parity.py"
check_file "presentation recheck operator helper" \
  "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh"
check_file "price-lookup dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh"
check_file "catalog-api dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh"
check_file "tenant-safety operator helper" \
  "$ROOT/scripts/cloudpanel_run_tenant_safety_operator.sh"
check_file "offline migration gate helper" \
  "$ROOT/scripts/cloudpanel_run_offline_migration_gate.sh"
check_file "surface-field parity operator helper" \
  "$ROOT/scripts/cloudpanel_run_surface_field_parity_operator.sh"
check_file "platform.env scaffold key parity validator" \
  "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"
check_file "operator verify index" \
  "$ROOT/docs/migration/evidence/OPERATOR_VERIFY.md"
check_file "YARP all-packs generator helper" \
  "$ROOT/scripts/generate_all_yarp_design_examples.sh"
check_file "php module catalog deeplink floor validator" \
  "$ROOT/scripts/validate_php_module_catalog_deeplink_floor.py"
check_file "php catalog coverage board builder" \
  "$ROOT/scripts/build_surface_field_catalog_coverage_board.py"
check_file "php catalog coverage board evidence" \
  "$ROOT/docs/migration/evidence/surface-parity/php-catalog-coverage-board.json"
check_file "cp menus item-field floor evidence" \
  "$ROOT/docs/migration/evidence/surface-parity/cp-menus-item-field-floor.json"
check_file "list digest item-field floor evidence" \
  "$ROOT/docs/migration/evidence/surface-parity/list-digest-item-field-floor.json"
check_file "hybrid directory full catalog floor validator" \
  "$ROOT/scripts/validate_hybrid_directory_full_catalog_floor.py"
check_file "hybrid directory full catalog floor evidence" \
  "$ROOT/docs/migration/evidence/hybrid-ui-dual-samples/hybrid-directory-full-catalog-floor.json"

if [[ "$FAIL" -ne 0 ]]; then
  printf 'FAIL: Enterprise BOS scaffold guardrails\n'
  exit 1
fi
printf 'PASS: Enterprise BOS scaffold guardrails\n'
exit 0
