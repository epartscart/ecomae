#!/usr/bin/env bash
# Publish CHPU crossbase modal (PHP openCrossModal twin) + CP∩crossbase provenance retag.
#
# Prior paste updated the source-tree JS but left the running ASP.NET DLL stale
# (HTML still loaded ?v=20260812-cross-price which nginx had cached as the old
# focusCross-only body; API still returned only source=cp). This script forces
# a direct FORCE_LIVE publish + hard HTTP prove before RESULT=PASS.
#
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/chpu-crossbase-modal-7529/scripts/cloudpanel_EPARTSCART_CHPU_CROSSBASE_MODAL_NOW.sh'
#   TMP=/tmp/epartscart-chpu-crossbase-modal-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/chpu-crossbase-modal-7529
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-chpu-crossbase-modal-now.log
#   grep -E 'RESULT=|GATE_|SHA=|PUBLISH' /root/epartscart-chpu-crossbase-modal-now.log | tail -100
set -euo pipefail
if [[ "$(id -u)" -ne 0 ]]; then printf 'ERROR: must run as root\n' >&2; exit 1; fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/chpu-crossbase-modal-7529}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
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
FULL="$(git rev-parse HEAD)"
note "REPO=${REPO} SHA=${SHA} FULL=${FULL}"

grep -q 'Source = "cp+crossbase"' aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs \
  || die "missing cp+crossbase overlap retag in source"
grep -q 'function openCrossModal(' content/general_pages/epc_warehouse_search_parity.js \
  || die "missing openCrossModal in parity JS"
grep -q 'openCrossModalFromButton' content/general_pages/epc_warehouse_search_parity.js \
  || die "missing openCrossModalFromButton wire"
grep -q '__epcLastCrossPayload' aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing CHPU payload cache for modal"
grep -q 'epc_warehouse_search_parity.js?v=20260812-cross-modal' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor \
  || die "missing parity JS cache-bust in Razor"

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

# Direct FORCE_LIVE — do not soft-skip publish via journey holdouts.
[[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]] || die "FORCE_LIVE missing"
note "---- FORCE_LIVE_NOW (direct publish + restart :5100) ----"
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
  | tee /root/epartscart-chpu-crossbase-modal-force-live.log | tail -100
FORCE_RC=${PIPESTATUS[0]}
set -e
note "force_live_exit=${FORCE_RC}"
[[ "$FORCE_RC" -eq 0 ]] || die "FORCE_LIVE failed — see /root/epartscart-chpu-crossbase-modal-force-live.log"

# Drop stale nginx proxy cache entries for the old parity JS query string if present.
for cache_root in /var/cache/nginx /var/cache/nginx-proxy /var/lib/nginx/cache /var/cache/cloudpanel; do
  if [[ -d "$cache_root" ]]; then
    find "$cache_root" -type f -mtime -30 2>/dev/null | head -5 >/dev/null || true
    # Best-effort: reload nginx so open file caches drop; full purge varies by vhost.
    note "NGINX_CACHE_DIR=${cache_root}"
  fi
done
if command -v nginx >/dev/null 2>&1; then
  nginx -t 2>/dev/null && systemctl reload nginx 2>/dev/null || true
fi

# Immediate post-publish gates (before longer prove) — catch stale DLL early.
note "---- post-publish DLL/HTML gates ----"
HTML=/tmp/epc_chpu_cb_modal_post.html
CODE="$(curl -sS -A 'EcomAE-ChpuCrossModalNow/1.0' -o "$HTML" -w '%{http_code}' --max-time 45 \
  'https://www.epartscart.com/en/parts/TOYOTA/1310154101' || echo 000)"
[[ "$CODE" == "200" ]] || die "CHPU HTTP=$CODE after publish"
grep -Fq 'epc_warehouse_search_parity.js?v=20260812-cross-modal' "$HTML" \
  || die "HTML still missing cross-modal cache-bust — ASP.NET DLL not republished"
grep -Fq '__epcLastCrossPayload' "$HTML" \
  || die "HTML missing __epcLastCrossPayload — Razor not republished"

JS=/tmp/epc_parity_post.js
curl -sS -A 'EcomAE-ChpuCrossModalNow/1.0' -o "$JS" --max-time 20 \
  'https://www.epartscart.com/platform-assets/epc_warehouse_search_parity.js?v=20260812-cross-modal' || true
grep -q 'function openCrossModal(' "$JS" || die "parity JS missing openCrossModal after publish"
# Old bust must not keep serving the focusCross-only body after reload.
OLD_JS=/tmp/epc_parity_oldbust.js
curl -sS -A 'EcomAE-ChpuCrossModalNow/1.0' -o "$OLD_JS" --max-time 20 \
  'https://www.epartscart.com/platform-assets/epc_warehouse_search_parity.js?v=20260812-cross-price' || true
if grep -q 'function openCrossModal(' "$OLD_JS"; then
  note "GATE_OK OLD_BUST_ALSO_NEW=YES"
else
  note "WARN: old ?v=20260812-cross-price still cached without modal — HTML now uses new bust so OK"
fi

API=/tmp/epc_toyota_cross_post.json
curl -sS -A 'EcomAE-ChpuCrossModalNow/1.0' -o "$API" --max-time 25 \
  'https://www.epartscart.com/storefront/cross-search?article=1310154101&brand=TOYOTA&limit=600&include_crossbase=1' || true
python3 - <<'PY' || die "API missing crossbase provenance after publish"
import json
from collections import Counter
d=json.load(open("/tmp/epc_toyota_cross_post.json"))
refs=d.get("references") or []
sources=Counter(str(r.get("source") or "") for r in refs)
tagged=sum(1 for r in refs if "crossbase" in str(r.get("source") or "").lower())
print("API sources", dict(sources), "crossbase_count", d.get("crossbase_count"), "tagged", tagged)
assert int(d.get("crossbase_count") or 0) > 0
assert tagged > 0, "expected cp+crossbase and/or crossbase sources after retag"
print("GATE_OK API_PROVENANCE=YES")
PY

bash scripts/cloudpanel_EPARTSCART_CHPU_CROSSBASE_MODAL_PROVE.sh 2>&1 \
  | tee /root/epartscart-chpu-crossbase-modal-prove.log
grep -q 'RESULT=PASS' /root/epartscart-chpu-crossbase-modal-prove.log || die "prove failed"
note "RESULT=PASS EPARTSCART_CHPU_CROSSBASE_MODAL SHA=${SHA}"
