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
- **Industry Subdomains** at `https://<industry>.ecomae.com/` — 28 industry-specific marketing storefronts

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

## Testing Industry Storefronts (28 Templates)

### Architecture
- Base template: `content/general_pages/industry_templates/_base_template.php` (~725 lines)
- Industry configs: `content/general_pages/industry_templates/<industry>.php` (28 files)
- Each template renders a full e-commerce storefront with: header, cart, auth modal, products, ERP features, gallery, sub-industries, testimonial, CTA, footer

### Local Rendering (No Server Required)
Render any industry template to static HTML for local testing:
```bash
cd /home/ubuntu/repos/ecomae
php -r "
define('_ASTEXE_', 1);
\$_industry_key = 'food_beverage';
ob_start();
include 'content/general_pages/industry_templates/food_beverage.php';
include 'content/general_pages/industry_templates/_base_template.php';
file_put_contents('/tmp/food_test.html', ob_get_clean());
echo 'Done: ' . filesize('/tmp/food_test.html') . ' bytes';
"
```

Then serve locally:
```bash
cd /tmp && python3 -m http.server 9090 &
# Browse to http://localhost:9090/food_test.html
```

### Expected Template Size
- Old template (pre-PR #126): ~14KB (dark gradient, minimal)
- New template (PR #126+): ~88KB (full storefront with cart, auth, photos)

### Key Elements to Verify
1. **Header**: Logo with industry icon + name, nav links (Products/Features/Industries/About), cart icon with badge, Login/Register buttons
2. **Hero**: Full-width photo background with industry icon, title, tagline, description, 2 CTA buttons
3. **Stats**: 4 KPI counters with labels (different per industry)
4. **Products**: 6 product cards with Unsplash images, category badges, names, prices, "Add to Cart" buttons
5. **ERP Features**: 6 feature cards with icons and descriptions
6. **Gallery**: 5 industry photos in a responsive grid
7. **Sub-industries**: Tag cloud of all sub-areas in that industry
8. **Testimonial**: Blockquote with industry-specific customer quote
9. **CTA**: Final call-to-action with "Start Free Demo" + "View All Industries"
10. **Footer**: 4-column layout (brand+social, quick links, support, newsletter) + demo credentials

### Cart Testing (Client-Side JavaScript)
The cart is entirely client-side (localStorage-based). Key functions:
- `window.addToCart(idx)` — adds product or increments qty
- `window.removeFromCart(idx)` — removes item
- `window.changeQty(idx, delta)` — modifies quantity
- `updateCartUI()` — re-renders badge + sidebar
- `window.openCart()` / `closeCart()` — slides mini-cart

**Test flow:**
1. Click "Add to Cart" on first product → badge shows "1"
2. Click "Add to Cart" on second product → badge shows "2"
3. Click cart icon → sidebar slides in from right
4. Verify items listed with images, names, prices, qty controls (+/- buttons), trash icon
5. Click "+" → qty increments, total recalculates
6. Click trash → item removed, total updates
7. Badge count always matches total items

### Auth Modal Testing
- Click "Register" → modal opens with Register tab active
- Form fields: Full Name, Email, Phone, Company, Industry (dropdown), Password
- Industry dropdown has current industry pre-selected as "(current)" option + all 28 industries
- Click "Login" tab → switches to Email + Password fields
- Demo credentials shown: "demo@ecomae.com / demo2026"
- X button or backdrop click closes modal

### Scroll Animations
- Sections use `class="reveal"` (NOT `scroll-reveal`)
- IntersectionObserver adds `visible` class when section enters viewport
- CSS transition: `opacity: 0 → 1`, `transform: translateY(24px) → translateY(0)`
- Verify via console: `document.querySelectorAll('.reveal.visible').length` should increase after scrolling

### Live Subdomain Verification
```bash
# Quick check all 28 subdomains
for domain in food automotive healthcare jewellery construction electronics fashion manufacturing professional education hospitality beauty retail agriculture logistics energy finance technology media sports homeliving wholesale rental nonprofit cleaning pet printing security; do
  size=$(curl -sk -o /dev/null -w "%{size_download}" "https://$domain.ecomae.com" --max-time 10 2>/dev/null)
  code=$(curl -sk -o /dev/null -w "%{http_code}" "https://$domain.ecomae.com" --max-time 10 2>/dev/null)
  echo "$domain.ecomae.com: HTTP $code | ${size}B"
done
```

Expected: HTTP 200, ~88KB (after PR #126 deploy) or ~14KB (before deploy)

### Industry Differentiation Checklist
When comparing two industry templates, verify ALL of these differ:
- Title text (e.g. "Food & Beverage" vs "Jewellery & Luxury Goods")
- Header icon (fa-cutlery vs fa-diamond)
- CSS color scheme (--primary variable)
- Hero background photo
- Stats section (different KPIs per industry)
- Product names and prices (industry-specific)
- ERP feature descriptions
- Gallery photos
- Sub-industry tags
- Testimonial quote and attribution
- Auth modal industry dropdown first option

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

### Key behavior:
- Page cache only stores complete HTML (must have `<!DOCTYPE`/`<html` at start AND `</html>` at end)
- Partial renders from server timeouts are silently discarded
- `try_serve()` also rejects and deletes stale partial files on read
- Cache is skipped for: logged-in users, POST requests, CP/BOS/API paths, `?nocache` param
- Cache TTL is 5 minutes (300s)

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

## Cursor / Devin Lane Split — DO NOT TOUCH

Cursor owns the epartscart.com storefront lane. Devin owns BOS, ERP, CP shell, backend.

**Files Devin must NEVER modify or overwrite:**
- `content/general_pages/epart_catalog_front_links.php` (homepage sections)
- `content/laximo/**` (Laximo OEM + Aftermarket)
- `content/shop/docpart/part_search_page.php` / `part_search_page_1.php`
- `content/general_pages/epc_cata_config.php`
- `config.epc-laximo.php` (server only, gitignored)

**Lane ownership:**
| Cursor (storefront) | Devin (backend) |
|---------------------|-----------------|
| Homepage widgets & cache | BOS / ERP / Super CP |
| Laximo OEM + Aftermarket UX | Import orchestration |
| Part search / warehouse car-mod | nginx / PHP-FPM tuning |
| Catalog theme, login/header UX | Tenant hub bulk deploys |

## Deployment Workflow

The user deploys manually. After merging a PR:
1. User runs `cd /home/ecomae/htdocs/www.ecomae.com && git stash && git pull origin main && git stash pop`
2. If `git stash pop` conflicts, tell user: `git checkout --theirs <file> && git stash drop`
3. User may need to restart PHP: `systemctl restart php*-fpm`
4. For new features requiring DB tables, run setup: `curl -sk "https://www.ecomae.com/epc-post-deploy-setup-all.php?token=epartscart-deploy-2026"`

## Common Issues

- **Server 524 timeout**: The storefront might return Cloudflare 524 errors when server load is high. BOS (ecomae.com) is lighter and usually works. Wait and retry, or ask user to check server load with `top -c`.
- **BOS pages load slowly (15-30s)**: The BOS page queries the platform DB which includes tenant routing. This is normal under load.
- **Browser cache**: After deploying BOS updates, hard-refresh (Ctrl+Shift+R) is needed to see new version.
- **epartscart.com HTTP 500 but renders content**: The storefront might return HTTP 500 status while still rendering full HTML (301KB+). This is likely a non-fatal PHP warning/notice. Check PHP error logs on the server. The page content still loads correctly.
- **Port conflicts when starting local HTTP server**: Kill existing processes before binding: `pkill -f "python.*http.server"` or use a non-standard port (9090 works well).
- **MySQL root password has `@`**: When running MySQL commands via CLI, use interactive mode (`mysql -u root -p`) to avoid shell escaping issues with the `@` character in the password.
- **SQLite vs MySQL schema differences**: Many ecomae schemas use `AUTO_INCREMENT`, `ENGINE=InnoDB`, etc. which SQLite doesn't support. For these, validate source code patterns instead of running live tests.
- **File paths vary by module**: ERP finance modules are in `content/shop/finance/`, not `content/general_pages/`. Platform/BOS modules are in `content/general_pages/`. Always check `git diff --name-only origin/main...HEAD` on the PR branch to find exact paths.
- **git stash pop conflicts during deploy**: The server may have local modifications. Always use `git stash` before `git pull` and handle conflicts from `git stash pop`.
- **nginx 404 on new PHP files**: The nginx config on CloudPanel may not serve arbitrary PHP files. New standalone PHP scripts might return 404. Integrate functionality into existing endpoints.
- **CP Shell Script Stripping**: The CP shell (`erp_desktop.php`) buffers ALL output from `erp_main.php` and strips every `<script>` tag. JS must be loaded in the `<head>` of `erp_desktop.php` or via PHP proxy files.
