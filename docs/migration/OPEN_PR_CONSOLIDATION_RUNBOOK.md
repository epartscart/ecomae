# Open Codex PR Consolidation Runbook

Use this before production deployment when many Codex-generated PRs are open and GitHub reports merge conflicts. Keep one final consolidated PR, close the superseded PRs, and merge only the clean final branch.

## Why this is required

Multiple PRs created from different migration iterations can overlap the same files. Leaving all of them open creates conflict noise and makes it unclear which branch should be deployed. Production should use one reviewed, current, conflict-free PR.

## Step 1: choose the single PR to keep

Use the newest final migration PR unless a maintainer explicitly selects another PR number. Record that number as `KEEP_PR`.

```bash
export KEEP_PR=<FINAL_PR_NUMBER>
```

## Step 2: dry-run the close plan

```bash
GITHUB_REPOSITORY=epartscart/ecomae KEEP_PR="$KEEP_PR" bash scripts/cleanup_codex_prs.sh
```

The dry run prints every open `codex` PR it would close and the one PR it will keep.

## Step 3: close superseded PRs

Only after confirming the dry-run list:

```bash
GITHUB_REPOSITORY=epartscart/ecomae KEEP_PR="$KEEP_PR" RUN_CLOSE=1 bash scripts/cleanup_codex_prs.sh
```

The script comments on each superseded PR before closing it.

## Step 4: update the final PR branch

Rebase or recreate the final branch from the latest `main`, then push it and wait for checks:

```bash
git fetch origin main
git checkout <FINAL_BRANCH>
git rebase origin/main
git push --force-with-lease
```

If rebase is risky, use the consolidated checkout helper already in this repo to rebuild a clean branch from `origin/main`.

## Step 5: merge only after checks pass

Merge the final PR only when:

- GitHub shows no merge conflicts.
- Required checks are green.
- The PR title/body clearly identify it as the final consolidated ASP.NET migration foundation PR.
- Deployment runbooks and diagnostics-only guardrails are included.

After merge, deploy from `main` or from the approved release tag created from `main`.
