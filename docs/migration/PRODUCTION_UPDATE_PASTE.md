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

PASS: publish finishes, `/health` and `/ready` are HTTP **200** on
`127.0.0.1:5100`, and PHP-FPM / the existing CloudPanel site stay enabled.

The wrapper may then **exit 2** when smoke secrets are missing
(`ECOMAE_PRICE_LOOKUP_API_KEY`, `ECOMAE_CATALOG_API_KEY`,
`ECOMAE_ADMIN_COOKIE_HEADER`). That is **not** a failed publish. It only
skips final-gate capture. Do **not** invent API keys or admin cookies.

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

## 6. 2026-10-10 CloudPanel result (honest)

Operator paste on `srv1672837` as root, `ECOMAE_BRANCH=main`,
`ECOMAE_SKIP_LIFEOS_MP4=YES`.

| Check | Result |
|---|---|
| Git HEAD | `b7f1554bf` Merge PR #2068 |
| Release | `/var/www/ecomae-aspnet/releases/20261010085717` |
| Symlink | `current` → that stamp |
| `ecomae-platform.service` | active (running) |
| `/health` `/ready` | HTTP **200** after 8s, then 0s on the wait script |
| Nginx reload | skipped (foundation only; PHP remains fallback) |
| Smoke keys / admin cookie | **MISSING** — capture **BLOCKED**, exit 2 |
| PHP | still authoritative; not removed |

`ECOMAE_EMERGENCY_PUBLISH=1` skipped foundation/unit tests on the server.
Live binary is **main through #2068**. In-flight Cursor stacked branches
(Cove / Dock / Pier / this work) are **not** in that publish until they
merge to `main` and this paste runs again.

Preferred unblock for smoke capture (do not invent values):

```bash
bash scripts/cloudpanel_diagnose_smoke_db.sh
ECOMAE_CONFIRM_CREATE_API_CLIENTS_TABLE=YES bash scripts/cloudpanel_ensure_epc_api_clients_table.sh
ECOMAE_CONFIRM_ISSUE_SMOKE_CREDS=YES ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES \
  bash scripts/cloudpanel_issue_smoke_credentials.sh
source /etc/ecomae-aspnet/platform.env
bash scripts/cloudpanel_validate_final_gate_env.sh
bash scripts/cloudpanel_capture_final_gate_artifacts.sh
```

Login `https://www.ecomae.com/CP/` first if the admin session probe fails.
Do **not** remove PHP.

## 7. Disk / incoming-traffic check (2026-10-11)

CloudPanel Overview on `srv1672837` showed **incoming 118.3 MB** (red line) and
**disk 60 GB**. Measured on the box at 2026-10-11T07:22:12Z (read-only
`cloudpanel_disk_load_diagnose.sh`). That is not a proven attack:

- Load **0.85 / 1.06 / 1.06**, up 25 days. Memory 2.8 Gi used / 5.0 Gi
  available. Incoming card is red by default in CloudPanel; outgoing is blue.
- Root disk **96G, 61G used (63%)**. Inodes 7%. Kestrel `/health` `/ready`
  **200** on loopback (6 ms / 3 ms). `ecomae-platform.service` running.
- `/opt/ecomae-aspnet-source` on the box is **main** (641M). The diagnose
  script is not there until this branch merges — that is why the local path
  failed. Curl from this branch is fine.

Measured hogs (do not delete the ones marked keep):

| Path | Size | Action |
|---|---:|---|
| `/var/www/ecomae-aspnet/releases` (**273 stamps**) | **24G** | Keep `current` (`20261010085717`) + one previous. Prune the rest. |
| `/home/ecomae/backups` | 7.9G | Owner inspect, then delete old site backups from CloudPanel / that folder. |
| `/home/mysql` | 7.7G | Keep. Live MariaDB datadir. |
| `/home/ecomae/htdocs` | 3.6G | Keep. Live PHP site. |
| `/var/log` (syslog 632M + syslog.1 958M, journal 1.1G) | 2.7G | Vacuum journal / rotate syslog. nginx is only 76K. |
| `/home/clp/backups` | 1.5G | CloudPanel snapshots (4 on Overview) — keep 1 recent in the UI. |
| `/root/ecomae` | 1.8G | Keep unless you confirm it is a stale clone. |
| `/opt/ecomae-aspnet-source` | 641M | Keep. |
| Ten `php*-fpm` services (7.1–8.5) | inside 3.8G `/usr` | **Do not prune.** They are not the 61G. |

Safe first cut — dry-run, then delete only listed release stamps:

```bash
curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/small-gaps-storefront-done-5dbd/scripts/cloudpanel_prune_aspnet_releases.sh -o /tmp/cloudpanel_prune_aspnet_releases.sh
bash /tmp/cloudpanel_prune_aspnet_releases.sh
# read KEEP / PRUNE lines. Expect current 20261010085717 + newest previous kept.
KEEP_PREVIOUS=1 CONFIRM=I_HAVE_READ_THE_LIST bash /tmp/cloudpanel_prune_aspnet_releases.sh
curl -fsS -o /dev/null -w 'health=%{http_code}\n' http://127.0.0.1:5100/health
curl -fsS -o /dev/null -w 'ready=%{http_code}\n' http://127.0.0.1:5100/ready
df -h /
```

Optional second cuts after the release prune (still do not touch PHP-FPM or DBs):

```bash
journalctl --vacuum-size=200M
# CloudPanel UI: Snapshots → keep the newest, delete the other three.
# Inspect /home/ecomae/backups before any rm there.
```

Read-only report again:

```bash
curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/small-gaps-storefront-done-5dbd/scripts/cloudpanel_disk_load_diagnose.sh | bash
```
