#!/usr/bin/env bash
set -euo pipefail

REMOTE="${REMOTE:-origin}"
BASE_BRANCH="${BASE_BRANCH:-main}"
PR_NUMBER="${PR_NUMBER:-569}"
FALLBACK_BRANCH="${PR_BRANCH:-pr-${PR_NUMBER}-conflict-fix}"
RUN_PUSH="${RUN_PUSH:-0}"
RUN_CHECKS="${RUN_CHECKS:-1}"
USE_GH="${USE_GH:-auto}"

printf '== EcomAE PR #%s conflict fixer ==\n' "$PR_NUMBER"
printf 'Remote: %s\n' "$REMOTE"
printf 'Base branch: %s\n' "$BASE_BRANCH"
printf 'Fallback branch: %s\n' "$FALLBACK_BRANCH"
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

checkout_method="pull-ref"
if [[ "$USE_GH" != "0" ]] && command -v gh >/dev/null 2>&1; then
  printf 'Using gh pr checkout so pushes update the real PR branch when permissions allow.\n'
  gh pr checkout "$PR_NUMBER"
  checkout_method="gh"
else
  printf 'gh not available; fetching PR head into local fallback branch %s.\n' "$FALLBACK_BRANCH"
  git fetch "$REMOTE" "pull/${PR_NUMBER}/head:${FALLBACK_BRANCH}"
  git checkout "$FALLBACK_BRANCH"
fi

CURRENT_BRANCH="$(git branch --show-current)"
printf 'Current branch: %s\n' "$CURRENT_BRANCH"

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
  if [[ "$checkout_method" == "gh" ]]; then
    git push --force-with-lease
  else
    cat <<PUSHHELP

Fetched PR #$PR_NUMBER through a read-only pull ref. Push to the real PR source branch explicitly.
If the source branch is in this repository:
  git push --force-with-lease $REMOTE HEAD:<actual-pr-569-source-branch>

If it is from a fork, push to that fork remote/branch instead.
PUSHHELP
    exit 3
  fi
else
  cat <<PLAN

Rebase/check flow completed locally.
To update PR #$PR_NUMBER after review:
  RUN_PUSH=1 bash scripts/resolve_pr_569_conflicts.sh

If gh is unavailable, push explicitly to the actual PR source branch:
  git push --force-with-lease $REMOTE HEAD:<actual-pr-569-source-branch>
PLAN
fi
