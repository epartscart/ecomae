# Cursor Handoff Status: ASP.NET Core Migration Foundation

## Handoff summary

The ASP.NET Core migration foundation work in this repository is complete for a diagnostics-only production rollout. The remaining work is operational: consolidate GitHub PRs, merge one final branch, clone the merged code to CloudPanel, run preflight, publish the app, start systemd, expose only diagnostics routes, and run live smoke checks.

## Completion percentages

| Area | Completion | Notes |
| --- | ---: | --- |
| Repository foundation | 100% | ASP.NET solution, platform host, worker host, route constants, middleware, modules, parity reporters, PHP alias compatibility, and deterministic test scripts are present. |
| Deployment guardrails | 100% | Production runbook, systemd units, Nginx diagnostics snippets, rollback script, proxy guardrail verifier, CloudPanel quick start, and missing-repo recovery docs are present. |
| GitHub merge readiness | 60% | Tooling exists, but an authenticated maintainer must close superseded Codex PRs and merge one final PR. |
| CloudPanel server readiness | 20% | The server screenshot showed the repo was not cloned; clone/checkout and server preflight are still pending. |
| Diagnostics-only production go-live | 55% | Ready after final PR merge, server clone, preflight, publish, service start, diagnostics proxy, and live smoke. |
| Full PHP replacement | 30-35% | Business workflow parity, data replay, auth/session parity, workers, and exact route approvals remain before broad cutover. |

## Cursor's first tasks

1. Keep one final ASP.NET migration PR and close superseded Codex-labeled PRs using `docs/migration/OPEN_PR_CONSOLIDATION_RUNBOOK.md` and `scripts/cleanup_codex_prs.sh`.
2. Rebase or recreate the final branch from `origin/main` and merge only after GitHub reports no conflicts and checks are green.
3. Clone the merged repository to CloudPanel using `deploy/aspnet/CLOUDPANEL_MISSING_REPO_RECOVERY.md` if the repo is still absent.
4. Run `bash tests/aspnet_migration/run_detailed_foundation_tests.sh` on the server.
5. Run `bash scripts/preflight_aspnet_production.sh` on the server.
6. Publish with `bash scripts/deploy_aspnet_foundation.sh`, using `ECOMAE_RUN_SYSTEMD=1` only when ready to install/restart systemd units.
7. Expose only `/health` and allowlisted `/migration/*` routes in CloudPanel/Nginx for the first production step.
8. Run live smoke from an approved network with `RUN_LIVE_ECOMAE_SMOKE=1` and credentials supplied from a secret manager.

## Do not do yet

- Do not remove PHP fallback.
- Do not proxy broad `/api`, `/cp`, `/erp`, `/bos`, or storefront locations.
- Do not enable worker write jobs beyond dry-run until job parity is approved.
- Do not commit production credentials or paste secrets into PR comments.

## Required evidence for diagnostics-only go-live

- One final merged PR, with older Codex PRs closed.
- `tests/aspnet_migration/run_detailed_foundation_tests.sh` passes on the deployment host.
- `scripts/preflight_aspnet_production.sh` passes on the deployment host.
- `systemctl status ecomae-platform.service --no-pager` shows the service running.
- `curl -i http://127.0.0.1:5100/health` returns healthy from the server.
- CloudPanel/Nginx exposes only `/health` and allowlisted `/migration/*` routes.
- Live smoke checks pass without printing secrets.

## Final status

This handoff is ready for Cursor to manage the operational merge and production rollout. The code foundation and safety tooling are complete; the remaining 45% to diagnostics-only go-live is environment and GitHub operations that require authenticated access outside this container.
