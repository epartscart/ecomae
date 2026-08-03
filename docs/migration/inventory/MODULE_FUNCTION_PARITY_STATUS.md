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
| Login / session | `/CP/` auth plugin | `/cp/login` bridge (opt-in Batch 3 dual-sample) | hybrid-deeplink |
| Command centre chrome | `desktop.php` widgets | `/cp/app` + `/cp/dashboard-summary-app` + mega-nav directory | digest-only + hybrid-deeplink (Batch 2 desktop) |
| Orders / OMS | `/CP/shop/orders/orders` | `/cp/orders` read UI + `/cp/orders-digest` KPI/list | digest-only + hybrid-deeplink (Batch 4; writes PHP) |
| Customers / users / groups | `/CP/control/users*` · `/CP/users/usergroups` | `/cp/users-app` + `/cp/groups-app` lists over digests | digest-only + hybrid-deeplink (Batch 4; writes PHP) |
| Modules manager | `/CP/modules/modules_manager` | `/cp/modules-app` over `/cp/modules` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Content pages | `/CP/content/content_manager` | `/cp/pages-app` over `/cp/pages` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Menu manager | `/CP/menu/menu_manager` | `/cp/menus-app` over `/cp/menus` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| BOS audit log | `/CP/control/portal/epc_boc_audit_log` | `/bos/audit-log-app` over `/bos/audit-log` digest | digest-only + hybrid-deeplink (append-only PHP; `/BOS/` session PHP) |
| Tenant control | `/CP/control/portal/epc_tenant_control_center` | `/cp/tenants-app` over `/cp/tenants` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Currency rates | `/CP/shop/finance/nastrojka-kursov-valyut` | `/cp/currencies-app` over `/cp/currencies` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Warehouses & storages | `/CP/shop/logistics/storages` | `/cp/storages-app` over `/cp/storages` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Admin sessions | `/CP/control/users` (session admin) | `/cp/admin-sessions-app` over `/cp/admin-sessions` digest | digest-only + hybrid-deeplink (tokens never returned; writes PHP; tenant chrome PHP) |
| API clients | `/CP/control/portal/epc_api_clients_manage` | `/cp/api-clients-app` over `/cp/api-clients` digest | digest-only + hybrid-deeplink (key hashes never returned; writes PHP; tenant chrome PHP) |
| Site config | `/CP/control/config_edit` | `/cp/config-items-app` over `/cp/config-items` digest | digest-only + hybrid-deeplink (secrets never returned; writes PHP; tenant chrome PHP) |
| Documents / crosses | `/CP/control/shop/docpart*` | directory → PHP | hybrid-deeplink |
| Procurement / POS | `/CP/control/shop/procurement*`, POS | directory → PHP | hybrid-deeplink |
| Channels / logistics / AI / marketing / payments / integrations | CP nav groups | directory → PHP | hybrid-deeplink |
| Portal / tenants / platform / operator | `/CP/control/portal*` | digests + directory | digest-only + hybrid-deeplink |
| Web tracker / pixels | CP integrations | directory → PHP | hybrid-deeplink |
| Brochure feature set | 405 named features | `/cp/app` directory | hybrid-deeplink |

## ERP

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login landing | `/ERP/` | `/erp/login` (Batch 3 admin cookie bridge) | hybrid-deeplink |
| Dashboard chrome | ERP desktop | `/erp/app` + `/erp/dashboard-summary-app` + topnav categories/tabs | digest-only + hybrid-deeplink (Batch 2 desktop) |
| Areas (35 with tabs) | `erp_nav_areas.php` | directory → PHP | hybrid-deeplink |
| Tab UIs (154) | `erp_tabs_*.php` + nav tabs | directory → PHP; SO list at `/erp/sales-orders-app` | hybrid-deeplink (+ Batch 4 SO read UI) |
| Sales orders tab | `erp_tabs_sales_orders.php` | `/erp/sales-orders-app` over `/erp/sales-orders` digest | digest-only + hybrid-deeplink (Batch 4; writes PHP) |
| Purchase orders tab | `erp_tabs_purchase_orders.php` | `/erp/purchase-orders-app` over `/erp/purchase-orders` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Invoices tab | `erp_tabs_invoices.php` | `/erp/invoices-app` over `/erp/invoices` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Cash & bank tab | `erp_tabs_cash_bank.php` | `/erp/cash-accounts-app` + `/erp/cash-entries-app` + `/erp/accounts-summary-app` over digests | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Chart of accounts | `erp_tabs_accounting.php` (`tab=coa`) | `/erp/coa-accounts-app` over `/erp/coa-accounts` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| General ledger | `erp_tabs_accounting.php` (`tab=gl`) | `/erp/gl-journals-app` over `/erp/gl-journals` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Warehouses / inventory | `erp_tabs_inventory.php` | `/erp/warehouses-app` + `/erp/inventory-stock-app` over digests | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Suppliers / AP payables | `erp_main.php` payables / `epc_erp_list_suppliers` | `/erp/suppliers-app` over `/erp/suppliers` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Purchases | `erp_main.php` purchases / `epc_erp_list_purchases` | `/erp/purchases-app` over `/erp/purchases` digest | digest-only + hybrid-deeplink (writes PHP; tenant chrome PHP) |
| Writes / ajax_erp / print | PHP | — | php-only |
| Digests | — | cash/suppliers/SO/PO/GL/… | digest-only |

## BOS

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Login (`$_SESSION`) | `/BOS/?action=login` | `/bos/login` admin-cookie bridge only | hybrid-deeplink (Batch 3 decision: keep `/BOS/` PHP-authoritative) |
| Fleet / tenant ops / commerce / catalogue / … | ~99–116 module IDs | `/bos/app` + `PhpBosDesktopChrome` topnav + directory → PHP | hybrid-deeplink (Batch 2 desktop; session model still PHP) |
| Digests | — | fleet-summary/tenants/health/readiness/… | digest-only (+ fleet-summary/tenants/health/readiness/audit-log Blazor apps) |

## Storefront (epartscart + tenants)

| Module family | PHP | ASP.NET | Status |
| --- | --- | --- | --- |
| Homepage / SEO / analytics | modex desktop + gtag/Clarity | `/storefront/app` + `PhpStorefrontDesktopChrome` + GA4 (+ Clarity hook) | hybrid-deeplink (Batch 2 preview; cart/checkout PHP) |
| Search / VIN / catalog browse | PHP + Laximo/UMAPI | `/storefront/search-app` read-only offers; VIN/tabs → PHP | digest-only + hybrid-deeplink (Batch 4 search; cart/tabs PHP) |
| Cart / checkout / payments | PHP `/shop/cart` + checkout | `/storefront/cart-app` read summary; checkout → PHP | digest-only + hybrid-deeplink (Batch 4 cart; writes/checkout PHP) |
| Account digests | PHP | `/storefront/account-summary|orders|garage|profile` | digest-only (+ Blazor KPI/list/read UIs) |
| Customer orders | PHP `/shop/orders` | `/storefront/orders-app` over `/storefront/orders` digest | digest-only + hybrid-deeplink (detail PHP; live storefront PHP) |
| Customer garage | PHP part_search garage | `/storefront/garage-app` over `/storefront/garage` digest | digest-only + hybrid-deeplink (add/edit PHP; live storefront PHP) |
| Customer profile | PHP `/users/profile` | `/storefront/profile-app` over `/storefront/profile` digest | digest-only + hybrid-deeplink (edits PHP; live storefront PHP) |
| Customer account summary | PHP `/users/` | `/storefront/account-summary-app` over `/storefront/account-summary` digest | digest-only + hybrid-deeplink (account tools PHP; live storefront PHP) |
| Customer login | PHP | `/storefront/login` bridge (Batch 3 PHP token formula) | hybrid-deeplink |

## Catalog / API

| Family | Status |
| --- | --- |
| Wired catalog exact-routes 18/18 | digest-only / cache readers (miss → PHP) |
| Price lookup | aspnet exact-route live |
| UMAPI live fill | php-only (Batch 5 miss harness + `catalog-miss-fill` dry-run; outbound/writes blocked; fill stays PHP) |

## Hybrid UI dual-sample harness

Contract stubs + compare under `docs/migration/evidence/hybrid-ui-dual-samples/` (capture: `cloudpanel_capture_hybrid_ui_dual_samples.sh`). Covers CP/ERP/BOS/storefront `*-app` (+ `/cp/orders`) www previews. Live cookie captures are operator-only; `cutoverAllowed=false`. Digests keep their separate `surface-parity/` dual-sample family.

## Module-function parity contract harness

Inventory + compare under `docs/migration/evidence/module-function-parity/` (`bash scripts/cloudpanel_run_module_function_parity_operator.sh`). Derived from hybrid UI TARGETS (37). **`aspnetCompleteCount=0`** until a human attaches `docs/migration/evidence/presentation/MODULE_FUNCTION_TEST_PASS.md` containing `MODULE_FUNCTION_PARITY_PASS`. Never invent that file. `cutoverAllowed=false`.

## Required before PHP removal

For each family still not `aspnet-complete`: human functional test (create/read/update where applicable) with evidence under `docs/migration/evidence/surface-parity/` plus the human pass marker above. Digests and hybrid deeplinks alone are insufficient for PHP removal.
