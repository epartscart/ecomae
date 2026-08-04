#!/usr/bin/env bash
# Run all dual-sample operator helpers (login, catalog-miss, digest, hybrid UI).
# Each helper asserts cutoverAllowed=false. Never invents RELEASE_OWNER_APPROVAL.md.
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
  local label="$1" script="$2"
  echo ""
  echo "== ${label} =="
  if bash "$script"; then
    echo "OK ${label}"
  else
    echo "FAIL ${label}" >&2
    FAIL=1
  fi
}

run_one "login-cookie" "$ROOT/scripts/cloudpanel_run_login_cookie_dual_sample_operator.sh"
run_one "catalog-miss" "$ROOT/scripts/cloudpanel_run_catalog_miss_dual_sample_operator.sh"
run_one "digest" "$ROOT/scripts/cloudpanel_run_digest_dual_sample_operator.sh"
run_one "hybrid-ui" "$ROOT/scripts/cloudpanel_run_hybrid_ui_dual_sample_operator.sh"
run_one "price-lookup" "$ROOT/scripts/cloudpanel_run_price_lookup_dual_sample_operator.sh"
run_one "catalog-api" "$ROOT/scripts/cloudpanel_run_catalog_api_dual_sample_operator.sh"
run_one "module-function" "$ROOT/scripts/cloudpanel_run_module_function_parity_operator.sh"

echo ""
if [[ "$FAIL" -ne 0 ]]; then
  echo "FAIL: one or more dual-sample operators failed (cutover still forbidden)"
  exit 1
fi
echo "PASS: all dual-sample operators completed with cutoverAllowed=false"
exit 0
