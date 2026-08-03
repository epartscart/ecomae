# Module function parity status (honest inventory)

Status values: `php-only` · `digest-only` · `hybrid-deeplink` · `aspnet-complete` · `not-started`

ASP.NET **aspnet-complete** count today for interactive product modules: **0**.

## Control Panel (ecomae + tenants)

| Module family | PHP route family | ASP.NET | Status |
| --- | --- | --- | --- |
| Login / session | `/CP/` auth plugin | `/cp/login` bridge (opt-in) | hybrid-deeplink |
| Command centre chrome | `desktop.php` widgets | `/cp/app` KPIs | digest-only |
| Orders / OMS | `/CP/control/shop/orders*` | nav link → PHP | php-only |
| Customers / users / groups | `/CP/control/users*` | digests `/cp/users|/groups` | digest-only |
| Documents / crosses | `/CP/control/shop/docpart*` | deeplink | php-only |
| Procurement / POS | `/CP/control/shop/procurement*`, POS | deeplink | php-only |
| Channels / logistics / AI / marketing / payments / integrations | CP nav groups | deeplink | php-only |
| Portal / tenants / platform / operator | `/CP/control/portal*` | digests `/cp/tenants` | digest-only |
| Web tracker / pixels | CP integrations | — | php-only |
| Brochure feature set | 405 named features | — | php-only |

## ERP

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login landing | `/ERP/` | `/erp/login` | hybrid-deeplink |
| Dashboard chrome | ERP desktop | `/erp/app` KPIs | digest-only |
| Areas (overview, finance, AP/AR, banking, inventory, HR, tax, …) | `erp_nav_areas.php` (~44 areas) | deeplink `?area=` | php-only |
| Tab UIs | ~160 `erp_tabs_*.php` | — | php-only |
| Writes / ajax_erp / print | PHP | — | php-only |
| Digests | — | cash/suppliers/SO/PO/GL/… | digest-only |

## BOS

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login (`$_SESSION`) | `/BOS/?action=login` | `/bos/login` admin-cookie bridge | hybrid-deeplink (model mismatch) |
| Fleet / tenant ops / commerce / catalogue / … | ~116 module IDs | deeplink `/BOS/` | php-only |
| Digests | — | fleet-summary/tenants/health/… | digest-only |

## Storefront (epartscart + tenants)

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Homepage / SEO / analytics | modex desktop + gtag/Clarity | `/storefront/app` preview | not-started (preview only) |
| Search / VIN / catalog browse | PHP + Laximo/UMAPI | — | php-only |
| Cart / checkout / payments | PHP | — | php-only |
| Account digests | PHP | `/storefront/account-summary|orders|garage|profile` | digest-only |
| Customer login | PHP | `/storefront/login` bridge | hybrid-deeplink |

## Catalog / API

| Family | Status |
| --- | --- |
| Wired catalog exact-routes 18/18 | digest-only / cache readers (miss → PHP) |
| Price lookup | aspnet exact-route live |
| UMAPI live fill | php-only |

## Required before PHP removal

For each row above marked `php-only` or `digest-only`: human functional test (create/read/update where applicable) with evidence under `docs/migration/evidence/surface-parity/`. Digests alone are insufficient.
