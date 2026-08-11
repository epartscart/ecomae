#!/usr/bin/env bash
# Force-live publish Super ERP + Tenant ERP + ERP-only module PHP look parity.
#
# Do NOT run from ~ as `bash scripts/...`.
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/erp-modules-php-look-parity-7b3b/scripts/cloudpanel_ERP_MODULES_PHP_LOOK_PARITY_NOW.sh'
#   TMP=/tmp/erp-modules-php-look-parity-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/erp-modules-php-look-parity-7b3b
#   bash "$TMP" 2>&1 | tee /root/erp-modules-php-look-parity.log
#   grep -E 'RESULT=|GATE_|SHA=' /root/erp-modules-php-look-parity.log | tail -80
#
# Silent "External action completed" without RESULT=PASS paste-back = FAIL.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/erp-modules-php-look-parity-7b3b}"
PUBLIC_SUPER="${ECOMAE_PUBLIC_SUPER:-https://www.ecomae.com}"
PUBLIC_TENANT="${ECOMAE_PUBLIC_TENANT:-https://www.epartscart.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== ERP MODULES PHP LOOK PARITY FORCE LIVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo unknown)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "ECOMAE_BRANCH=${ECOMAE_BRANCH}"
note "Expect: epc_erp_aspnet_module_parity + PhpErpModulePageHeader on AR/AP + no CP link for ERP-only"

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  mkdir -p /opt
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
note "REPO=${REPO} SHA=${SHA}"

grep -q 'platform-assets/epc_erp_aspnet_module_parity.css' \
  aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs \
  || die "ErpStylesheets missing platform-assets parity CSS"
grep -q 'PhpErpModulePageHeader' \
  aspnet/src/EcomAE.Platform/Components/Pages/ErpReceivablesApp.razor \
  || die "Receivables missing PhpErpModulePageHeader"
grep -q 'PhpErpModulePageHeader' \
  aspnet/src/EcomAE.Platform/Components/Pages/ErpPayablesApp.razor \
  || die "Payables missing PhpErpModulePageHeader"
grep -q '_showControlPanel' \
  aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor \
  || die "ERP chrome missing ERP-only CP gate"
test -f content/shop/finance/epc_erp_aspnet_module_parity.css || die "parity CSS missing"
test -f scripts/cloudpanel_ERP_MODULES_PHP_LOOK_PARITY_PROVE.sh || die "prove script missing"

if [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  note ""
  note "---- FORCE_LIVE ----"
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/erp-modules-php-look-force-live.log | tail -80
  note "force_live_exit=${PIPESTATUS[0]}"
  set -e
elif [[ -f scripts/cloudpanel_find_and_redeploy.sh ]]; then
  note ""
  note "---- REDEPLOY ----"
  export ECOMAE_EMERGENCY_PUBLISH=1
  export ECOMAE_BRANCH
  bash scripts/cloudpanel_find_and_redeploy.sh 2>&1 | tee /root/erp-modules-php-look-redeploy.log | tail -80
  systemctl restart ecomae-platform.service || true
  bash scripts/wait_for_aspnet_health.sh || true
else
  die "no FORCE_LIVE / redeploy script"
fi

note ""
note "---- PROVE ----"
export ECOMAE_PUBLIC_SUPER="$PUBLIC_SUPER"
export ECOMAE_PUBLIC_TENANT="$PUBLIC_TENANT"
bash scripts/cloudpanel_ERP_MODULES_PHP_LOOK_PARITY_PROVE.sh 2>&1 \
  | tee /root/erp-modules-php-look-prove.log
PROVE_RC=${PIPESTATUS[0]}
note "prove_exit=${PROVE_RC}"
[[ "$PROVE_RC" -eq 0 ]] || die "prove failed"

note "RESULT=PASS ERP_MODULES_PHP_LOOK=YES SHA=${SHA}"
exit 0
