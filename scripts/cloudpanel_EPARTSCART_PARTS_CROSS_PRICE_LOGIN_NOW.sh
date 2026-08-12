#!/usr/bin/env bash
# Publish CHPU crossbase reserve + logged-in price/term unmask for ePartsCart.
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/parts-chpu-cross-price-login-7b3b/scripts/cloudpanel_EPARTSCART_PARTS_CROSS_PRICE_LOGIN_NOW.sh'
#   TMP=/tmp/epartscart-parts-cross-price-login-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/parts-chpu-cross-price-login-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-parts-cross-price-login-now.log
#   grep -E 'RESULT=|GATE_|SHA=' /root/epartscart-parts-cross-price-login-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/parts-chpu-cross-price-login-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART PARTS CROSS+PRICE LOGIN FORCE LIVE ========"
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

grep -q 'safeLimit \* 0.65' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs || die "missing crossbase slot reserve"
grep -q 'uniqueCrossbase' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs || die "missing uniqueCrossbase merge"
grep -q 'Stale/invalid admin cookies must NOT wipe' aspnet/src/EcomAE.Platform/Auth/DbBackedLegacySessionValidator.cs || die "missing admin fall-through"
grep -q 'LegacySessionKind.Customer or LegacySessionKind.Admin' aspnet/src/EcomAE.Platform/Storefront/StorefrontPriceAccess.cs || die "missing admin price unlock"
grep -q '__epcPriceUnmaskRepoll' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor || die "missing price unmask repoll"
grep -q 'include_crossbase=1' content/general_pages/epc_warehouse_search_parity.js || die "missing parity JS include_crossbase"

# Warm DT068 crossbase disk cache so live merge does not depend on first-hit HTTP.
CACHE_DIRS=(
  content/shop/docpart/cache/crossbase
  /var/www/epartscart_com/htdocs/content/shop/docpart/cache/crossbase
  /home/epartscart/htdocs/www.epartscart.com/content/shop/docpart/cache/crossbase
)
for cdir in "${CACHE_DIRS[@]}"; do
  mkdir -p "$cdir" 2>/dev/null || true
  if [[ -d "$cdir" ]] && [[ -w "$cdir" ]]; then
    curl -fsSL --max-time 8 'https://crossbase.ru/cross/?q=DT068' -o "${cdir}/DT068.html" && \
      note "WARM_CROSSBASE_CACHE=${cdir}/DT068.html" || true
  fi
done

if [[ -f scripts/lib/nginx_safe_bak.py ]]; then
  python3 scripts/lib/nginx_safe_bak.py prune 2>&1 | tee /root/epartscart-nginx-bak-prune.log || true
fi

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-parts-cross-price-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/epartscart-parts-cross-price-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_PARTS_CROSS_PRICE_LOGIN_PROVE.sh 2>&1 | tee /root/epartscart-parts-cross-price-prove.log
grep -q 'RESULT=PASS' /root/epartscart-parts-cross-price-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_PARTS_CROSS_PRICE_LOGIN SHA=${SHA}"
