---
name: testing-ecomae-platform
description: Test the ecomae multi-tenant ERP platform (BOS, CP, ERP, storefront). Use when verifying BOS UI, CP/ERP features, storefront performance, or platform-wide changes.
---

# Testing the ecomae Platform

## Overview

ecomae is a multi-tenant ERP platform with:
- **BOS** (Business Operating System) at `https://www.ecomae.com/bos/` — manages all tenants
- **CP** (Control Panel) at `https://<tenant>.com/cp/` — tenant admin
- **ERP** modules inside CP — finance, HR, inventory, etc.
- **Storefront** at `https://<tenant>.com/` — customer-facing e-commerce

## Devin Secrets Needed

- `ECOMAE_DEPLOY_TOKEN` — used for cache purge and deploy scripts (currently: `epartscart-deploy-2026`)
- `ECOMAE_LOGIN_EMAIL` — BOS/CP login email
- `ECOMAE_LOGIN_PASSWORD` — BOS/CP login password

## Environment Setup

1. Install PHP SQLite extension for running DB-free tests:
   ```bash
   sudo apt-get install -y php-sqlite3
   ```

2. The repo is at `/home/ubuntu/repos/ecomae`

## Running PHP Tests

### DB-free tests (always runnable)
```bash
cd /home/ubuntu/repos/ecomae
for f in tests/erp_advanced/run_*_tests.php; do
  timeout 5 php "$f" 2>&1 | tail -3
done
```

The following test suites run without MySQL:
- `run_dashboard_tests.php` — executive dashboard data-layer (most relevant for ERP changes)
- `run_demo_portal_tests.php` — demo portal functionality
- `run_demo_tests.php` — demo data generation
- `run_enterprise_tests.php` — enterprise parity checks
- `run_free_tools_tests.php` — free tools (largest suite, 138 tests)
- `run_guide_tests.php` — guide content
- `run_hr_law_tests.php` — HR labour law
- `run_i18n_tests.php` — internationalization
- `run_localization_tests.php` — localization
- `run_pwa_tests.php` — PWA/mobile
- `run_theme_tests.php` — theme system
- `run_treasury_tests.php` — treasury + audit tools

### MySQL-dependent tests
Most other test suites (28+) require a local MySQL database. They connect to `mysql:host=127.0.0.1;dbname=erp_test` with user `erp`. These cannot run in Devin's default environment without setting up a local MySQL instance.

### CP Lint
```bash
php tests/erp_advanced/run_cp_lint.php
```

### Syntax validation
```bash
php -l <file.php>
```

## BOS Browser Testing

1. Navigate to `https://www.ecomae.com/bos/`
2. If not authenticated, login with provided credentials
3. The BOS session might already be active from previous browser usage (check for `ecomaedxb@gmail.com` in top-right)

### Key areas to test:
- **Command Center**: Module grid with colored sections (Fleet Command, Tenant Operations, Commerce, etc.)
- **Sidebar clicks**: Each module should show a hero card with description, detail cards, and Launch button — NOT blank
- **Tenant dropdown**: Click tenant selector in header bar. Should show ALL/COMMERCE/ERP ONLY/DEMO filter tabs and list all tenants
- **Tenant switching**: Selecting a different tenant should reload the dashboard with that tenant's modules
- **Version number**: Check bottom of sidebar or via curl: `curl -sk "https://www.ecomae.com/bos/" | grep -o 'v[0-9]\.[0-9]\.[0-9]'`

## Storefront Testing

The storefront at `https://www.epartscart.com/` might be slow or timeout due to server load (3.9GB RAM server, 133K+ parts). This is a known issue.

### Quick health check:
```bash
curl -sk -o /dev/null -w "HTTP %{http_code} | %{time_total}s | %{size_download} bytes" "https://www.epartscart.com/en/" --max-time 60
```

### After deploying performance PRs (page cache, gzip):
```bash
# Verify gzip compression
curl -sk -H "Accept-Encoding: gzip" -o /dev/null -w "%{size_download}" "https://www.epartscart.com/en/" --max-time 60

# Verify page cache (second request should be much faster)
curl -sk -o /dev/null -w "%{time_total}" "https://www.epartscart.com/en/" --max-time 60
curl -sk -o /dev/null -w "%{time_total}" "https://www.epartscart.com/en/" --max-time 60
```

## Deploy Commands

After merging a PR:
```bash
cd /home/ecomae/htdocs/www.ecomae.com && git pull origin main
```

Cache purge (after performance-related deploys):
```bash
curl -sk "https://www.epartscart.com/epc-cache-purge.php?token=epartscart-deploy-2026"
```

Widget cache warm:
```bash
curl -sk --max-time 300 "https://www.epartscart.com/epc-home-widgets-warm.php?token=epartscart-deploy-2026&lang=/en"
```

## Page Cache Testing

The page cache (`content/general_pages/epc_page_cache.php`) caches rendered HTML for anonymous visitors. When testing cache-related changes:

### Verify cache validation logic locally:
```bash
cd /home/ubuntu/repos/ecomae
# Test that partial renders are rejected
php -r "
define('_ASTEXE_', 1);
require 'content/general_pages/epc_page_cache.php';
\$partial = 'function foo() { document.getElementById(\"x\"); }';
\$trimmed = trim(\$partial);
\$ok = (stripos(\$trimmed, '<!doctype') === 0 || stripos(\$trimmed, '<html') !== false)
    && (stripos(\$trimmed, '</html>') !== false);
echo \$ok ? 'BUG: would cache partial' : 'OK: partial rejected';
"
```

### Verify cache behavior on live site:
```bash
# 1. Purge cache
curl -sk "https://www.epartscart.com/epc-cache-purge.php?token=epartscart-deploy-2026"

# 2. Request page (may timeout under load — that's fine)
curl -sk -o /dev/null -w "HTTP %{http_code} | %{time_total}s" "https://www.epartscart.com/en/" --max-time 180

# 3. Check if cache was populated with valid HTML (not broken fragments)
curl -sk "https://www.epartscart.com/en/" --max-time 30 | head -1
# Should start with <!DOCTYPE or be empty — NEVER raw JavaScript

# 4. Check cache header
curl -sk -D- -o /dev/null "https://www.epartscart.com/en/" --max-time 30 2>/dev/null | grep -i 'x-epc-cache'
# X-EPC-Cache: HIT means served from cache
```

### Key behavior:
- Page cache only stores complete HTML (must have `<!DOCTYPE`/`<html` at start AND `</html>` at end)
- Partial renders from server timeouts are silently discarded
- `try_serve()` also rejects and deletes stale partial files on read
- Cache is skipped for: logged-in users, POST requests, CP/BOS/API paths, `?nocache` param
- Cache TTL is 5 minutes (300s)

## Testing P0 Enterprise Features (Design Tokens, Readiness Score, Setup Runner)

### Design Tokens (`epc_design_tokens.php`)
Test the token resolution hierarchy locally without a DB:
```bash
cd /home/ubuntu/repos/ecomae
php -r "
define('_ASTEXE_', 1);
require 'content/general_pages/epc_design_tokens.php';
// Verify tenant catalog overrides defaults
\$tokens = epc_design_tokens_resolve('epartscart', 'auto_parts');
echo \$tokens['--epc-brand-primary']; // Should be #dc2626, NOT #0d6efd
// Verify unknown tenant falls back to defaults
\$tokens2 = epc_design_tokens_resolve('unknown', '');
echo \$tokens2['--epc-brand-primary']; // Should be #0d6efd
// Verify CSS output
\$css = epc_design_tokens_css('stylenlook', 'fashion');
echo (strpos(\$css, ':root {') !== false) ? 'OK' : 'FAIL';
// Verify login brand markup
\$html = epc_design_tokens_login_brand('electronicae');
echo (strpos(\$html, 'electronicae.png') !== false) ? 'OK' : 'FAIL';
"
```

### Readiness Score (`epc_readiness_score.php`)
Uses SQLite mock for DB-free testing of score calculation:
```bash
php -r "
define('_ASTEXE_', 1);
require 'content/general_pages/epc_readiness_score.php';
\$pdo = new PDO('sqlite::memory:');
\$pdo->exec('CREATE TABLE epc_settings (site_key TEXT, setting_key TEXT, setting_value TEXT)');
// Each check returns array with keys: id, label, weight, earned, status, detail, icon, remediation
\$check = epc_readiness_check_isolation(\$pdo, 'test');
echo \$check['status']; // 'not_run' when no data
\$pdo->exec(\"INSERT INTO epc_settings VALUES ('test', 'isolation_audit_status', 'pass')\");
\$check2 = epc_readiness_check_isolation(\$pdo, 'test');
echo \$check2['earned']; // 20
"
```

### Setup-All Runner (`epc-post-deploy-setup-all.php`)
Cannot be fully executed locally (needs platform DB). Validate structure:
```bash
# Auth guard present
grep -c "epartscart-deploy-2026" epc-post-deploy-setup-all.php
grep -c "http_response_code(403)" epc-post-deploy-setup-all.php
# Schema steps
grep -c "ensure_schema" epc-post-deploy-setup-all.php  # Should be 10+
```

After deploy, test via HTTP:
```bash
curl -sk "https://www.ecomae.com/epc-post-deploy-setup-all.php?token=epartscart-deploy-2026"
```

### AJAX Handler Wiring
When adding new BOS modules, verify case handlers and functions exist:
```bash
grep -c "case 'design_tokens'" bos/ajax_epc_bos.php
grep -c "function epc_bos_ajax_design_tokens" bos/ajax_epc_bos.php
```

Note: Each PR branch may add its own handlers. Check the correct branch for each feature (e.g., readiness_score handler is on the readiness-score branch, not the design-tokens branch).

### BOS Sidebar Registration
Verify new items appear in `epc_bos_unified.php`:
```bash
grep "'design_tokens'" content/general_pages/epc_bos_unified.php
grep "'readiness_score'" content/general_pages/epc_bos_unified.php
```

## Testing MFA (epc_auth_mfa.php)

MFA schema uses MySQL `AUTO_INCREMENT` — cannot test with SQLite mock. Instead, validate function signatures and source code patterns:
```bash
php -r "
define('_ASTEXE_', 1);
\$src = file_get_contents('content/general_pages/epc_auth_mfa.php');
echo (strpos(\$src, 'function epc_mfa_enroll') !== false) ? 'PASS' : 'FAIL';
echo (strpos(\$src, 'function epc_mfa_verify_totp') !== false) ? 'PASS' : 'FAIL';
echo (strpos(\$src, 'function epc_mfa_enforce_route_guard') !== false) ? 'PASS' : 'FAIL';
"
```
For full TOTP verification testing, you need a running MySQL instance.

## Testing Events + Webhooks (epc_events.php, epc_webhooks.php)

Validate function presence and security patterns:
```bash
php -r "
define('_ASTEXE_', 1);
\$evtSrc = file_get_contents('content/general_pages/epc_events.php');
\$whSrc = file_get_contents('content/general_pages/epc_webhooks.php');
echo (strpos(\$whSrc, 'hash_hmac') !== false) ? 'HMAC-OK' : 'HMAC-MISSING';
echo (strpos(\$whSrc, 'openssl_encrypt') !== false) ? 'AES-OK' : 'AES-MISSING';
echo (strpos(\$whSrc, 'epc_webhook_dlq') !== false) ? 'DLQ-OK' : 'DLQ-MISSING';
"
```

## Testing GL Period Close + ERP Command Center

Files are under `content/shop/finance/` (not `content/general_pages/`):
```bash
php -l content/shop/finance/epc_erp_period_close.php
php -l content/shop/finance/epc_erp_command_center.php
php -l cp/content/shop/finance/erp/ajax_erp.php
```

## Testing E-invoice ASP API

E-invoice file is at `content/shop/finance/epc_einvoice.php` (not `content/general_pages/`):
```bash
php -l content/shop/finance/epc_einvoice.php
php -l epc-einvoice-poll-status.php
```

## Bulk Site Health Check

Check all tenant sites respond:
```bash
for site in ecomae.com/bos/ ecomae.com epartscart.com/en/ epartscart.com/cp/ electronicae.com stylenlook.com taxofinca.com thejewellerytrend.com; do
  curl -sk -o /dev/null -w "$site: HTTP %{http_code}, %{time_total}s\n" --max-time 20 "https://www.$site" &
done
wait
```

## Merging PRs

Devin cannot merge PRs directly to main/master — the user must merge them on GitHub. After merge, deploy with:
```bash
cd /home/ecomae/htdocs/www.ecomae.com && git pull origin main
```

For P0 features, also run the post-deploy setup:
```bash
php epc-post-deploy-setup-all.php
# OR via HTTP:
curl -sk "https://www.ecomae.com/epc-post-deploy-setup-all.php?token=epartscart-deploy-2026"
```

## Testing BOS Modules (40+ sidebar items)

After deploying DEVIN-HANDOFF PRs, the BOS sidebar has 40+ modules across 3 sections:
- **Fleet Command** (26 items): Command Center, Platform Health, Governance, Audit Log, Failover Runbook, Isolation Audit, MFA/2FA, Event Bus, Webhooks, Readiness Score, Notifications, DB Migrations, CP Roles, Credit Limits, Order→ERP, PO Approval, API v2, Fulfillment, BI Metrics, AI Classify, Tenant Config, Workflows, Forecasting, Multi-Currency, SSO/SAML, Payroll + Collections, Warranty/RMA, Dealer Portal, AI Copilot, NL Reports, Industry Packs, Multi-Entity, Promotions, Sandbox, Imports, Doc Vault, Billing, SOC 2, Marketplace, AI Service, Metabase BI, Anomaly AI
- **Tenant Operations** (8 items): Tenant Hub, Tenant Control, Feature Matrix, Demo Tenants, Industry/ERP Packs, Customer Board, Integrations Hub, Design Tokens
- **Platform** (6 items): Portal Settings, Auth Settings, Communication, Data Policy, API Docs, Operator Guide

Each module loads as a hero card with description, section label, context, and type. To verify all modules:
```bash
# Count sidebar items in source
grep -c "'id'" content/general_pages/epc_bos_unified.php
# Count AJAX handlers
grep -c "case '" bos/ajax_epc_bos.php
```

To test via browser: scroll the sidebar to find modules near the bottom (AI Service, Metabase BI, Anomaly AI are at the very bottom of Fleet Command section).

## Testing Platform ERP

The platform's own ERP portal is at `https://www.ecomae.com/erp/` — this is NOT a tenant site. It shows:
- ERP Finance section with department sign-in
- Super CP link to tenant hub
- Platform marketing overview
- Login form (By password / Email code tabs)

The platform CP is at `https://www.ecomae.com/cp/` — shows a login form. Requires valid credentials for the ecomae platform tenant.

## AJAX Endpoint Testing

BOS AJAX endpoints require an authenticated session. Testing via curl without cookies will return empty responses. To test properly:
1. Login via browser first
2. Use browser devtools Network tab to see AJAX responses
3. Or extract session cookies and pass them with curl

## Deployment Workflow

The user deploys manually. After merging a PR:
1. User runs `cd /home/ecomae/htdocs/www.ecomae.com && git stash && git pull origin main && git stash pop`
2. If `git stash pop` conflicts, tell user: `git checkout --theirs <file> && git stash drop`
3. User may need to restart PHP: `systemctl restart php*-fpm`
4. For new features requiring DB tables, run setup: `curl -sk "https://www.ecomae.com/epc-post-deploy-setup-all.php?token=epartscart-deploy-2026"`

## Cursor / Devin Lane Split — DO NOT TOUCH

Cursor owns the epartscart.com storefront lane. Devin owns BOS, ERP, CP shell, backend.

**Files Devin must NEVER modify or overwrite:**
- `content/general_pages/epart_catalog_front_links.php` (homepage sections)
- `content/laximo/**` (Laximo OEM + Aftermarket)
- `content/shop/docpart/part_search_page.php` / `part_search_page_1.php`
- `content/general_pages/epc_cata_config.php`
- `config.epc-laximo.php` (server only, gitignored)

**After any deploy to shared docroot, verify:**
```bash
curl -sk "https://www.epartscart.com/epc-pf-own-home-verify.php?fast=1&token=epartscart-deploy-2026"
curl -sk "https://www.epartscart.com/epc-laximo-remote-check.php?token=epartscart-deploy-2026"
```

**Lane ownership:**
| Cursor (storefront) | Devin (backend) |
|---------------------|-----------------|
| Homepage widgets & cache | BOS / ERP / Super CP |
| Laximo OEM + Aftermarket UX | Import orchestration |
| Part search / warehouse car-mod | nginx / PHP-FPM tuning |
| Catalog theme, login/header UX | Tenant hub bulk deploys |

## Testing Jewellery ERP Module

The Jewellery ERP is a 31-tab module under `?area=jewellery` in the tenant ERP. It uses `epc_jewel_*` prefixed tables and `jw_*` prefixed AJAX actions.

### Key URLs
- **Tenant ERP base:** `https://www.ecomae.com/cp/demo/<site_key>/shop/finance/erp?area=jewellery&tab=jw_karat`
- **Indus Jewellers LLC:** site_key = `demo_260627_eo`
- **AJAX endpoint:** `cp/content/shop/finance/erp/ajax_erp_endpoint.php`

### Testing Checklist for Jewellery Changes
1. **Tab rendering:** Navigate to each tab URL and verify ef-window renders (blue gradient title bar, toolbar buttons, grid headers). A blank content body with only breadcrumb visible = HTTP 500 from SQL error.
2. **AJAX save:** Click "+ New", fill form, click "Save". Should redirect (303) back to list view — NOT show raw JSON. If "ERP unavailable" appears, the AJAX endpoint might not be bootstrapping demo tenant context.
3. **Seed defaults:** Karat Master has a "Seed defaults" button that populates standard karats (9K-24K, PLT, SC). After seeding, button disappears and grid shows rows.
4. **Table name mapping:** Schema creates `epc_jewel_*` tables but code may reference `jw_*` names. If a tab crashes, check the list function's SQL query against the actual `CREATE TABLE` statement in `epc_jewel_ensure_schema()`.

### Known Patterns
- **Computer tool clicks may fail** on this site. Use CDP via `chrome-remote-interface` npm package as fallback:
  ```bash
  node -e "const CDP=require('chrome-remote-interface');(async()=>{const c=await CDP({port:29229});await c.Runtime.evaluate({expression:\"document.querySelector('button').click()\"});await c.close();})()"
  ```
- **Demo tenant AJAX context:** Demo tenants at `/cp/demo/<key>/` need special bootstrap in `ajax_erp_endpoint.php` to detect the tenant from the URL path. Without this, AJAX forms save to the platform DB instead of the tenant DB.
- **Column name mapping in save functions:** Form fields may use different names than schema columns (e.g., form sends `item_code` but schema has `code`). Save functions should accept both via `??` operator.
- **Sidebar visibility:** New ERP areas must be registered in THREE places: `erp_nav_areas.php` (navigation), `epc_erp_staff_all_tabs()` (access control), and `epc_portal_erp_modules.php` (always-on areas list for tenants with stored module settings).

### Tab File Locations
- Tab files: `cp/content/shop/finance/erp/erp_tabs_jw_*.php` (31 files)
- Core module: `content/shop/finance/epc_erp_jewellery.php` (schema, list functions, save handlers, AJAX routing)
- Navigation: `cp/content/shop/finance/erp/erp_nav_areas.php`
- Staff access: `content/shop/finance/epc_erp_staff.php`

## Testing Jewellery Sample Data Seeding

The jewellery seed function is at `content/shop/finance/epc_erp_jewellery_integration.php` → `epc_jw_seed_sample_data()`. It seeds 8 categories of data.

### Seed Data Tab
- URL: `?area=setup&tab=jw_seed_data`
- The seed form's inline `<script>` is stripped by CP shell output buffer (same issue as voice command JS). The form falls through to regular POST instead of AJAX, but data still gets seeded. No user feedback appears.
- To fix: serve the seed form JS via PHP proxy (same pattern as `epc_erp_voice_command_js.php`).

### Known Seed Data Issues (updated PR #94)
1. **Sales orders now seed properly** — PR #94 fixed: creates SO headers first, then links lines. 15 SOs visible with proper numbers.
2. **Repairs seed once, duplicate on re-seed:** First seed creates 3-4 repairs with unique `repair_no`. Subsequent seeds fail with `Duplicate entry 'RPR-xxx' for key 'x_repair_no'`. Use INSERT IGNORE or check existence first.
3. **Suppliers/customers fail:** Seed tries to INSERT with `contact_type` column in `epc_erp_contacts` table, but column doesn't exist in schema. All 10 supplier/customer inserts fail with `SQLSTATE[42S22]: Column not found`. Fix: either add column to schema or remove from INSERT.
4. **Seed form button JS stripped:** The seed data form's inline `<script>` is stripped by CP shell output buffer. Must trigger via browser console AJAX call to `/content/general_pages/ajax_epc_erp.php` with `action=jw_seed_sample_data`.

### Verifying Seed Results (updated PR #94)
```
Dashboard: ?area=overview&tab=dashboard → Industry = "Jewellery & watches", Inventory value > 0
Inventory: ?area=inventory_mgmt&tab=inventory → 15 items with Metal/Karat/Weight columns (117,024 AED)
POs: ?area=procurement&tab=purchase_orders → 5 POs with Karat/Weight/Rate columns
SOs: ?area=sales&tab=sales_orders → 15 orders with Karat/Weight/Total (219,729 AED) — FIXED in PR #94
Repairs: ?area=service_mgmt&tab=jw_repairs → 4 repair jobs with Metal/Karat/Wt In columns
```

### AI Assistant Tab
- URL: `?area=setup&tab=ai_assistant`
- **Known issue:** Inline `<script>` is stripped by CP shell output buffer (same root cause as voice command PRs #89-92)
- UI renders (welcome message, example queries, input field, Ask button) but form can't submit
- CSRF token hidden input is never rendered → AJAX calls return `{"error":"Error! CSRF 1"}`
- **Fix needed:** Move AI assistant JS to external file loaded in `erp_desktop.php` `<head>` or via PHP proxy (same pattern as voice command fix)

### CP Shell Script Stripping — Critical Pattern
The CP shell (`erp_desktop.php`) buffers ALL output from `erp_main.php` and strips every `<script>` tag (both inline and `<script src>`).

**What works:**
- JS loaded in the `<head>` of `erp_desktop.php` directly (outside the buffer) — e.g., nav JS, voice command JS
- JS via PHP proxy files that output `Content-Type: application/javascript`

**What gets stripped:**
- Inline `<script>` tags in tab PHP files
- `<script src>` tags echo'd from tab PHP files
- CSRF token hidden inputs generated by inline scripts

**Impact on testing:**
- Any tab with interactive JS (forms, AJAX, dynamic UI) might not work from the browser
- Must test via browser console AJAX calls as workaround
- The correct AJAX endpoint for www.ecomae.com is `/content/general_pages/ajax_epc_erp.php` (resolved by `epc_erp_resolve_ajax_endpoint()` in `epc_erp_access.php`)

## Jewellery Integration Architecture

**User requirement:** Jewellery must NOT be a separate sidebar module. It must be integrated into existing ERP modules (Inventory, Purchase, Sales, GL, etc.) with industry-driven field visibility.

### Current State (PR #94 — verified)
- Standalone "Jewellery" sidebar module REMOVED (35 sidebar modules, no "Jewellery" entry)
- 31 jw_ tabs distributed into existing ERP modules with `'jw' => true` metadata:
  - System administration: Sample data, Currency master, Devin AI assistant
  - Inventory management: Inventory (with Metal/Karat/Weight columns)
  - Sales and marketing: Sales orders (with Karat/Weight columns)
  - Service management: Repair jobs (with Metal/Karat/Wt In columns)
  - Procurement and sourcing: Purchase orders (with Karat/Weight/Rate columns)

### Correct Architecture
- Jewellery-specific columns (Metal, Karat, Weight, Purity, etc.) appear conditionally in existing modules when `erp_industry_profile = 'jewellery'`
- Repairs are under Service Management
- Master data (Karat, Rate Type, etc.) under System Administration
- The separate "Jewellery" sidebar entry has been removed (PR #94)

### Dual Reporting Concept
Jewellery ERP tracks TWO dimensions for every transaction:
1. **Weight/Quantity** (grams, carats, tola) — physical reality
2. **Value** (AED/currency) — financial reality
Every report, trial balance, and inventory view should show both dimensions side by side.

### Voice Command Testing
- Voice command widget works (PR #92 deployed)
- Alt+V keyboard shortcut opens the panel with 8 example commands
- `window.epcVoice` is defined and functional
- Mic access shows "Error: not-allowed" in automated testing environments (expected — no mic permission granted)
- Test via: press Alt+V on any ERP page, verify panel opens

### Compliance Modules (Jewellery)
Jewellery industry requires these compliance modules to be linked with tenant data:
- **VAT filing** — Should show jewellery transaction data
- **External Audit reporting** — Should include jewellery inventory/sales data
- **CT (Corporate Tax) filing** — Should reflect jewellery business financials
- **AML compliance** — Anti-money laundering checks specific to gold/jewellery trade

These modules need to pull data from the jewellery-specific tables and show industry-relevant reporting.

## Common Issues

- **Server 524 timeout**: The storefront might return Cloudflare 524 errors when server load is high. BOS (ecomae.com) is lighter and usually works. Wait and retry, or ask user to check server load with `top -c`.
- **BOS pages load slowly (15-30s)**: The BOS page queries the platform DB which includes tenant routing. This is normal under load.
- **Browser cache**: After deploying BOS updates, hard-refresh (Ctrl+Shift+R) is needed to see new version.
- **epartscart.com HTTP 500 but renders content**: The storefront might return HTTP 500 status while still rendering full HTML (301KB+). This is likely a non-fatal PHP warning/notice. Check PHP error logs on the server. The page content still loads correctly.
- **Storefront loading spinner**: epartscart.com shows a CSS loading spinner while JS hydrates the page. This can take 15-30s. The DOM is present even while the spinner shows — check the HTML source via curl to verify content is being served.
- **MySQL root password has `@`**: When running MySQL commands via CLI, use interactive mode (`mysql -u root -p`) to avoid shell escaping issues with the `@` character in the password.
- **SQLite vs MySQL schema differences**: Many ecomae schemas use `AUTO_INCREMENT`, `ENGINE=InnoDB`, etc. which SQLite doesn't support. For these, validate source code patterns instead of running live tests. The Readiness Score module is an exception — its checks accept any PDO and work with SQLite.
- **File paths vary by module**: ERP finance modules are in `content/shop/finance/`, not `content/general_pages/`. Platform/BOS modules are in `content/general_pages/`. Always check `git diff --name-only origin/main...HEAD` on the PR branch to find exact paths.
- **Server reboots may stop MySQL**: After a server reboot, MySQL (Percona) might not auto-start. Run `systemctl start mysql`. Also avoid `pkill -f php-fpm` as it might kill nginx too — use `pkill -f "php-fpm: pool"` to target only PHP workers.
- **nginx 404 on new PHP files**: The nginx config on CloudPanel may not serve arbitrary PHP files. New standalone PHP scripts might return 404. Integrate functionality into existing endpoints or ask user to update nginx config.
- **git stash pop conflicts during deploy**: The server may have local modifications (e.g., `epc_stock_brands_helpers.php`). Always use `git stash` before `git pull` and handle conflicts from `git stash pop`.
