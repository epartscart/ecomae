#!/usr/bin/env bash
set -euo pipefail

REMOTE="${REMOTE:-origin}"
BASE_BRANCH="${BASE_BRANCH:-main}"
PR_NUMBER="${PR_NUMBER:-569}"
PR_BRANCH="${PR_BRANCH:-pr-${PR_NUMBER}-conflict-fix}"
RUN_PUSH="${RUN_PUSH:-0}"
RUN_CHECKS="${RUN_CHECKS:-1}"

printf '== EcomAE PR #%s conflict fixer ==\n' "$PR_NUMBER"
printf 'Remote: %s\n' "$REMOTE"
printf 'Base branch: %s\n' "$BASE_BRANCH"
printf 'Local fix branch: %s\n' "$PR_BRANCH"
printf 'Push enabled: %s\n' "$RUN_PUSH"

if ! command -v git >/dev/null 2>&1; then
  printf 'git is required\n' >&2
  exit 1
fi

if ! git diff --quiet || ! git diff --cached --quiet; then
  printf 'Working tree is not clean. Commit or stash changes before resolving PR conflicts.\n' >&2
  exit 1
fi

git fetch "$REMOTE" "$BASE_BRANCH"
git fetch "$REMOTE" "pull/${PR_NUMBER}/head:${PR_BRANCH}"
git checkout "$PR_BRANCH"

printf '\nRebasing PR #%s onto %s/%s...\n' "$PR_NUMBER" "$REMOTE" "$BASE_BRANCH"
if ! git rebase "$REMOTE/$BASE_BRANCH"; then
  cat <<HELP

Rebase stopped on conflicts.
Resolve them in the listed files, then run:
  git status --short
  git add <resolved-files>
  git rebase --continue

To abort and return to the pre-rebase PR branch:
  git rebase --abort

After the rebase completes, run checks:
  bash tests/aspnet_migration/run_detailed_foundation_tests.sh
  bash scripts/verify_aspnet_proxy_guardrails.sh
  python3 -m py_compile scripts/generate_zero_php_100_evidence_templates.py
  python3 scripts/verify_zero_php_90_readiness.py || true
  python3 scripts/verify_zero_php_100_readiness.py || true
HELP
  exit 2
fi

if [[ "$RUN_CHECKS" == "1" ]]; then
  bash tests/aspnet_migration/run_detailed_foundation_tests.sh
  bash scripts/verify_aspnet_proxy_guardrails.sh
  python3 -m py_compile scripts/generate_zero_php_100_evidence_templates.py
  python3 scripts/verify_zero_php_90_readiness.py || true
  python3 scripts/verify_zero_php_100_readiness.py || true
  if command -v dotnet >/dev/null 2>&1; then
    dotnet test aspnet/tests/EcomAE.Platform.Tests
  else
    printf 'WARN dotnet SDK is not installed; skipped dotnet test.\n'
  fi
fi

if [[ "$RUN_PUSH" == "1" ]]; then
  git push --force-with-lease "$REMOTE" "$PR_BRANCH"
  printf 'Pushed rebased PR branch %s. If #569 uses a different source branch, push to that branch instead after review.\n' "$PR_BRANCH"
else
  cat <<PLAN

Dry-run complete. Review the rebased branch locally.
To update the PR source branch after review, run one of these:
  RUN_PUSH=1 bash scripts/resolve_pr_569_conflicts.sh

If PR #569 uses a different source branch, push explicitly with:
  git push --force-with-lease $REMOTE HEAD:<pr-569-source-branch>
PLAN
fi
