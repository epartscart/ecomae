#!/usr/bin/env bash
# Publish CHPU Fitment check button PHP parity for ePartsCart (ASP.NET Core).
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-fitment-php-parity-7b3b/scripts/cloudpanel_EPARTSCART_CHPU_FITMENT_NOW.sh'
#   TMP=/tmp/epartscart-chpu-fitment-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-fitment-php-parity-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-fitment-now.log
#   grep -E 'RESULT=|GATE_|SHA=|FITMENT_' /root/epartscart-chpu-fitment-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-fitment-php-parity-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU FITMENT FORCE LIVE ========"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ) BRANCH=${ECOMAE_BRANCH}"

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
[[ -n "$REPO" ]] || { mkdir -p /opt; git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source; REPO=/opt/ecomae-aspnet-source; }
cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
note "REPO=${REPO} SHA=${SHA}"

grep -q '/storefront/fitment?' content/general_pages/epc_warehouse_search_parity.js || die "missing /storefront/fitment in parity.js"
grep -q '/storefront/fitment-widget.js' content/general_pages/epc_warehouse_search_parity.js || die "missing fitment-widget.js wiring"
grep -q 'loadEpartscrossFitmentFallback' content/general_pages/epc_warehouse_search_parity.js || die "missing epartscross fallback"
grep -q 'StorefrontFitment' aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs || die "missing StorefrontFitment route map"
grep -q '20260812-fitment' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing fitment cache buster"

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-fitment-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-chpu-fitment-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_CHPU_FITMENT_PROVE.sh 2>&1 | tee /root/epartscart-chpu-fitment-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-fitment-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_FITMENT SHA=${SHA}"
