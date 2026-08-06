# CloudPanel deploy paste (after PR merges → `main`)

Paste on the **production CloudPanel server** as root. Deploys latest `main` (includes human `RELEASE_OWNER_APPROVAL.md` + exact-route ASP.NET primary execute operator). Keeps PHP as **reference**; does not broad-cut `/api|/cp|/erp|/bos|/storefront`.

## 0🚨) CLICK → `/storefront/search-app` → warm-up → back to `/` (do immediately)

**What you see:** any menu click opens `/storefront/search-app`, shows “Loading your store…”, then returns to the homepage.

**Root cause:** classic-entry installer only wrote `location = /…` and **never installed** `location ^~ /storefront/ → :5100`. Home works; every `/storefront/search-app` click misses Kestrel → splash → splash JS sends you home.

```bash
# MUST use this branch (installer fix). Paste as root — wait for RESULT=PASS
ECOMAE_BRANCH=cursor/unbreak-epartscart-php-storefront-7b3b \
ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT=YES \
ECOMAE_ALSO_FORCE_LIVE=YES \
  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/unbreak-epartscart-php-storefront-7b3b/scripts/cloudpanel_unbreak_epartscart_storefront_now.sh)"

# Prove click target is ASP.NET (NOT splash):
curl -sS -o /tmp/sf.html -w 'search-app %{http_code} %{size_download}\n' https://www.epartscart.com/storefront/search-app
grep -q 'Loading your store' /tmp/sf.html && echo FAIL_STILL_SPLASH || echo PASS_SEARCH_APP
# Shopper: hard-refresh https://www.epartscart.com/ then click Catalog / search again
```

## 0◆) TEMP ASP.NET-ONLY DEEP TEST — pause PHP HTTP (incl. `/php-reference`)

`#885` is on `main`. Protocol: **no new PHP feature work**. Pause PHP HTTP so ASP.NET Core can be deep-tested. Files stay on disk (`KeepPhpProjectAvailable=true`). `cutoverAllowed` / `readyForPhpRemoval` stay **false**.

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin main && git checkout -f main && git reset --hard origin/main
ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES \
  bash scripts/cloudpanel_temporarily_deactivate_php_serving.sh
# Expect RESULT=PASS — /php-reference and /en/ → 503; product / /cp /erp stay ASP.NET
# Board: curl -sS https://www.epartscart.com/migration/php-reference-mode | jq '{status,mode,temporarilyDeactivatePhpServing,cutoverAllowed,readyForPhpRemoval,keepPhpProjectAvailable}'
# Restore: ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh
```

## 0⚠) CP AUTH + TOP MENU — FORCE LIVE then sync (paste as one block)

`#887` (CP auth) is on `main`. This paste also needs `#886` top-menu visibility until that merges. **Always `cd` into the repo** before `bash scripts/…` (running from `~` → “No such file”). Prefer **GET** proves — `curl -I` often returns **405** on `/cp`.

```bash
# 1) Publish ASP.NET binary (top-menu branch until #886 merges; then use main)
ECOMAE_BRANCH=cursor/storefront-header-topmenu-visible-7b3b \
  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/storefront-header-topmenu-visible-7b3b/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"
# Must print RESULT=PASS. On FAIL: bash scripts/cloudpanel_DIAGNOSE_STALE_HOME.sh

# 2) cd REPO (required) — sync SecretSuccession + classic-entry /cp/control → :5100
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin cursor/storefront-header-topmenu-visible-7b3b
git checkout -f cursor/storefront-header-topmenu-visible-7b3b
git reset --hard origin/cursor/storefront-header-topmenu-visible-7b3b
# after #886 merges: git fetch origin main && git checkout -f main && git reset --hard origin/main

ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
  bash scripts/cloudpanel_sync_secret_succession_from_php.sh

ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts

# 3) Prove (GET, not HEAD)
curl -sS -o /dev/null -w 'cp %{http_code} -> %{redirect_url}\n' https://www.epartscart.com/cp
curl -sS -o /dev/null -w 'cp/control %{http_code} -> %{redirect_url}\n' https://www.epartscart.com/cp/control
# expect 302 → …/cp/login
curl -sS https://www.epartscart.com/cp/login | grep -i 'no login' && echo FAIL guest-browse || echo PASS no-guest-browse
curl -sS https://www.epartscart.com/ | grep -Fq 'color:rgba(255,255,255,.88) !important' && echo PASS top-menu-visible || echo FAIL top-menu-stale
```

## 0★) STOREFRONT `/` STALE / TOP MENU INVISIBLE — FORCE LIVE only

Public `https://www.epartscart.com/` is nginx → `:5100/storefront/app`. PHP sync / marker updates are **not** enough. Until the hardened script prints `RESULT=PASS`, home stays on the old binary (`action="/storefront/search-app"`, dark bar + nero dark-gray links = **invisible top menu**).

```bash
ECOMAE_BRANCH=cursor/storefront-header-topmenu-visible-7b3b \
  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/storefront-header-topmenu-visible-7b3b/scripts/cloudpanel_FORCE_LIVE_NOW.sh)"
# After #886 merges: use main URL / ECOMAE_BRANCH=main
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
bash scripts/cloudpanel_DIAGNOSE_STALE_HOME.sh
```

## 0) EXECUTE NOW — tenant-shared `/cp` `/erp` `/` → ASP.NET (URL unchanged)

Release owner confirmed: **epartscart.com** and **ecomae.com** shared links must keep working as `/cp` `/erp` (no change to tenant-facing URLs). **Product `/bos` is Super-CP only** (`www.ecomae.com` / `cp.ecomae.com`) — tenant hosts must **404** `/bos` (confidential). PHP reference is **separate** under `/php-reference/*`.

### 0a) Pull fix + emergency restore + server-block scoped install

On this CloudPanel, **epartscart is a `server_name www.epartscart.com` block inside** `/etc/nginx/sites-enabled/www.ecomae.com.conf` (mega-conf). A file-scoped tenant install overwrites the www pack. Pull the server-block scoped installer first, restore, strip wildcard pollution, then install both hosts.

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin cursor/aspnet-same-style-cut-php-links-7b3b
git checkout -f cursor/aspnet-same-style-cut-php-links-7b3b
git reset --hard origin/cursor/aspnet-same-style-cut-php-links-7b3b
# after merge: git fetch origin main && git checkout -f main && git reset --hard origin/main

# Prefers labeled baks; fall back to older stamps.
ls -1t /root/www.ecomae.com.conf.bak.classic-entry-aspnet.* 2>/dev/null | head -n 20

# Restore mega-conf to state BEFORE the bad tenant overwrite when possible:
#   ...bak.classic-entry-aspnet.20260805072224 → after www pack, before tenant overwrite
cp -a /root/www.ecomae.com.conf.bak.classic-entry-aspnet.20260805072224 \
  /etc/nginx/sites-enabled/www.ecomae.com.conf

# Strip leftover classic-entry from industry wildcard (*.ecomae.com only):
python3 scripts/lib/ecomae_nginx_server_block_edit.py strip \
  /etc/nginx/sites-enabled/wildcard-ecomae --all-servers

nginx -t && systemctl reload nginx

bash scripts/cloudpanel_discover_epartscart_nginx_conf.sh
# Expect: EPARTSCART_VHOST=.../www.ecomae.com.conf + INSTALL_TARGET_HOST=www.epartscart.com

ECOMAE_CONFIRM_ENSURE_EPARTSCART_VHOST=YES \
  bash scripts/cloudpanel_ensure_epartscart_nginx_vhost.sh
# On mega-conf this is a no-op (prints NOTE); do not create a duplicate vhost.

# REQUIRED — redeploy ASP.NET binary BEFORE classic-entry probe.
# Nginx install alone leaves the OLD binary (still shows CONTROL / Admin users / ERP banners).
# PR #869+#870 are on main — publish main, not a stale cursor/* branch.
#
# FASTEST for CP/ERP chrome (#869):
#   bash scripts/cloudpanel_publish_cp_erp_chrome_now.sh
#
# Or hard-reset both checkouts to main then emergency publish:
export ECOMAE_BRANCH=main
for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    git -C "$d" fetch origin "$ECOMAE_BRANCH"
    git -C "$d" checkout -f "$ECOMAE_BRANCH"
    git -C "$d" reset --hard "origin/$ECOMAE_BRANCH"
    git -C "$d" rev-parse --short HEAD
  fi
done
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
# Source must include #869 markers before publish:
grep -n 'bindCpTopNav\|<span>Control</span>' aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor | head
grep -n 'Orders today' aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor | head
grep -n 'ns-dash\|nsChartAr' aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor | head
ECOMAE_EMERGENCY_PUBLISH=1 bash scripts/cloudpanel_find_and_redeploy.sh
systemctl restart ecomae-platform.service
curl -sS http://127.0.0.1:5100/health || true

# Prove #869 chrome is loaded (NOT old CONTROL / Admin users / epc-erp-banner):
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/cp \
  | grep -oE 'bindCpTopNav|<span>Control</span>|Orders today|>CONTROL<|Admin users' | sort -u
# expect: bindCpTopNav + Control + Orders today — NOT CONTROL / Admin users
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/erp \
  | grep -oE 'bindErpTopNav|ns-dash|nsChartAr|chart\.js@4\.4\.1|epc-erp-banner' | sort -u
# expect: bindErpTopNav + ns-dash + nsChartAr + chart.js — NOT epc-erp-banner
curl -sS -o /dev/null -w '%{http_code}\n' -A 'Mozilla/5.0' http://127.0.0.1:5100/cp
# expect 200
# Then hard-refresh browser: https://www.ecomae.com/cp  and  /erp

# REQUIRED for same PHP credentials on /cp/login /erp/login /bos/login:
# Sync PHP secret_succession into ASP.NET (never prints the secret):
ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES \
ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
  bash scripts/cloudpanel_sync_secret_succession_from_php.sh
# Verify (must print OK, never prints secret):
bash scripts/cloudpanel_verify_secret_succession_configured.sh

# REQUIRED — proxy login POST to ASP.NET (fixes HTTP 500 on /auth/login/admin):
ECOMAE_CONFIRM_INSTALL_AUTH_LOGIN_ADMIN=YES \
  bash scripts/cloudpanel_install_auth_login_admin_route.sh
# Or full classic-entry reinstall (includes /auth/login/admin after this pack):
# ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
# ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
#   bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts

# Probe (expect 302/401 JSON — not empty HTTP 500):
curl -sS -D - -o /tmp/login_probe.body -X POST https://www.ecomae.com/cp/login \
  -H 'Content-Type: application/x-www-form-urlencoded' -H 'Accept: text/html' \
  -d 'contact=x@y.com&password=wrong&contact_type=email&surface=cp&redirect=/cp' | head -n 20
# Then sign in with the SAME admin email/password used on PHP /CP/ /ERP/ /BOS/.

## 0c) Fix CP/ERP/BOS login — DB access + ONE-SHOT

Journal root cause when you see `login_backend_error`:
`Access denied for user 'ecomae_aspnet'@'127.0.0.1' to database 'ecomae'`

**Fastest fix (run now):** point TenantRegistry at PHP DP_Config credentials:

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES \
ECOMAE_CONFIRM_RESTART_PLATFORM=YES \
  bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh

# Probe — expect 302 to ?error=invalid_credentials (wrong password), NOT login_backend_error
curl -sS -D - -o /dev/null -X POST 'https://www.ecomae.com/cp/login' \
  -H 'Content-Type: application/x-www-form-urlencoded' -H 'Accept: text/html' \
  -d 'contact=x@y.com&password=wrong&contact_type=email&surface=cp&redirect=/cp' | head -n 12
```

Full oneshot (publish + secret + PHP DB credentials):

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin cursor/login-bridge-oneshot-7b3b
git checkout -f cursor/login-bridge-oneshot-7b3b
git reset --hard origin/cursor/login-bridge-oneshot-7b3b
bash scripts/cloudpanel_fix_login_bridge_now.sh
# Then open https://www.ecomae.com/cp/login — same PHP admin email/password
# Do NOT open /auth/login/admin
```

# Installs into server{} by host — ALL product tenants (no half-and-half):
#   www.ecomae.com ← www pack (marketing ASP.NET home + login bridges)
#   www.epartscart.com + www.electronicae.com + www.stylenlook.com
#   + www.thejewellerytrend.com + www.taxofinca.com ← tenant pack:
#     `/` `/cp` `/erp` `/bos` + deep trees = ASP.NET
# PHP product compare ONLY via /php-reference/* → index.php
# Also set in /etc/ecomae-aspnet/platform.env:
#   MigrationRouteCutover__StorefrontAspNetEnabled=true
#   MigrationRouteCutover__AdminAspNetEnabled=true
ECOMAE_CONFIRM_INSTALL_CLASSIC_ENTRY_ASPNET_PRIMARY=YES \
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  bash scripts/cloudpanel_install_classic_entry_aspnet_primary.sh --all-hosts

bash scripts/cloudpanel_probe_classic_entry_aspnet_primary.sh
# Expect: www / /cp /erp /bos = ASP.NET; tenants / /cp /erp = ASP.NET; tenant /bos = 404;
# PHP only via /php-reference/*
```

**What this does / does not do**

| Does | Does not |
| --- | --- |
| Same-URL proxy: `/cp` `/erp` `/` → ASP.NET on www + **all 5 named tenants**; `/bos` Super-CP only | Leave some tenants on PHP product chrome; never proxy `/bos` on tenants |
| PHP reference at `/php-reference/home\|cp\|erp\|bos\|storefront` only | Mix PHP `/CP/` `/ERP/` `/BOS/` into product clicks |
| Admin/Storefront ASP.NET flags enabled for all tenants | Delete PHP source / PHP-FPM / cron |
| Deep ASP.NET trees proxied; uppercase PHP shells remapped | Invent `cutoverAllowed=true` / PHP source removal |

## 0b) BOS login dark dual-form — if `:5100` is down / deploy stopped at Failed: 6

Root cause: stale foundation/proxy checks aborted deploy **before publish**, so
`ecomae-platform.service` never came up (`curl: (7) Failed to connect … :5100`).

### Emergency publish (paste now — skips broken gates)

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
export ECOMAE_BRANCH=cursor/bos-login-deploy-paste-7b3b
# after #862 merges: export ECOMAE_BRANCH=main
git fetch origin "$ECOMAE_BRANCH"
git checkout -f "$ECOMAE_BRANCH"
git reset --hard "origin/$ECOMAE_BRANCH"
grep -n 'Sign In to BOS' aspnet/src/EcomAE.Platform/Components/Pages/BosLoginApp.razor | head

# Manual publish (does not run the 1731 foundation checks)
STAMP=$(date -u +%Y%m%d%H%M%S)
RELEASE=/var/www/ecomae-aspnet/releases/$STAMP
mkdir -p "$RELEASE/platform" "$RELEASE/workers"
dotnet publish aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj -c Release -o "$RELEASE/platform"
dotnet publish aspnet/src/EcomAE.Workers/EcomAE.Workers.csproj -c Release -o "$RELEASE/workers"
ln -sfn "$RELEASE" /var/www/ecomae-aspnet/current
install -m 0644 deploy/aspnet/ecomae-platform.service /etc/systemd/system/ecomae-platform.service
systemctl daemon-reload
systemctl enable --now ecomae-platform.service
systemctl restart ecomae-platform.service
systemctl status ecomae-platform.service --no-pager
bash scripts/wait_for_aspnet_health.sh
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/bos/login \
  | grep -oE 'bos-body--login|Sign In to BOS|Access ERP System|temporarily unavailable' | sort -u
# expect Sign In to BOS + bos-body--login — NOT temporarily unavailable
# if health fails: journalctl -u ecomae-platform.service -n 80 --no-pager
```

### Normal redeploy (after gate-fix PR is on the branch)

```bash
export ECOMAE_BRANCH=main
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin "$ECOMAE_BRANCH" && git checkout -f "$ECOMAE_BRANCH" && git reset --hard "origin/$ECOMAE_BRANCH"
bash scripts/cloudpanel_find_and_redeploy.sh
# If gates still block: ECOMAE_EMERGENCY_PUBLISH=1 bash scripts/cloudpanel_find_and_redeploy.sh
```

**Skip** `cloudpanel_install_presentation_app_shadows.sh` for BOS login — classic-entry already proxies `/bos/login`.

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
