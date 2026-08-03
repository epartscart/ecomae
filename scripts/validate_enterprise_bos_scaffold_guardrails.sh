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
for needle in \
  AddDbContext \
  AddReverseProxy \
  UseSerilog \
  AddOpenTelemetry \
  AddStackExchangeRedis \
  MapGraphQL \
  AddGrpc \
  AddRateLimiter \
  PublishAot \
  EcomAeRedisScaffoldOptions \
  EcomAeKafkaScaffoldOptions \
  EcomAePostgresScaffoldOptions
do
  check_not_contains "platform Program.cs omits $needle" "$PLATFORM_PROGRAM" "$needle"
done
check_not_contains "workers Program.cs omits AddDbContext" "$WORKERS_PROGRAM" "AddDbContext"
check_not_contains "workers Program.cs omits UseSerilog" "$WORKERS_PROGRAM" "UseSerilog"

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

# YARP regenerator still green.
if bash "$ROOT/scripts/generate_all_yarp_design_examples.sh" >/tmp/ecomae-yarp-guardrails.log 2>&1; then
  pass "generate_all_yarp_design_examples.sh"
else
  fail "generate_all_yarp_design_examples.sh (see /tmp/ecomae-yarp-guardrails.log)"
fi

# Operator helpers exist.
check_file "hybrid UI dual-sample operator helper" \
  "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh"
check_file "YARP all-packs generator helper" \
  "$ROOT/scripts/generate_all_yarp_design_examples.sh"

if [[ "$FAIL" -ne 0 ]]; then
  printf 'FAIL: Enterprise BOS scaffold guardrails\n'
  exit 1
fi
printf 'PASS: Enterprise BOS scaffold guardrails\n'
exit 0
