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

## Common Issues

- **Server 524 timeout**: The storefront might return Cloudflare 524 errors when server load is high. BOS (ecomae.com) is lighter and usually works. Wait and retry, or ask user to check server load with `top -c`.
- **BOS pages load slowly (15-30s)**: The BOS page queries the platform DB which includes tenant routing. This is normal under load.
- **Browser cache**: After deploying BOS updates, hard-refresh (Ctrl+Shift+R) is needed to see new version.
- **MySQL root password has `@`**: When running MySQL commands via CLI, use interactive mode (`mysql -u root -p`) to avoid shell escaping issues with the `@` character in the password.
