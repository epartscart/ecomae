# Module function parity status (honest inventory)

Status values: `php-only` · `digest-only` · `hybrid-deeplink` · `aspnet-complete` · `not-started`

ASP.NET **aspnet-complete** count today for interactive product modules: **0**.

Hybrid directory (Batch 0): every tracked module is listed on ASP.NET shells with PHP deeplinks + optional iframe workspace (`?php=`). See `docs/migration/PHP_LEVEL_FULL_PARITY_PLAN.md` and `GET /migration/php-module-catalog`.

| Catalog | Count | Status |
| --- | ---: | --- |
| CP brochure features | 405 | hybrid-deeplink (directory on `/cp/app`) |
| ERP categories | 9 | hybrid-deeplink (`/erp/app` nav) |
| ERP areas | 35 | hybrid-deeplink |
| ERP tabs | 154 | hybrid-deeplink |
| BOS modules | 99 | hybrid-deeplink (`/bos/app`) |
| Storefront surfaces | 12 | hybrid-deeplink (`/storefront/app`) |

## Control Panel (ecomae + tenants)

| Module family | PHP route family | ASP.NET | Status |
| --- | --- | --- | --- |
| Login / session | `/CP/` auth plugin | `/cp/login` bridge (opt-in) | hybrid-deeplink |
| Command centre chrome | `desktop.php` widgets | `/cp/app` KPIs + full 405 directory | digest-only + hybrid-deeplink |
| Orders / OMS | `/CP/control/shop/orders*` | directory → PHP | hybrid-deeplink |
| Customers / users / groups | `/CP/control/users*` | digests + directory → PHP | digest-only + hybrid-deeplink |
| Documents / crosses | `/CP/control/shop/docpart*` | directory → PHP | hybrid-deeplink |
| Procurement / POS | `/CP/control/shop/procurement*`, POS | directory → PHP | hybrid-deeplink |
| Channels / logistics / AI / marketing / payments / integrations | CP nav groups | directory → PHP | hybrid-deeplink |
| Portal / tenants / platform / operator | `/CP/control/portal*` | digests + directory | digest-only + hybrid-deeplink |
| Web tracker / pixels | CP integrations | directory → PHP | hybrid-deeplink |
| Brochure feature set | 405 named features | `/cp/app` directory | hybrid-deeplink |

## ERP

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login landing | `/ERP/` | `/erp/login` | hybrid-deeplink |
| Dashboard chrome | ERP desktop | `/erp/app` KPIs + categories | digest-only + hybrid-deeplink |
| Areas (35 with tabs) | `erp_nav_areas.php` | directory → PHP | hybrid-deeplink |
| Tab UIs (154) | `erp_tabs_*.php` + nav tabs | directory → PHP | hybrid-deeplink |
| Writes / ajax_erp / print | PHP | — | php-only |
| Digests | — | cash/suppliers/SO/PO/GL/… | digest-only |

## BOS

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login (`$_SESSION`) | `/BOS/?action=login` | `/bos/login` admin-cookie bridge | hybrid-deeplink (model mismatch) |
| Fleet / tenant ops / commerce / catalogue / … | ~99–116 module IDs | `/bos/app` directory → PHP | hybrid-deeplink |
| Digests | — | fleet-summary/tenants/health/… | digest-only |

## Storefront (epartscart + tenants)

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Homepage / SEO / analytics | modex desktop + gtag/Clarity | `/storefront/app` preview + GA4 (+ Clarity hook) | hybrid-deeplink (preview) |
| Search / VIN / catalog browse | PHP + Laximo/UMAPI | directory → PHP | hybrid-deeplink |
| Cart / checkout / payments | PHP | directory → PHP | hybrid-deeplink |
| Account digests | PHP | `/storefront/account-summary|orders|garage|profile` | digest-only |
| Customer login | PHP | `/storefront/login` bridge | hybrid-deeplink |

## Catalog / API

| Family | Status |
| --- | --- |
| Wired catalog exact-routes 18/18 | digest-only / cache readers (miss → PHP) |
| Price lookup | aspnet exact-route live |
| UMAPI live fill | php-only |

## Required before PHP removal

For each family still not `aspnet-complete`: human functional test (create/read/update where applicable) with evidence under `docs/migration/evidence/surface-parity/`. Digests and hybrid deeplinks alone are insufficient for PHP removal.
