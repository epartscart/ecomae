#!/usr/bin/env bash
# Offline migration gate: dual-sample operators + presentation/tenant floors + scaffold guardrails.
# Default needs no CloudPanel cookies/API keys. Always expects cutoverAllowed=false.
# Never invents RELEASE_OWNER_APPROVAL.md / MODULE_FUNCTION_TEST_PASS.md.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

FAIL=0
run_one() {
  local label="$1"
  shift
  echo ""
  echo "== ${label} =="
  if "$@"; then
    echo "OK ${label}"
  else
    echo "FAIL ${label}" >&2
    FAIL=1
  fi
}

echo "== EcomAE offline migration gate =="
echo "Law: same-to-same tenant chrome stays PHP; cutoverAllowed=false; no invented approval."

run_one "dual-sample operators" bash "$ROOT/scripts/cloudpanel_run_all_dual_sample_operators.sh"
run_one "presentation recheck" bash "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh"
run_one "tenant safety" bash "$ROOT/scripts/cloudpanel_run_tenant_safety_operator.sh"
run_one "enterprise BOS scaffold guardrails" bash "$ROOT/scripts/validate_enterprise_bos_scaffold_guardrails.sh"
run_one "platform.env scaffold key parity" python3 "$ROOT/scripts/validate_platform_env_scaffold_key_parity.py"

echo ""
if [[ "$FAIL" -ne 0 ]]; then
  echo "FAIL: offline migration gate (cutover still forbidden)"
  exit 1
fi
echo "PASS: offline migration gate green; cutoverAllowed=false; Batch 6 blocked"
exit 0
