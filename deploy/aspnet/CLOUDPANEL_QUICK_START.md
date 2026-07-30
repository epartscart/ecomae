# CloudPanel ASP.NET Foundation Quick Start

The command `bash scripts/preflight_aspnet_production.sh` must be run from the repository root. If CloudPanel shows `No such file or directory`, you are not inside the checked-out repo yet or the repository has not been cloned to that server.

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

If this prints `ECOMAE repo not found`, the source code is not present on the server yet.

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

Replace placeholders in `/etc/ecomae-aspnet/platform.env`. Never commit production secrets.

## 5. Run preflight from the repo root

```bash
bash scripts/preflight_aspnet_production.sh
```

The preflight should only pass after .NET, PHP, curl, release directories, and the server-only environment file are ready.

## 6. Deploy diagnostics-only ASP.NET

```bash
sudo ECOMAE_RUN_SYSTEMD=1 \
ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet \
bash scripts/deploy_aspnet_foundation.sh
```

Then expose only `/health` and allowlisted `/migration/*` through CloudPanel/Nginx. Do not proxy broad CP/ERP/BOS/API/storefront routes until route-level parity is approved.
