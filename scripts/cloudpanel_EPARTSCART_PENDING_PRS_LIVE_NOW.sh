#!/usr/bin/env bash
# ONE CloudPanel paste after merging pending ePartsCart PRs (#1005–#1012 family).
# Merge alone does NOT republish :5100 — this FORCE_LIVE / LIVE_PUBLISH does.
#
# Prerequisites: merge #1006 + #1009 + #1012 (and any other open storefront/CP PRs) into main.
#
# As root on CloudPanel:
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_PENDING_PRS_LIVE_NOW.sh'
#   TMP=/tmp/epartscart-pending-prs-live-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   grep -q EPARTSCART_PENDING_PRS_LIVE_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   export ECOMAE_BRANCH=main
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-pending-prs-live-now.log
#   grep -E 'RESULT=|GATE_|SHA=|PROVE_|PUBLISH_' /root/epartscart-pending-prs-live-now.log | tail -120
set -euo pipefail

printf '======== EPARTSCART_PENDING_PRS_LIVE_NOW ========\n'
printf 'HOST=%s DATE_UTC=%s UID=%s\n' \
  "$(hostname -f 2>/dev/null || hostname)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$(id -u)"
die() { printf 'RESULT=FAIL %s\n' "$*" >&2; exit 1; }
[[ "$(id -u)" -eq 0 ]] || die "must_run_as_root_on_CloudPanel"

BRANCH="${ECOMAE_BRANCH:-main}"
export ECOMAE_BRANCH="$BRANCH"
export ECOMAE_EPARTSCART_SHOP_DB="${ECOMAE_EPARTSCART_SHOP_DB:-docpart}"
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
export ECOMAE_SKIP_CP_LOGIN_DIAG="${ECOMAE_SKIP_CP_LOGIN_DIAG:-YES}"
export ECOMAE_ALLOW_PHP_REFERENCE_503="${ECOMAE_ALLOW_PHP_REFERENCE_503:-YES}"
export ECOMAE_SOFT_JOURNEY_HOLDOUTS="${ECOMAE_SOFT_JOURNEY_HOLDOUTS:-YES}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  [[ -n "$d" && -d "$d/.git" ]] && REPO="$d" && break
done
[[ -n "$REPO" ]] || { mkdir -p /opt; git clone https://github.com/epartscart/ecomae.git /opt/ecomae-aspnet-source; REPO=/opt/ecomae-aspnet-source; }

cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$BRANCH" || die "git_fetch_failed $BRANCH"
git checkout -f "$BRANCH" || die "git_checkout_failed"
git reset --hard "origin/$BRANCH" || die "git_reset_failed"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s BRANCH=%s SHOP_DB=%s\n' "$REPO" "$SHA" "$BRANCH" "$ECOMAE_EPARTSCART_SHOP_DB"

# Preflight markers from pending PR family (fail fast if wrong tip / not merged yet).
need() { grep -q "$2" "$1" || die "missing_marker $1 :: $2"; }
need content/general_pages/epc_warehouse_search_parity.js 'include_crossbase=1'
need content/general_pages/epc_warehouse_search_parity.js 'confirmWrites'
need aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs 'uniqueCrossbase'
need aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor '__epcPriceUnmaskRepoll'
need aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor 'GroupWarehouseOffers'
need aspnet/src/EcomAE.Platform/Components/Pages/CpUsersApp.razor 'epc-scp-users-workspace'
need aspnet/src/EcomAE.Platform/Storefront/StorefrontPriceAccess.cs 'ValidateCustomerAsync'
printf 'PREFLIGHT_MARKERS=OK\n'

if [[ -f scripts/lib/nginx_safe_bak.py ]]; then
  python3 scripts/lib/nginx_safe_bak.py prune 2>&1 | tee /root/epartscart-nginx-bak-prune.log || true
fi

# Warm AISIN/DT068 crossbase cache so first prove is not HTTP-dependent.
for cdir in \
  content/shop/docpart/cache/crossbase \
  /var/www/epartscart_com/htdocs/content/shop/docpart/cache/crossbase \
  /home/epartscart/htdocs/www.epartscart.com/content/shop/docpart/cache/crossbase
do
  mkdir -p "$cdir" 2>/dev/null || true
  if [[ -d "$cdir" && -w "$cdir" ]]; then
    curl -fsSL --max-time 8 'https://crossbase.ru/cross/?q=DT068' -o "${cdir}/DT068.html" \
      && printf 'WARM_CROSSBASE=%s/DT068.html\n' "$cdir" || true
  fi
done

PUBLISH=""
if [[ -f scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  PUBLISH=scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh
elif [[ -f scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  PUBLISH=scripts/cloudpanel_FORCE_LIVE_NOW.sh
else
  die "no LIVE_PUBLISH / FORCE_LIVE script"
fi

printf '\n---- PUBLISH (%s) ----\n' "$PUBLISH"
set +e
ECOMAE_BRANCH="$BRANCH" ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
  bash "$PUBLISH" 2>&1 | tee /root/epartscart-pending-prs-publish.log | tail -100
PUB_RC=${PIPESTATUS[0]}
set -e
printf 'PUBLISH_EXIT=%s\n' "$PUB_RC"
[[ "$PUB_RC" -eq 0 ]] || die "publish_failed — see /root/epartscart-pending-prs-publish.log"

run_prove() {
  local script="$1"
  local label="$2"
  if [[ ! -f "$script" ]]; then
    printf 'PROVE_SKIP %s missing=%s\n' "$label" "$script"
    return 0
  fi
  printf '\n---- PROVE %s ----\n' "$label"
  set +e
  bash "$script" 2>&1 | tee "/root/epartscart-prove-${label}.log"
  local rc=${PIPESTATUS[0]}
  set -e
  if grep -q 'RESULT=PASS' "/root/epartscart-prove-${label}.log" 2>/dev/null; then
    printf 'PROVE_OK %s\n' "$label"
    return 0
  fi
  printf 'PROVE_BAD %s rc=%s\n' "$label" "$rc"
  return 1
}

FAIL=0
run_prove scripts/cloudpanel_EPARTSCART_CART_ADD_LOGIN_PROVE.sh cartadd || FAIL=1
run_prove scripts/cloudpanel_EPARTSCART_CHPU_WH_GROUP_PROVE.sh whgroup || FAIL=1
run_prove scripts/cloudpanel_EPARTSCART_PARTS_CROSS_PRICE_LOGIN_PROVE.sh crossprice || FAIL=1
# Users detail — lightweight live probe if dedicated prove missing.
if [[ -f scripts/cloudpanel_EPARTSCART_CP_USERS_DETAIL_NOW.sh ]]; then
  # NOW scripts republish; prefer a read-only probe here.
  printf '\n---- PROVE users-detail (HTTP) ----\n'
  set +e
  curl -sS -o /tmp/epc_users_app.html -D /tmp/epc_users_app.hdr -w 'USERS_HTTP=%{http_code}\n' --max-time 30 \
    'https://www.epartscart.com/cp/users-app' || true
  set -e
  if grep -Eiq 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_users_app.hdr \
    && grep -Eq 'epc-scp-users-workspace|users-app' /tmp/epc_users_app.html; then
    printf 'PROVE_OK users-detail\n'
  else
    printf 'PROVE_BAD users-detail\n'
    FAIL=1
  fi
fi

[[ "$FAIL" -eq 0 ]] || die "one_or_more_proves_failed — see /root/epartscart-prove-*.log"
printf 'RESULT=PASS EPARTSCART_PENDING_PRS_LIVE SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
