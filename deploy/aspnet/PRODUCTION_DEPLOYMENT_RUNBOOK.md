# EcomAE ASP.NET Foundation Production Deployment Runbook

This runbook deploys the ASP.NET Core migration foundation beside the existing PHP application. PHP must remain authoritative until parity/cutover reports prove a route is safe to move.

## Safety rules

1. Rotate any password shared outside the secret manager before or immediately after the deployment window.
2. Keep PHP-FPM and the existing CloudPanel site enabled.
3. Start ASP.NET on `127.0.0.1` only.
4. Expose `/health` and allowlisted `/migration/*` first.
5. Cut over one exact route at a time only after parity approval.
6. Roll back by removing ASP.NET proxy blocks so PHP handles the route again.

## Files in this folder

| File | Purpose |
| --- | --- |
| `platform.env.example` | Copy to `/etc/ecomae-aspnet/platform.env` and fill server-only values. |
| `ecomae-platform.service` | systemd unit for the ASP.NET platform host. |
| `ecomae-workers.service` | systemd unit for migration workers; keep disabled until worker dry-runs are approved. |
| `nginx-diagnostics-only.conf` | Safe first CloudPanel/Nginx include for `/health` and allowlisted `/migration/*`. |
| `nginx-api-shadow-example.conf` | Example exact-route ASP.NET API proxy block after parity approval. |
| `cloudpanel-site-include.template.conf` | CloudPanel include wrapper for diagnostics-only first exposure. |
| `GO_LIVE_CHECKLIST.md` | Per-route approval checklist for exact-match cutovers. |
| `remote-deploy.env.example` | Local operator template for dry-run-first SSH deployment automation. |
| `CLOUDPANEL_QUICK_START.md` | Troubleshooting-first command sequence for CloudPanel shells that are not in the repo root. |
| `CLOUDPANEL_MISSING_REPO_RECOVERY.md` | Paste-safe clone flow when the repo finder confirms no checkout exists on the server. |

## Step 1: prepare server directories

```bash
sudo mkdir -p /var/www/ecomae-aspnet/releases /etc/ecomae-aspnet
sudo chown -R "$USER":"$USER" /var/www/ecomae-aspnet
sudo install -m 0600 deploy/aspnet/platform.env.example /etc/ecomae-aspnet/platform.env
sudo nano /etc/ecomae-aspnet/platform.env
```

Do not commit real secrets. Set `ConnectionStrings__TenantRegistry` only on the server or in a secret manager.

## Step 2: run deterministic checks

```bash
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/preflight_aspnet_production.sh
bash scripts/verify_aspnet_proxy_guardrails.sh
```

This runs foundation wiring, PHP alias regression, PHP syntax, shell syntax, optional .NET tests, optional live smoke checks, a production preflight for required commands, environment-file safety, PHP fallback, and proxy guardrails that block broad route cutover.

## Step 3: publish the application

```bash
DOTNET_CONFIGURATION=Release \
ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet \
bash scripts/deploy_aspnet_foundation.sh
```

For systemd installation/restart in the same run, execute as a privileged operator:

```bash
sudo ECOMAE_RUN_SYSTEMD=1 \
ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet \
bash scripts/deploy_aspnet_foundation.sh
```

## Step 4: install or verify systemd services manually

```bash
sudo cp deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service
sudo systemctl daemon-reload
sudo systemctl enable ecomae-platform.service
sudo systemctl restart ecomae-platform.service
sudo systemctl status ecomae-platform.service --no-pager
```

Keep `ecomae-workers.service` disabled until worker dry-run parity is approved.

## Step 5: local service verification

```bash
curl -i http://127.0.0.1:5100/health
curl -i http://127.0.0.1:5100/migration/status
curl -i http://127.0.0.1:5100/migration/readiness
curl -i http://127.0.0.1:5100/migration/progress
curl -i http://127.0.0.1:5100/migration/cutover-validation
```

## Step 6: expose diagnostics in CloudPanel/Nginx

1. Add the contents of `deploy/aspnet/nginx-diagnostics-only.conf` or `deploy/aspnet/cloudpanel-site-include.template.conf` to the existing site server block.
2. Replace `YOUR_OFFICE_IP` with the approved operator IP.
3. Validate and reload:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

## Step 7: live smoke from an allowed network

```bash
export RUN_LIVE_ECOMAE_SMOKE=1
export ECOMAE_BASE_URL="https://www.ecomae.com"
export ECOMAE_SUPER_USERNAME="<from-secret-manager>"
export ECOMAE_SUPER_PASSWORD="<from-secret-manager>"
export ECOMAE_SUPER_LOGIN_PATH="/login"
export ECOMAE_CLOUDPANEL_DASHBOARD_PATH="/dashboard"
bash tests/live_smoke/run_ecomae_surface_smoke.sh
```

The script redacts secrets and never prints passwords.

## Step 8: exact-route cutover after parity approval

Only after `/migration/readiness`, `/migration/data-parity`, `/migration/cutover-validation`, live smoke, business validation, and `GO_LIVE_CHECKLIST.md` agree, add an exact route proxy block like `nginx-api-shadow-example.conf`.

Do not proxy broad `/api/`, `/cp`, `/erp`, `/bos`, or storefront locations until each surface has parity evidence.

## Rollback

To roll back ASP.NET traffic immediately:

1. Remove the ASP.NET `location` block for the affected route.
2. Validate and reload Nginx:

```bash
sudo nginx -t
sudo systemctl reload nginx
```

3. Optionally move the release symlink back:

```bash
sudo ECOMAE_RUN_SYSTEMD=1 bash scripts/rollback_aspnet_foundation.sh /var/www/ecomae-aspnet/releases/<previous-release>
```

PHP remains the authoritative fallback throughout this phase.


## CloudPanel shell says script not found

If `bash scripts/preflight_aspnet_production.sh` returns `No such file or directory`, the shell is not inside the repository root or the repo has not been cloned to the server yet. Follow `deploy/aspnet/CLOUDPANEL_QUICK_START.md`: run the paste-safe repo finder. If it prints `ECOMAE repo not found`, use `deploy/aspnet/CLOUDPANEL_MISSING_REPO_RECOVERY.md` to clone the approved branch or tag. Then `cd` into the real repo path that contains `scripts/preflight_aspnet_production.sh` and run the preflight. Do not paste `/path/to/ecomae-repo` literally.

## Optional remote deployment automation

Prepare a local shell from `deploy/aspnet/remote-deploy.env.example`, keep `ECOMAE_RUN_REMOTE_DEPLOY=0` for the first run, and execute:

```bash
source deploy/aspnet/remote-deploy.env.example
bash scripts/remote_aspnet_foundation_deploy.sh
```

After reviewing the dry-run plan and ensuring SSH, repository access, `/etc/ecomae-aspnet/platform.env`, .NET, PHP, and release directories are ready on the server, set `ECOMAE_RUN_REMOTE_DEPLOY=1`. Keep `ECOMAE_RUN_NGINX_RELOAD=0` unless an approved exact diagnostics include has already been installed through CloudPanel/Nginx change control.
