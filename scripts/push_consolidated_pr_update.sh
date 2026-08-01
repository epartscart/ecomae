#!/usr/bin/env bash
set -euo pipefail

TARGET_BRANCH="${1:-aspnet-migration-consolidated}"
REMOTE="${REMOTE:-origin}"

if ! git rev-parse --verify "$TARGET_BRANCH" >/dev/null 2>&1; then
  echo "Target branch '$TARGET_BRANCH' does not exist locally." >&2
  echo "Run scripts/rebase_conflicted_pr_range.sh 500 508 first." >&2
  exit 1
fi

git checkout "$TARGET_BRANCH"
git status --short

if [[ -n "$(git status --short)" ]]; then
  echo "Working tree is not clean; commit or stash before pushing." >&2
  exit 1
fi

git push -u "$REMOTE" "$TARGET_BRANCH"

cat <<MSG

Pushed $TARGET_BRANCH.
Open/update a single consolidated PR:
  https://github.com/epartscart/ecomae/compare/main...$TARGET_BRANCH

Then close superseded/conflicted PRs #500 through #508 and link them to the consolidated PR.
MSG
