# CloudPanel deploy paste (after PR merges → `main`)

Paste on the **production CloudPanel server** as root. Deploys latest `main` (includes human `RELEASE_OWNER_APPROVAL.md` + exact-route ASP.NET primary execute operator). Keeps PHP as **reference**; does not broad-cut `/api|/cp|/erp|/bos|/storefront`.

## 0🔥) EPARTSCART LIVE PUBLISH — STILL 404 AFTER ACKS (do immediately)

**Symptom (2026-08-08):** `#967` is on `main`; publish pack also stacks `#969` (Google/autofill) + `#970` (guest prices). Live still: `/storefront/register-app` **404**, shop DB unbound, `/php-reference/*` **Archive paused**, `/cp/login` still autofills. `/storefront/own-catalog-app` 200 — Kestrel up, **binary never republished**.

**Do not mark complete** unless the log contains `RESULT=PASS`. Paste the `grep` lines back. Prior “completed” acks did not republish.

**Paste as root — download-guarded (cannot silently no-op). `#968` is on `main`; merge alone does not republish `:5100`:**

```bash
set -euxo pipefail
URL='https://raw.githubusercontent.com/epartscart/ecomae/main/scripts/cloudpanel_EPARTSCART_LIVE_PUBLISH_NOW.sh'
TMP=/tmp/epartscart-live-publish-now.sh
curl -fsSL "$URL" -o "$TMP"
wc -c "$TMP"
grep -q LIVE_PUBLISH_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
export ECOMAE_BRANCH=main ECOMAE_SKIP_LIFEOS_MP4=YES
bash "$TMP" 2>&1 | tee /root/epartscart-live-publish-now.log
grep -E 'RESULT=|PREFLIGHT|GATE_|RECHECK|ERROR|SHA=|HOST=' /root/epartscart-live-publish-now.log | tail -100
```

**PASS criteria:** `RESULT=PASS`; register-app 200; bunches not unbound; php-reference not Archive paused; `/cp/login` shows Continue with Google.

If `RESULT=FAIL`, send `/root/epartscart-live-publish-now.log`.

## 0b) CP login `tenant_db_unbound` / bunches unbound / LifeOS clients-board 302 (ONE paste)

**Symptom:** `/cp/login` → `?error=tenant_db_unbound`; bunches unbound; `lifeos…/clients-board` → 302 login. **2026-08-09 probe after a silent “External action completed” ack still FAIL** — treat ack without pasted `RESULT=PASS` + `PASTE_ME_*` as FAIL.

**Merging a PR is not a republish.** Agent re-probes public URLs; silent UI completion does not count.

ePartsCart shop DB is **`docpart`**. Finish branch also fills `docpart` in ASP.NET when portal `db_name` empty (needs LIVE_PUBLISH).

**ONE paste (bind + republish + LifeOS prove) — must paste PASTE_ME block back:**

```bash
set -euxo pipefail
URL='https://raw.githubusercontent.com/epartscart/ecomae/cursor/finish-pending-epartscart-lifeos-7b3b/scripts/cloudpanel_FINISH_PENDING_EPARTSCART_LIFEOS_NOW.sh'
TMP=/tmp/finish-pending-epartscart-lifeos-now.sh
curl -fsSL "$URL" -o "$TMP"
wc -c "$TMP"
grep -q FINISH_PENDING_EPARTSCART_LIFEOS_NOW "$TMP" || { echo RESULT=FAIL bad_download; exit 1; }
bash "$TMP" 2>&1 | tee /root/finish-pending-epartscart-lifeos-now.log
grep -E 'RESULT=|PASTE_ME_|GATE_|BOUND_|POST_LOGIN|SHA=|clients-board|ERROR' /root/finish-pending-epartscart-lifeos-now.log | tail -120
```

**PASS:** log contains `RESULT=PASS FINISH_PENDING…`, `BOUND_BUNCHES=YES`, `CLIENTS_BOARD=PUBLIC`, and `POST_LOGIN_REDIRECT` is not `tenant_db_unbound`.

## 0☠) ALL SITES 502 / epartscart stuck on “Loading — Please wait”

**Symptom (2026-08-08):** `cp.ecomae.com`, `erp`, `bos`, `ip`, `lifeos`, `platform` → Cloudflare **502**; `www.epartscart.com/` and `/storefront/app` → static warmup splash (**Loading — Please wait**), not ASP.NET chrome.

**Root cause:** shared origin Kestrel `ecomae-platform` **:5100 is down**. Merge alone cannot fix this.

**Paste as root — wait for `RESULT=PASS`:**

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae || echo REPO_NOT_FOUND
git fetch origin cursor/all-sites-502-recover-7b3b
git checkout -f cursor/all-sites-502-recover-7b3b
git reset --hard origin/cursor/all-sites-502-recover-7b3b
export ECOMAE_BRANCH=cursor/all-sites-502-recover-7b3b
export ECOMAE_SKIP_LIFEOS_MP4=YES
bash scripts/cloudpanel_FORCE_LIVE_502_ALL_RECOVER.sh 2>&1 | tee /root/force-live-502-all-recover.log
grep -E 'RESULT=|ERROR|FAIL|local_5100|BAD ' /root/force-live-502-all-recover.log | tail -40
```

If `RESULT=FAIL`, send `/root/force-live-502-all-probe.txt` + `journalctl -u ecomae-platform -n 200 --no-pager`.

**PASS:** Super-CP hosts not 502; epartscart home shows nero/ASP.NET chrome (not splash); `curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5100/storefront/app` → `200`.

## 0🆕) HOME STILL OLD AFTER #892 MERGE — binary was never republished

**Symptom:** `#892` merged but `https://www.epartscart.com/` is unchanged even after hard refresh — no 4-banner grid, no "Didn't find the part you need?" card, no Family Product / Epart Catalog / Available Brands / Original Catalog sections; top menu links may be invisible (dark-on-dark).

**Root cause:** live Kestrel `:5100` is running a **stale binary** — recent pastes synced PHP/nginx but never republished the ASP.NET build. Also, the old `cloudpanel_FORCE_LIVE_NOW.sh` prover could wrongly print `RESULT=FAIL` on a good deploy when PHP serving is paused (it treated `action="/storefront/search-app"` as "old binary"), and its `Location:` parsing broke on mawk. Both fixed on this branch.

**⚠ If a previous paste "did nothing"** (no output, prompt returns instantly, marker not updated): `bash -c "$(curl …)"` silently runs an EMPTY string when curl fails (raw.githubusercontent blocked / rate-limited) or when the multi-line paste is mangled. Use the git-based paste below — every step prints, and the log survives in `/root/force-live.log`.

**Paste as root, one block — wait for `RESULT=PASS`:**

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae || echo REPO_NOT_FOUND
git fetch origin cursor/fix-force-live-prove-892-7b3b
git checkout -f cursor/fix-force-live-prove-892-7b3b
git reset --hard origin/cursor/fix-force-live-prove-892-7b3b
export ECOMAE_BRANCH=cursor/fix-force-live-prove-892-7b3b
bash scripts/cloudpanel_FORCE_LIVE_NOW.sh 2>&1 | tee /root/force-live.log
grep -E 'RESULT=|ERROR|FAIL' /root/force-live.log | tail -20
```

If it prints anything other than `RESULT=PASS`, send the whole `/root/force-live.log`. After this branch merges to `main`, substitute `main` for the branch name.

**PASS criteria (proved by the script):** public `/` contains `epc-asp-home-banners`, `section-vin`, `epart-front-original-data`, `id="epc-umapi"`, `id="epc-brands"`, and does NOT contain the pre-#892 scaffold (`epc-sf-home-depth`). Then hard-refresh (Ctrl+Shift+R) `https://www.epartscart.com/`.

## 0☠) STOP PRODUCT PHP NOW — `/cp/control` still old Homer login

**Symptom:** `https://www.epartscart.com/cp/control` shows the old PHP login (`bootstrap_admin` / Homer) even after archive pause.

**Why:** `TemporarilyDeactivatePhpServing` only pauses `/php-reference/*` and `/en/*`. Product `/cp/control` was still answered by the PHP docroot because nginx never hard-wired it to `:5100`.

**Do this once as root** — wait for `RESULT=PASS`. Then hard-refresh / new tab:

```bash
ECOMAE_BRANCH=cursor/stop-product-php-now-7b3b \
ECOMAE_CONFIRM_STOP_PRODUCT_PHP_NOW=YES \
ECOMAE_ALSO_FORCE_LIVE=YES \
  bash -c "$(curl -fsSL https://raw.githubusercontent.com/epartscart/ecomae/cursor/stop-product-php-now-7b3b/scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh)"
```

**Expect after PASS:**
- `/cp/control` → platform login gate or Command Centre (NOT `bootstrap_admin`)
- `/cp`, `/cp/login`, `/`, `/storefront/*`, `/erp` → `:5100`
- `/php-reference`, `/en/` → `503` (archive paused)
- PHP-FPM stays up for chrome assets (`/epc-static.php`); product routes do not use it

**Browser test:** close the old tab, open `https://www.epartscart.com/cp/login` then `/cp` and `/cp/control`. Do **not** use `/php-reference/cp`.

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

## 0◆) How to test the real platform CP (not the old `/cp/control` login)

**Why `/cp/control` still “works” after pausing archive serving:**  
`TemporarilyDeactivatePhpServing` only pauses **`/php-reference/*`** and interim **`/en/*`**. It does **not** turn off product `/cp`. Product CP should already be platform-primary (`:5100`).

**Live gap (before unbreak):**  
- `https://www.epartscart.com/cp` → platform Command Centre (correct test target)  
- `https://www.epartscart.com/cp/control` → **still old docroot login** (nginx never installed `location = /cp/control → :5100`)  
- `https://www.epartscart.com/cp/login` → platform login  

**Do this to test the real product CP:**

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
# 1) Install platform routes (includes /cp/control → :5100) — use unbreak branch until #889 merges
ECOMAE_BRANCH=cursor/unbreak-epartscart-php-storefront-7b3b \
ECOMAE_CONFIRM_UNBREAK_EPARTSCART_STOREFRONT=YES \
ECOMAE_ALSO_FORCE_LIVE=YES \
  bash scripts/cloudpanel_unbreak_epartscart_storefront_now.sh

# 2) Prove product surfaces are platform (not legacy login HTML)
bash scripts/cloudpanel_prove_product_is_platform_primary.sh
# Expect PASS_PLATFORM / PASS_AUTH_GATE for /cp and /cp/control
# FAIL_LEGACY_DOCROOT on /cp/control means nginx still serving old login — re-run step 1

# 3) Manual browser test (real platform result)
#    - Open /cp/login  → platform login
#    - Open /cp        → after login, Command Centre
#    - Open /cp/control → must match /cp (same platform app), NOT the old Homer login
#    - Do NOT use /php-reference/cp for product testing
```

## 0◇) TEMP archive pause — `/php-reference` + `/en/` only (optional)

`#885` is on `main`. This does **not** stop product `/cp`. It only pauses archive/compare URLs so you are not hopping into `/php-reference`. Files stay on disk. `cutoverAllowed` / `readyForPhpRemoval` stay **false**.

```bash
cd /opt/ecomae-aspnet-source 2>/dev/null || cd /root/ecomae
git fetch origin main && git checkout -f main && git reset --hard origin/main
ECOMAE_CONFIRM_TEMP_DEACTIVATE_PHP_SERVING=YES \
  bash scripts/cloudpanel_temporarily_deactivate_php_serving.sh
# Expect: /php-reference and /en/ paused; product / /cp /erp stay platform
# Prove product CP is platform: bash scripts/cloudpanel_prove_product_is_platform_primary.sh
# Restore archive: ECOMAE_CONFIRM_RESTORE_PHP_REFERENCE_SERVING=YES bash scripts/cloudpanel_restore_php_reference_serving.sh
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
