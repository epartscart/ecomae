# CloudPanel ASP.NET Foundation Quick Start

The command `bash scripts/preflight_aspnet_production.sh` must be run from the repository root. If CloudPanel shows `No such file or directory`, you are not inside the checked-out repo yet.

## 1. Find or clone the repository

```bash
pwd
find /var/www /opt /root -maxdepth 3 -type d -name .git 2>/dev/null
```

If the repository is not present, clone it to a stable source directory:

```bash
mkdir -p /opt
cd /opt
git clone <REPO_URL> ecomae-aspnet-source
cd /opt/ecomae-aspnet-source
git checkout <APPROVED_BRANCH_OR_TAG>
```

If it is already present, `cd` into that repository root before running scripts:

```bash
cd /path/to/ecomae-repo
git status --short
```

## 2. Run local repository checks

```bash
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/verify_aspnet_proxy_guardrails.sh
```

## 3. Prepare production-only environment file

```bash
sudo mkdir -p /etc/ecomae-aspnet /var/www/ecomae-aspnet/releases
sudo install -m 0600 deploy/aspnet/platform.env.example /etc/ecomae-aspnet/platform.env
sudo nano /etc/ecomae-aspnet/platform.env
```

Replace placeholders in `/etc/ecomae-aspnet/platform.env`. Never commit production secrets.

## 4. Run preflight again from the repo root

```bash
cd /opt/ecomae-aspnet-source
bash scripts/preflight_aspnet_production.sh
```

The preflight should only pass after .NET, PHP, curl, release directories, and the server-only environment file are ready.

## 5. Deploy diagnostics-only ASP.NET

```bash
sudo ECOMAE_RUN_SYSTEMD=1 \
ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet \
bash scripts/deploy_aspnet_foundation.sh
```

Then expose only `/health` and allowlisted `/migration/*` through CloudPanel/Nginx. Do not proxy broad CP/ERP/BOS/API/storefront routes until route-level parity is approved.
