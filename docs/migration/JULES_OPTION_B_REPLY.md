# Jules Option B Handoff Reply

Use this response when Jules provides an Option B handoff package but cannot push branches, close pull requests, or merge in GitHub.

## Reply to Jules

Jules, thank you. I accept the Option B handoff, but GitHub cleanup and production deployment are not complete until an authenticated operator executes them.

Please provide the actual transferable artifact, not only instructions:

1. Attach or publish `aspnet-core-final.patch` generated from the stated base commit to the final commit.
2. Attach or publish `aspnet-core-final.diffstat.txt`.
3. Confirm the final base commit, final commit, branch name, and PR number to keep.
4. Confirm that PR cleanup remains blocked from your environment because authenticated GitHub write access is unavailable.
5. Do not mark old PR cleanup complete unless GitHub shows every superseded Codex PR closed and only the final consolidated PR remains open.

Codex/admin will then verify the handoff by applying or fetching the final branch, running the detailed foundation checks, running the PHP route/lint checks, and running the .NET tests in a .NET-capable environment.

## Authenticated operator checklist

Run these only from a machine with GitHub access and `gh auth status` passing:

```bash
export KEEP_PR=542
GITHUB_REPOSITORY=epartscart/ecomae KEEP_PR="$KEEP_PR" bash scripts/cleanup_codex_prs.sh
```

After reviewing the dry run:

```bash
GITHUB_REPOSITORY=epartscart/ecomae KEEP_PR="$KEEP_PR" RUN_CLOSE=1 bash scripts/cleanup_codex_prs.sh
```

Merge only after the final PR is conflict-free and green.

## Production reminder

After merge, deploy ASP.NET Core diagnostics-only first:

- expose only `/health` and `/migration/*`;
- keep PHP fallback enabled;
- do not proxy broad `/api`, `/cp`, `/erp`, `/bos`, or storefront routes;
- keep worker writes disabled or dry-run only.
