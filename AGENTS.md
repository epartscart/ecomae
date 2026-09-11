# AGENTS.md

## Cursor Cloud specific instructions

This repo is **ecomae ERP** — a large, monolithic PHP application (custom "DP" framework, no
Laravel/Symfony) with several UI surfaces (marketing site, storefront, tenant CP, Super CP, BOS)
plus an optional Python price-import helper (`pyprices`). There is **no build step** and **no root
package manager manifest** — PHP dependencies are vendored under `lib/`, and JS/CSS is served as-is.

### Services / stack
- **PHP 8.3 CLI** (installed in the VM image) — runs the app, tests, and the built-in dev web server.
- **MySQL 8.0** (installed in the VM image) — primary datastore; multi-tenant (a DB per tenant).
  Started with `sudo service mysql start`. A local dev user `erp` / `erp` exists with full
  privileges, and dev/test databases are pre-created (see below).
- **Redis** — optional cache/sessions; the app falls back to a file page-cache, so it is not required.
- **pyprices** (optional) — a CGI-style Python helper invoked on demand by PHP via `proc_open`; it is
  **not** a long-running server and degrades gracefully if Python deps are absent. Its virtualenv
  lives at `pyprices/venv` (auto-discovered by `pyprices/pyprices-api.php`).

### Starting MySQL (required each fresh VM session)
MySQL is not auto-started. Run `sudo service mysql start` before running tests or the app. The
`erp` user and the `erp_test`, `erp_test_b`, `erp_tenantA_test`, `erp_tenantB_test`, `erp_fin_test`,
and `ecomae_erp` databases are created as part of the VM image; if they are missing on a fresh
machine, recreate them as:
```
sudo mysql -e "CREATE USER IF NOT EXISTS 'erp'@'127.0.0.1' IDENTIFIED WITH mysql_native_password BY 'erp';
GRANT ALL PRIVILEGES ON *.* TO 'erp'@'127.0.0.1' WITH GRANT OPTION; FLUSH PRIVILEGES;
CREATE DATABASE IF NOT EXISTS erp_test; CREATE DATABASE IF NOT EXISTS erp_test_b;
CREATE DATABASE IF NOT EXISTS erp_tenantA_test; CREATE DATABASE IF NOT EXISTS erp_tenantB_test;
CREATE DATABASE IF NOT EXISTS erp_fin_test; CREATE DATABASE IF NOT EXISTS ecomae_erp;"
```

### Tests + lint (the primary dev workflow)
The real test surface is `tests/erp_advanced/` (~2,199 checks across ~57 CLI runners). Runners are
**CLI-only** and read `DB_HOST/DB_NAME/DB_USER/DB_PASS` from the environment.
- Full suite: `DB_HOST=127.0.0.1 DB_NAME=erp_test DB_NAME2=erp_test_b DB_USER=erp DB_PASS=erp bash tests/erp_advanced/run_all.sh`
- Single suite: `DB_HOST=127.0.0.1 DB_NAME=erp_test DB_USER=erp DB_PASS=erp php tests/erp_advanced/run_finance_tests.php`
- Lint gate (custom CP render-safety linter — this repo has no PHPCS/PHPStan/ESLint):
  `php tests/erp_advanced/run_cp_lint.php` (also runs as part of `run_all.sh`).

**Gotcha:** `run_all.sh` only wipes/creates `DB_NAME` and `DB_NAME2`, but
`run_industry_currency_tests.php` hard-codes and requires `erp_tenantA_test` and `erp_tenantB_test`.
Those two databases must exist or that one suite reports `0 pass / 0 fail` (a false failure). They
are pre-created in the VM image.

### Running the app (dev)
There is no framework dev-server script. Serve the repo root (it is the document root) with PHP's
built-in server:
```
php -S 0.0.0.0:8080   # run from the repo root
```
This requires a root `config.php` (gitignored) defining `class DP_Config` with DB credentials
(`host`, `db`, `user`, `password`, `domain_path`, `backend_dir`, etc.). A local dev `config.php`
pointing at `127.0.0.1` / `erp` / `erp` / `ecomae_erp` is created in the VM image.

**Gotcha — which surfaces render without a full tenant DB:**
- The **marketing site** (`www.ecomae.com` / `ecomae.com` host) renders fully **without** MySQL —
  e.g. `curl -H "Host: www.ecomae.com" http://127.0.0.1:8080/` and `/platform`. For a browser, add
  `127.0.0.1 www.ecomae.com` to `/etc/hosts` and visit `http://www.ecomae.com:8080/`.
- The **storefront** (bare `localhost` root) and the **tenant CP** (`/cp/`) require the full legacy
  CMS/tenant schema (`page_pages`, `lang_languages`, modules, etc.), which is **not** shipped as a
  seed in the repo. Without a seeded tenant DB they short-circuit with a plain `No DB connect` body.
  Do not expect the storefront/CP to render end-to-end from a fresh DB.

### Onboarding wizard (creates admin account + company)
`deploy/on-premises/setup-wizard.php` is a CLI onboarding tool. Against a fresh DB it creates the
minimal `epc_*` tables, an `admin` user (random password printed once), a company profile, and
activates the core modules:
```
DB_HOST=127.0.0.1 DB_DATABASE=ecomae_erp DB_USERNAME=erp DB_PASSWORD=erp \
  APP_URL=http://localhost:8080 php deploy/on-premises/setup-wizard.php
```
The `deploy/on-premises/*` Docker stack is a **production** installer (needs a license key); prefer
the plain `php -S` dev server above for local work.

### pyprices (optional)
`pyprices/requirements.txt` pins are stale and do not all install on Python 3.12 (e.g.
`pkg_resources==0.0.0`). Install the core deps unpinned into `pyprices/venv` instead (done by the
update script). pyprices is optional and the app runs without it.
