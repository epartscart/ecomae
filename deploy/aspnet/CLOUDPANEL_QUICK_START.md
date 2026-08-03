# CloudPanel ASP.NET Core Foundation Quick Start

The command `bash scripts/preflight_aspnet_production.sh` must be run from the repository root. If CloudPanel shows `No such file or directory`, you are not inside the checked-out repo yet or the repository has not been cloned to that server.

**Do not use `/var/www/ecomae`.** That path is usually missing. Source checkouts live under `/root/ecomae` or `/opt/ecomae-aspnet-source`. Published releases live under `/var/www/ecomae-aspnet`.

PR #603 (ensure→issue smoke unlock) is on **main**. Preferred paste-safe redeploy + capture:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_redeploy_final_gate_branch.sh)"
```

Or refresh `/opt/ecomae-aspnet-source` to latest main, then deploy:

```bash
mkdir -p /opt
cd /opt
if [ ! -d ecomae-aspnet-source/.git ]; then
  git clone https://github.com/epartscart/ecomae.git ecomae-aspnet-source
fi
cd /opt/ecomae-aspnet-source
git fetch origin main
git checkout -f main
git reset --hard origin/main
bash scripts/cloudpanel_find_and_redeploy.sh
```

If `scripts/cloudpanel_find_and_redeploy.sh` is still missing, your checkout never updated. Run:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"
```

**Common mistake:** `cd /opt/ecomae-aspnet-source` then run a new script without `git fetch/reset` first. An old checkout will not contain newly merged scripts.

Do not paste the example path `/path/to/ecomae-repo` literally. Replace it with the real repository path found by the commands below.

## 1. Paste-safe repo finder

Run this exact block on the CloudPanel SSH shell:

```bash
ECOMAE_REPO="$(find /var/www /opt /root -maxdepth 5 -type f -path '*/scripts/preflight_aspnet_production.sh' -print -quit 2>/dev/null | sed 's#/scripts/preflight_aspnet_production.sh##')"
if [ -n "$ECOMAE_REPO" ]; then
  cd "$ECOMAE_REPO" && pwd && git status --short
else
  echo "ECOMAE repo not found on this server. Clone it first, then re-run this block."
fi
```

If this prints `ECOMAE repo not found`, the source code is not present on the server yet. Follow `deploy/aspnet/CLOUDPANEL_MISSING_REPO_RECOVERY.md` to clone the approved branch or tag, then rerun this finder.

## 2. Clone the repository if it is missing

Use the actual approved repository URL and branch or tag from your release process:

```bash
mkdir -p /opt
cd /opt
git clone <REPO_URL> ecomae-aspnet-source
cd /opt/ecomae-aspnet-source
git checkout <APPROVED_BRANCH_OR_TAG>
```

After cloning, confirm that the script exists:

```bash
test -f scripts/preflight_aspnet_production.sh && echo "preflight script found"
```

## 3. Run local repository checks from the repo root

```bash
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/verify_aspnet_proxy_guardrails.sh
```

## 4. Prepare production-only environment file

```bash
sudo mkdir -p /etc/ecomae-aspnet /var/www/ecomae-aspnet/releases
sudo install -m 0600 deploy/aspnet/platform.env.example /etc/ecomae-aspnet/platform.env
sudo nano /etc/ecomae-aspnet/platform.env
```

If `nano` opens `/etc/ecomae-aspnet/platform.env`:

1. Confirm `ConnectionStrings__TenantRegistry=...` has real Server/Database/User/Password (no `<db_user>` placeholders).
2. Keep these flags as-is for now (PHP remains authoritative):
   - `MigrationRouteCutover__StorefrontAspNetEnabled=false`
   - `MigrationRouteCutover__AdminAspNetEnabled=false`
   - `MigrationRouteCutover__RequirePhpFallback=true`
3. Optional for the final 5% smoke gate (add at bottom if you have real keys):
   - `ECOMAE_PRICE_LOOKUP_API_KEY=epc_pricepro_...`
   - `ECOMAE_CATALOG_API_KEY=epc_catalog_...`
4. Save: `Ctrl+O`, then `Enter`.
5. Exit: `Ctrl+X`.
6. Continue deploy:

```bash
cd /opt/ecomae-aspnet-source   # or /root/ecomae — your real repo path
bash scripts/cloudpanel_continue_after_env.sh
```

After deploy succeeds, issue smoke secrets then capture (still does not remove PHP):

```bash
cd /opt/ecomae-aspnet-source
git fetch origin main && git checkout -f main && git reset --hard origin/main
ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh
ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES bash scripts/cloudpanel_issue_smoke_credentials.sh
source /etc/ecomae-aspnet/platform.env
bash scripts/cloudpanel_validate_final_gate_env.sh
bash scripts/cloudpanel_capture_final_gate_artifacts.sh
bash scripts/cloudpanel_commit_final_gate_smoke.sh
```

Replace placeholders in `/etc/ecomae-aspnet/platform.env`. Never commit production secrets. Never flip broad storefront/admin ASP.NET flags from this file until exact-route smoke + release-owner approval exist.

## 5. Run preflight from the repo root

```bash
bash scripts/preflight_aspnet_production.sh
```

The preflight should only pass after .NET, PHP, curl, release directories, and the server-only environment file are ready.

## 6. Deploy diagnostics-only ASP.NET Core

One-shot from repo root (preferred):

```bash
cd /root/ecomae   # or the real repo path from step 1
sudo ECOMAE_BRANCH=main \
ECOMAE_RUN_SYSTEMD=1 \
ECOMAE_INSTALL_DIAGNOSTICS_NGINX=0 \
bash scripts/cloudpanel_production_deploy_foundation.sh
```

Or the lower-level deploy:

```bash
sudo ECOMAE_RUN_SYSTEMD=1 \
ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet \
bash scripts/deploy_aspnet_foundation.sh
```

Then expose only `/health` and allowlisted `/migration/*` through CloudPanel/Nginx. Do not proxy broad CP/ERP/BOS/API/storefront routes until route-level parity is approved. Never set `ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW=1` in the one-shot script; that flag is rejected until staging smoke evidence exists.
