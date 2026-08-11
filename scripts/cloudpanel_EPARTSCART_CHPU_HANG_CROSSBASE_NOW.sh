#!/usr/bin/env bash
# Publish CHPU hang-fix + full crossbase merge for ePartsCart.
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-hang-crossbase-fast-7b3b/scripts/cloudpanel_EPARTSCART_CHPU_HANG_CROSSBASE_NOW.sh'
#   TMP=/tmp/epartscart-chpu-hang-crossbase-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-hang-crossbase-fast-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-hang-crossbase-now.log
#   grep -E 'RESULT=|GATE_|SHA=' /root/epartscart-chpu-hang-crossbase-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-hang-crossbase-fast-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU HANG+CROSSBASE FORCE LIVE ========"
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

grep -q 'include_crossbase=1' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing include_crossbase"
grep -q 'Never leave "Polling suppliers' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing finishPoll hang fix"
grep -q 'CrossbaseReferenceLoader' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs || die "missing CrossbaseReferenceLoader wiring"
grep -q 'Debounce' content/general_pages/epc_warehouse_search_parity.js || die "missing MutationObserver debounce"

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-hang-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-chpu-hang-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_CHPU_HANG_CROSSBASE_PROVE.sh 2>&1 | tee /root/epartscart-chpu-hang-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-hang-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_HANG_CROSSBASE SHA=${SHA}"
