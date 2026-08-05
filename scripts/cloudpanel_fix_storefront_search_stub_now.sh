#!/usr/bin/env bash
# Fix live /storefront/search-app 404 blunder (part number search → PHP /en/shop/part_search).
# 1) Sync PHP edge redirect into checkout + tenant www
# 2) Reinstall classic-entry nginx stub redirects
# 3) Emergency-publish ASP.NET so home forms stop posting to search-app
#
# CloudPanel root:
#   bash scripts/cloudpanel_fix_storefront_search_stub_now.sh
# Or:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_fix_storefront_search_stub_now.sh)"
set -euo pipefail

ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
ROOT=""
for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    ROOT="$d"
    break
  fi
done
if [[ -z "$ROOT" ]]; then
  printf 'ERROR: no ecomae checkout\n' >&2
  exit 1
fi

printf '== Fix storefront search-app stub → PHP part_search ==\n'
for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    git -C "$d" fetch origin "$ECOMAE_BRANCH"
    git -C "$d" checkout -f "$ECOMAE_BRANCH"
    git -C "$d" reset --hard "origin/$ECOMAE_BRANCH"
    printf '  %s → %s\n' "$d" "$(git -C "$d" rev-parse --short HEAD)"
  fi
done
cd "$ROOT"

test -f epc_storefront_stub_redirect.php
grep -q 'epc_storefront_stub_redirect_maybe_exit' index.php
grep -q 'location = /storefront/search-app' deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf
grep -q 'StorefrontPhpCanonical.PartSearch' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
printf 'OK source markers present\n'

# Sync PHP edge redirect into tenant docroots (nginx often sends /storefront/* to PHP-FPM).
for base in \
  /home/*/htdocs/www.epartscart.com \
  /home/*/htdocs/epartscart.com \
  /var/www/epartscart* \
  /var/www/*/epartscart* \
  "$ROOT"
do
  for dir in $base; do
    if [[ -d "$dir" && -f "$dir/index.php" ]]; then
      cp -f "$ROOT/epc_storefront_stub_redirect.php" "$dir/epc_storefront_stub_redirect.php"
      # Ensure index.php includes the redirect (full pull preferred; patch if missing).
      if ! grep -q 'epc_storefront_stub_redirect.php' "$dir/index.php"; then
        cp -f "$ROOT/index.php" "$dir/index.php"
      fi
      printf '  synced stub redirect → %s\n' "$dir"
    fi
  done
done

# Reinstall classic-entry nginx (includes search-app → /en/shop/part_search redirects).
if [[ -f scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
  ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
    bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts || true
fi

export ECOMAE_EMERGENCY_PUBLISH=1
export ECOMAE_BRANCH
bash scripts/cloudpanel_find_and_redeploy.sh
systemctl restart ecomae-platform.service || true
bash scripts/wait_for_aspnet_health.sh || true

printf '\n== Prove search stub redirect ==\n'
fail=0
CODE="$(curl -sS -o /dev/null -w '%{http_code}' -A 'Mozilla/5.0' --max-time 20 \
  'http://127.0.0.1:5100/storefront/search-app?article=1310154101' || true)"
LOC="$(curl -sSI -A 'Mozilla/5.0' --max-time 20 \
  'http://127.0.0.1:5100/storefront/search-app?article=1310154101' | awk 'BEGIN{IGNORECASE=1} /^location:/{print $2}' | tr -d '\r' | head -1)"
printf 'Kestrel search-app → %s Location=%s\n' "$CODE" "$LOC"
if [[ "$LOC" == *'/en/shop/part_search'* && "$LOC" == *'article=1310154101'* ]]; then
  printf 'PASS  Kestrel stub middleware\n'
else
  printf 'FAIL  Kestrel stub middleware\n'
  fail=1
fi

HOME_BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 30 http://127.0.0.1:5100/ || true)"
if grep -Fq 'StorefrontPhpCanonical.PartSearch' <<<"$HOME_BODY" || grep -Fq 'action="/en/shop/part_search"' <<<"$HOME_BODY"; then
  printf 'PASS  home search form points at /en/shop/part_search\n'
elif grep -Fq 'action="/storefront/search-app"' <<<"$HOME_BODY"; then
  printf 'FAIL  home still posts to /storefront/search-app (stale binary)\n'
  fail=1
else
  printf 'WARN  could not assert home form action\n'
fi

printf '\nPublic probe:\n'
printf '  curl -sSI "https://www.epartscart.com/storefront/search-app?article=1310154101" | head\n'
printf '  Expect: Location: /en/shop/part_search?article=1310154101\n'

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL\n' >&2
  exit 1
fi
printf '\nRESULT=PASS — search-app stub no longer 404s\n'
exit 0
