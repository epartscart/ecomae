# PR #569 CloudPanel Quick Fix and Detached Deploy

Use this page when the CloudPanel terminal shows a prompt like `root@srv...:~#` and commands such as `bash scripts/resolve_pr_569_conflicts.sh`, `git status`, or `git rebase --continue` fail with `No such file or directory` or `not a git repository`.

The failure means the shell is in `/root`, not in the EcomAE repository checkout. First find or clone the repo, then run PR and deploy commands from that repo root.

## 1. Paste-safe repo finder from any CloudPanel directory

Paste this whole block exactly at the CloudPanel SSH prompt:

```bash
set -euo pipefail
ECOMAE_REPO="$(find /var/www /opt /root -maxdepth 6 -type f -path '*/scripts/preflight_aspnet_production.sh' -print -quit 2>/dev/null | sed 's#/scripts/preflight_aspnet_production.sh##')"

if [ -z "$ECOMAE_REPO" ]; then
  echo "EcomAE repo not found under /var/www, /opt, or /root. Cloning a fresh working copy to /opt/ecomae-aspnet-source."
  mkdir -p /opt
  cd /opt
  if [ ! -d /opt/ecomae-aspnet-source/.git ]; then
    git clone https://github.com/epartscart/ecomae.git ecomae-aspnet-source
  fi
  ECOMAE_REPO="/opt/ecomae-aspnet-source"
fi

cd "$ECOMAE_REPO"
echo "Repo root: $(pwd)"
git status --short
```

Do not run any `scripts/...` command until `pwd` prints the repository path and `git status --short` does not say `not a git repository`.

## 2. Repair PR #569 conflicts from the repo root

Run this from the repo root found above:

```bash
git fetch origin main
if command -v gh >/dev/null 2>&1; then
  gh pr checkout 569
else
  git fetch origin pull/569/head:pr-569-conflict-fix
  git checkout pr-569-conflict-fix
fi

git rebase origin/main
```

If Git reports conflicts, resolve the listed files, then continue:

```bash
git status --short
git add <resolved-files>
git rebase --continue
```

After the rebase is clean, run the local validation commands:

```bash
bash tests/aspnet_migration/run_foundation_checks.sh
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/verify_aspnet_proxy_guardrails.sh
bash scripts/run_zero_php_final_gate_checklist.sh
python3 scripts/verify_zero_php_90_readiness.py || true
if command -v dotnet >/dev/null 2>&1; then dotnet test aspnet/tests/EcomAE.Platform.Tests; else echo "WARN: dotnet SDK is not installed; skipping dotnet test"; fi
```

Final-gate / 90% readiness must still report **NOT READY** until real route/job parity, rollback approval, and production smoke evidence exists for all tracked PHP route/job items.

## 3. Push the repaired PR branch

If `gh pr checkout 569` was used, update the PR branch with:

```bash
git push --force-with-lease
```

If `gh` was not available, the local `pr-569-conflict-fix` branch was created from GitHub's read-only pull ref. PR #569's source branch is `codex/audit-project-performance-of-agent-n5lnu3`, so push the rebased branch back there:

```bash
git push --force-with-lease origin HEAD:codex/audit-project-performance-of-agent-n5lnu3
```

If Git prompts for `Username for 'https://github.com':`, authenticate with a GitHub account/token that has permission to push to `epartscart/ecomae`, or install/login GitHub CLI first:

```bash
apt update
apt install -y gh
gh auth login
gh pr checkout 569
git push --force-with-lease
```

## 4. Deploy after PR #569 is merged

After the fixed PR is merged into `main`, update the production checkout and deploy diagnostics-only ASP.NET in a detached process:

```bash
cd "$ECOMAE_REPO"
git fetch origin
git checkout main
git pull --ff-only origin main

sudo mkdir -p /var/www/ecomae-aspnet/releases /etc/ecomae-aspnet
if [ ! -f /etc/ecomae-aspnet/platform.env ]; then
  sudo install -m 0600 deploy/aspnet/platform.env.example /etc/ecomae-aspnet/platform.env
  echo "Edit /etc/ecomae-aspnet/platform.env before deploying."
  sudo nano /etc/ecomae-aspnet/platform.env
fi

bash scripts/preflight_aspnet_production.sh
sudo env ECOMAE_RUN_SYSTEMD=1 ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet bash scripts/deploy_aspnet_foundation_detached.sh
```

Follow the deploy log printed by the detached wrapper. If no log path is visible, use:

```bash
tail -f deploy/logs/aspnet-foundation-deploy-*.log
```

## 5. Safety boundary

This deploy is diagnostics-only. Keep PHP fallback enabled and do not broad-proxy `/cp`, `/erp`, `/bos`, `/api`, or storefront traffic to ASP.NET until exact route/job parity and rollback evidence is approved.
