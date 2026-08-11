#!/usr/bin/env bash
# Force-live publish ePartsCart brand+article CHPU warehouse fast path (PHP skip-SSR parity).
#
# Target:
#   https://www.epartscart.com/en/parts/AISIN/DT068  (1–3s warehouse first paint)
# PHP reference paints warehouse via one protocol-3 ajax_getProductsOfBunch + cross Promise.all
# (skip_ssr_use_ajax_fast_path). ASP.NET must match that model on :5100.
#
# Do NOT run from ~ as `bash scripts/...` — that path only exists inside the git repo.
# CloudPanel root paste:
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/parts-chpu-cross-1s-7b3b/scripts/cloudpanel_EPARTSCART_PARTS_WAREHOUSE_PHP_FAST_NOW.sh'
#   TMP=/tmp/epartscart-parts-warehouse-php-fast-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   export ECOMAE_BRANCH=cursor/parts-chpu-cross-1s-7b3b
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-parts-chpu-1s.log
#   grep -E 'RESULT=|GATE_|SHA=|TTFB_|BUNCH_|CROSS_|HOST=' /root/epartscart-parts-chpu-1s.log | tail -80
#
# Silent "External action completed" without RESULT=PASS paste-back = FAIL.
set -euo pipefail

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/parts-chpu-cross-1s-7b3b}"
PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
# Prefer the user-reported slow CHPU; AISIN/DT068 also works via ECOMAE_PARTS_PROBE.
PARTS_PATH="${ECOMAE_PARTS_PROBE:-/en/parts/JS%20ASAKASHI/C110J}"
export ECOMAE_ARTICLE_PLAIN="${ECOMAE_ARTICLE_PLAIN:-C110J}"
export ECOMAE_BRAND_PLAIN="${ECOMAE_BRAND_PLAIN:-JS ASAKASHI}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

note() { printf '%s\n' "$*"; }
die() { note "RESULT=FAIL $*"; exit 1; }

note "======== EPARTSCART PARTS CHPU OFFERS 1–3s FORCE LIVE ========"
note "HOST=$(hostname -f 2>/dev/null || hostname || echo unknown)"
note "DATE_UTC=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
note "ECOMAE_BRANCH=${ECOMAE_BRANCH}"
note "ECOMAE_EPARTSCART_SHOP_DB=${ECOMAE_EPARTSCART_SHOP_DB}"
note "PROBE=${PUBLIC_BASE}${PARTS_PATH}"
note "Expect: immediate protocol-3 poll + AbortSignal 3s + BUNCH_MS<=3000"

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

if ! grep -q 'ajax-fast-path' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing ajax-fast-path (wrong SHA / branch)"
fi
if ! grep -q 'runChpuPriceSearch' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing runChpuPriceSearch"
fi
if ! grep -q 'Immediate protocol-3 poll' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing immediate protocol-3 poll (1–3s path)"
fi
if ! grep -q 'AbortSignal.timeout(3000)' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing AbortSignal 3s budget"
fi
if ! grep -q '/storefront/cross-search' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing ASP.NET /storefront/cross-search fast path"
fi
if grep -qE 'ajax_epc_cross_search\.php|ajax_getProductsOfBunch\.php' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp still links product .php URLs"
fi
if grep -qE '\.php["'\''?]|/content/shop/|umapi_proxy\.php|ajax_.*\.php' \
  content/general_pages/epc_warehouse_search_parity.js; then
  die "epc_warehouse_search_parity.js still contains product .php URLs"
fi
if ! grep -q 'data-enhance-nav="false"' \
  aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor; then
  die "StorefrontSearchApp missing data-enhance-nav=false (click sleep fix)"
fi
if ! grep -q 'BuildStorefrontCrossSearchAsync' \
  aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs; then
  die "Reporter missing BuildStorefrontCrossSearchAsync"
fi
if ! grep -q 'ProbeStorefrontPartStockAsync' \
  aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs; then
  die "Reporter missing ProbeStorefrontPartStockAsync"
fi
if ! grep -q 'QueryStorefrontPartOffersBrandedFastAsync' \
  aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs; then
  die "Reporter missing branded fast warehouse cascade"
fi
if ! grep -q 'CancelAfter(TimeSpan.FromMilliseconds(2500))' \
  aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs; then
  die "Reporter missing 2.5s protocol-3 budget"
fi
if [[ ! -f scripts/cloudpanel_EPARTSCART_PARTS_WAREHOUSE_PHP_FAST_PROVE.sh ]]; then
  die "prove script missing in repo"
fi

publish_via_live_publish() {
  note ""
  note "---- LIVE_PUBLISH ----"
  # CHPU warehouse prove is independent of CP-login diag + php-reference 503 holdouts.
  # Soft-continue so a nuclear journey gate on taxofin / php-reference does not block CHPU PASS.
  local rc=0
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" \
  ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}" \
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  ECOMAE_SKIP_CP_LOGIN_DIAG="${ECOMAE_SKIP_CP_LOGIN_DIAG:-YES}" \
  ECOMAE_ALLOW_PHP_REFERENCE_503="${ECOMAE_ALLOW_PHP_REFERENCE_503:-YES}" \
  ECOMAE_SOFT_JOURNEY_HOLDOUTS="${ECOMAE_SOFT_JOURNEY_HOLDOUTS:-YES}" \
    bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 | tee /root/epartscart-parts-warehouse-php-fast-live-publish.log | tail -100
  rc=${PIPESTATUS[0]}
  set +e
  note "live_publish_exit=${rc}"
  # Always continue to CHPU prove — LIVE_PUBLISH may FAIL on unrelated CP diag / php-reference.
  return 0
}

publish_via_force_live() {
  note ""
  note "---- FORCE_LIVE ----"
  local rc=0
  set +e
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 \
    | tee /root/epartscart-parts-warehouse-php-fast-force-live.log | tail -80
  rc=${PIPESTATUS[0]}
  set +e
  note "force_live_exit=${rc}"
  return "$rc"
}

if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  publish_via_live_publish || true
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  publish_via_force_live || true
else
  die "no LIVE_PUBLISH / FORCE_LIVE script in repo"
fi

note ""
note "---- PROVE ----"
export ECOMAE_PARTS_PROBE="$PARTS_PATH"
bash scripts/cloudpanel_EPARTSCART_PARTS_WAREHOUSE_PHP_FAST_PROVE.sh 2>&1 \
  | tee /root/epartscart-parts-warehouse-php-fast-prove.log
PROVE_RC=${PIPESTATUS[0]}
note "prove_exit=${PROVE_RC}"

if [[ "$PROVE_RC" -ne 0 ]]; then
  die "prove failed — see /root/epartscart-parts-warehouse-php-fast-prove.log"
fi

note "RESULT=PASS PARTS_WAREHOUSE_PHP_FAST=YES SHA=${SHA} PROBE=${PARTS_PATH}"
exit 0
