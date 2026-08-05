#!/usr/bin/env bash
# Publish main so storefront header PHP look (#877+) is live on :5100 / epartscart.
# Nginx classic-entry alone does NOT update the ASP.NET binary.
#
# Usage (CloudPanel root):
#   bash scripts/cloudpanel_publish_storefront_header_now.sh
# Or one-shot:
#   bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_publish_storefront_header_now.sh)"
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
  printf 'ERROR: no ecomae checkout at /opt/ecomae-aspnet-source or /root/ecomae\n' >&2
  printf 'Bootstrap:\n' >&2
  printf '  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"\n' >&2
  exit 1
fi

printf '== Publish storefront header look (#877) from %s @ %s ==\n' "$ROOT" "$ECOMAE_BRANCH"

for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    git -C "$d" fetch origin "$ECOMAE_BRANCH"
    git -C "$d" checkout -f "$ECOMAE_BRANCH"
    git -C "$d" reset --hard "origin/$ECOMAE_BRANCH"
    printf '  %s → %s\n' "$d" "$(git -C "$d" rev-parse --short HEAD)"
  fi
done

cd "$ROOT"

grep -q 'epc_storefront_professional_shell.css' aspnet/src/EcomAE.Platform/Presentation/LegacyPresentationAssets.cs
grep -q 'Catalog <span class="hidden-sm">of products</span>' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
grep -q 'ERP Login' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor
test -f content/general_pages/epc_storefront_professional_shell.css
printf 'OK source markers (#877 header) present\n'

# Copy static CSS into common PHP/www docroots so nginx can serve /content/*.css
# even when the request does not hit Kestrel.
CSS_SRC="$ROOT/content/general_pages/epc_storefront_professional_shell.css"
CSS_PHP_SRC="$ROOT/content/general_pages/epc_storefront_professional_shell_css.php"
for www in \
  /home/*/htdocs/www.epartscart.com \
  /home/*/htdocs/epartscart.com \
  /var/www/epartscart* \
  /var/www/*/epartscart* \
  /opt/ecomae-aspnet-source \
  /root/ecomae
do
  for base in $www; do
    dest_dir="$base/content/general_pages"
    if [[ -d "$base" && ( -d "$dest_dir" || -d "$base/content" ) ]]; then
      mkdir -p "$dest_dir"
      cp -f "$CSS_SRC" "$dest_dir/epc_storefront_professional_shell.css"
      cp -f "$CSS_PHP_SRC" "$dest_dir/epc_storefront_professional_shell_css.php" 2>/dev/null || true
      # Keep shell source available for the PHP helper fallback.
      if [[ -f "$ROOT/content/general_pages/site_professional_shell.php" ]]; then
        cp -f "$ROOT/content/general_pages/site_professional_shell.php" "$dest_dir/site_professional_shell.php" 2>/dev/null || true
      fi
      printf '  synced CSS → %s\n' "$dest_dir"
    fi
  done
done

# Delegate to nuclear direct-publish (foundation gates left stale :5100 after #877/#878).
export ECOMAE_BRANCH
bash scripts/cloudpanel_publish_storefront_now.sh
