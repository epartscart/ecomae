# PR Rebase and Conflict Runbook

Use this runbook when several ASP.NET migration PRs contain overlapping commits and GitHub reports conflicts against `main`.

## Recommended resolution

Do not try to merge every duplicate PR. Create one consolidated branch from latest `origin/main`, copy the final migration tree, and open one replacement PR.

```bash
git checkout work
git pull --ff-only origin work
scripts/rebase_conflicted_pr_range.sh 500 508
git push -u origin aspnet-migration-consolidated
```

Then open:

```text
https://github.com/epartscart/ecomae/compare/main...aspnet-migration-consolidated
```

Close the duplicate/conflicting PRs after the consolidated PR is open.

## Why this avoids conflicts

The script does not merge old PR histories. It creates a clean branch from latest `origin/main` and uses `prepare_consolidated_aspnet_pr.sh` to copy the final migration file state from the source branch. That avoids repeated conflicts in files such as `Program.cs`, `EcomAeRoutes.cs`, the migration plan, and the foundation check script.

## Secret handling

Do not place GitHub tokens or login passwords in commands, commits, PR descriptions, or shell history. Use GitHub credential manager, `gh auth login`, or secure CI secrets.
