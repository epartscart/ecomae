#!/usr/bin/env bash
# Recover ALL product sites from Cloudflare 502 / warmup splash.
# Cause: shared origin Kestrel ecomae-platform (:5100) down or crash-looping.
# Merge alone does NOT restore — run as root on CloudPanel.
#
# CloudPanel root paste (this branch):
#   ECOMAE_BRANCH=cursor/all-sites-502-recover-7b3b ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/all-sites-502-recover-7b3b/scripts/cloudpanel_FORCE_LIVE_502_ALL_RECOVER.sh)" \
#     2>&1 | tee /root/force-live-502-all-recover.log
#
# After merge to main:
#   ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES \
#     bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_FORCE_LIVE_502_ALL_RECOVER.sh)" \
#     2>&1 | tee /root/force-live-502-all-recover.log
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/all-sites-502-recover-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
LOG=/root/force-live-502-all-recover.log
PROBE=/root/force-live-502-all-probe.txt

printf '======== FORCE LIVE 502 ALL RECOVER (%s) ========\n' "$ECOMAE_BRANCH"
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
FULL="$(git rev-parse HEAD)"
printf 'REPO=%s SHA=%s FULL=%s\n' "$REPO" "$SHA" "$FULL"

chmod +x scripts/cloudpanel_FORCE_LIVE_NOW.sh \
  scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh \
  scripts/cloudpanel_FORCE_LIVE_LIFEOS_502_RECOVER.sh \
  scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh 2>/dev/null || true

is_splash_or_502() {
  local body_file="$1" code="$2"
  if [[ "$code" == "502" || "$code" == "503" || "$code" == "000" ]]; then
    return 0
  fi
  if grep -qiE 'Loading — Please wait|epc_warmup|error code: 502' "$body_file" 2>/dev/null; then
    return 0
  fi
  return 1
}

probe_public() {
  local url="$1"
  local code
  code="$(curl -sS -o /tmp/502-all-probe.body -w '%{http_code}' --max-time 25 -A 'ecomae-502-all-recover' -k "$url" || echo 000)"
  local title
  title="$(grep -oiE '<title>[^<]+' /tmp/502-all-probe.body 2>/dev/null | head -n1 | sed 's/<title>//I' || true)"
  local flag=OK
  if is_splash_or_502 /tmp/502-all-probe.body "$code"; then
    flag=BAD
  fi
  printf '%s %s %s %s\n' "$flag" "$code" "$url" "${title:0:70}"
}

printf '\n---- [0] preflight public (expect BAD while :5100 down) ----\n'
: > "$PROBE"
for u in \
  https://cp.ecomae.com/ \
  https://www.ecomae.com/ \
  https://lifeos.ecomae.com/ \
  https://erp.ecomae.com/ \
  https://bos.ecomae.com/ \
  https://www.epartscart.com/ \
  https://www.epartscart.com/storefront/app
do
  probe_public "$u" | tee -a "$PROBE"
done

printf '\n---- [1] fast bounce ecomae-platform (:5100) ----\n'
set +e
systemctl stop ecomae-platform.service || true
fuser -k 5100/tcp 2>/dev/null || true
sleep 1
systemctl start ecomae-platform.service || systemctl restart ecomae-platform.service
sleep 4
systemctl --no-pager --full status ecomae-platform.service | sed -n '1,25p' || true
ss -lntp 2>/dev/null | grep -E ':5100\b' || netstat -lntp 2>/dev/null | grep -E ':5100\b' || true
LOCAL_CODE="$(curl -sS -o /dev/null -w '%{http_code}' --max-time 12 http://127.0.0.1:5100/storefront/app || echo 000)"
printf 'local_5100_storefront_app=%s\n' "$LOCAL_CODE"
set -e

NEED_FULL=1
if [[ "$LOCAL_CODE" == "200" ]]; then
  # Quick public check — if Super CP recovered, still run full publish for freshness.
  CP_CODE="$(curl -sS -o /tmp/502-all-probe.body -w '%{http_code}' --max-time 20 -k https://cp.ecomae.com/ || echo 000)"
  if ! is_splash_or_502 /tmp/502-all-probe.body "$CP_CODE"; then
    NEED_FULL=0
    printf 'Fast bounce restored public CP (http=%s) — still running ALL_SITES for classic-entry/prove.\n' "$CP_CODE"
  fi
fi

printf '\n---- [2] FORCE_LIVE_ALL_SITES (republish + classic-entry + prove) ----\n'
set +e
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_ALL_SITES.sh 2>&1 | tee /root/force-live-502-all-inner.log
ALL_RC=${PIPESTATUS[0]}
set -e
printf 'FORCE_LIVE_ALL_SITES exit=%s need_full_was=%s\n' "$ALL_RC" "$NEED_FULL"

# Warmup splash loop can leave epartscart on static splash even after :5100 is up.
if [[ -x scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh ]]; then
  printf '\n---- [3] warmup splash loop fix (epartscart) ----\n'
  set +e
  ECOMAE_CONFIRM_FIX_WARMUP_SPLASH_LOOP=YES \
    bash scripts/cloudpanel_fix_warmup_splash_storefront_loop.sh 2>&1 | tee -a /root/force-live-502-all-inner.log
  set -e
fi

printf '\n---- [4] postflight public (must be OK, not 502/splash) ----\n'
: > "$PROBE"
BAD=0
for u in \
  https://cp.ecomae.com/ \
  https://cp.ecomae.com/cp \
  https://www.ecomae.com/ \
  https://www.ecomae.com/cp \
  https://www.ecomae.com/erp \
  https://lifeos.ecomae.com/ \
  https://erp.ecomae.com/ \
  https://bos.ecomae.com/ \
  https://ip.ecomae.com/ \
  https://www.epartscart.com/ \
  https://www.epartscart.com/storefront/app \
  https://www.epartscart.com/cp \
  https://www.epartscart.com/erp
do
  line="$(probe_public "$u")"
  printf '%s\n' "$line" | tee -a "$PROBE"
  if [[ "$line" == BAD* ]]; then
    BAD=$((BAD + 1))
  fi
done

printf '\n======== 502 ALL RECOVER DONE SHA=%s ========\n' "$SHA"
printf 'Inner: %s\n' /root/force-live-502-all-inner.log
printf 'Probe: %s\n' "$PROBE"
printf 'Policy: cutoverAllowed=false; writes PHP-authoritative; PHP via /php-reference/* only\n'

if [[ "$BAD" -gt 0 ]]; then
  printf 'RESULT=FAIL bad_hosts=%s (send %s + journalctl -u ecomae-platform -n 200)\n' "$BAD" "$PROBE" >&2
  exit 1
fi
printf 'RESULT=PASS all probed hosts out of 502/splash\n'
exit 0
