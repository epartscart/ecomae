#!/usr/bin/env bash
# Run as root on the CloudPanel host when `git pull origin main` aborts
# because push-deploys left the docroot dirty/untracked.
#
#   bash /home/ecomae/htdocs/www.ecomae.com/epc-git-pull-main-root.sh
#
# Safe: only moves/stashes paths that would block merging origin/main.
# Other live-only dirty files (garage, my_orders, etc.) stay put.
set -euo pipefail
ROOT="/home/ecomae/htdocs/www.ecomae.com"
cd "$ROOT"

git config --global --add safe.directory "$ROOT" || true

ASIDE="/tmp/epc-pull-aside-$(date +%Y%m%d%H%M%S)"
mkdir -p "$ASIDE"
echo "aside=$ASIDE"
echo "HEAD_BEFORE=$(git rev-parse --short HEAD)"

git fetch origin main

# 1) Untracked files that already exist on origin/main → move aside
mapfile -t UNTRACKED < <(git status --porcelain -uall | awk '/^\?\?/ {print substr($0,4)}')
for f in "${UNTRACKED[@]:-}"; do
  [[ -z "${f:-}" ]] && continue
  # skip bulky cache / venv noise unless it is a merge path
  if git cat-file -e "origin/main:$f" 2>/dev/null; then
    dest="$ASIDE/untracked/$f"
    mkdir -p "$(dirname "$dest")"
    mv -f "$f" "$dest"
    echo "moved untracked $f"
  fi
done

# 2) Tracked local modifications that origin/main also changes → stash those only
mapfile -t MAIN_TOUCHES < <(git diff --name-only HEAD origin/main)
STASH_LIST=()
for f in "${MAIN_TOUCHES[@]:-}"; do
  [[ -z "${f:-}" ]] && continue
  # dirty or staged?
  if ! git diff --quiet -- "$f" 2>/dev/null || ! git diff --quiet --cached -- "$f" 2>/dev/null; then
    STASH_LIST+=("$f")
  fi
done

if ((${#STASH_LIST[@]})); then
  echo "stashing ${#STASH_LIST[@]} dirty paths that conflict with origin/main"
  git stash push -m "pre-main-pull-$(date +%Y%m%d%H%M%S)" -- "${STASH_LIST[@]}"
else
  echo "no conflicting dirty tracked files to stash"
fi

# 3) Pull (ff-only preferred; fall back to merge)
if git merge-base --is-ancestor HEAD origin/main; then
  git merge --ff-only origin/main
else
  git pull --no-edit origin main
fi

# Prefer main for the stashed conflict set (live already had those via push-deploy)
if git stash list | head -1 | grep -q 'pre-main-pull-'; then
  git stash drop || true
  echo "dropped pre-main-pull stash (kept origin/main versions)"
fi

# 4) Sync root-owned Laximo JS from writable storefront copy
if [[ -f api/laximo_storefront.js ]]; then
  mkdir -p api/Laximo
  cp -f api/laximo_storefront.js api/Laximo/laximo.js
  chmod 664 api/Laximo/laximo.js || true
  echo "synced api/Laximo/laximo.js sha256=$(sha256sum api/Laximo/laximo.js | awk '{print $1}')"
else
  echo "WARN: api/laximo_storefront.js missing — skip laximo.js sync"
fi

if id ecomae >/dev/null 2>&1; then
  chown -R ecomae:ecomae api/Laximo 2>/dev/null || true
  # search-tab embed also root-owned on this host
  if [[ -d content/shop/catalogue/search_tabs/tabs_content/laximo_catalog ]]; then
    chown -R ecomae:ecomae content/shop/catalogue/search_tabs/tabs_content/laximo_catalog || true
  fi
  echo "chown api/Laximo (+ laximo_catalog tab) -> ecomae"
fi

echo "HEAD_AFTER=$(git rev-parse --short HEAD)"
git log -1 --oneline
echo "--- status (first 40) ---"
git status --short | head -40
echo "Done. Aside copies: $ASIDE"
echo "Do NOT git reset --hard — remaining dirty files may be intentional live fixes."
