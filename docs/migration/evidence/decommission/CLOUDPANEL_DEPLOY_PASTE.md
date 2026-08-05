# CloudPanel deploy paste (after PR merges → `main`)

Paste on the **production CloudPanel server** as root. Deploys latest `main` (includes human `RELEASE_OWNER_APPROVAL.md` + exact-route ASP.NET primary execute operator). Keeps PHP as **reference**; does not broad-cut `/api|/cp|/erp|/bos|/storefront`.

## 0) EXECUTE NOW — tenant-shared `/cp` `/erp` `/bos` `/` → ASP.NET (URL unchanged)

Release owner confirmed: **epartscart.com** and **ecomae.com** shared links must keep working as `/cp` `/erp` `/bos` (no change to tenant-facing URLs). PHP reference is **separate** under `/php-reference/*`.

```bash
# Pull latest main + republish ASP.NET
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_find_and_redeploy.sh)"

cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin main && git checkout -f main && git reset --hard origin/main

# www.ecomae.com + www.epartscart.com (URL-preserved proxies)
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts

bash scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh

# Or full operator (includes digests/presentation + classic-entry --all-hosts):
ECOMAE_CONFIRM_ASPNET_PRIMARY_CUTOVER=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_execute_aspnet_primary_cutover_operator.sh
```

**What this does / does not do**

| Does | Does not |
| --- | --- |
| Same-URL proxy: `/cp` `/erp` `/bos` `/` → ASP.NET on ecomae + epartscart | Redirect tenants to `/cp/app` (URL stays `/cp`) |
| PHP reference at `/php-reference/home\|cp\|erp\|bos\|storefront` | Broad `location /cp|/erp|/bos|/storefront` prefix trees |
| Keeps deep `/cp/...` module paths on PHP until per-route dual-sample | Delete PHP source / PHP-FPM / cron |
| Enables Admin/Storefront ASP.NET route flags (full operator) | Invent presentation/module PASS or `cutoverAllowed=true` |

## 1) One-shot find + redeploy

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_find_and_redeploy.sh)"
```

If the helper itself is missing / checkout is stale:

```bash
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_bootstrap_from_github.sh)"
# then re-run find+redeploy
bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_find_and_redeploy.sh)"
```

## 2) Explicit branch deploy (same effect)

```bash
export ECOMAE_BRANCH=main
export ECOMAE_RUN_SYSTEMD=1
export ECOMAE_RUN_NGINX_RELOAD=0
export ECOMAE_ASPNET_RELEASE_ROOT=/var/www/ecomae-aspnet
export ECOMAE_ASPNET_ENV_DIR=/etc/ecomae-aspnet

# If repo already exists:
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin main
git checkout -f main
git reset --hard origin/main
bash scripts/cloudpanel_production_deploy_foundation.sh
```

## 3) Health check

```bash
bash scripts/wait_for_aspnet_health.sh
curl -i http://127.0.0.1:5100/health
systemctl status ecomae-platform.service --no-pager
```

## 4) Confirm boards (after nginx can reach ASP.NET)

```bash
curl -sS https://www.ecomae.com/migration/php-reference-mode | jq '{status,mode,keepPhpProjectAvailable,storefrontAspNetEnabled,adminAspNetEnabled,requirePhpFallback,cutoverAllowed,readyForPhpRemoval}'
curl -sS https://www.ecomae.com/migration/aspnet-zero-php-path | jq '{targetEndState,status,cutoverAllowed,readyForPhpRemoval}'
curl -sS https://www.ecomae.com/migration/php-decommission-readiness | jq '{readyToRemovePhp,blockerCount,checklistCompletePercent}'
```

## 5) www shadow closeout only (subset of §0)

```bash
bash scripts/cloudpanel_www_shadow_closeout_preflight.sh
ECOMAE_CONFIRM_WWW_SHADOW_CLOSEOUT=YES bash scripts/cloudpanel_www_shadow_closeout_operator.sh
```

## 6) Rollback (keeps PHP reference)

```bash
bash scripts/rollback_aspnet_foundation.sh --keep-php-fallback
```

## Operator links (browser)

| Board | URL |
| --- | --- |
| PHP reference mode | https://www.ecomae.com/migration/php-reference-mode |
| Human compare (PHP ref vs ASP.NET) | https://www.ecomae.com/migration/compare |
| ASP.NET primary path | https://www.ecomae.com/migration/aspnet-zero-php-path |
| Live surface links | https://www.ecomae.com/migration/live-surface-links |
| Cutover validation | https://www.ecomae.com/migration/cutover-validation |
| PHP decommission readiness | https://www.ecomae.com/migration/php-decommission-readiness |
| Zero-PHP completion | https://www.ecomae.com/migration/zero-php-completion |
| Residual board (JSON) | packed under ContentRoot after deploy |
