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

ASP.NET already has **312** Blazor `@page` apps (CP 131, ERP 62+, storefront 52, marketing 37, LifeOS 18, BOS 7). Navigation coverage is high. **Interactive write complete count stays 0** until dual-sample evidence. Type-2 cart, OMS item status, payroll approve, credit limits, PO approve/reject, and forecast recompute now write on ASP.NET when `confirmWrites=true`.

## Status key

| Status | Meaning |
| --- | --- |
| `aspnet-digest` | Blazor read UI + JSON digest. Writes stay PHP. |
| `aspnet-routed` | PHP href remaps to a related existing app. |
| `aspnet-hub` | PHP href remaps to `/erp` or `/cp` hub only. |
| `php-writes` | Ajax / cron / gateway still PHP-authoritative. |
| `writesOwner=aspnet` | Primary mutation for that row now posts to an ASP.NET live twin. |

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

- Storefront type-1 cart + checkout/create (`content/shop/order_process` residual ajax)
- Payment `go_to_pay.php` / notify
- Live Laximo VIN decode (`content/laximo` Guayaquil SDK)
- UMAPI miss-fill
- Remaining OMS ajax (message / courier / delete / fulfillment stage)
- CP PyPrices ingest + cron
- ~430 CP module ajax actions (`CpModuleAjaxWriteCatalog`)
- ERP `ajax_erp.php` residual tab writes (generate/pay payroll, GL residual)
- Theme `desktop.php` compare-only chrome (CSS already reused)

**Now ASP.NET-live** (`confirmWrites=true` + native forms): type-2 cart qty/delete/check, cart add, OMS `set_item_status`, payroll approve, credit-limit set, PO approve/reject, forecast recompute.

Do not flip removal flags until each family has a human `MODULE_FUNCTION_PARITY_PASS` plus dual-sample evidence.
