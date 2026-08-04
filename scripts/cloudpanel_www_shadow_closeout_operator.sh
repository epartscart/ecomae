#!/usr/bin/env bash
# One-shot www shadow closeout on CloudPanel (storefront digests + marketing apps).
# Never broad cutover. Live / must remain PHP epm-hub until dual-sample + approval.
# Refuses without ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

if [[ "${ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT:-}" != "YES" ]]; then
  printf 'Refusing without ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES\n' >&2
  printf 'This operator installs www exact-route shadows only (storefront digests + /marketing/*).\n' >&2
  printf 'It does NOT remove PHP or claim cutoverAllowed=true.\n' >&2
  exit 2
fi

if [[ -f /etc/ecomae-aspnet/platform.env ]]; then
  set -a
  # shellcheck disable=SC1091
  source /etc/ecomae-aspnet/platform.env
  set +a
fi

FAIL=0
step() {
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

echo "www shadow closeout operator — cutoverAllowed stays false; PHP remains authoritative."

step "1/5 install storefront digest shadows (7 routes incl checkout)" \
  env ECOMAE_CONFIRM_INSTALL_STOREFRONT_DIGEST_SHADOWS=YES \
  bash "$ROOT/scripts/cloudpanel_install_storefront_digest_shadows.sh"

step "2/5 probe storefront digest shadows" \
  bash "$ROOT/scripts/cloudpanel_probe_storefront_digest_shadows.sh"

step "3/5 install marketing app shadows (/marketing/* exact-route)" \
  env ECOMAE_CONFIRM_INSTALL_MARKETING_APP_SHADOWS=YES \
  bash "$ROOT/scripts/cloudpanel_install_marketing_app_shadows.sh"

step "4/5 probe marketing shadows + assert live / still PHP epm-hub" \
  bash "$ROOT/scripts/cloudpanel_probe_marketing_app_shadows.sh"

echo ""
echo "== 4b/5 assert live www / still PHP epm-hub (not Blazor) =="
if bash "$ROOT/scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh"; then
  echo "OK live / remains PHP epm-hub"
else
  echo "FAIL live / must stay PHP epm-hub — aborting closeout" >&2
  FAIL=1
fi

if [[ "${ECOMAE_WWW_SHADOW_SKIP_PRESENTATION_RECHECK:-}" == "1" ]]; then
  echo ""
  echo "== 5/5 presentation recheck skipped (ECOMAE_WWW_SHADOW_SKIP_PRESENTATION_RECHECK=1) =="
else
  echo ""
  echo "== 5/5 presentation recheck (soft — never claims PHP removal) =="
  export ECOMAE_PRESENTATION_LIVE="${ECOMAE_PRESENTATION_LIVE:-0}"
  export ECOMAE_PRESENTATION_SOFT="${ECOMAE_PRESENTATION_SOFT:-1}"
  if bash "$ROOT/scripts/cloudpanel_run_presentation_recheck_operator.sh"; then
    echo "OK presentation recheck operator (soft/offline floor)"
  else
    echo "WARN presentation recheck did not pass — recorded soft-fail; PHP still authoritative" >&2
  fi
fi

echo ""
echo "== Next dual-sample steps (PHP delete still refused) =="
cat <<'EOF'
- Authenticated digest dual-sample: bash scripts/cloudpanel_run_digest_dual_sample_operator.sh
- Module-ajax contract floor: bash scripts/cloudpanel_run_module_ajax_dual_sample_operator.sh
- ERP/BOS ajax dry-run catalogs: bash scripts/cloudpanel_run_write_dryrun_dual_sample_operator.sh
- Hybrid UI + login-cookie samples: bash scripts/cloudpanel_run_all_dual_sample_operators.sh
- Functional live-smoke capture: docs/migration/evidence/decommission/functional-flows/LIVE_SMOKE_CAPTURE.md
- Tenant same-to-same: ECOMAE_TENANT_LIVE=1 bash scripts/cloudpanel_run_tenant_safety_operator.sh
- Human RELEASE_OWNER_APPROVAL.md only after ReadyToRemovePhp checklist green
- PHP runtime decommission: ECOMAE_CONFIRM_PHP_DECOMMISSION=YES bash scripts/cloudpanel_php_decommission_gated.sh
- PHP source deletion is a separate human PR — never agent-invented
EOF

if [[ "$FAIL" -ne 0 ]]; then
  echo ""
  echo "FAIL: www shadow closeout had hard step failure(s); cutover still forbidden" >&2
  exit 1
fi

echo ""
echo "PASS: www shadow closeout steps completed with cutoverAllowed=false"
exit 0
