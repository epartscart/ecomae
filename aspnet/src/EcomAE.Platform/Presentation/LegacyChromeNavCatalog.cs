using System.Linq;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-aligned chrome navigation for ASP.NET presentation shells.
/// Links point at live PHP routes for unported modules (hybrid preserve-functionality).
/// Source maps: epc_cp_nav_tree.php, erp_nav_areas.php, epc_bos_unified.php.
/// </summary>
public static class LegacyChromeNavCatalog
{
    public sealed record NavItem(string Label, string Href, string? Hint = null);

    /// <summary>CP top bar short labels → PHP Control Panel entry points.</summary>
    public static readonly IReadOnlyList<NavItem> ControlPanel =
    [
        new("CONTROL", "/CP/", "Command centre home"),
        new("Commerce", "/CP/shop/orders/orders", "OMS / orders (PHP console)"),
        new("Customers", "/CP/control/users", "Users & customers"),
        new("Documents", "/CP/control/shop/docpart", "Documents"),
        new("ERP", "/ERP/", "ERP shell"),
        new("Purchase", "/CP/control/shop/procurement", "Procurement"),
        new("Channels", "/CP/control/shop/channels", "Sales channels"),
        new("Logistics", "/CP/control/shop/logistics", "Logistics"),
        new("AI", "/CP/control/shop/ai", "AI tools"),
        new("Marketing", "/CP/control/shop/marketing", "Marketing"),
        new("Payments", "/CP/control/shop/payments", "Payments"),
        new("Integrations", "/CP/control/shop/integrations", "Integrations"),
        new("Portal", "/CP/control/portal", "Portal / tenants"),
        new("Platform", "/CP/control/portal/epc_super_cp_fleet_dashboard", "Platform fleet"),
        new("Operator", "/CP/control", "Operator tools")
    ];

    public static readonly IReadOnlyList<NavItem> ControlPanelQuickActions =
    [
        new("Orders (OMS)", "/cp/orders"),
        new("Orders PHP OMS", "/CP/shop/orders/orders"),
        new("Users list", "/cp/users-app"),
        new("Groups list", "/cp/groups-app"),
        new("Modules list", "/cp/modules-app"),
        new("Modules PHP", "/CP/modules/modules_manager"),
        new("Pages list", "/cp/pages-app"),
        new("Content manager PHP", "/CP/content/content_manager"),
        new("Menus list", "/cp/menus-app"),
        new("Menu manager PHP", "/CP/menu/menu_manager"),
        new("Tenants list", "/cp/tenants-app"),
        new("Tenant control PHP", "/CP/control/portal/epc_tenant_control_center"),
        new("Currencies list", "/cp/currencies-app"),
        new("Currency rates PHP", "/CP/shop/finance/nastrojka-kursov-valyut"),
        new("Storages list", "/cp/storages-app"),
        new("Storages PHP", "/CP/shop/logistics/storages"),
        new("Admin sessions list", "/cp/admin-sessions-app"),
        new("API clients list", "/cp/api-clients-app"),
        new("API clients PHP", "/CP/control/portal/epc_api_clients_manage"),
        new("Config items list", "/cp/config-items-app"),
        new("Site config PHP", "/CP/control/config_edit"),
        new("Users PHP", "/CP/control/users"),
        new("Groups PHP", "/CP/users/usergroups"),
        new("Multivendor", "/CP/control/shop/multivendor"),
        new("Crosses", "/CP/control/shop/docpart/crosses"),
        new("Procurement", "/CP/control/shop/procurement"),
        new("POS terminal", "/CP/control/shop/pos"),
        new("ERP finance", "/ERP/?epc_erp_shell=1&area=overview"),
        new("Stock", "/CP/control/shop/catalogue/stock"),
        new("Prices", "/CP/control/shop/prices"),
        new("Web tracker", "/CP/control/shop/web_tracker")
    ];

    /// <summary>ERP category bar → PHP ERP shell areas (epc_erp_nav_categories_config via PhpModuleCatalog).</summary>
    public static IReadOnlyList<NavItem> Erp =>
        new NavItem[] { new("Ecom BOS", "/ERP/", "ERP home") }
            .Concat(PhpModuleCatalog.ErpCategories.Select(c => new NavItem(c.Label, c.Href, c.Id)))
            .ToArray();

    public static readonly IReadOnlyList<NavItem> ErpQuickActions =
    [
        new("Sales orders list", "/erp/sales-orders-app"),
        new("Sales orders PHP", "/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders"),
        new("Purchase orders list", "/erp/purchase-orders-app"),
        new("Purchase orders PHP", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_orders"),
        new("Invoices list", "/erp/invoices-app"),
        new("Invoices PHP", "/ERP/?epc_erp_shell=1&area=sales&tab=invoices"),
        new("Accounts summary KPIs", "/erp/accounts-summary-app"),
        new("Cash & bank list", "/erp/cash-accounts-app"),
        new("Cash & bank PHP", "/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank"),
        new("Chart of accounts list", "/erp/coa-accounts-app"),
        new("Chart of accounts PHP", "/ERP/?epc_erp_shell=1&area=finance&tab=coa"),
        new("GL journals list", "/erp/gl-journals-app"),
        new("General ledger PHP", "/ERP/?epc_erp_shell=1&area=finance&tab=gl"),
        new("Warehouses list", "/erp/warehouses-app"),
        new("Inventory stock KPIs", "/erp/inventory-stock-app"),
        new("Inventory PHP", "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inventory"),
        new("Suppliers list", "/erp/suppliers-app"),
        new("Payables PHP", "/ERP/?epc_erp_shell=1&area=ap&tab=payables"),
        new("Purchases list", "/erp/purchases-app"),
        new("Purchases PHP", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchases"),
        new("Staff", "/ERP/?epc_erp_shell=1&area=people"),
        new("Income Statement", "/ERP/?epc_erp_shell=1&area=finance&tab=pl"),
        new("Balance Sheet", "/ERP/?epc_erp_shell=1&area=finance&tab=balance_sheet")
    ];

    /// <summary>BOS sidebar sections → PHP /BOS/ (and CP portal modules when applicable).</summary>
    public static readonly IReadOnlyList<NavItem> Bos =
    [
        new("Fleet Command", "/BOS/?m=command_center"),
        new("Tenant Operations", "/BOS/?m=fleet_cp"),
        new("Commerce", "/BOS/"),
        new("Catalogue", "/BOS/"),
        new("Logistics", "/BOS/"),
        new("Marketing", "/BOS/"),
        new("Professional", "/BOS/"),
        new("ERP Finance", "/ERP/?epc_erp_shell=1&area=overview"),
        new("Auto Parts", "/BOS/"),
        new("Tax & Advisory", "/BOS/"),
        new("Platform", "/CP/control/portal")
    ];

    public static readonly IReadOnlyList<NavItem> BosQuickActions =
    [
        new("Audit log list", "/bos/audit-log-app"),
        new("Audit log PHP", "/CP/control/portal/epc_boc_audit_log"),
        new("Fleet tenants list", "/bos/tenants-app"),
        new("Tenant control PHP", "/CP/control/portal/epc_tenant_control_center"),
        new("Fleet health KPIs", "/bos/fleet-health-app"),
        new("Fleet readiness KPIs", "/bos/fleet-readiness-app"),
        new("Platform health PHP", "/CP/control/portal/epc_platform_health_checkup"),
        new("Fleet command", "/bos/app"),
        new("Native BOS", "/BOS/")
    ];

    public static readonly IReadOnlyList<NavItem> Storefront =
    [
        new("Home", "https://epartscart.com/"),
        new("Search parts", "/storefront/search-app"),
        new("Search PHP", "https://epartscart.com/shop/part_search"),
        new("Catalog", "https://epartscart.com/"),
        new("Account summary", "/storefront/account-summary-app"),
        new("Account PHP", "https://epartscart.com/users/"),
        new("Cart", "/storefront/cart-app"),
        new("Cart PHP", "https://epartscart.com/shop/cart"),
        new("My orders", "/storefront/orders-app"),
        new("Orders PHP", "https://epartscart.com/shop/orders"),
        new("Garage", "/storefront/garage-app"),
        new("Garage PHP", "https://epartscart.com/shop/part_search"),
        new("Profile", "/storefront/profile-app"),
        new("Profile PHP", "https://epartscart.com/users/profile"),
        new("Checkout PHP", "https://epartscart.com/shop/checkout/how_get")
    ];
}
