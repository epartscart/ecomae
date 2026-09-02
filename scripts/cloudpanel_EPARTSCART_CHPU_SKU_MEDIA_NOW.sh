#!/usr/bin/env bash
# Publish CHPU Spec + Photos (PHP sku_media) for ePartsCart ASP.NET.
#
# CloudPanel root paste (pin by commit SHA):
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/b94953e2af44fdf4a76bb0dbe73efe88e666c707/scripts/cloudpanel_EPARTSCART_CHPU_SKU_MEDIA_NOW.sh'
#   TMP=/tmp/epartscart-chpu-sku-media-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   grep -q 'StorefrontSkuMedia' "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=cursor/chpu-fitment-sku-media-7529
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-sku-media-now.log
#   grep -E 'RESULT=|GATE_|SHA=|SKU_' /root/epartscart-chpu-sku-media-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-fitment-sku-media-7529}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU SKU MEDIA (SPEC+PHOTOS) FORCE LIVE ========"
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

grep -q 'IStorefrontSkuMediaService' aspnet/src/EcomAE.Platform/Storefront/StorefrontSkuMediaService.cs \
  || die "missing StorefrontSkuMediaService"
grep -q 'StorefrontSkuMedia' aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs \
  || die "missing sku-media route"
grep -q 'epc-spec-check-btn' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing Spec button markup"
grep -q 'epc-sku-media-part-page' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing Photos gallery markup"
grep -q '20260812-fitment-sku' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing fitment-sku cache bust"
grep -q 'window.epcOpenSpecSplash' content/general_pages/epc_warehouse_search_parity.js \
  || die "missing Spec splash JS"
grep -q '/platform-assets/epc_sku_media.css' aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs \
  || die "missing sku media CSS asset map"

if [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
    bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-chpu-sku-media-force-live.log | tail -80
else
  die "missing FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_CHPU_SKU_MEDIA_PROVE.sh 2>&1 | tee /root/epartscart-chpu-sku-media-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-sku-media-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_SKU_MEDIA SHA=${SHA}"
