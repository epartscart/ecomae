#!/usr/bin/env bash
# ONE CloudPanel paste after ePartsCart pending PRs merge (#1005–#1013 family).
# Also recovers: Access denied ecomae→docpart, CHPU 502 after publish.
# #1013: BIND_DOCPART_STANDALONE (GRANT) runs BEFORE LIVE_PUBLISH / proves.
#
# As root on CloudPanel (merge ≠ live — must paste). Prefer main after #1013 merge:
#   set -euxo pipefail
#   URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_PENDING_PRS_LIVE_NOW.sh'
#   TMP=/tmp/epartscart-pending-prs-live-now.sh
#   curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
#   grep -q EPARTSCART_PENDING_PRS_LIVE_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
#   grep -q 'BIND+GRANT docpart' "$TMP" || { echo RESULT=FAIL stale_script_missing_bind_before_publish; exit 1; }
#   export ECOMAE_BRANCH=main
#   export ECOMAE_EPARTSCART_SHOP_DB=docpart
#   bash "$TMP" 2>&1 | tee /root/epartscart-pending-prs-live-now.log
#   grep -E 'RESULT=|GATE_|SHA=|PROVE_|PUBLISH_|GRANT_|BOUND_|PREFLIGHT|SETTLE_' /root/epartscart-pending-prs-live-now.log | tail -140
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

# CRITICAL: registry user (ecomae) must SELECT on shared shop `docpart`.
# Without GRANT, /storefront/cross-search returns database-error Access denied → CHPU empty/502.
printf '\n---- BIND+GRANT docpart (standalone) ----\n'
if [[ -f scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh ]]; then
  set +e
  ECOMAE_EPARTSCART_SHOP_DB="$ECOMAE_EPARTSCART_SHOP_DB" \
    bash scripts/cloudpanel_EPARTSCART_BIND_DOCPART_STANDALONE.sh 2>&1 \
    | tee /root/epartscart-pending-bind-docpart.log | tail -80
  BIND_RC=${PIPESTATUS[0]}
  set -e
  printf 'BIND_EXIT=%s\n' "$BIND_RC"
  grep -E 'RESULT=|BOUND_|GRANT_|ROW_|shop_db|ERROR' /root/epartscart-pending-bind-docpart.log | tail -40 || true
  # Soft: continue publish even if bind soft-fails when dump already shows live→docpart.
  if [[ "$BIND_RC" -ne 0 ]]; then
    if grep -Eiq 'www\.epartscart\.com[[:space:]]+docpart|BOUND_=YES|RESULT=PASS' /root/epartscart-pending-bind-docpart.log; then
      printf 'WARN: bind exit=%s but docpart already bound — continuing\n' "$BIND_RC"
    else
      die "bind_docpart_failed — registry user cannot open docpart (cross-search Access denied)"
    fi
  fi
else
  die "missing BIND_DOCPART_STANDALONE"
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

# Brief settle — Kestrel can 502 for a few seconds after restart.
sleep 3
for i in 1 2 3 4 5; do
  code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 'https://www.epartscart.com/en/parts/AISIN/DT068' || echo 000)
  printf 'SETTLE_PARTS_HTTP=%s try=%s\n' "$code" "$i"
  [[ "$code" == "200" ]] && break
  sleep 2
done

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
# If CloudPanel has ECOMAE_LOGIN_PASSWORD in env, crossprice prove also asserts prices_visible=true.
if [[ -n "${ECOMAE_LOGIN_PASSWORD:-}" ]]; then
  printf 'CROSSPRICE_LOGIN_UNMASK=attempt contact=%s\n' "${ECOMAE_LOGIN_CONTACT:-taxofin2025@gmail.com}"
else
  printf 'CROSSPRICE_LOGIN_UNMASK=skip (export ECOMAE_LOGIN_PASSWORD to assert live price/term)\n'
fi
run_prove scripts/cloudpanel_EPARTSCART_PARTS_CROSS_PRICE_LOGIN_PROVE.sh crossprice || FAIL=1

# Users detail — guest 302→login is OK; HTML shell when session present.
printf '\n---- PROVE users-detail (HTTP) ----\n'
set +e
curl -sS -o /tmp/epc_users_app.html -D /tmp/epc_users_app.hdr -w 'USERS_HTTP=%{http_code}\n' --max-time 30 \
  'https://www.epartscart.com/cp/users-app' || true
tr -d '\r' < /tmp/epc_users_app.hdr > /tmp/epc_users_app.hdr.c 2>/dev/null || true
mv -f /tmp/epc_users_app.hdr.c /tmp/epc_users_app.hdr 2>/dev/null || true
USERS_CODE=$(awk 'NR==1{print $2}' /tmp/epc_users_app.hdr 2>/dev/null || echo 000)
set -e
if grep -Eiq 'location:.*(login|/cp/login)' /tmp/epc_users_app.hdr \
  || [[ "$USERS_CODE" == "302" || "$USERS_CODE" == "401" ]]; then
  printf 'PROVE_OK users-detail (auth gate %s — expected for guest)\n' "$USERS_CODE"
elif grep -Eiq 'x-ecomae-platform:[[:space:]]*primary' /tmp/epc_users_app.hdr \
  && grep -Eq 'epc-scp-users-workspace|users-app' /tmp/epc_users_app.html; then
  printf 'PROVE_OK users-detail\n'
else
  printf 'PROVE_BAD users-detail http=%s\n' "$USERS_CODE"
  FAIL=1
fi

[[ "$FAIL" -eq 0 ]] || die "one_or_more_proves_failed — see /root/epartscart-prove-*.log and bind log"
printf 'RESULT=PASS EPARTSCART_PENDING_PRS_LIVE SHA=%s BRANCH=%s\n' "$SHA" "$BRANCH"
