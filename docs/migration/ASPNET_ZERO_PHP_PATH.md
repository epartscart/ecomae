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

`cutoverAllowed` and `readyForPhpRemoval` stay **false** until dual-sample + human `RELEASE_OWNER_APPROVAL.md` (never invent that file).

## Phases

1. Inventory — done  
2. Digests + hybrid shells — done on www  
3. Presentation parity (heroes/fonts/menus) — in progress (`/marketing/app` hub+home; most solutions nav scaffolded incl. free-tools/guides/customer-results/continuity; CP/ERP/BOS/storefront chrome)  
4. Function parity (writes/menus) — in progress (`POST` dry-runs across cart/quote/garage, OMS status/message/courier/delete, ERP cash/GL/purchase/invoice/SO/PO; `aspNetInteractiveComplete=0` until human dual-sample pass)  
5. Tenant exact-route cutover — blocked on parity  
6. PHP removal — blocked on approval  

## Operator commands

```bash
# Path board (after deploy)
curl -sS https://www.ecomae.com/migration/aspnet-zero-php-path | jq .

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
