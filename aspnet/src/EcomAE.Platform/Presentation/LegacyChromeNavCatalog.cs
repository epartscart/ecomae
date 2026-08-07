using System.Linq;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP-aligned chrome navigation for ASP.NET presentation shells.
/// Primary product hrefs are ASP.NET only. PHP stays under /php-reference/* (compare/archive).
/// Source maps (reference): epc_cp_nav_tree.php, erp_nav_areas.php, epc_bos_unified.php.
/// </summary>
public static class LegacyChromeNavCatalog
{
    public sealed record NavItem(string Label, string Href, string? Hint = null);

    /// <summary>CP top bar short labels → ASP.NET Control Panel entry points (PHP only via /php-reference/*).</summary>
    public static readonly IReadOnlyList<NavItem> ControlPanel =
    [
        new("CONTROL", "/cp", "Command centre home"),
        new("Commerce", "/cp/orders", "OMS / orders"),
        new("Customers", "/cp/users-app", "Users & customers"),
        new("Documents", "/cp/document-control-app", "Documents"),
        new("ERP", "/erp", "ERP shell"),
        new("Purchase", "/cp/purchase-requests-app", "Procurement"),
        new("Channels", "/cp/marketplace-channels-app", "Sales channels"),
        new("Logistics", "/cp/carriers-app", "Logistics"),
        new("AI", "/cp/ai-service-app", "AI tools"),
        new("Marketing", "/cp/marketing-broadcast-app", "Marketing"),
        new("Payments", "/cp/payment-gateways-app", "Payments"),
        new("Integrations", "/cp/integrations-app", "Integrations"),
        new("Portal", "/cp/tenants-app", "Portal / tenants"),
        new("Platform", "/bos", "Platform fleet"),
        new("Operator", "/cp", "Operator tools")
    ];

    public static readonly IReadOnlyList<NavItem> ControlPanelQuickActions =
    [
        new("Dashboard summary KPIs", "/cp/dashboard-summary-app"),
        new("Orders (OMS)", "/cp/orders"),
        new("Abandoned carts", "/cp/abandoned-carts-app"),
        new("Users list", "/cp/users-app"),
        new("Groups list", "/cp/groups-app"),
        new("Modules list", "/cp/modules-app"),
        new("Pages list", "/cp/pages-app"),
        new("Menus list", "/cp/menus-app"),
        new("Tenants list", "/cp/tenants-app"),
        new("Currencies list", "/cp/currencies-app"),
        new("Storages list", "/cp/storages-app"),
        new("Admin sessions list", "/cp/admin-sessions-app"),
        new("API clients list", "/cp/api-clients-app"),
        new("Power BI list", "/cp/power-bi-app"),
        new("Mobile apps summary", "/cp/mobile-apps-app"),
        new("Metabase list", "/cp/metabase-app"),
        new("NL reporting list", "/cp/nl-reporting-app"),
        new("Marketing broadcast list", "/cp/marketing-broadcast-app"),
        new("Demo tenants list", "/cp/demo-tenants-app"),
        new("Parts Agent chats", "/cp/parts-agent-chats-app"),
        new("POS overview", "/cp/pos-overview-app"),
        new("Tax toolkits", "/cp/tax-toolkits-app"),
        new("SMS / WhatsApp", "/cp/sms-whatsapp-app"),
        new("CRM board", "/cp/crm-board-app"),
        new("Document control", "/cp/document-control-app"),
        new("Delivery methods", "/cp/delivery-methods-app"),
        new("Crosses list", "/cp/crosses-app"),
        new("HR overview", "/cp/hr-overview-app"),
        new("Production overview", "/cp/production-overview-app"),
        new("Projects overview", "/cp/projects-overview-app"),
        new("Industry packs", "/cp/industry-packs-app"),
        new("Jewellery retail", "/cp/jewellery-retail-app"),
        new("Price lists", "/cp/price-lists-app"),
        new("Auto price", "/cp/auto-price-app"),
        new("UAE tax compliance", "/cp/uae-tax-compliance-app"),
        new("Budgets", "/cp/budgets-app"),
        new("Carriers", "/cp/carriers-app"),
        new("Payment gateways", "/cp/payment-gateways-app"),
        new("Workflows", "/cp/workflows-app"),
        new("Purchase requests", "/cp/purchase-requests-app"),
        new("Promotions", "/cp/promotions-app"),
        new("CRM opportunities", "/cp/crm-opportunities-app"),
        new("Integrations", "/cp/integrations-app"),
        new("Page builder", "/cp/page-builder-app"),
        new("Product catalogue", "/cp/product-catalogue-app"),
        new("Platform governance", "/cp/platform-governance-app"),
        new("E-invoice documents", "/cp/einvoice-documents-app"),
        new("Jewellery repairs", "/cp/jewellery-repairs-app"),
        new("CRM tickets", "/cp/crm-tickets-app"),
        new("Marketing growth", "/cp/marketing-growth-app"),
        new("SOC 2 compliance", "/cp/soc2-compliance-app"),
        new("Cost models", "/cp/cost-models-app"),
        new("Fin advanced", "/cp/fin-advanced-app"),
        new("Blockchain proofs", "/cp/blockchain-proofs-app"),
        new("Landed cost", "/cp/landed-cost-app"),
        new("Warehouse WMS", "/cp/warehouse-wms-app"),
        new("AI service", "/cp/ai-service-app"),
        new("Returns RMA", "/cp/returns-rma-app"),
        new("Isolation audit", "/cp/isolation-audit-app"),
        new("AML compliance", "/cp/aml-compliance-app"),
        new("Jewellery masters", "/cp/jewellery-masters-app"),
        new("Consolidations", "/cp/consolidations-app"),
        new("CRM activities", "/cp/crm-activities-app"),
        new("Auth MFA", "/cp/auth-mfa-app"),
        new("Electronic reporting", "/cp/electronic-reporting-app"),
        new("Collections dunning", "/cp/collections-dunning-app"),
        new("Marketplace channels", "/cp/marketplace-channels-app"),
        new("Demand intelligence", "/cp/demand-intelligence-app"),
        new("Credit limits", "/cp/credit-limits-app"),
        new("Insurance compliance", "/cp/insurance-compliance-app"),
        new("Audit trail", "/cp/audit-trail-app"),
        new("Doc expiry", "/cp/doc-expiry-app"),
        new("Tenant config", "/cp/tenant-config-app"),
        new("Jewellery stock verification", "/cp/jewellery-stock-verification-app"),
        new("Tax external reporting", "/cp/tax-external-reporting-app"),
        new("PO approvals", "/cp/po-approvals-app"),
        new("Finance close", "/cp/finance-close-app"),
        new("Jewellery fixing", "/cp/jewellery-fixing-app"),
        new("Web tracker", "/cp/web-tracker-app"),
        new("Quote requests", "/cp/quote-requests-app"),
        new("Platform communication", "/cp/platform-communication-app"),
        new("Info blocks", "/cp/info-blocks-app"),
        new("Free tools", "/cp/free-tools-app"),
        new("Config sandbox", "/cp/config-sandbox-app"),
        new("Marketplace apps", "/cp/marketplace-apps-app"),
        new("Notifications", "/cp/notifications-app"),
        new("Portal settings", "/cp/portal-settings-app"),
        new("Data migrations", "/cp/data-migrations-app"),
        new("Geo / regions", "/cp/geo-regions-app"),
        new("Product filters", "/cp/product-filters-app"),
        new("Search tabs", "/cp/search-tabs-app"),
        new("System requests", "/cp/system-requests-app"),
        new("Additional texts", "/cp/additional-texts-app"),
        new("Slider / banners", "/cp/slider-banners-app"),
        new("Structure dumps", "/cp/structure-dumps-app"),
        new("Communications test", "/cp/communications-test-app"),
        new("Languages", "/cp/languages-app"),
        new("Plugins manager", "/cp/plugins-manager-app"),
        new("Templates manager", "/cp/templates-manager-app"),
        new("Design tokens", "/cp/design-tokens-app"),
        new("Sitemap", "/cp/sitemap-app"),
        new("Failover status", "/cp/failover-status-app"),
        new("Ops guides", "/cp/ops-guides-app"),
        new("LifeOS system guide", "/cp/lifeos-guide-app"),
        new("File manager", "/cp/file-manager-app"),
        new("Server IP", "/cp/server-ip-app"),
        new("Config items list", "/cp/config-items-app"),
    ];

    /// <summary>ERP category bar → ASP.NET ERP browse routes (PHP catalog rewritten at bind time).</summary>
    public static IReadOnlyList<NavItem> Erp =>
        new NavItem[] { new("Ecom BOS", "/erp", "ERP home") }
            .Concat(PhpModuleCatalog.ErpCategories.Select(c =>
                new NavItem(c.Label, PhpSurfaceLinkMap.AspNetPrimaryHref(c.Href), c.Id)))
            .ToArray();

    public static readonly IReadOnlyList<NavItem> ErpQuickActions =
    [
        new("Sales orders list", "/erp/sales-orders-app"),
        new("Purchase orders list", "/erp/purchase-orders-app"),
        new("Invoices list", "/erp/invoices-app"),
        new("Dashboard summary KPIs", "/erp/dashboard-summary-app"),
        new("Accounts summary KPIs", "/erp/accounts-summary-app"),
        new("Cash & bank list", "/erp/cash-accounts-app"),
        new("Cash ledger entries", "/erp/cash-entries-app"),
        new("Chart of accounts list", "/erp/coa-accounts-app"),
        new("GL journals list", "/erp/gl-journals-app"),
        new("Warehouses list", "/erp/warehouses-app"),
        new("Inventory stock", "/erp/inventory-stock-app"),
        new("Stock movements", "/erp/stock-movements-app"),
        new("Report center", "/erp/report-center-app"),
        new("Aging", "/erp/aging-app"),
        new("Bank reconciliation", "/erp/bank-reconciliation-app"),
        new("Stock transfers", "/erp/stock-transfers-app"),
        new("Sales quotations", "/erp/sales-quotations-app"),
        new("Workspace favorites", "/erp/workspace-favorites-app"),
        new("Fixed assets", "/erp/fixed-assets-app"),
        new("Suppliers list", "/erp/suppliers-app"),
        new("Purchases list", "/erp/purchases-app"),
        new("Staff", "/cp/hr-overview-app"),
        new("Income Statement", "/erp/report-center-app"),
        new("Balance Sheet", "/erp/report-center-app"),
    ];

    /// <summary>BOS sidebar sections → ASP.NET BOS / CP / ERP routes only.</summary>
    public static readonly IReadOnlyList<NavItem> Bos =
    [
        new("Fleet Command", "/bos", "Command centre"),
        new("Tenant Operations", "/bos/tenants-app", "Fleet tenants"),
        new("Commerce", "/cp/orders", "OMS"),
        new("Catalogue", "/cp/product-catalogue-app", "Catalogue"),
        new("Logistics", "/cp/carriers-app", "Logistics"),
        new("Marketing", "/cp/marketing-broadcast-app", "Marketing"),
        new("Professional", "/bos", "Professional"),
        new("ERP Finance", "/erp", "ERP"),
        new("Auto Parts", "/storefront/search-app", "Parts search"),
        new("Tax & Advisory", "/cp/uae-tax-compliance-app", "Tax"),
        new("Platform", "/cp/tenants-app", "Portal"),
    ];

    public static readonly IReadOnlyList<NavItem> BosQuickActions =
    [
        new("Audit log list", "/bos/audit-log-app"),
        new("Fleet tenants list", "/bos/tenants-app"),
        new("Fleet health KPIs", "/bos/fleet-health-app"),
        new("Fleet readiness KPIs", "/bos/fleet-readiness-app"),
        new("Fleet summary KPIs", "/bos/fleet-summary-app"),
        new("Fleet command", "/bos/app"),
        new("Native BOS", "/bos"),
    ];

    public static readonly IReadOnlyList<NavItem> Storefront =
    [
        new("Home", "/"),
        new("Search parts", "/storefront/search-app"),
        new("Catalog", "/"),
        new("Account summary", "/storefront/account-summary-app"),
        new("Cart", "/storefront/cart-app"),
        new("Checkout", "/storefront/checkout-app"),
        new("My orders", "/storefront/orders-app"),
        new("Garage", "/storefront/garage-app"),
        new("Profile", "/storefront/profile-app"),
    ];
}
