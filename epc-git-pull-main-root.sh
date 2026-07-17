#!/usr/bin/env bash
# Run as root on the CloudPanel host to finish git pull when push-deploys
# left the docroot dirty (blocks #241/#242 merges).
#   bash /home/ecomae/htdocs/www.ecomae.com/epc-git-pull-main-root.sh
set -euo pipefail
ROOT="/home/ecomae/htdocs/www.ecomae.com"
cd "$ROOT"

git config --global --add safe.directory "$ROOT" || true

ASIDE="/tmp/epc-pull-aside-$(date +%Y%m%d%H%M%S)"
mkdir -p "$ASIDE"
echo "aside=$ASIDE"

# Untracked paths that exist in origin/main and block the merge
for f in \
  epc-apply-brands-fix.php \
  epc-article-search-backfill.php \
  epc-deploy-rename.php
do
  if [[ -e "$f" ]]; then
    mv -f "$f" "$ASIDE/"
    echo "moved untracked $f"
  fi
done

# Stash only the tracked files that block this pull (keeps other live deploys)
git stash push -m "pre-main-pull-$(date +%Y%m%d%H%M%S)" -- \
  api/laximo_proxy.php \
  api/Laximo/laximo.js \
  content/laximo/com_guayaquil/Config.php \
  content/laximo/com_guayaquil/guayaquillib/data/GuayaquilSoapWrapper.php \
  content/shop/docpart/docpart_article_match.php \
  content/shop/docpart/docpart_epc_article_brands.php \
  content/shop/docpart/part_search_page.php \
  content/shop/docpart/suppliers_handlers/prices/common_interface.php \
  core/dp_helper.php || true

git fetch origin main
git pull origin main

# Prefer main versions of the stashed blockers
if git stash list | head -1 | grep -q 'pre-main-pull-'; then
  git stash drop || true
fi

# Sync root-owned Laximo JS from the writable storefront copy (PHP cannot write api/Laximo/).
if [[ -f api/laximo_storefront.js ]]; then
  cp -f api/laximo_storefront.js api/Laximo/laximo.js
  chmod 664 api/Laximo/laximo.js || true
  echo "synced api/Laximo/laximo.js from storefront sha256=$(sha256sum api/Laximo/laximo.js | awk '{print $1}')"
else
  echo "WARN: api/laximo_storefront.js missing — skip laximo.js sync"
fi

# Optional: make api/Laximo writable by PHP-FPM user for future deploys
if id ecomae >/dev/null 2>&1; then
  chown -R ecomae:ecomae api/Laximo || true
  echo "chown api/Laximo -> ecomae"
fi

echo "HEAD=$(git rev-parse --short HEAD)"
git log -1 --oneline
git status --short | head -40
echo "Done. Do NOT git reset --hard — other push-deploys may still be dirty on purpose."
