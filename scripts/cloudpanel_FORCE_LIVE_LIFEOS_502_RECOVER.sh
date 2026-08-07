#!/usr/bin/env bash
# Recover lifeos.ecomae.com (and shared :5100) from Cloudflare 502.
# Cause: origin Kestrel ecomae-platform (:5100) down/stale after a failed publish.
# Merge alone does NOT restore the site — run this as root on CloudPanel.
#
# Paste:
#   ECOMAE_BRANCH=cursor/www-lifeos-film-frontpage-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/www-lifeos-film-frontpage-7b3b/scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh)" \
#     2>&1 | tee /root/force-live-lifeos-502-recover.log
#
# Or after merge:
#   ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh)" \
#     2>&1 | tee /root/force-live-lifeos-502-recover.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/www-lifeos-film-frontpage-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

LIFEOS_BASE="${ECOMAE_LIFEOS_BASE:-https://lifeos.ecomae.com}"
WWW_BASE="${ECOMAE_WWW_BASE:-https://www.ecomae.com}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '======== FORCE LIVE LIFEOS 502 RECOVER (%s) ========\n' "$ECOMAE_BRANCH"
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
git remote set-url origin https://github.com/epartscart/ecomae.git || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'REPO=%s SHA=%s\n' "$REPO" "$SHA"

# Fast local bounce first — if unit/binary exists, bring :5100 back before full publish.
set +e
systemctl restart ecomae-platform.service
sleep 3
ss -lntp 2>/dev/null | grep -E ':5100\b' || netstat -lntp 2>/dev/null | grep -E ':5100\b' || true
curl -sS -o /dev/null -w 'local_5100_lifeos=%{http_code}\n' --max-time 8 http://127.0.0.1:5100/lifeos || true
set -e

if [[ ! -x scripts/cloudpanel_FORCE_LIVE_NOW.sh ]]; then
  chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh || true
fi

set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live-lifeos-502-inner.log
FORCE_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_NOW exit=%s (storefront RESULT may WARN; LifeOS prove below is authoritative)\n' "$FORCE_RC"

if [[ -x scripts/cloudpanel_install_lifeos_host_nginx.sh ]]; then
  set +e
  bash scripts/cloudpanel_install_lifeos_host_nginx.sh 2>&1 | tee -a /root/force-live-lifeos-502-inner.log
  set -e
fi

nginx -t
systemctl reload nginx
systemctl restart ecomae-platform.service || true
sleep 5

printf '\n== LifeOS / www 502 recover hard prove ==\n'
fail=0
prove() {
  local name="$1" url="$2" needle="${3:-}"
  local body tmp code
  tmp="$(mktemp)"
  code="$(curl -4 -sS -o "$tmp" -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
    "${url}?epc_prove=$(date +%s)" || echo 000)"
  body="$(cat "$tmp" 2>/dev/null || true)"
  rm -f "$tmp"
  if [[ "$code" != "200" && "$code" != "301" && "$code" != "302" ]]; then
    printf 'FAIL %s http=%s url=%s\n' "$name" "$code" "$url"
    fail=1
    return
  fi
  # For 3xx, follow once for content needles.
  if [[ "$code" == "301" || "$code" == "302" ]]; then
    code="$(curl -4 -sS -L -o "$tmp" -w '%{http_code}' --connect-timeout 20 -A 'Mozilla/5.0' \
      "${url}?epc_prove=$(date +%s)" || echo 000)"
    body="$(cat "$tmp" 2>/dev/null || true)"
    rm -f "$tmp"
  fi
  if [[ "$code" != "200" ]]; then
    printf 'FAIL %s follow http=%s url=%s\n' "$name" "$code" "$url"
    fail=1
    return
  fi
  if [[ -n "$needle" ]] && ! grep -Fq "$needle" <<<"$body"; then
    printf 'FAIL %s missing needle %q (http=200)\n' "$name" "$needle"
    fail=1
    return
  fi
  printf 'PASS %s http=%s\n' "$name" "$code"
}

# Local origin must be up (502 root cause).
prove local-lifeos "http://127.0.0.1:5100/lifeos" 'LifeOS'
prove public-lifeos-root "$LIFEOS_BASE/" 'LifeOS'
prove public-lifeos-app "$LIFEOS_BASE/lifeos" 'LifeOS'
prove public-lifeos-css "$LIFEOS_BASE/lifeos/lifeos-product.css" ''
# www shares :5100 — soft prove (do not fail recover if only marketing nginx snip lags)
set +e
prove www-home "$WWW_BASE/" 'epm-hub'
set -e

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — LifeOS still 502 / broken (SHA=%s)\n' "$SHA"
  printf 'Hints:\n'
  printf '  systemctl status ecomae-platform.service --no-pager -l | head -40\n'
  printf '  journalctl -u ecomae-platform.service -n 80 --no-pager\n'
  printf '  ss -lntp | grep 5100\n'
  printf '  curl -sS -D- http://127.0.0.1:5100/lifeos | head -20\n'
  exit 1
fi

printf '\nRESULT=PASS — lifeos.ecomae.com recovered from 502 (SHA=%s)\n' "$SHA"
printf 'Open: %s/  and  %s/lifeos\n' "$LIFEOS_BASE" "$LIFEOS_BASE"
exit 0
