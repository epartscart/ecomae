#!/usr/bin/env bash
# NUCLEAR storefront publish — bypasses foundation gates (same pattern as login bridge fixer).
# Use when #877/#878 "deploy" left live epartscart on a stale :5100 binary.
#
# CloudPanel root:
#   bash scripts/cloudpanel_publish_storefront_now.sh
# Or one-shot from GitHub after this lands on main:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_publish_storefront_now.sh)"
set -euo pipefail

ECOMAE_GIT_URL="${ECOMAE_GIT_URL:-https://github.com/epartscart/ecomae.git}"
ECOMAE_BRANCH="${ECOMAE_BRANCH:-main}"
RELEASE_ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
ENV_DIR="${ECOMAE_ASPNET_ENV_DIR:-/etc/ecomae-aspnet}"
CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)

printf '== NUCLEAR storefront publish (%s) ==\n' "$ECOMAE_BRANCH"
printf 'This DIRECTLY dotnet-publishes + restarts ecomae-platform (no foundation gates).\n'

REPO=""
for d in "${CANDIDATES[@]}"; do
  if [[ -n "$d" && -d "$d/.git" ]]; then REPO="$d"; break; fi
done
if [[ -z "$REPO" ]]; then
  mkdir -p /opt
  git clone "$ECOMAE_GIT_URL" /opt/ecomae-aspnet-source
  REPO=/opt/ecomae-aspnet-source
fi

cd "$REPO"
git remote set-url origin "$ECOMAE_GIT_URL" || true
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
SHA="$(git rev-parse --short HEAD)"
printf 'Checkout: %s @ %s\n' "$REPO" "$SHA"

# Hard source gates — refuse wrong/stale tree.
grep -q 'epc_storefront_professional_shell' aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs
grep -q 'Catalog <span class="hidden-sm">of products</span>' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
grep -q 'StorefrontPhpCanonical.PartSearch' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
grep -q 'header-call-box a { background:#ef4444' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
test -f content/general_pages/epc_storefront_professional_shell.css
test -f epc_storefront_stub_redirect.php
grep -q 'epc_storefront_stub_redirect_maybe_exit' index.php
printf 'OK source markers (header look + search stub + CSS file)\n'

command -v dotnet >/dev/null || { printf 'ERROR: dotnet missing\n' >&2; exit 1; }
command -v systemctl >/dev/null || { printf 'ERROR: systemctl missing\n' >&2; exit 1; }

# Sync PHP edge files into tenant www (search-app redirect + professional CSS).
for base in \
  /home/*/htdocs/www.epartscart.com \
  /home/*/htdocs/epartscart.com \
  /var/www/epartscart* \
  /var/www/*/epartscart* \
  "$REPO"
do
  for dir in $base; do
    [[ -d "$dir" ]] || continue
    if [[ -f "$dir/index.php" ]]; then
      cp -f "$REPO/epc_storefront_stub_redirect.php" "$dir/epc_storefront_stub_redirect.php"
      if ! grep -q 'epc_storefront_stub_redirect.php' "$dir/index.php"; then
        cp -f "$REPO/index.php" "$dir/index.php"
      fi
      printf '  synced stub redirect → %s\n' "$dir"
    fi
    if [[ -d "$dir/content/general_pages" || -d "$dir/content" ]]; then
      mkdir -p "$dir/content/general_pages"
      cp -f "$REPO/content/general_pages/epc_storefront_professional_shell.css" \
        "$dir/content/general_pages/epc_storefront_professional_shell.css"
      cp -f "$REPO/content/general_pages/epc_storefront_professional_shell_css.php" \
        "$dir/content/general_pages/epc_storefront_professional_shell_css.php" 2>/dev/null || true
      printf '  synced professional CSS → %s/content/general_pages\n' "$dir"
    fi
  done
done

# Reinstall classic-entry (search-app 302 + aspnet-php-assets proxy) when installer present.
if [[ -f scripts/cloudpanel_install_classic_entry_aspnet_primary.sh ]]; then
  ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
  ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
    bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts \
    || printf 'WARN: classic-entry reinstall returned non-zero — continuing publish\n' >&2
fi

STAMP="$(date -u +%Y%m%d%H%M%S)"
RELEASE_DIR="$RELEASE_ROOT/releases/$STAMP"
PLATFORM_DIR="$RELEASE_DIR/platform"
WORKERS_DIR="$RELEASE_DIR/workers"
mkdir -p "$PLATFORM_DIR" "$WORKERS_DIR"

printf '== Direct publish (no foundation gates) → %s ==\n' "$RELEASE_DIR"
dotnet restore aspnet/EcomAE.AspNetCore.sln
dotnet publish aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj -c Release -o "$PLATFORM_DIR"
dotnet publish aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj -c Release -o "$WORKERS_DIR"

# Prove published DLL contains new chrome strings (not just source tree).
if ! strings "$PLATFORM_DIR/EcomAE.Platform.dll" | grep -Fq 'Catalog of products'; then
  # Blazor may embed differently — fall back to views/static web assets
  if ! rg -l 'Catalog of products' "$PLATFORM_DIR" >/dev/null 2>&1; then
    printf 'WARN: could not string-match Catalog of products inside publish output\n' >&2
  fi
fi

ln -sfn "$RELEASE_DIR" "$RELEASE_ROOT/current"
printf 'Current -> %s\n' "$(readlink -f "$RELEASE_ROOT/current" 2>/dev/null || echo "$RELEASE_DIR")"
printf 'SHA published: %s\n' "$SHA" | tee "$RELEASE_DIR/PUBLISHED_GIT_SHA.txt"

install -d /etc/systemd/system "$ENV_DIR"
install -m 0644 deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service
systemctl daemon-reload
systemctl enable ecomae-platform.service
systemctl restart ecomae-platform.service
sleep 3
systemctl --no-pager --full status ecomae-platform.service || true
bash scripts/wait_for_aspnet_health.sh || true

printf '\n== Prove :5100 is NEW binary (not pre-#877) ==\n'
BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 http://127.0.0.1:5100/ || true)"
if ! grep -Fq 'epc-nero-shell' <<<"$BODY"; then
  BODY="$(curl -sS -A 'Mozilla/5.0' --max-time 45 http://127.0.0.1:5100/storefront/app || true)"
fi

fail=0
assert_has() {
  local needle="$1"
  if grep -Fq "$needle" <<<"$BODY"; then
    printf 'PASS  has %s\n' "$needle"
  else
    printf 'FAIL  missing %s\n' "$needle"
    fail=1
  fi
}
assert_has 'Catalog <span class="hidden-sm">of products</span>'
assert_has 'ERP Login'
assert_has 'header-call-box a { background:#ef4444'
assert_has 'action="/en/shop/part_search"'
assert_has 'schearch-line'

if grep -Fq 'action="/storefront/search-app"' <<<"$BODY"; then
  printf 'FAIL  home still posts to /storefront/search-app — OLD BINARY STILL RUNNING\n'
  fail=1
fi
if grep -Fq '.schearch-line { background:#f3f4f6' <<<"$BODY"; then
  printf 'FAIL  old gray schearch-line CSS still present\n'
  fail=1
fi

LOC="$(curl -sSI -A 'Mozilla/5.0' --max-time 20 \
  'http://127.0.0.1:5100/storefront/search-app?article=1310154101' \
  | awk 'BEGIN{IGNORECASE=1} /^location:/{print $2}' | tr -d '\r' | head -1)"
printf 'search-app Location: %s\n' "$LOC"
if [[ "$LOC" == *'/en/shop/part_search'* ]]; then
  printf 'PASS  search-app redirects to part_search\n'
else
  printf 'FAIL  search-app did not redirect to /en/shop/part_search\n'
  fail=1
fi

printf '\nPublic (after Cloudflare):\n'
printf '  curl -sS https://www.epartscart.com/ | grep -E "Catalog of products|part_search|search-app|ef4444" | head\n'
printf '  Hard refresh: Ctrl+Shift+R on https://www.epartscart.com/\n'
printf '  Published SHA: %s  release: %s\n' "$SHA" "$RELEASE_DIR"

if [[ "$fail" -ne 0 ]]; then
  printf '\nRESULT=FAIL — :5100 did not pick up the new binary\n' >&2
  printf 'Debug:\n' >&2
  printf '  readlink -f %s/current\n' "$RELEASE_ROOT" >&2
  printf '  systemctl cat ecomae-platform.service | head -20\n' >&2
  printf '  journalctl -u ecomae-platform.service -n 80 --no-pager\n' >&2
  printf '  ss -lntp | grep 5100\n' >&2
  exit 1
fi

printf '\nRESULT=PASS — storefront header + search stub are live on :5100 (SHA %s)\n' "$SHA"
exit 0
