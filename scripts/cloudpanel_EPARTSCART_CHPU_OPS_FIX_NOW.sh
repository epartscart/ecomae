#!/usr/bin/env bash
# Publish nginx ENAMETOOLONG bak fix + JA ASHIKA brand resolve + hang/crossbase prove.
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-nginx-bak-ja-ashika-7b3b/scripts/cloudpanel_EPARTSCART_CHPU_OPS_FIX_NOW.sh'
#   TMP=/tmp/epartscart-chpu-ops-fix-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-nginx-bak-ja-ashika-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-ops-fix-now.log
#   grep -E 'RESULT=|GATE_|SHA=|pruned=' /root/epartscart-chpu-ops-fix-now.log | tail -80
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-nginx-bak-ja-ashika-7b3b}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART CHPU OPS FIX FORCE LIVE ========"
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

grep -q 'ScoreWarehouseBrandMatch' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs \
  || die "missing JA ASHIKA ScoreWarehouseBrandMatch"
grep -q 'shared >= 4' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs \
  || die "missing LCS>=4 brand score"
grep -q 'nginx_safe_bak' scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh \
  || die "missing nginx_safe_bak in warmup"
test -f scripts/lib/nginx_safe_bak.py || die "missing scripts/lib/nginx_safe_bak.py"
grep -q 'include_crossbase=1' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing crossbase client"

# Prune ENAMETOOLONG nginx .bak litter BEFORE journey/warmup runs.
python3 scripts/lib/nginx_safe_bak.py prune 2>&1 | tee /root/epartscart-nginx-bak-prune.log || true

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" \
  ECOMAE_SKIP_LIFEOS_MP4=YES \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG=YES \
  ECOMAE_ALLOW_PHP_REFERENCE_503=YES \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS=YES \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-ops-live-publish.log | tail -100
  set +e
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/epartscart-chpu-ops-force-live.log | tail -60
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

note "---- CHPU HANG+CROSSBASE PROVE ----"
bash scripts/cloudpanel_EPARTSCART_CHPU_HANG_CROSSBASE_PROVE.sh 2>&1 \
  | tee /root/epartscart-chpu-ops-hang-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-ops-hang-prove.log || die "hang/crossbase prove failed"

note "---- SEARCH SPEED PROVE (JA ASHIKA soft OK) ----"
bash scripts/cloudpanel_EPARTSCART_SEARCH_FASTER_THAN_PHP_PROVE.sh 2>&1 \
  | tee /root/epartscart-chpu-ops-search-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-ops-search-prove.log || die "search prove failed"

note "RESULT=PASS EPARTSCART_CHPU_OPS_FIX SHA=${SHA}"
