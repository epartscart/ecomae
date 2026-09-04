using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Living PHP vs ASP.NET product-surface matrix.
/// Navigation twins are not PHP deletion. Writes stay PHP-authoritative until a human
/// <c>readyForPhpRemoval</c> gate plus dual-sample pass.
/// </summary>
public static class PhpVsAspNetRemovalMatrix
{
    public const bool ReadyForPhpRemoval = false;
    public const bool PhpSourceDeletionAllowed = false;
    public const bool CutoverAllowed = false;
    public const int AspNetInteractiveCompleteCount = 0;
    public const string AsOfUtc = "2026-09-04";

    public static IReadOnlyList<PhpVsAspNetMatrixRow> Rows { get; } =
    [
        // Storefront
        Row("sf-home", "storefront", "templates/*/desktop.php + content/general_pages", "/", "/storefront/app", "aspnet-digest", "php", "Homepage chrome is ASP.NET; checkout/pay writes stay PHP."),
        Row("sf-search", "storefront", "content/shop/docpart/ajax_part_search.php", "/shop/part_search", "/en/shop/part_search", "aspnet-digest", "php", "Offer list is ASP.NET; live supplier poll stays PHP. Cart add is /storefront/cart/add."),
        Row("sf-vin", "storefront", "content/laximo + content/general_pages/vin_zapros.php", "/en/katalog-laximo", "/storefront/vin-app", "aspnet-digest", "php", "VIN cache reader only. Live Laximo decode stays PHP."),
        Row("sf-cart", "storefront", "content/shop/order_process", "/shop/cart", "/en/shop/cart", "aspnet-digest", "aspnet", "Type-2 qty / delete / check-for-order write on ASP.NET. Type-1 and checkout stay PHP."),
        Row("sf-checkout", "storefront", "content/shop/order_process", "/shop/checkout", "/storefront/checkout-app", "aspnet-digest", "php", "Checkout shell exists; pay / place-order stay PHP."),
        Row("sf-orders", "storefront", "content/shop/order_process", "/shop/orders", "/storefront/orders-app", "aspnet-digest", "php", "Customer order list digest. Order/return messages are ASP.NET-live. Guest order writes stay PHP."),
        Row("sf-returns", "storefront", "content/shop/returns", "/shop/returns", "/storefront/returns-app", "aspnet-digest", "aspnet", "Full-qty create-return + return message are ASP.NET-live. Partial-qty split and photos stay PHP."),
        Row("sf-garage", "storefront", "content/shop/docpart garage", "/shop/garage", "/storefront/garage-app", "aspnet-digest", "php", "Garage list digest; add/edit PHP."),
        Row("sf-profile", "storefront", "content/users/profileform.php", "/users/profile", "/storefront/profile-app", "aspnet-digest", "aspnet", "users_profiles UPSERT is ASP.NET-live. Password / email / phone confirm stay PHP."),
        Row("sf-wishlist", "storefront", "modules/shop/bottom_panel/bottom_panel.php", "/shop/zakladki", "/storefront/wishlist-app", "aspnet-digest", "aspnet", "bookmarks cookie add/remove is ASP.NET-live."),
        Row("sf-compare", "storefront", "modules/shop/bottom_panel/bottom_panel.php", "/shop/sravneniya", "/storefront/compare-app", "aspnet-digest", "aspnet", "compare cookie add/remove is ASP.NET-live."),
        Row("sf-bulk-upload", "storefront", "content/shop/bulk_upload", "/shop/bulk-upload", "/storefront/bulk-upload-app", "aspnet-digest", "php", "Excel check/cross/add-selected may be ASP.NET on later branches; history INSERT + /process stay PHP."),
        Row("sf-balance", "storefront", "content/shop/finance/my_balance.php", "/shop/finance/my_balance", "/storefront/account-summary-app", "aspnet-routed", "php", "Customer balance / top-up writes stay PHP ajax_create_operation."),
        Row("sf-pay-order", "storefront", "content/shop/finance/pay_for_order.php", "/shop/finance/pay_for_order", "/storefront/checkout-app", "aspnet-routed", "php", "Gateway go_to_pay / notify stay PHP."),
        Row("sf-umapi", "storefront", "content/umapi_catalog.php", "/umapi_catalog", "/en/umapi_catalog", "aspnet-digest", "php", "UMAPI miss-fill stays PHP."),
        Row("sf-ucats", "storefront", "content/shop/ucats", "/shop/ucats", "/storefront/app", "aspnet-hub", "php", "UCATS product-detail twins are hub-only."),
        Row("sf-workshop-gms", "storefront", "content/shop/workshop/garage_manager_portal.php", "/shop/workshop", "/storefront/garage-manager-app", "aspnet-digest", "php", "GMS board is thin; portal writes stay PHP."),

        // CP shop families
        Row("cp-orders", "cp", "cp/content/shop/orders + order_process", "/CP/shop/orders/orders", "/cp/orders", "aspnet-digest", "aspnet", "OMS item/items status, update_item/update_items, message, courier, delete, comment, viewed, supplier fulfillment stage are ASP.NET-live. Payment notify and warehouse reprice stay PHP."),
        Row("cp-users", "cp", "cp/content/users", "/CP/users/user_manager", "/cp/users-app", "aspnet-digest", "aspnet", "Staff users.comment and users.unlocked (plus session delete on lock) are ASP.NET-live. Create / password stay PHP."),
        Row("cp-lang", "cp", "cp/content/lang", "/CP/lang", "/cp/languages-app", "aspnet-digest", "aspnet", "lang_text_strings flags, description, translation UPSERT, and unused-custom delete are ASP.NET-live. Restricted-mode, create-string keying, and used-found scan stay PHP."),
        Row("cp-catalogue", "cp", "cp/content/shop/catalogue", "/CP/shop/catalogue/products", "/cp/product-catalogue-app", "aspnet-digest", "aspnet", "shop_catalogue_products min_limit / min_limit_enable UPDATEs are ASP.NET-live. Tree / SKU / media stay PHP."),
        Row("cp-channels", "cp", "cp/content/shop/channels", "/CP/shop/channels", "/cp/marketplace-channels-app", "aspnet-digest", "aspnet", "toggle_channel UPDATE + sync-log INSERT are ASP.NET-live. Seed / inventory sync / order import stay PHP."),
        Row("cp-carriers", "cp", "cp/content/shop/logistics", "/CP/shop/logistics/carriers", "/cp/carriers-app", "aspnet-digest", "aspnet", "toggle_carrier UPDATE + sync-log INSERT are ASP.NET-live. Seed / create-shipment stay PHP."),
        Row("cp-workshop", "cp", "cp/content/shop/workshop", "/CP/shop/workshop", "/cp/workshop-app", "aspnet-digest", "aspnet", "assign / save_bay / save_tech are ASP.NET-live. Seed, create-job, status helpers, and appointments stay PHP."),
        Row("cp-synonyms", "cp", "cp/content/shop/manufacturers_synonyms", "/CP/shop/manufacturers_synonyms", "/cp/synonyms-app", "aspnet-digest", "aspnet", "Manufacturer and synonym add/save/delete are ASP.NET-live. List/get stay digest. PHP manufacturers_synonyms remains the compare twin."),
        Row("cp-prices-upload", "cp", "cp/content/shop/prices_upload", "/CP/shop/prices_upload", "/cp/prices-upload-app", "aspnet-digest", "php", "PyPrices cron/tasks stay PHP."),
        Row("cp-currencies", "cp", "content/shop/finance/nastrojka-kursov-valyut.php", "/CP/shop/finance/nastrojka-kursov-valyut", "/cp/currencies-app", "aspnet-digest", "php", "FX rate writes stay PHP."),
        Row("cp-payments", "cp", "cp/content/shop/payments", "/CP/shop/payments", "/cp/payment-gateways-app", "aspnet-digest", "php", "Gateway secrets/config writes stay PHP."),
        Row("cp-fulfillment", "cp", "content/shop/finance/epc_fulfillment_queue.php", "/CP/shop/finance/epc_fulfillment_queue", "/cp/fulfillment-queue-app", "aspnet-digest", "php", "Queue mutations stay PHP."),
        Row("cp-collections", "cp", "content/shop/finance/epc_collections_dunning.php", "/CP/shop/finance/epc_collections_dunning", "/cp/collections-dunning-app", "aspnet-digest", "php", "Dunning letters stay PHP."),
        Row("cp-credit-limits", "cp", "content/shop/finance/epc_credit_limit.php", "/CP/shop/finance/epc_credit_limit", "/cp/credit-limits-app", "aspnet-digest", "aspnet", "epc_credit_set_limit UPSERT is ASP.NET-live."),
        Row("cp-po-approvals", "cp", "content/shop/finance/epc_po_approval.php", "/CP/shop/finance/epc_po_approval", "/cp/po-approvals-app", "aspnet-digest", "aspnet", "epc_po_approve / epc_po_reject are ASP.NET-live."),
        Row("cp-warranty-rma", "cp", "content/shop/finance/epc_warranty_rma.php + ajax_return_action.php", "/CP/shop/finance/epc_warranty_rma", "/cp/returns-rma-app", "aspnet-digest", "aspnet", "set_return_status / decide_line / finalize_return are ASP.NET-live when statuses already exist. Status seeding stays PHP."),
        Row("cp-vin-requests", "cp", "cp/content/requests/ajax_set_users_vin_viewed.php", "/CP/requests", "/cp/system-requests-app", "aspnet-digest", "aspnet", "users_vin.viewed UPDATE is ASP.NET-live. Live Laximo decode stays PHP."),
        Row("cp-account-ops", "cp", "content/shop/finance/account_operations", "/CP/shop/finance/account_operations", "/cp/credit-limits-app", "aspnet-routed", "php", "Closest twin is credit-limits; account-op ajax stays PHP."),
        Row("cp-payment-systems", "cp", "content/shop/finance/payment_systems", "/CP/shop/finance/payment_systems", "/cp/payment-gateways-app", "aspnet-routed", "php", "Per-gateway go_to_pay stays PHP."),
        Row("cp-tax-toolkit", "cp", "content/shop/finance/epc_tax_toolkit.php", "/CP/shop/finance/epc_tax_toolkit", "/cp/tax-toolkits-app", "aspnet-routed", "php", "Super-CP gated toolkit; tenant tax UI is UAE compliance."),
        Row("cp-einvoice", "cp", "content/shop/finance/epc_einvoice.php", "/CP/shop/finance/epc_einvoice", "/cp/einvoice-documents-app", "aspnet-routed", "php", "E-invoice issue/submit stay PHP."),
        Row("cp-live-fx", "cp", "content/shop/finance/epc_currency_live_rates.php", "/CP/shop/finance/epc_currency_live_rates", "/cp/currencies-app", "aspnet-routed", "php", "Live FX pull stays PHP."),
        Row("cp-custom-ship", "cp", "content/shop/finance/epc_custom_shipping.php", "/CP/shop/finance/epc_custom_shipping", "/cp/carriers-app", "aspnet-routed", "php", "Custom shipping rules stay PHP."),
        Row("cp-finance-hub", "cp", "cp/content/shop/finance", "/CP/shop/finance", "/erp", "aspnet-hub", "php", "Module hub is intentional. Specific epc_* hrefs must map before this catch-all."),

        // ERP standalone finance pages closed in this wave
        Row("erp-order-pipeline", "erp", "content/shop/finance/epc_order_erp_pipeline.php", "/CP/shop/finance/epc_order_erp_pipeline", "/erp/order-pipeline-app", "aspnet-digest", "php", "Read-only epc_order_erp_log. Pipeline run stays PHP."),
        Row("erp-inventory-forecast", "erp", "content/shop/finance/epc_inventory_forecast.php", "/CP/shop/finance/epc_inventory_forecast", "/erp/inventory-forecast-app", "aspnet-digest", "aspnet", "epc_forecast_compute UPSERT is ASP.NET-live. Demand-history ingest stays PHP."),
        Row("erp-multi-entity", "erp", "content/shop/finance/epc_multi_entity.php", "/CP/shop/finance/epc_multi_entity", "/erp/multi-entity-app", "aspnet-digest", "php", "Read-only epc_entity_groups / members / IC txns. Consolidation write stays PHP."),
        Row("erp-multi-currency-gl", "erp", "content/shop/finance/epc_multi_currency_gl.php", "/CP/shop/finance/epc_multi_currency_gl", "/erp/multi-currency-gl-app", "aspnet-digest", "php", "Read-only epc_fx_rates + epc_gl_currency_entries. Revaluation stays PHP."),
        Row("erp-payroll", "erp", "content/shop/finance/epc_wps_payroll.php + erp_tabs_payroll.php", "/CP/shop/finance/epc_wps_payroll", "/erp/payroll-app", "aspnet-digest", "aspnet", "payroll_approve is ASP.NET-live. Generate/pay stay PHP dry-run."),
        Row("erp-subscriptions", "erp", "content/shop/finance/epc_subscription_billing.php", "/CP/shop/finance/epc_subscription_billing", "/erp/sales-orders-app?tab=subscriptions", "aspnet-routed", "php", "Subscription billing lands on sales-orders subscriptions tab."),
        Row("erp-insights", "erp", "content/shop/finance/epc_insights_suite.php", "/CP/shop/finance/epc_insights_suite", "/erp/dashboard-summary-app", "aspnet-routed", "php", "Insights suite routed to ERP dashboard digest."),
        Row("erp-sales-orders", "erp", "cp/content/shop/finance/erp/erp_tabs_sales_orders.php", "/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app", "aspnet-digest", "php", "SO list digest; create/post stay PHP ajax_erp."),
        Row("erp-purchase-orders", "erp", "erp_tabs_purchase_orders.php", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_orders", "/erp/purchase-orders-app", "aspnet-digest", "php", "PO list digest; writes PHP."),
        Row("erp-invoices", "erp", "erp_tabs_invoices.php", "/ERP/?epc_erp_shell=1&area=sales&tab=invoices", "/erp/invoices-app", "aspnet-digest", "php", "Invoice list digest; writes PHP."),
        Row("erp-gl", "erp", "erp_tabs_accounting.php", "/ERP/?epc_erp_shell=1&area=finance&tab=gl", "/erp/gl-journals-app", "aspnet-digest", "php", "GL list digest; posting stay PHP (some live write services exist behind dry-run gates)."),
        Row("erp-inventory", "erp", "erp_tabs_inventory.php", "/ERP/?epc_erp_shell=1&area=inventory&tab=inventory", "/erp/inventory-stock-app", "aspnet-digest", "php", "Stock digest; movements stay PHP."),
        Row("erp-tabs-residual", "erp", "cp/content/shop/finance/erp/erp_tabs_*.php (~160)", "/ERP/?epc_erp_shell=1", "/erp", "aspnet-hub", "php", "Bare ERP shell lands on /erp. ErpPhpTabRouteMap covers named tabs; residual shells use /erp/module-app."),

        // BOS
        Row("bos-shell", "bos", "bos/index.php + content/shop/finance/epc_bos_*.php", "/BOS/", "/bos/app", "aspnet-hub", "php", "BOS session model stays PHP. Fleet digests are read-only."),

        // Write families that block PHP removal
        Row("write-cp-ajax", "writes", "cp/content/**/ajax_*.php (~430 catalogued)", "CP module ajax", "dry-run /cp/ajax/*", "php-writes", "php", "CpModuleAjaxWriteCatalog: writes=0, phpAuthoritative=true."),
        Row("write-erp-ajax", "writes", "cp/content/shop/finance/erp/ajax_erp.php", "ajax_erp.php", "dry-run /erp/ajax/*", "php-writes", "php", "ErpAjaxWriteCatalog: writes=0. Live GL/SO helpers stay gated."),
        Row("write-storefront-cart", "writes", "content/shop/order_process/ajax_*.php", "cart/checkout ajax", "/storefront/cart/add", "php-writes", "php", "Type-2 add/qty/delete/check and signed-in checkout/create are ASP.NET-live. Guest checkout + payment stay PHP."),
        Row("write-payments", "writes", "content/shop/finance/payment_systems/*/go_to_pay.php", "payment notify", "/php-reference/content/shop/finance/payment_systems/", "php-writes", "php", "Gateway capture/notify cannot move without PCI + live dual-sample."),
        Row("write-laximo", "writes", "content/laximo/com_guayaquil", "Laximo VIN decode", "/storefront/vin-app", "php-writes", "php", "Guayaquil SDK is not ported. Cache-only on ASP.NET."),
        Row("write-pyprices", "writes", "cp/content/shop/prices_upload + pyprices", "price ingest cron", "/cp/prices-upload-app", "php-writes", "php", "Price file ingest + cron stay PHP/Python."),
        Row("chrome-templates", "chrome", "templates/*/*.php (6 files)", "theme desktop.php", "Php*DesktopChrome", "aspnet-digest", "none", "Theme CSS is reused; PHP desktop.php is compare-only."),
    ];

    public static IReadOnlyDictionary<string, object> BuildReport()
    {
        var rows = Rows.Select(r =>
        {
            var mapped = string.IsNullOrWhiteSpace(r.PhpHref) || !r.PhpHref.StartsWith('/')
                ? r.AspNetRoute
                : PhpSurfaceLinkMap.AspNetPrimaryHref(r.PhpHref);
            var mapsToExpected = string.Equals(mapped, r.AspNetRoute, StringComparison.OrdinalIgnoreCase)
                || (r.AspNetRoute.Contains('?', StringComparison.Ordinal)
                    && mapped.StartsWith(r.AspNetRoute.Split('?')[0], StringComparison.OrdinalIgnoreCase));
            return new Dictionary<string, object?>
            {
                ["id"] = r.Id,
                ["surface"] = r.Surface,
                ["phpSource"] = r.PhpSource,
                ["phpHref"] = r.PhpHref,
                ["aspNetRoute"] = r.AspNetRoute,
                ["mappedHref"] = mapped,
                ["mapsToExpected"] = mapsToExpected,
                ["status"] = r.Status,
                ["writesOwner"] = r.WritesOwner,
                ["note"] = r.Note
            };
        }).ToList();

        var byStatus = Rows.GroupBy(r => r.Status, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var bySurface = Rows.GroupBy(r => r.Surface, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var stillMissingDedicatedApp = Rows
            .Where(r => r.Status is "missing-app" or "aspnet-hub")
            .Select(r => r.Id)
            .ToArray();
        var phpWrites = Rows.Where(r => r.WritesOwner == "php").Select(r => r.Id).ToArray();

        return new Dictionary<string, object>
        {
            ["role"] = "php-vs-aspnet-removal-matrix",
            ["asOfUtc"] = AsOfUtc,
            ["readyForPhpRemoval"] = ReadyForPhpRemoval,
            ["phpSourceDeletionAllowed"] = PhpSourceDeletionAllowed,
            ["cutoverAllowed"] = CutoverAllowed,
            ["aspNetInteractiveCompleteCount"] = AspNetInteractiveCompleteCount,
            ["keepPhpProjectAvailable"] = true,
            ["phpFileInventory"] = new Dictionary<string, object>
            {
                ["content"] = 1437,
                ["cp"] = 638,
                ["templates"] = 6,
                ["bos"] = 2,
                ["productFacingApprox"] = 2083,
                ["note"] = "Exclude vendor/node_modules/pyprices venv and repo-root setup scripts. Laximo SDK dominates content/laximo."
            },
            ["counts"] = new Dictionary<string, object>
            {
                ["rows"] = Rows.Count,
                ["byStatus"] = byStatus,
                ["bySurface"] = bySurface,
                ["phpWriteRows"] = phpWrites.Length,
                ["hubOrMissing"] = stillMissingDedicatedApp.Length
            },
            ["stillBlockingPhpRemoval"] = new[]
            {
                "Remaining PHP writes: CP ajax ~430, ERP ajax_erp residual, storefront type-1/checkout/pay, Laximo decode, PyPrices ingest, UMAPI miss-fill.",
                "aspnet-complete interactive module count is 0 (no dual-sample deletion gate).",
                "Digests + href remaps are not a deletion gate.",
                "Human MODULE_FUNCTION_TEST_PASS + dual-sample evidence required per family."
            },
            ["closedThisWave"] = new[]
            {
                "Type-2 cart qty/delete/check + cart add → ASP.NET live writes",
                "OMS set_item_status → /cp/orders/set-item-status live",
                "Payroll approve → /erp/ajax/payroll-approve live",
                "Credit limit set → /cp/credit-limits/set live",
                "PO approve/reject → /cp/po-approvals/approve|reject live",
                "Inventory forecast recompute → /erp/inventory-forecast/recompute live",
                "shop/finance/epc_credit_limit → /cp/credit-limits-app",
                "shop/finance/epc_po_approval → /cp/po-approvals-app",
                "shop/finance/epc_warranty_rma → /cp/returns-rma-app",
                "shop/finance/epc_wps_payroll → /erp/payroll-app",
                "shop/finance/epc_subscription_billing → /erp/sales-orders-app?tab=subscriptions",
                "shop/finance/epc_order_erp_pipeline → /erp/order-pipeline-app (new digest)",
                "shop/finance/epc_inventory_forecast → /erp/inventory-forecast-app (new digest)",
                "shop/finance/epc_multi_entity → /erp/multi-entity-app (new digest)",
                "shop/finance/epc_multi_currency_gl → /erp/multi-currency-gl-app (new digest)",
                "/CP/shop/finance hub still maps to /erp (intentional)"
            },
            ["rows"] = rows,
            ["endpoint"] = "/migration/php-vs-aspnet-matrix",
            ["note"] = "Do not delete PHP. Reference keep ≠ removal. This matrix is the browse/digest gap board, not a cutover switch."
        };
    }

    private static PhpVsAspNetMatrixRow Row(
        string id,
        string surface,
        string phpSource,
        string phpHref,
        string aspNetRoute,
        string status,
        string writesOwner,
        string note)
        => new(id, surface, phpSource, phpHref, aspNetRoute, status, writesOwner, note);
}

public sealed record PhpVsAspNetMatrixRow(
    string Id,
    string Surface,
    string PhpSource,
    string PhpHref,
    string AspNetRoute,
    string Status,
    string WritesOwner,
    string Note);
