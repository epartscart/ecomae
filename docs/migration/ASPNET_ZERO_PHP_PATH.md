# ASP.NET Core → 0 PHP path

**Target end-state:** 100% ASP.NET Core · 0 PHP in production.

**Not the destination:** PHP-primary on live tenants/marketing. That is a **parity gate** so same-to-same UX holds while ASP.NET is finished.

Live JSON: `GET /migration/aspnet-zero-php-path`  
Related: `docs/migration/ZERO_PHP_PRODUCTION_CUTOVER_ROADMAP.md`

## Parity gate (interim)

| Surface | Today | Gate | Unlock |
| --- | --- | --- | --- |
| Named live tenants (5) | PHP product chrome | Same-to-same dual-sample | `ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES` for exact-route parity shadows → then staged cutover |
| www marketing `/` | PHP `epm-hub` | ASP.NET `/marketing/app` dual-sample | Exact-route promotion after approval |
| www hybrid apps | ASP.NET scaffold | Function dual-sample | Per-route exact-route promotion |
| On-premises ERP pack | PHP `deploy/on-premises/*` + license/health APIs | Dual-sample vs ASP.NET scaffolds/dry-runs | Exact-route + ASP.NET installer pack after approval |
| SaaS ERP-only tenants | `TenantMode.ErpOnlyTenant` mapped | Navigation/storefront-denial dual-sample | Staged cutover (≠ on-prem installer) |

`cutoverAllowed` and `readyForPhpRemoval` stay **false** until dual-sample + human `RELEASE_OWNER_APPROVAL.md` (never invent that file).

## On-premises ERP (mandatory for 0 PHP)

PHP ships a **self-hosted ERP option** (`deploy/on-premises/*`, `erp_tabs_on_premises.php`, `api/v1/on-premises/health.php`, `api/v1/licenses/activate.php`, `epc_onprem_licenses`). This is **not** the same as SaaS ERP-only tenants (`erp_only` / `TenantMode.ErpOnlyTenant`).

| Track | ASP.NET today | Mandate |
| --- | --- | --- |
| ERP-only SaaS mode | Mode mapping + digests | Keep dual-sampling; no invented cutover |
| On-premises installer | Scaffold `deploy/on-premises-aspnet/` + setup-wizard/backup dry-runs | PHP pack remains authoritative until dual-sample |
| License/health APIs | Health + activate + setup-wizard + backup dry-runs + read digest (`GET /erp/on-premises/licenses`, keys masked) | PHP activate/health/registry remain authoritative |
| Operator tab | `/erp/on-premises-app` overview scaffold | Dual-sample vs PHP tab before exact-route |
| ERP ajax writes | Full `ajax_erp.php` catalog: **321/321 dedicated** dry-runs (+ registry gate retained) | Live writes remain PHP; `aspNetInteractiveComplete=0` |
| CP/storefront module ajax + classic forms | Wave C–F catalog: ajax holdouts + classic forms (**390** actions; **245** dedicated + registry) | Live writes remain PHP; dual-sample pending before 100% |

Live board: `GET /migration/on-premises-parity`

## Phases

1. Inventory — done  
2. Digests + hybrid shells — done on www  
3. Presentation parity (heroes/fonts/menus) — in progress (`/marketing/app` hub+home; solutions+resources+legal aliases; CP/ERP/BOS/storefront chrome; `/erp/on-premises-app`)  
4. Function parity (writes/menus) — in progress (full `ajax_erp.php` catalog dedicated; BOS ajax catalog; CP module ajax catalog; concurrency/BOS/OPL/PF/AML/bank dry-runs; quote/garage/OMS + on-premises health/activate/setup-wizard/backup; write-dryrun dual-sample operator floor; `aspNetInteractiveComplete=0` until human dual-sample pass)  
5. Tenant exact-route cutover — blocked on parity  
6. PHP removal — blocked on approval (SaaS **and** on-premises installer pack)  

## Operator commands

```bash
# Path board (after deploy)
curl -sS https://www.ecomae.com/migration/aspnet-zero-php-path | jq .

# On-premises ERP track (installer ≠ ERP-only SaaS)
curl -sS https://www.ecomae.com/migration/on-premises-parity | jq .
curl -sS -X POST https://www.ecomae.com/erp/on-premises/health-dry-run \
  -H 'content-type: application/json' \
  -d '{"licenseKey":"DEMO-KEY-XXXX","status":"ok","confirmWrites":false}'
curl -sS -X POST https://www.ecomae.com/erp/on-premises/license-activate-dry-run \
  -H 'content-type: application/json' \
  -d '{"licenseKey":"LIC-2026-ABCD-EFGH","fingerprint":"fp-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","confirmWrites":false}'
curl -sS https://www.ecomae.com/erp/on-premises/licenses | jq .
curl -sS -X POST https://www.ecomae.com/erp/on-premises/setup-wizard-dry-run \
  -H 'content-type: application/json' \
  -d '{"tenantCode":"demo","confirmWrites":false}'
curl -sS -X POST https://www.ecomae.com/erp/on-premises/backup-dry-run \
  -H 'content-type: application/json' \
  -d '{"label":"dry-run","confirmWrites":false}'

# Full ERP ajax_erp.php coverage board (dedicated + registry)
curl -sS https://www.ecomae.com/erp/ajax-writes/catalog | jq '{totalActions,dedicatedDryRuns,registryDryRuns,coveragePct,cutoverAllowed}'
curl -sS -X POST https://www.ecomae.com/erp/ajax-writes/dry-run/agenda_save \
  -H 'content-type: application/json' -H "Cookie: $ADMIN_COOKIE" \
  -d '{"confirmWrites":false}'

# Marketing ASP.NET scaffold vs live PHP
curl -sS https://www.ecomae.com/marketing/app
bash scripts/cloudpanel_probe_ecomae_marketing_php_chrome.sh

# Tenant still PHP-primary until parity unlock
bash scripts/cloudpanel_verify_tenant_hosts_still_php.sh

# When dual-sample ready — exact-route parity shadow on a named tenant (not broad /):
ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW=YES \
  ECOMAE_NGINX_SITE_CONF=/etc/nginx/sites-enabled/epartscart.com.conf \
  bash scripts/cloudpanel_install_exact_route_shadow.sh …
```
