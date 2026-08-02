#!/usr/bin/env bash
set -euo pipefail

BASE_REMOTE="${BASE_REMOTE:-origin}"
BASE_BRANCH="${BASE_BRANCH:-main}"
SOURCE_BRANCH="${SOURCE_BRANCH:-work}"
START_PR="${1:-500}"
END_PR="${2:-508}"
CONSOLIDATED_BRANCH="${CONSOLIDATED_BRANCH:-aspnet-migration-consolidated}"

if ! command -v git >/dev/null 2>&1; then
  echo "git is required" >&2
  exit 1
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Working tree is not clean. Commit or stash changes before running." >&2
  exit 1
fi

git fetch "$BASE_REMOTE" "$BASE_BRANCH"

for pr in $(seq "$START_PR" "$END_PR"); do
  echo "Fetching PR #$pr for audit..."
  git fetch "$BASE_REMOTE" "pull/$pr/head:refs/heads/pr-$pr-original" || {
    echo "  PR #$pr could not be fetched; skipping."
    continue
  }
done

# Create one clean branch from latest main. This is the recommended replacement
# for conflicting PRs #500-#508. Push this branch, open one PR, then close the
# duplicate conflicted PRs.
"$(dirname "$0")/prepare_consolidated_aspnet_pr.sh" "$SOURCE_BRANCH" "$CONSOLIDATED_BRANCH"

echo "Push consolidated branch: git push -u $BASE_REMOTE $CONSOLIDATED_BRANCH"
echo "Open PR: https://github.com/epartscart/ecomae/compare/$BASE_BRANCH...$CONSOLIDATED_BRANCH"
echo "After the consolidated PR is open, close duplicate/conflicting PRs #$START_PR through #$END_PR."
