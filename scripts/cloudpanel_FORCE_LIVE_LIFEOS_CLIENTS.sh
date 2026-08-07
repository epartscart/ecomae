#!/usr/bin/env bash
# Republish :5100 for LifeOS join/results/clients board and HARD-PROVE those routes.
# Storefront home prove can RESULT=FAIL while LifeOS is still correctly live — this
# script treats LifeOS prove as the gate.
#
# CloudPanel root paste (preferred — includes no-login board from PR branch):
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/lifeos-clients-public-no-login-7b3b/scripts/cloudpanel_FORCE_LIVE_LIFEOS_CLIENTS.sh)"
#
# After that PR merges to main:
#   ECOMAE_BRANCH=main bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_LIFEOS_CLIENTS.sh)"
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/lifeos-clients-public-no-login-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

LIFEOS_BASE="${ECOMAE_LIFEOS_BASE:-https://lifeos.ecomae.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE LIFEOS CLIENTS (%s) ========\n' "$ECOMAE_BRANCH"
if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  printf 'ERROR: ecomae git checkout not found under /opt or /root\n' >&2
  exit 1
fi

cd "$REPO"
# Fetch the wrapper branch first so this script file exists locally when pasted via curl.
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s\n' "$REPO" "$SHA" "$FULL"

if [[ ! -x scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh || true
fi

set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-lifeos-clients.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s (storefront RESULT may FAIL; LifeOS prove below is authoritative)\n' "$FORCE_RC"

printf '\n== LifeOS hard prove ==\n'
fail=0
prove() {
  local name="$1" url="$2" needle="$3"
  local body tmp code
  tmp="$(mktemp)"
  code="$(curl -sL -o "$tmp" -w '%{http_code}' --connect-timeout 20 "$url" || echo 000)"
  body="$(cat "$tmp" 2>/dev/null || true)"
  rm -f "$tmp"
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s http=%s url=%s\n' "$name" "$code" "$url"
    fail=1
    return
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" <<<"$body"; then
    printf 'FAIL %s missing needle %q (http=200)\n' "$name" "$needle"
    fail=1
    return
  fi
  if grep -Fq 'login?returnUrl=' <<<"$body" && [[ "$name" == clients-board || "$name" == cp-clients ]]; then
    printf 'FAIL %s redirected to login\n' "$name"
    fail=1
    return
  fi
  printf 'PASS %s\n' "$name"
}

prove join-js "$LIFEOS_BASE/lifeos/join.js" "fetch('/lifeos/join'"
prove join-html "$LIFEOS_BASE/lifeos/join" "/lifeos/join.js"
prove clients-cp "$LIFEOS_BASE/lifeos/clients/cp" "totalClients"
prove clients-board "$LIFEOS_BASE/lifeos/clients-board" "Joined clients"
prove cp-clients "$LIFEOS_BASE/cp/lifeos-clients-app" "Joined clients"
prove results "$LIFEOS_BASE/lifeos/results" "My results"
prove directory "$LIFEOS_BASE/lifeos/directory" "clientsBoard"

# Local loopback also — proves Kestrel, not only Cloudflare cache.
if curl -sS --connect-timeout 3 http://127.0.0.1:5100/health >/dev/null 2>&1; then
  prove loopback-clients-cp "http://127.0.0.1:5100/lifeos/clients/cp" "totalClients"
  prove loopback-join-js "http://127.0.0.1:5100/lifeos/join.js" "fetch('/lifeos/join'"
fi

if [[ "$fail" -ne 0 ]]; then
  cat <<EOF >&2

#####################################################################
#  RESULT=FAIL — LifeOS clients board / join fetch NOT live
#  SHA attempted: $FULL
#  Log: /root/force-live-lifeos-clients.log
#####################################################################
EOF
  printf 'Debug:\n' >&2
  printf '  readlink -f /var/www/ecomae-aspnet/current\n' >&2
  printf '  cat /var/www/ecomae-aspnet/current/PUBLISHED_GIT_SHA.txt\n' >&2
  printf '  systemctl is-active ecomae-platform.service; ss -lntp | grep 5100\n' >&2
  printf '  curl -sS http://127.0.0.1:5100/lifeos/directory | head\n' >&2
  exit 1
fi

cat <<EOF

#####################################################################
#  RESULT=PASS — LifeOS clients board + join fetch live (SHA $SHA)
#  https://lifeos.ecomae.com/lifeos/clients-board
#  https://lifeos.ecomae.com/lifeos/clients/cp
#  https://lifeos.ecomae.com/cp/lifeos-clients-app
#####################################################################
EOF
exit 0
