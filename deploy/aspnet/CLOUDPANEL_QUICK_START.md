# CloudPanel ASP.NET Core Foundation Quick Start

The command `bash scripts/preflight_aspnet_production.sh` must be run from the repository root. If CloudPanel shows `No such file or directory`, you are not inside the checked-out repo yet or the repository has not been cloned to that server.

**Do not use `/var/www/ecomae`.** That path is usually missing. Source checkouts live under `/root/ecomae` or `/opt/ecomae-aspnet-source`. Published releases live under `/var/www/ecomae-aspnet`.

After a merged PR, prefer the paste-safe one-shot:

```bash
# If you already have a checkout somewhere:
ECOMAE_REPO="$(find /var/www /opt /root -maxdepth 6 -type f -path '*/scripts/cloudpanel_find_and_redeploy.sh' -print -quit 2>/dev/null | sed 's#/scripts/cloudpanel_find_and_redeploy.sh##')"
if [ -n "$ECOMAE_REPO" ]; then
  cd "$ECOMAE_REPO" && bash scripts/cloudpanel_find_and_redeploy.sh
else
  mkdir -p /opt && cd /opt
  git clone https://github.com/epartscart/ecomae.git ecomae-aspnet-source
  cd /opt/ecomae-aspnet-source && bash scripts/cloudpanel_find_and_redeploy.sh
fi
```

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
2. Save: `Ctrl+O`, then `Enter`.
3. Exit: `Ctrl+X`.
4. Continue deploy:

```bash
cd /root/ecomae   # or your real repo path
bash scripts/cloudpanel_continue_after_env.sh
```

Replace placeholders in `/etc/ecomae-aspnet/platform.env`. Never commit production secrets.

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
