#!/usr/bin/env bash
# Local/operator final-gate checklist for Zero-PHP. Never removes PHP.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EVIDENCE="$ROOT/docs/migration/evidence/decommission"
SMOKE_DIR="$EVIDENCE/staging-smoke"
PARITY_DIR="$EVIDENCE/parity-samples"
pass=0
fail=0
skip=0

record_pass() { pass=$((pass + 1)); printf '  PASS  %s\n' "$1"; }
record_fail() { fail=$((fail + 1)); printf '  FAIL  %s\n' "$1"; }
record_skip() { skip=$((skip + 1)); printf '  SKIP  %s\n' "$1"; }

echo "== Zero-PHP final gate checklist =="
echo "Evidence root: $EVIDENCE"
echo "This script never removes PHP-FPM, PHP cron, or PHP rewrites."

[[ -d "$EVIDENCE" ]] && record_pass "decommission evidence directory exists" || record_fail "decommission evidence directory missing"
[[ -f "$ROOT/docs/migration/PHP_DECOMMISSION_READINESS.md" ]] && record_pass "PHP decommission readiness doc exists" || record_fail "readiness doc missing"
[[ -f "$ROOT/scripts/rollback_aspnet_foundation.sh" ]] && record_pass "rollback script exists" || record_fail "rollback script missing"
[[ -f "$ROOT/deploy/aspnet/nginx-price-lookup-shadow-example.conf" ]] && record_pass "price lookup exact-route shadow example exists" || record_fail "price shadow example missing"
[[ -f "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" ]] && record_pass "surface digest exact-route shadow example exists" || record_fail "surface shadow example missing"
[[ -x "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh" ]] && record_pass "price lookup smoke runner executable" || record_fail "price smoke runner missing"
[[ -x "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh" ]] && record_pass "catalog status smoke runner executable" || record_fail "catalog smoke runner missing"
[[ -x "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh" ]] && record_pass "surface digest smoke runner executable" || record_fail "surface smoke runner missing"

if [[ -f "$SMOKE_DIR/price-lookup-aspnet.json" ]]; then
  record_pass "attached price-lookup staging smoke artifact"
else
  record_skip "price-lookup-aspnet.json not attached yet (run opt-in smoke on staging, then copy into staging-smoke/)"
fi

if [[ -f "$SMOKE_DIR/catalog-status-aspnet.json" ]]; then
  record_pass "attached catalog-status staging smoke artifact"
else
  record_skip "catalog-status-aspnet.json not attached yet"
fi

if [[ -f "$SMOKE_DIR/surface-digests-aspnet.json" ]]; then
  record_pass "attached surface-digests staging smoke artifact"
else
  record_skip "surface-digests-aspnet.json not attached yet"
fi

parity_count=0
if [[ -d "$PARITY_DIR" ]]; then
  parity_count="$(find "$PARITY_DIR" -type f -name '*.json' | wc -l | tr -d ' ')"
fi
if [[ "$parity_count" -gt 0 ]]; then
  record_pass "attached $parity_count parity sample json file(s)"
else
  record_skip "no parity sample json files under parity-samples/ yet"
fi

if [[ -f "$EVIDENCE/RELEASE_OWNER_APPROVAL.md" ]] && grep -Fq 'APPROVED_TO_REMOVE_PHP_FALLBACK' "$EVIDENCE/RELEASE_OWNER_APPROVAL.md"; then
  record_pass "release-owner approval artifact present"
else
  record_skip "RELEASE_OWNER_APPROVAL.md with APPROVED_TO_REMOVE_PHP_FALLBACK not present (required for final 5%)"
fi

# Guardrail: refuse to claim PHP removable from this checklist alone.
if grep -Eq 'trueZeroPhpCompletionPercent.: 100' "$ROOT/docs/migration/inventory/zero-php-progress-status.json"; then
  record_fail "progress json claims 100% while final gate checklist is running"
else
  record_pass "progress json does not claim 100% Zero-PHP yet"
fi

if [[ "${RUN_PRICE_LOOKUP_SMOKE:-0}" == "1" ]]; then
  bash "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh" && record_pass "live price lookup smoke" || record_fail "live price lookup smoke"
else
  record_skip "live price lookup smoke not requested (set RUN_PRICE_LOOKUP_SMOKE=1)"
fi

if [[ "${RUN_CATALOG_STATUS_SMOKE:-0}" == "1" ]]; then
  bash "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh" && record_pass "live catalog status smoke" || record_fail "live catalog status smoke"
else
  record_skip "live catalog status smoke not requested (set RUN_CATALOG_STATUS_SMOKE=1)"
fi

if [[ "${RUN_SURFACE_DIGEST_SMOKE:-0}" == "1" ]]; then
  bash "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh" && record_pass "live surface digest smoke" || record_fail "live surface digest smoke"
else
  record_skip "live surface digest smoke not requested (set RUN_SURFACE_DIGEST_SMOKE=1)"
fi

echo "----------------------------"
echo "Passed: $pass  Skipped: $skip  Failed: $fail"
echo "Remaining Zero-PHP pending is PHP runtime decommission (5%) until staging artifacts + release-owner approval exist."
echo "Do NOT remove PHP-FPM/cron/rewrites from this script."
exit $(( fail > 0 ? 1 : 0 ))
