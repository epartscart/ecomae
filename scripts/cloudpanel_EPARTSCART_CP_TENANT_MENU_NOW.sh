#!/usr/bin/env bash
# CloudPanel paste — publish epartscart CP tenant menu (Shop OMS, hide jewellery).
#
# URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/cp-tenant-menu-epartscart-php-7b3b/scripts/cloudpanel_EPARTSCART_CP_TENANT_MENU_NOW.sh'
# TMP=/tmp/cp-tenant-menu-now.sh
# curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
# export ECOMAE_BRANCH=cursor/cp-tenant-menu-epartscart-php-7b3b
# bash "$TMP" 2>&1 | tee /root/cp-tenant-menu-parity.log
# grep -E 'RESULT=|PASS |FAIL |SHA=' /root/cp-tenant-menu-parity.log | tail -80
set -euo pipefail
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/cp-tenant-menu-epartscart-php-7b3b}"
export ECOMAE_BRANCH
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
chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh || true
ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/cp-tenant-menu-force-live.log
fail=0
prove() {
  local name="$1" url="$2" needle="$3"
  local body code
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' -L --max-redirs 2 "${url}?epc_prove=$(date +%s)" || echo 000)"
  if [[ "$code" != "200" ]] && [[ "$code" != "302" ]] && [[ "$code" != "401" ]] && [[ "$code" != "403" ]]; then
    printf 'FAIL %s http=%s\n' "$name" "$code"; fail=1; rm -f "$body"; return
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" "$body"; then
    if grep -Eqi 'login|bos-login|epc-cp-login' "$body"; then
      printf 'PASS %s auth-gate (login shell) http=%s\n' "$name" "$code"
      rm -f "$body"; return
    fi
    printf 'FAIL %s missing %q http=%s\n' "$name" "$needle" "$code"; fail=1; rm -f "$body"; return
  fi
  printf 'PASS %s http=%s\n' "$name" "$code"
  rm -f "$body"
}
prove cp-app 'https://www.epartscart.com/cp/app' 'epc-cp-topnav'
prove cp-commerce 'https://www.epartscart.com/cp/app' 'Commerce'
if [[ "$fail" -eq 0 ]]; then
  printf 'RESULT=PASS SHA=%s\n' "$SHA"
else
  printf 'RESULT=FAIL SHA=%s\n' "$SHA"
  exit 1
fi
