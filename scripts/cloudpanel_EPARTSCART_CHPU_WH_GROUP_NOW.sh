#!/usr/bin/env bash
# Publish CHPU multi-warehouse grouping + left filter ranges (PHP part_search parity).
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-multi-warehouse-group-filters-7b3b/scripts/cloudpanel_EPARTSCART_CHPU_WH_GROUP_NOW.sh'
#   TMP=/tmp/epartscart-chpu-wh-group-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-multi-warehouse-group-filters-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-wh-group-now.log
#   grep -E 'RESULT=|GATE_|SHA=|WHG_' /root/epartscart-chpu-wh-group-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-multi-warehouse-group-filters-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU WAREHOUSE GROUP FORCE LIVE ========"
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

grep -q 'GroupWarehouseOffers' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing GroupWarehouseOffers"
grep -q 'epc-warehouse-subrow' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing subrow markup"
grep -q 'fillRangeInputsFromOffers' content/general_pages/epc_warehouse_search_parity.js || die "missing filter range fill"
grep -q 'epc_filter_exist_min' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing exist range inputs"
grep -q '20260812-whgroup' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing whgroup cache buster"

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-wh-group-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-chpu-wh-group-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_CHPU_WH_GROUP_PROVE.sh 2>&1 | tee /root/epartscart-chpu-wh-group-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-wh-group-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_WH_GROUP SHA=${SHA}"
