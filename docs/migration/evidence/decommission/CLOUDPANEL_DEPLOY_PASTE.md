# CloudPanel deploy paste (after PR merges → `main`)

Paste on the **production CloudPanel server** as root. Deploys latest `main` (includes human `RELEASE_OWNER_APPROVAL.md` + exact-route ASP.NET primary execute operator). Keeps PHP as **reference**; does not broad-cut `/api|/cp|/erp|/bos|/storefront`.

## 0) EXECUTE NOW — tenant-shared `/cp` `/erp` `/bos` `/` → ASP.NET (URL unchanged)

Release owner confirmed: **epartscart.com** and **ecomae.com** shared links must keep working as `/cp` `/erp` `/bos` (no change to tenant-facing URLs). PHP reference is **separate** under `/php-reference/*`.

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
# Nginx install alone leaves the OLD binary that still 302s /cp → /cp/login
# and shows “Sign-in temporarily unavailable” with NO credential fields.
#
# HARD RESET both checkouts (deploy fails with RZ1006 if either tree is pre-#852):
#   /opt/ecomae-aspnet-source  AND/OR  /root/ecomae
# Broken one-liner (DO NOT keep): if (!_isAdmin) { return; // guest browse … }
# Fixed tip must include ErpAgingApp multi-line guest return (merged in #852).
# Until #854 merges (catalog tests + CP login CTA + /cp AmbiguousMatch fix), use the PR branch:
export ECOMAE_BRANCH=cursor/aspnet-primary-catalog-tests-7b3b
for d in /opt/ecomae-aspnet-source /root/ecomae; do
  if [[ -d "$d/.git" ]]; then
    git -C "$d" fetch origin "$ECOMAE_BRANCH"
    git -C "$d" checkout -f "$ECOMAE_BRANCH"
    git -C "$d" reset --hard "origin/$ECOMAE_BRANCH"
    git -C "$d" rev-parse --short HEAD
    # expect tip with "Fix CP login" + no MapGet shell aliases for /cp|/erp|/bos
  fi
done
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
# Prove brace fix is present before build:
grep -A3 'if (!_isAdmin)' aspnet/src/EcomAE.Platform/Components/Pages/ErpAgingApp.razor | head -n 5
# must NOT be a single line with `{ return; // guest browse`
bash scripts/cloudpanel_find_and_redeploy.sh
systemctl restart ecomae-platform.service
curl -sS http://127.0.0.1:5100/health || true
# Prove new chrome is loaded (must print Garage Manager):
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/storefront/app | grep -o 'Garage Manager' | head -n1

# CP/ERP/BOS guest browse (no login) — must NOT 302 to /cp/login and must NOT 500 AmbiguousMatch:
curl -sS -o /dev/null -w '%{http_code}\n' -A 'Mozilla/5.0' http://127.0.0.1:5100/cp
# expect 200 (not 302 / not 500)
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/cp | grep -oE 'CONTROL|Command centre' | head -n1
curl -sS -A 'Mozilla/5.0' http://127.0.0.1:5100/cp/login | grep -oE 'Enter CP \(no login\)|Enter your E-mail|features--card' | head
# expect: Enter CP (no login) + email field + features--card

# After redeploy, open https://www.epartscart.com/cp — shell loads without credentials.
# If you land on /cp/login, click “Enter CP (no login)”.

# Optional — enable ASP.NET login bridge (same PHP admin credentials):
#   1) Copy PHP secret_succession into platform.env (never commit the secret):
#        grep -n secret_succession /path/to/php/config.php   # find value on server only
#        printf 'EcomAE__SecretSuccession=%s\n' '<value>' >> /etc/ecomae-aspnet/platform.env
#   2) bash scripts/cloudpanel_verify_secret_succession_configured.sh
#   3) systemctl restart ecomae-platform.service
# Until that is set, credential fields stay hidden and “Enter CP (no login)” is the way in.

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
# Expect: www + all 5 named tenants / /cp /erp /bos = ASP.NET;
# PHP only via /php-reference/*
```

**What this does / does not do**

| Does | Does not |
| --- | --- |
| Same-URL proxy: `/cp` `/erp` `/bos` `/` → ASP.NET on www + **all 5 named tenants** | Leave some tenants on PHP product chrome |
| PHP reference at `/php-reference/home\|cp\|erp\|bos\|storefront` only | Mix PHP `/CP/` `/ERP/` `/BOS/` into product clicks |
| Admin/Storefront ASP.NET flags enabled for all tenants | Delete PHP source / PHP-FPM / cron |
| Deep ASP.NET trees proxied; uppercase PHP shells remapped | Invent `cutoverAllowed=true` / PHP source removal |

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
