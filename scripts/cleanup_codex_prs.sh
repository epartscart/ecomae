#!/usr/bin/env bash
set -euo pipefail

REPO="${GITHUB_REPOSITORY:-epartscart/ecomae}"
LABEL="${CODEX_PR_LABEL:-codex}"
KEEP_PR="${KEEP_PR:-}"
RUN_CLOSE="${RUN_CLOSE:-0}"
COMMENT="${COMMENT:-Superseded by the consolidated ASP.NET Core migration PR. Keeping one clean PR avoids conflict churn before production deployment.}"

if ! command -v gh >/dev/null 2>&1; then
  echo "gh CLI is required. Install GitHub CLI and run: gh auth login" >&2
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "gh is not authenticated. Run: gh auth login" >&2
  exit 1
fi

mapfile -t prs < <(gh pr list --repo "$REPO" --state open --label "$LABEL" --json number,updatedAt --jq 'sort_by(.updatedAt) | reverse | .[].number')

if [ "${#prs[@]}" -eq 0 ]; then
  echo "No open PRs found with label '$LABEL' in $REPO."
  exit 0
fi

if [ -z "$KEEP_PR" ]; then
  KEEP_PR="${prs[0]}"
fi

echo "Repository: $REPO"
echo "Label: $LABEL"
echo "Keeping PR #$KEEP_PR"
echo "RUN_CLOSE=$RUN_CLOSE"

for pr in "${prs[@]}"; do
  if [ "$pr" = "$KEEP_PR" ]; then
    echo "KEEP   #$pr"
    continue
  fi

  if [ "$RUN_CLOSE" = "1" ]; then
    echo "CLOSE  #$pr"
    gh pr comment "$pr" --repo "$REPO" --body "$COMMENT"
    gh pr close "$pr" --repo "$REPO"
  else
    echo "DRYRUN close #$pr"
  fi
done

if [ "$RUN_CLOSE" != "1" ]; then
  cat <<NEXT

Dry run only. To close all other open '$LABEL' PRs and keep #$KEEP_PR, run:
  KEEP_PR=$KEEP_PR RUN_CLOSE=1 bash scripts/cleanup_codex_prs.sh
NEXT
fi
