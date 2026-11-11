# AGENTS.md

## Cursor Cloud specific instructions

This repo is **ecomae / EPC** (epartscart) — a large, monolithic PHP application (custom "DP"
engine, no Laravel/Symfony). It serves several UI surfaces from a single front controller
(`index.php`): a marketing site, a multi-tenant storefront, a tenant control panel (`cp/`), a
"Super CP" platform admin, and an ERP suite. PHP dependencies are vendored under `lib/`; there is
**no root build step and no root package manager** (`composer.json`/`package.json` only exist inside
vendored libs). An optional Python price-import helper lives in `pyprices/`.

### Stack / services
- **PHP 8.3 CLI** — runs the app (via PHP's built-in server), the CLI tests, and the maintenance
  scripts. Extensions in use: `pdo_mysql`, `mysqli`, `mbstring`, `gd`, `curl`, `intl`, `soap`, `zip`.
- **MySQL 8.0** — primary datastore; multi-tenant (a DB per tenant). A dev user `erp`/`erp` exists
  with full privileges, and these DBs are pre-created: `erp_test`, `erp_test_b`, `erp_tenantA_test`,
  `erp_tenantB_test`, `erp_fin_test`, `ecomae_erp`.
- **Redis** — optional; the app falls back to a file page-cache, so it is not required.
- **pyprices** (optional) — a CGI-style Python helper invoked on demand by PHP via `proc_open`; it is
  **not** a long-running server and degrades gracefully if its deps are missing. Its virtualenv lives
  at `pyprices/venv` (auto-discovered by `pyprices/pyprices-api.php`).

### Start MySQL each fresh session (required)
MySQL is **not** auto-started on VM boot. Before running tests or the app:
```
sudo service mysql start
```
If the `erp` user or the dev/test databases are ever missing, recreate them:
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
- Single suite: `DB_HOST=127.0.0.1 DB_NAME=erp_fin_test DB_USER=erp DB_PASS=erp php tests/erp_advanced/run_finance_tests.php`
- Lint gate (custom CP render-safety linter — this repo has no PHPCS/PHPStan/ESLint):
  `php tests/erp_advanced/run_cp_lint.php` (also runs as part of `run_all.sh`).

**Gotcha — `run_industry_currency_tests.php`** hard-codes and requires `erp_tenantA_test` and
`erp_tenantB_test`; `run_all.sh` only wipes/creates `DB_NAME`/`DB_NAME2`. Those two DBs must already
exist (they are pre-created) or that one suite reports a false `0 pass / 0 fail`.

**Gotcha — one known benign failure.** With a working dev DB + `config.php` present,
`run_free_tools_tests.php` fails exactly one check: `usage_stats not ok without DB`. That check
asserts a "no database" scenario, but `epc_free_tools_usage_stats()` reads the real `config.php` and
connects successfully, so it (correctly) returns `ok=true`. This is a test-design contradiction, not
an environment problem — expect `run_all.sh` to report `2198 passed, 1 failed`.

### Running the app (dev)
There is no framework dev-server script. Serve the repo root (it is the document root) with PHP's
built-in server:
```
php -S 0.0.0.0:8080   # run from the repo root
```
This requires a root **`config.php`** (gitignored) defining `class DP_Config` with DB credentials
(`host`, `db`, `user`, `password`, `domain_path`, `backend_dir`, `multilang`, ...). A local dev
`config.php` pointing at `127.0.0.1` / `erp` / `erp` / `ecomae_erp` already exists in this
environment. If it is ever missing, recreate it as a class `DP_Config` with those four DB fields plus
`domain_path=''`, `backend_dir='cp'`, `multilang='1'` (annotate the class with
`#[\AllowDynamicProperties]` for PHP 8.3).

**Gotcha — which surfaces render without a full tenant DB:**
- The **marketing site** (`www.ecomae.com` host) renders fully **without** MySQL — e.g.
  `curl -H "Host: www.ecomae.com" http://127.0.0.1:8080/` and `/platform`. For a browser, add
  `127.0.0.1 www.ecomae.com` to `/etc/hosts` and visit `http://www.ecomae.com:8080/`.
- The **storefront** (bare `localhost` root) and the **tenant CP** (`/cp/`) require the full legacy
  CMS/tenant schema (`page_pages`, `lang_languages`, modules, ...), which is **not** shipped as a seed
  in the repo. Without a seeded tenant DB they short-circuit (e.g. `No DB connect` or
  `License error 1.04`). Do not expect the storefront/CP to render end-to-end from a fresh DB.

### Onboarding wizard (creates admin account + company)
`deploy/on-premises/setup-wizard.php` is a non-interactive CLI onboarding tool. Against a fresh DB it
creates minimal `epc_*` tables, an `admin` user (random password printed once), a company profile,
and activates the core modules:
```
DB_HOST=127.0.0.1 DB_DATABASE=ecomae_erp DB_USERNAME=erp DB_PASSWORD=erp \
  APP_URL=http://localhost:8080 php deploy/on-premises/setup-wizard.php
```
The rest of `deploy/on-premises/*` is a **production** Docker installer (needs a license key); prefer
the plain `php -S` dev server above for local work.

### pyprices (optional)
`pyprices/requirements.txt` pins are stale and do not all install on Python 3.12 (e.g.
`pkg_resources==0.0.0`). Install the core deps unpinned into `pyprices/venv` instead (the update
script does this). pyprices is optional and the app runs without it.
