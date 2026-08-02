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
[[ -f "$ROOT/deploy/aspnet/nginx-api-shadow-example.conf" ]] && record_pass "catalog/api exact-route shadow example exists" || record_fail "catalog/api shadow example missing"
[[ -f "$ROOT/deploy/aspnet/nginx-surface-digests-shadow-example.conf" ]] && record_pass "surface digest exact-route shadow example exists" || record_fail "surface shadow example missing"
[[ -f "$ROOT/deploy/aspnet/nginx-storefront-digests-shadow-example.conf" ]] && record_pass "storefront digest exact-route shadow example exists" || record_fail "storefront shadow example missing"
[[ -x "$ROOT/scripts/cloudpanel_commit_final_gate_smoke.sh" ]] && record_pass "CloudPanel smoke commit helper executable" || record_fail "cloudpanel_commit_final_gate_smoke.sh missing"
[[ -x "$ROOT/scripts/cloudpanel_validate_final_gate_env.sh" ]] && record_pass "CloudPanel smoke env validator executable" || record_fail "cloudpanel_validate_final_gate_env.sh missing"
[[ -x "$ROOT/scripts/wait_for_aspnet_health.sh" ]] && record_pass "ASP.NET health wait helper executable" || record_fail "wait_for_aspnet_health.sh missing"
[[ -x "$ROOT/scripts/cloudpanel_prepare_smoke_secrets.sh" ]] && record_pass "CloudPanel smoke secrets helper executable" || record_fail "cloudpanel_prepare_smoke_secrets.sh missing"
[[ -f "$ROOT/docs/migration/EXACT_ROUTE_PROMOTION_PRICE_CATALOG.md" ]] && record_pass "price/catalog exact-route promotion runbook exists" || record_fail "EXACT_ROUTE_PROMOTION_PRICE_CATALOG.md missing"
[[ -x "$ROOT/scripts/compare_catalog_status_parity.py" ]] && record_pass "catalog status parity compare script executable" || record_fail "compare_catalog_status_parity.py missing"
[[ -x "$ROOT/scripts/compare_digest_dual_samples.py" ]] && record_pass "digest dual-sample compare script executable" || record_fail "compare_digest_dual_samples.py missing"
[[ -x "$ROOT/tests/live_smoke/run_price_lookup_exact_route_smoke.sh" ]] && record_pass "price lookup smoke runner executable" || record_fail "price smoke runner missing"
[[ -x "$ROOT/tests/live_smoke/run_catalog_status_exact_route_smoke.sh" ]] && record_pass "catalog status smoke runner executable" || record_fail "catalog smoke runner missing"
[[ -x "$ROOT/tests/live_smoke/run_surface_digest_exact_route_smoke.sh" ]] && record_pass "surface digest smoke runner executable" || record_fail "surface smoke runner missing"
[[ -x "$ROOT/scripts/cloudpanel_capture_final_gate_artifacts.sh" ]] && record_pass "CloudPanel final-gate capture script executable" || record_fail "CloudPanel capture script missing"
[[ -x "$ROOT/scripts/probe_live_surface_stack.sh" ]] && record_pass "live surface stack probe executable" || record_fail "live surface stack probe missing"
[[ -x "$ROOT/scripts/cloudpanel_php_decommission.sh" ]] && record_pass "gated PHP decommission script executable" || record_fail "cloudpanel_php_decommission.sh missing"
[[ -x "$ROOT/scripts/run_php_decommission_area_tests.sh" ]] && record_pass "final-gate area test runner executable" || record_fail "run_php_decommission_area_tests.sh missing"
[[ -f "$ROOT/docs/migration/LIVE_SURFACE_LINKS.md" ]] && record_pass "live surface links catalog doc exists" || record_fail "LIVE_SURFACE_LINKS.md missing"

if [[ -f "$EVIDENCE/public-probes/www-zero-php-completion.json" && -f "$EVIDENCE/public-probes/www-php-decommission-readiness.json" ]]; then
  record_pass "public production diagnostic probes attached"
else
  record_skip "public probes not attached under public-probes/"
fi

if [[ -f "$SMOKE_DIR/price-lookup-aspnet.json" ]] && python3 - "$SMOKE_DIR/price-lookup-aspnet.json" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
if doc.get("ok") is False:
    raise SystemExit(1)
err = doc.get("error")
if isinstance(err, dict) and err.get("code") in {"missing_api_key", "unauthorized", "invalid_api_key"}:
    raise SystemExit(1)
PY
then
  record_pass "attached validated price-lookup staging smoke artifact"
else
  record_skip "price-lookup-aspnet.json missing or unauthenticated (run opt-in smoke, then attach)"
fi

if [[ -f "$SMOKE_DIR/catalog-status-aspnet.json" ]] && python3 - "$SMOKE_DIR/catalog-status-aspnet.json" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
if doc.get("ok") is False or isinstance(doc.get("error"), dict):
    raise SystemExit(1)
for key in ("connected", "counts", "source"):
    if key not in doc:
        raise SystemExit(1)
PY
then
  record_pass "attached validated catalog-status staging smoke artifact"
else
  record_skip "catalog-status-aspnet.json missing or invalid"
fi

if [[ -f "$SMOKE_DIR/surface-digests-aspnet.json" ]] && python3 - "$SMOKE_DIR/surface-digests-aspnet.json" <<'PY'
import json, sys
doc = json.load(open(sys.argv[1], encoding="utf-8"))
if doc.get("ok") is not True:
    raise SystemExit(1)
routes = doc.get("routes") or []
ok = any(
    isinstance(r, dict)
    and int(r.get("status") or 0) == 200
    and not str(r.get("route") or "").startswith("/migration/")
    for r in routes
)
raise SystemExit(0 if ok else 1)
PY
then
  record_pass "attached validated surface-digests staging smoke artifact"
else
  record_skip "surface-digests-aspnet.json missing or lacks authenticated digest HTTP 200"
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
