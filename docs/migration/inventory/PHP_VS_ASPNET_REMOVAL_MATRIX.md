# PHP vs ASP.NET removal matrix

Living board: `GET /migration/php-vs-aspnet-matrix`  
Source: `aspnet/src/EcomAE.Platform/Migration/PhpVsAspNetRemovalMatrix.cs`  
Evidence snapshot: `docs/migration/evidence/php-vs-aspnet-removal-matrix.json`

**PHP files are not deleted by this work.** `readyForPhpRemoval=false`, `phpSourceDeletionAllowed=false`, `cutoverAllowed=false`. Digests and href remaps are browse twins, not a cutover switch.

## Inventory (product PHP, exclude vendor / venv)

| Tree | `.php` files | Role |
| --- | ---: | --- |
| `content/` | 1437 | Storefront + shop + Laximo SDK |
| `cp/` | 638 | Control panel |
| `templates/` | 6 | Theme chrome |
| `bos/` | 2 | BOS shell |
| **Product-facing approx.** | **2083** | |

ASP.NET already has **312** Blazor `@page` apps (CP 131, ERP 62+, storefront 52, marketing 37, LifeOS 18, BOS 7). Navigation coverage is high. **Interactive write complete count is 0.**

## Status key

| Status | Meaning |
| --- | --- |
| `aspnet-digest` | Blazor read UI + JSON digest. Writes stay PHP. |
| `aspnet-routed` | PHP href remaps to a related existing app. |
| `aspnet-hub` | PHP href remaps to `/erp` or `/cp` hub only. |
| `php-writes` | Ajax / cron / gateway still PHP-authoritative. |

## Closed this wave (finance hrefs that used to collapse to `/erp`)

| PHP href | ASP.NET app |
| --- | --- |
| `/CP/shop/finance` | `/erp` (hub, intentional) |
| `/CP/shop/finance/epc_credit_limit` | `/cp/credit-limits-app` |
| `/CP/shop/finance/epc_po_approval` | `/cp/po-approvals-app` |
| `/CP/shop/finance/epc_warranty_rma` | `/cp/returns-rma-app` |
| `/CP/shop/finance/epc_wps_payroll` | `/erp/payroll-app` |
| `/CP/shop/finance/epc_subscription_billing` | `/erp/sales-orders-app?tab=subscriptions` |
| `/CP/shop/finance/epc_order_erp_pipeline` | `/erp/order-pipeline-app` (**new digest**) |
| `/CP/shop/finance/epc_inventory_forecast` | `/erp/inventory-forecast-app` (**new digest**) |
| `/CP/shop/finance/epc_multi_entity` | `/erp/multi-entity-app` (**new digest**) |
| `/CP/shop/finance/epc_multi_currency_gl` | `/erp/multi-currency-gl-app` (**new digest**) |
| `/CP/shop/finance/payment_systems` | `/cp/payment-gateways-app` |
| `/CP/shop/finance/account_operations` | `/cp/credit-limits-app` |

New JSON digests: `/erp/order-pipeline`, `/erp/inventory-forecast`, `/erp/multi-entity`, `/erp/multi-currency-gl`.

## Still missing for PHP removal

These families have ASP.NET shells or remaps but **writes stay PHP**:

- Storefront cart / checkout / pay (`content/shop/order_process/ajax_*`, payment `go_to_pay.php`)
- Live Laximo VIN decode (`content/laximo` Guayaquil SDK)
- UMAPI miss-fill
- CP OMS status/item writes
- CP PyPrices ingest + cron
- ~430 CP module ajax actions (`CpModuleAjaxWriteCatalog`)
- ERP `ajax_erp.php` tab writes
- Payment gateway notify / capture
- Theme `desktop.php` compare-only chrome (CSS already reused)

Do not flip removal flags until each family has a human `MODULE_FUNCTION_PARITY_PASS` plus dual-sample evidence.
