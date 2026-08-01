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
# while preserving the complete ASP.NET Core/PHP-compatibility migration state.
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
  scripts/cleanup_codex_prs.sh
  scripts/deploy_aspnet_foundation.sh
  scripts/inventory_php_routes.sh
  scripts/preflight_aspnet_production.sh
  scripts/prepare_consolidated_aspnet_pr.sh
  scripts/push_consolidated_pr_update.sh
  scripts/rebase_conflicted_pr_range.sh
  scripts/remote_aspnet_foundation_deploy.sh
  scripts/rollback_aspnet_foundation.sh
  scripts/verify_aspnet_proxy_guardrails.sh
  tests/aspnet_migration
  tests/erp_advanced/run_surface_route_alias_tests.php
  tests/live_smoke
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
