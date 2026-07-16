# Tenant scale readiness (1000+ mix tenants)

This document describes the foundations shipped so the platform can absorb a large mixed fleet (commerce + ERP-only + demos) without rewriting PHP to Python.

## Goals

1. **DB isolation by default** for new tenants (`dedicated_mysql`)
2. **Connection reuse** in fleet/cron paths (process-local PDO pool)
3. **Background jobs** for heavy work (warmup, health, future syncs)
4. **Keep legacy Model C** (`shared_docpart`) working for existing storefronts

## Scale policy

| Policy | Flag | Runtime DB | When to use |
|---|---|---|---|
| `dedicated_mysql` | `dedicated_db=1` | Registry `db_name` / `db_user` / `db_password` | **Default for new onboardings** |
| `shared_docpart` | `dedicated_db=0` | Shared `docpart` + logical isolation | Legacy commerce tenants |

ERP-only shared companies (`erp_only_shared=1`) already provision a dedicated MySQL; schema backfill marks them `dedicated_mysql`.

## Onboarding (Super CP)

Path: Tenant Hub → Onboard

- **Scale policy** select defaults to **Dedicated MySQL (recommended)**
- On save, empty password + dedicated policy → auto-provision DB/user via existing CloudPanel/MySQL helpers
- A `tenant_warmup_pdo` job is enqueued (best-effort) so the first login is warmer

Opt out only when you intentionally want shared `docpart`.

## Connection manager

File: `content/general_pages/epc_tenant_pdo.php`

- `epc_tenant_pdo($host, $db, $user, $pass)` — pooled PDO
- `epc_tenant_pdo_from_row($row)` — registry row helper (`db_password` / `db_pass`)
- Soft cap: `EPC_TENANT_PDO_POOL_MAX` (default **24**) per PHP process

Wired into:

- `epc_portal_tenant_storefront_pdo()`
- `epc_portal_shared_erp_tenant_pdo()`
- `epc_portal_tenant_control_tenant_pdo_connect()`

## Platform job queue

| Piece | Path |
|---|---|
| Library | `content/general_pages/epc_platform_jobs.php` |
| Cron / HTTP worker | `epc-platform-jobs-cron.php` |
| Table | `epc_platform_jobs` (created on first use in platform DB) |

### Cron

```cron
* * * * * php /var/www/ecomae/epc-platform-jobs-cron.php >/dev/null 2>&1
```

HTTP (optional):

```
GET /epc-platform-jobs-cron.php?limit=10&key=YOUR_CRON_KEY
```

Set `EPC_PLATFORM_JOBS_CRON_KEY` (or constant) when exposing over HTTP.

### Built-in job types

- `tenant_health_ping` — connect + `SELECT 1`
- `tenant_warmup_pdo` — ping + soft table touches
- `noop` — test

Register custom handlers:

```php
epc_platform_jobs_register_handler('my_job', function ($tenantKey, $payload, $job) {
    return ['ok' => true, 'result' => []];
});
```

Enqueue:

```php
epc_platform_jobs_enqueue('tenant_health_ping', 'acme', [], ['priority' => 100, 'dedupe' => true]);
```

## Runtime resolve

`epc_portal_resolve_tenant_db()` now prefers registry credentials when the tenant is dedicated (`dedicated_db`, `scale_policy=dedicated_mysql`, or non-`docpart` `db_name`). Legacy client hosts without dedicated flags still resolve to shared `docpart`.

## Ops checklist before a large inbound wave

1. Deploy this branch / merge PR
2. Ensure MySQL user used by provision has `CREATE DATABASE` / `CREATE USER` (or CloudPanel web provision works)
3. Raise MySQL `max_connections` for expected PHP-FPM workers × pool size (not 1×1000)
4. Install `epc-platform-jobs-cron.php` every minute
5. Onboard new tenants with **Dedicated MySQL** (default)
6. Leave existing `docpart` tenants alone unless migrating deliberately
7. Monitor: failed jobs in `epc_platform_jobs`, MySQL threads, disk for new schemas

## What this is not

- Not a PHP→Python rewrite
- Not automatic migration of all existing `docpart` tenants
- Not a global cross-worker connection pool (PHP-FPM is process-local; that is expected)

## Smoke test

```bash
php tests/erp_advanced/run_tenant_scale_tests.php
```
