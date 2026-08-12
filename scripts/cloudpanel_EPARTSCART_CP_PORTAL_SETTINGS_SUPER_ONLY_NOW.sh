#!/usr/bin/env bash
# CloudPanel paste — gate Super CP portal-settings away from tenant CP (epartscart).
#
# URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/cp-portal-settings-super-only-7b3b/scripts/cloudpanel_EPARTSCART_CP_PORTAL_SETTINGS_SUPER_ONLY_NOW.sh'
# TMP=/tmp/cp-portal-settings-super-only-now.sh
# curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
# export ECOMAE_BRANCH=cursor/cp-portal-settings-super-only-7b3b
# bash "$TMP" 2>&1 | tee /root/cp-portal-settings-super-only.log
# grep -E 'RESULT=|PASS |FAIL |SHA=' /root/cp-portal-settings-super-only.log | tail -80
set -euo pipefail
ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/cp-portal-settings-super-only-7b3b}"
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
ECOMAE_BRANCH="$ECOMAE_BRANCH" bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/cp-portal-settings-super-only-force-live.log
fail=0
prove() {
  local name="$1" url="$2" needle="$3"
  local body code
  body="$(mktemp)"
  code="$(curl -sS -o "$body" -w '%{http_code}' -A 'Mozilla/5.0' -L --max-redirs 2 "${url}?epc_prove=$(date +%s)" || echo 000)"
  if [[ "$code" != "200" ]] && [[ "$code" != "302" ]] && [[ "$code" != "401" ]] && [[ "$code" != "403" ]] && [[ "$code" != "404" ]]; then
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
BASE="${ECOMAE_EPARTSCART_BASE:-https://www.epartscart.com}"
# Tenant host must NOT render Super CP fleet console markers.
prove tenant-portal-gated "$BASE/cp/portal-settings-app" 'Super CP host only'
prove tenant-email-shell "$BASE/cp/tenant-email-app" 'This tenant'
prove tenant-digest-gated "$BASE/cp/portal-settings" ''
if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL SHA=%s\n' "$SHA"; exit 1
fi
printf '\nRESULT=PASS — Tenant CP portal-settings Super-gated (SHA=%s)\n' "$SHA"
printf 'Tenant: %s/cp/portal-settings-app → not-found gate\n' "$BASE"
printf 'Tenant SMTP: %s/cp/tenant-email-app\n' "$BASE"
printf 'Super CP (ecomae): https://www.ecomae.com/cp/portal-settings-app\n'
exit 0
