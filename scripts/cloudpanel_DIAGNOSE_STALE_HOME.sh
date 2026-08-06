#!/usr/bin/env bash
# Run on CloudPanel as root. Prints why public / can look "deployed" but stay stale.
#   bash scripts/cloudpanel_DIAGNOSE_STALE_HOME.sh
set -euo pipefail

PUBLIC_BASE="${ECOMAE_PUBLIC_BASE:-https://www.epartscart.com}"
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"

printf '======== DIAGNOSE STALE EPARTSCART HOME ========\n'
printf 'time=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"

printf '\n-- systemd / release --\n'
systemctl is-active ecomae-platform.service || true
readlink -f "$RELEASE_ROOT/current" || true
ls -l "$RELEASE_ROOT/current/platform/EcomAE.Platform.dll" 2>/dev/null || true
if [[ -f "$RELEASE_ROOT/current/PUBLISHED_GIT_SHA.txt" ]]; then
  printf 'PUBLISHED_GIT_SHA=%s\n' "$(cat "$RELEASE_ROOT/current/PUBLISHED_GIT_SHA.txt")"
fi
ss -lntp 2>/dev/null | grep 5100 || netstat -lntp 2>/dev/null | grep 5100 || true
ps auxww | grep -E '[E]comAE.Platform.dll|[d]otnet.*5100' | head -10 || true

printf '\n-- nginx home target (must be :5100/storefront/app) --\n'
if [[ -f scripts/cloudpanel_discover_epartscart_nginx_conf.sh ]]; then
  bash scripts/cloudpanel_discover_epartscart_nginx_conf.sh 2>/dev/null | head -80 || true
fi
grep -Rnh "location = /" /etc/nginx/sites-enabled /etc/nginx/sites-available 2>/dev/null \
  | head -40 || true
grep -Rnh "5100/storefront/app" /etc/nginx/sites-enabled /etc/nginx/sites-available 2>/dev/null \
  | head -20 || true

printf '\n-- LOCAL :5100/storefront/app form actions --\n'
curl -sS -A 'Mozilla/5.0' --max-time 30 http://127.0.0.1:5100/storefront/app \
  | grep -oE 'action="[^"]+"' | sort -u | head -20 || true

printf '\n-- PUBLIC %s/ form actions --\n' "$PUBLIC_BASE"
curl -sS -A 'Mozilla/5.0' --max-time 30 "${PUBLIC_BASE}/?q=$(date +%s)" \
  | grep -oE 'action="[^"]+"' | sort -u | head -20 || true

printf '\n-- PUBLIC marker --\n'
curl -sS --max-time 15 "${PUBLIC_BASE}/epc-live-deploy-marker.txt?q=$(date +%s)" || true
printf '\n'

printf '\n-- Compare: if LOCAL has part_search but PUBLIC has search-app → nginx/CDN mismatch --\n'
printf '-- Compare: if BOTH have search-app → :5100 binary never updated (publish/permissions) --\n'
printf '-- Compare: if LOCAL missing nero → wrong process on 5100 or service down --\n'
printf '\nDone. Paste this whole output.\n'
