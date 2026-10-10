# Production update paste (completed ASP.NET so far)

This is the **only** supported way to put completed ASP.NET work on the live
CloudPanel host. It republishes `:5100` from **`main`**. PHP stays the
authoritative fallback. It does **not** remove PHP. It does **not** cut over
broad `/api`, `/cp`, `/erp`, `/bos`, or storefront locations.

## What this does **not** do

- It does **not** deploy the in-flight Cursor stacked branches. Those stay
  off production until they merge to `main` and a release owner approves.
- Merge to `main` alone does **not** update live. Kestrel keeps the last
  published binary until this paste republishes `/var/www/ecomae-aspnet`.
- Tenant onboard kernel / social publish twins are **not** live UI until
  those branches merge **and** this paste runs.
- Weighted headline stays a migration-gate number (~20.4%). This paste is
  not 100% completion and is not ERP acceptance.

## 1. Preferred one-paste (root on CloudPanel)

```bash
set -euxo pipefail
export ECOMAE_BRANCH=main
export ECOMAE_SKIP_LIFEOS_MP4=YES
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_redeploy_final_gate_branch.sh)"
curl -fsS -o /dev/null -w 'health=%{http_code}\n' http://127.0.0.1:5100/health
curl -fsS -o /dev/null -w 'ready=%{http_code}\n' http://127.0.0.1:5100/ready || true
curl -fsS http://127.0.0.1:5100/health
```

PASS: the script finishes without error, `/health` is **200**, and PHP-FPM /
the existing CloudPanel site stay enabled.

## 2. If the server already has `/opt/ecomae-aspnet-source`

```bash
set -euxo pipefail
cd /opt/ecomae-aspnet-source
git fetch origin main
git checkout -f main
git reset --hard origin/main
export ECOMAE_BRANCH=main
export ECOMAE_RUN_SYSTEMD=1
bash scripts/cloudpanel_find_and_redeploy.sh
bash scripts/wait_for_aspnet_health.sh
curl -i http://127.0.0.1:5100/health
curl -i http://127.0.0.1:5100/migration/status
```

Do **not** `cd /var/www/ecomae`. That path is usually missing. Source is
`/opt/ecomae-aspnet-source` or `/root/ecomae`. Releases are
`/var/www/ecomae-aspnet`.

## 3. First-time foundation (env file missing)

```bash
sudo mkdir -p /etc/ecomae-aspnet /var/www/ecomae-aspnet/releases
sudo install -m 0600 /opt/ecomae-aspnet-source/deploy/aspnet/platform.env.example \
  /etc/ecomae-aspnet/platform.env
sudo nano /etc/ecomae-aspnet/platform.env
```

Fill `ConnectionStrings__TenantRegistry` with real Server/Database/User/Password.
Keep `MigrationRouteCutover__RequirePhpFallback=true`. Then:

```bash
cd /opt/ecomae-aspnet-source
export ECOMAE_BRANCH=main ECOMAE_RUN_SYSTEMD=1
bash scripts/cloudpanel_production_deploy_foundation.sh
```

## 4. Rollback (PHP takes the route again)

```bash
# 1) Remove the ASP.NET location block for the affected exact route
sudo nginx -t && sudo systemctl reload nginx
# 2) Optional: point the release symlink at the previous stamp
sudo ECOMAE_RUN_SYSTEMD=1 bash /opt/ecomae-aspnet-source/scripts/rollback_aspnet_foundation.sh \
  /var/www/ecomae-aspnet/releases/<previous-release>
```

## 5. After this paste

- Confirm `/health` and `/ready` on `127.0.0.1:5100`.
- Confirm storefront / CP / ERP still answer from PHP for uncut routes.
- Do not enable `ECOMAE_ENABLE_PRICE_LOOKUP_SHADOW` from the foundation script.
- Exact-route cutover still needs `/migration/readiness`, dual samples, and
  release-owner approval (`deploy/aspnet/GO_LIVE_CHECKLIST.md`).
