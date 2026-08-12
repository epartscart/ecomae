#!/usr/bin/env bash
# CloudPanel paste — republish CP/ERP digests so modules load docpart shop data (not empty).
#
# URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/cp-erp-modules-live-data-7b3b/scripts/cloudpanel_EPARTSCART_CP_ERP_LIVE_DATA_NOW.sh'
# TMP=/tmp/cp-erp-live-data-now.sh
# curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
# export ECOMAE_BRANCH=cursor/cp-erp-modules-live-data-7b3b
# bash "$TMP" 2>&1 | tee /root/cp-erp-live-data.log
# grep -E 'RESULT=|PASS |FAIL |SHA=|GATE_' /root/cp-erp-live-data.log | tail -100
set -euo pipefail
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/cp-erp-modules-live-data-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  git clone --depth 1 --branch "$ECOMAE_BRANCH" https://github.com/epartscart/ecomae.git /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi
cd "$REPO"
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s BRANCH=%s SHA=%s\n' "$REPO" "$ECOMAE_BRANCH" "$SHA"
chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>/dev/null || true
if [[ -x scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh ]]; then
  ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES bash scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh 2>&1 | tee /root/cp-erp-live-data-publish.log
else
  ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/cp-erp-live-data-publish.log
fi
fail=0
prove() {
  local name="$1" url="$2" needle="$3" expect_code="${4:-}"
  local body code
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' --max-redirs 0 "$url" || echo 000)"
  if [[ -n "$expect_code" && "$code" != "$expect_code" ]]; then
    # Allow auth redirect for HTML apps
    if [[ "$expect_code" == "302" && "$code" == "302" ]]; then
      :
    elif [[ "$code" != "$expect_code" ]]; then
      printf 'FAIL %s http=%s want=%s\n' "$name" "$code" "$expect_code"; fail=1; rm -f "$body"; return
    fi
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" "$body"; then
    # 302 Location header path — body may be empty; check redirect target via -D
    if [[ "$code" == "302" ]]; then
      local loc
      loc="$(curl -sS -I -A 'Mozilla/5.0' --max-redirs 0 "$url" | tr -d '\r' | awk -F': ' 'tolower($1)==\"location\"{print $2; exit}')"
      if [[ "$loc" == *"$needle"* ]]; then
        printf 'PASS %s http=%s loc=%s\n' "$name" "$code" "$loc"
        rm -f "$body"; return
      fi
    fi
    printf 'FAIL %s missing %q http=%s\n' "$name" "$needle" "$code"; fail=1; rm -f "$body"; return
  fi
  printf 'PASS %s http=%s\n' "$name" "$code"
  rm -f "$body"
}
BASE="${ECOMAE_EPARTSCART_BASE:-https://www.epartscart.com}"
# Blazor dashboard apps must 302 to login (NOT 401 JSON).
prove cp-dash-app-redirect "$BASE/cp/dashboard-summary-app" '/cp/login' 302
prove erp-dash-app-redirect "$BASE/erp/dashboard-summary-app" '/erp/login' 302
prove users-app-redirect "$BASE/cp/users-app" '/cp/login' 302
prove erp-shell-redirect "$BASE/erp" '/erp/login' 302
# Storefront shop DB still healthy (docpart).
prove storefront-brands "$BASE/storefront/search-brands?article=DA320" '"ok":true'
if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL SHA=%s\n' "$SHA"; exit 1
fi
printf '\nRESULT=PASS — CP/ERP live-data publish (SHA=%s)\n' "$SHA"
printf 'After admin login, open:\n'
printf '  %s/cp/users-app\n' "$BASE"
printf '  %s/cp/orders\n' "$BASE"
printf '  %s/cp/dashboard-summary-app\n' "$BASE"
printf '  %s/erp/dashboard-summary-app\n' "$BASE"
printf 'Expect non-zero KPIs / rows (source=database). Empty tables with source=database-error means GRANT/DB still wrong.\n'
exit 0
