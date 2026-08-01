#!/usr/bin/env bash
set -euo pipefail

SOURCE_BRANCH="${1:-work}"
TARGET_BRANCH="${2:-aspnet-migration-consolidated}"
BASE_REMOTE="${BASE_REMOTE:-origin}"
BASE_BRANCH="${BASE_BRANCH:-main}"
COMMIT_MESSAGE="${COMMIT_MESSAGE:-Add consolidated ASP.NET Core migration foundation}"

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Working tree is not clean. Commit or stash changes before running." >&2
  exit 1
fi

if ! git rev-parse --verify "$SOURCE_BRANCH" >/dev/null 2>&1; then
  echo "Source branch '$SOURCE_BRANCH' does not exist locally." >&2
  exit 1
fi

git fetch "$BASE_REMOTE" "$BASE_BRANCH"
git checkout -B "$TARGET_BRANCH" "$BASE_REMOTE/$BASE_BRANCH"

# Copy the final migration tree from SOURCE_BRANCH onto latest main instead of merging
# the old PR history. This avoids conflicts caused by duplicate PRs (#500-#503)
# while preserving the complete ASP.NET/PHP-compatibility migration state.
paths=(
  .htaccess
  aspnet
  content/general_pages/epc_portal_route_aliases.php
  cp/content/shop/finance/erp/erp_dashboard.php
  cp/content/users/statistics/app.php
  deploy/aspnet
  docs/migration
  epc-all-tasks-final-report.php
  epc-apai-tenant-industry-fix.php
  epc-regenerate-issues-report.php
  index.php
  pyapi/requirements.txt
  scripts
  tests
)

for path in "${paths[@]}"; do
  if git ls-tree -r --name-only "$SOURCE_BRANCH" -- "$path" | grep -q .; then
    git checkout "$SOURCE_BRANCH" -- "$path"
  fi
done

if git diff --quiet && git diff --cached --quiet; then
  echo "No consolidated changes to commit. Latest main may already contain this work."
  exit 0
fi

git add "${paths[@]}"
git commit -m "$COMMIT_MESSAGE"

echo "Created consolidated branch: $TARGET_BRANCH"
echo "Push it with: git push -u $BASE_REMOTE $TARGET_BRANCH"
echo "Open PR: https://github.com/epartscart/ecomae/compare/$BASE_BRANCH...$TARGET_BRANCH"
