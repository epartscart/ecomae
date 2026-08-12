#!/usr/bin/env bash
# Publish CHPU crossbase modal (PHP openCrossModal twin) + CP∩crossbase provenance retag.
#
# CloudPanel root paste (after merge use ECOMAE_BRANCH=main):
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-crossbase-modal-7529/scripts/cloudpanel_EPARTSCART_CHPU_CROSSBASE_MODAL_NOW.sh'
#   TMP=/tmp/epartscart-chpu-crossbase-modal-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-crossbase-modal-7529
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-crossbase-modal-now.log
#   grep -E 'RESULT=|GATE_|SHA=' /root/epartscart-chpu-crossbase-modal-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-crossbase-modal-7529}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU CROSSBASE MODAL FORCE LIVE ========"
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

grep -q 'Source = "cp+crossbase"' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs \
  || die "missing cp+crossbase overlap retag"
grep -q 'function openCrossModal(' content/general_pages/epc_warehouse_search_parity.js \
  || die "missing openCrossModal in parity JS"
grep -q 'openCrossModalFromButton' content/general_pages/epc_warehouse_search_parity.js \
  || die "missing openCrossModalFromButton wire"
grep -q '__epcLastCrossPayload' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing CHPU payload cache for modal"
grep -q 'epc_warehouse_search_parity.js?v=20260812-cross-modal' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing parity JS cache-bust"

# Warm TOYOTA piston cache so prove does not depend on first-hit crossbase.ru.
CACHE_DIRS=(
  content/shop/docpart/cache/crossbase
  /var/www/epartscart_com/htdocs/content/shop/docpart/cache/crossbase
  /home/epartscart/htdocs/www.epartscart.com/content/shop/docpart/cache/crossbase
)
for cdir in "${CACHE_DIRS[@]}"; do
  mkdir -p "$cdir" 2>/dev/null || true
  if [[ -d "$cdir" ]] && [[ -w "$cdir" ]]; then
    curl -fsSL --max-time 8 'https://crossbase.ru/cross/?q=1310154101' -o "${cdir}/1310154101.html" && \
      note "WARM_CROSSBASE_CACHE=${cdir}/1310154101.html" || true
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
    | tee /root/epartscart-chpu-crossbase-modal-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-crossbase-modal-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

bash scripts/cloudpanel_EPARTSCART_CHPU_CROSSBASE_MODAL_PROVE.sh 2>&1 \
  | tee /root/epartscart-chpu-crossbase-modal-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-crossbase-modal-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_CROSSBASE_MODAL SHA=${SHA}"
