#!/usr/bin/env bash
# CloudPanel paste — fix www.ecomae.com footer / below-fold (Layla scroll lock + assets).
#
# URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/ecomae-home-footer-parity-7b3b/scripts/cloudpanel_ECOMAE_HOME_FOOTER_PARITY_NOW.sh'
# TMP=/tmp/ecomae-home-footer-parity-now.sh
# curl -fsSL "$URL" -o "$TMP" && test -s "$TMP"
# export ECOMAE_BRANCH=cursor/ecomae-home-footer-parity-7b3b
# bash "$TMP" 2>&1 | tee /root/ecomae-home-footer-parity.log
# grep -E 'RESULT=|PASS |FAIL ' /root/ecomae-home-footer-parity.log | tail -60
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-cursor/ecomae-home-footer-parity-7b3b}"
export ECOMAE_BRANCH
export ECOMAE_SKIP_LIFEOS_MP4="${ECOMAE_SKIP_LIFEOS_MP4:-YES}"

if [[ "$(id -u)" -ne 0 ]]; then
  printf 'ERROR: must run as root on the CloudPanel server\n' >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_CANDIDATES=("${ECOMAE_REPO:-}" "$SCRIPT_DIR/.." /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
REPO=""
for d in "${REPO_CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" && -f "$d/scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh" ]]; then
    REPO="$(cd "$d" && pwd)"
    break
  fi
done

if [[ -z "$REPO" ]]; then
  printf 'Cloning ecomae into /opt/ecomae-aspnet-source…\n'
  mkdir -p /opt
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

chmod +x scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh || true
ECOMAE_BRANCH="$ECOMAE_BRANCH" ECOMAE_SKIP_LIFEOS_MP4=YES \
  bash scripts/cloudpanel_FORCE_LIVE_WWW_MARKETING.sh
RC=$?
printf 'FORCE_LIVE_WWW_MARKETING exit=%s SHA=%s\n' "$RC" "$SHA"
exit "$RC"
