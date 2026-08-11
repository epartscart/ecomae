#!/usr/bin/env bash
# Publish + prove ePartsCart ASP.NET CHPU search faster than PHP-ajax display.
#
# Do NOT run from ~ as `bash scripts/...`.
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/epartscart-search-faster-than-php-7b3b/scripts/cloudpanel_EPARTSCART_SEARCH_FASTER_THAN_PHP_NOW.sh'
#   TMP=/tmp/epartscart-search-faster-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/epartscart-search-faster-than-php-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-search-faster-now.log
#   grep -E 'RESULT=|GATE_|SHA=|TTFB_|POB_' /root/epartscart-search-faster-now.log | tail -80
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/epartscart-search-faster-than-php-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART SEARCH FASTER THAN PHP FORCE LIVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo unknown)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "ECOMAE_BRANCH=${ECOMAE_BRANCH}"

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

grep -q 'SSR-seed local warehouse rows' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "StorefrontSearchApp missing SSR offer seed"
grep -q 'ResolveWarehouseBrandForArticleAsync' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs \
  || die "Reporter missing brand resolve"
grep -q '__epcChpuCrossBootstrapped' content/general_pages/epc_warehouse_search_parity.js \
  || die "parity.js missing cross dedupe"

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" \
  ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES \
  ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-search-faster-live-publish.log | tail -80
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/epartscart-search-faster-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

note "---- SEARCH SPEED PROVE ----"
bash scripts/cloudpanel_EPARTSCART_SEARCH_FASTER_THAN_PHP_PROVE.sh 2>&1 \
  | tee /root/epartscart-search-faster-prove.log

grep -E 'RESULT=' /root/epartscart-search-faster-prove.log | tail -5
grep -q 'RESULT=PASS' /root/epartscart-search-faster-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_SEARCH_FASTER SHA=${SHA}"
