# Human compare board — Super CP + ePartsCart

Use this checklist (or live `/migration/compare` after deploy) to compare PHP vs ASP.NET yourself.

Locks: `cutoverAllowed=false`, `readyForPhpRemoval=false`, interactive ASP.NET complete = 0.

## Super CP PHP (authoritative look / function)

| Area | URL |
| --- | --- |
| Marketing | https://www.ecomae.com/ |
| CP | https://www.ecomae.com/CP/ |
| ERP | https://www.ecomae.com/ERP/ |
| BOS | https://www.ecomae.com/BOS/ |
| Dedicated CP | https://cp.ecomae.com/CP/ |

Check: brand, login graphics/particles/3D-style motion, desktop chrome, full menus.

## Super CP ASP.NET hybrid (www only)

| Area | Shell | Login | Sample apps |
| --- | --- | --- | --- |
| CP | https://www.ecomae.com/cp/app | https://www.ecomae.com/cp/login | users-app, groups-app, orders |
| ERP | https://www.ecomae.com/erp/app | https://www.ecomae.com/erp/login | sales-orders-app |
| BOS | https://www.ecomae.com/bos/app | https://www.ecomae.com/bos/login | tenants-app, fleet-health-app, audit-log-app |
| Storefront | https://www.ecomae.com/storefront/app | https://www.ecomae.com/storefront/login | search-app, cart-app |

## Tenant ePartsCart PHP (same-to-same)

| Area | apex | www |
| --- | --- | --- |
| Storefront | https://epartscart.com/ | https://www.epartscart.com/ |
| CP | https://epartscart.com/CP/ | https://www.epartscart.com/CP/ |
| ERP | https://epartscart.com/ERP/ | https://www.epartscart.com/ERP/ |

Safety: https://epartscart.com/cp/app and https://epartscart.com/health must stay **404**.

## Probe floor (2026-08-04)

- Digests live on www: **35 / 127** (401 = healthy auth gate)
- Presentation apps live: **19 / 144**
- Files: `live-super-cp-tenant-probe.json`, `www-live-exact-route-probe.json`

## Visual QA notes from agent pass

- Super CP PHP CP/ERP/BOS logins: particle/logo animations present.
- ePartsCart storefront: brand + parts/3D-style motion present; CP/ERP PHP chrome OK.
- Tenant ASP.NET cutover absent (404) — correct.
- www hybrid: `/cp/app`, `/bos/app`, `/storefront/app` and early apps live; most Wave 9–23 routes still 404 pending nginx shadow install.
- BOS login bridge: production copy tightened (no Batch-3 debug callout panel).
