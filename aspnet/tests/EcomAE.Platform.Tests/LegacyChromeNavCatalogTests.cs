using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyChromeNavCatalogTests
{
    [Fact]
    public void ControlPanelNavLinksPhpModules()
    {
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "Commerce" && item.Href.StartsWith("/CP/", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "ERP" && item.Href.Contains("/ERP", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Dashboard summary KPIs" && item.Href == "/cp/dashboard-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Orders (OMS)" && item.Href == "/cp/orders");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Users list" && item.Href == "/cp/users-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Groups list" && item.Href == "/cp/groups-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Modules list" && item.Href == "/cp/modules-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Modules PHP" && item.Href == "/CP/modules/modules_manager");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Pages list" && item.Href == "/cp/pages-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Content manager PHP" && item.Href == "/CP/content/content_manager");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Menus list" && item.Href == "/cp/menus-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Menu manager PHP" && item.Href == "/CP/menu/menu_manager");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Tenants list" && item.Href == "/cp/tenants-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Tenant control PHP" && item.Href == "/CP/control/portal/epc_tenant_control_center");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Currencies list" && item.Href == "/cp/currencies-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Currency rates PHP" && item.Href == "/CP/shop/finance/nastrojka-kursov-valyut");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Storages list" && item.Href == "/cp/storages-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Storages PHP" && item.Href == "/CP/shop/logistics/storages");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Admin sessions list" && item.Href == "/cp/admin-sessions-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "API clients list" && item.Href == "/cp/api-clients-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Power BI list" && item.Href == "/cp/power-bi-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Mobile apps summary" && item.Href == "/cp/mobile-apps-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Metabase list" && item.Href == "/cp/metabase-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "NL reporting list" && item.Href == "/cp/nl-reporting-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Marketing broadcast list" && item.Href == "/cp/marketing-broadcast-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Demo tenants list" && item.Href == "/cp/demo-tenants-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Parts Agent chats" && item.Href == "/cp/parts-agent-chats-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "POS overview" && item.Href == "/cp/pos-overview-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Tax toolkits" && item.Href == "/cp/tax-toolkits-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "SMS / WhatsApp" && item.Href == "/cp/sms-whatsapp-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "CRM board" && item.Href == "/cp/crm-board-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Document control" && item.Href == "/cp/document-control-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Delivery methods" && item.Href == "/cp/delivery-methods-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Crosses list" && item.Href == "/cp/crosses-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "HR overview" && item.Href == "/cp/hr-overview-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Production overview" && item.Href == "/cp/production-overview-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Projects overview" && item.Href == "/cp/projects-overview-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Industry packs" && item.Href == "/cp/industry-packs-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Jewellery retail" && item.Href == "/cp/jewellery-retail-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Price lists" && item.Href == "/cp/price-lists-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Auto price" && item.Href == "/cp/auto-price-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "UAE tax compliance" && item.Href == "/cp/uae-tax-compliance-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Budgets" && item.Href == "/cp/budgets-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Carriers" && item.Href == "/cp/carriers-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Payment gateways" && item.Href == "/cp/payment-gateways-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Workflows" && item.Href == "/cp/workflows-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Purchase requests" && item.Href == "/cp/purchase-requests-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Promotions" && item.Href == "/cp/promotions-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "CRM opportunities" && item.Href == "/cp/crm-opportunities-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Integrations" && item.Href == "/cp/integrations-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Page builder" && item.Href == "/cp/page-builder-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Product catalogue" && item.Href == "/cp/product-catalogue-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Platform governance" && item.Href == "/cp/platform-governance-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "E-invoice documents" && item.Href == "/cp/einvoice-documents-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Jewellery repairs" && item.Href == "/cp/jewellery-repairs-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "CRM tickets" && item.Href == "/cp/crm-tickets-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Marketing growth" && item.Href == "/cp/marketing-growth-app");

        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "SOC 2 compliance" && item.Href == "/cp/soc2-compliance-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Cost models" && item.Href == "/cp/cost-models-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Fin advanced" && item.Href == "/cp/fin-advanced-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Blockchain proofs" && item.Href == "/cp/blockchain-proofs-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Landed cost" && item.Href == "/cp/landed-cost-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Warehouse WMS" && item.Href == "/cp/warehouse-wms-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "AI service" && item.Href == "/cp/ai-service-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Returns RMA" && item.Href == "/cp/returns-rma-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Isolation audit" && item.Href == "/cp/isolation-audit-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "AML compliance" && item.Href == "/cp/aml-compliance-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Jewellery masters" && item.Href == "/cp/jewellery-masters-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Consolidations" && item.Href == "/cp/consolidations-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "CRM activities" && item.Href == "/cp/crm-activities-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Auth MFA" && item.Href == "/cp/auth-mfa-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Electronic reporting" && item.Href == "/cp/electronic-reporting-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Collections dunning" && item.Href == "/cp/collections-dunning-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "API clients PHP" && item.Href == "/CP/control/portal/epc_api_clients_manage");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Config items list" && item.Href == "/cp/config-items-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Site config PHP" && item.Href == "/CP/control/config_edit");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Groups PHP" && item.Href == "/CP/users/usergroups");
        Assert.All(LegacyChromeNavCatalog.ControlPanelQuickActions, item => Assert.False(string.IsNullOrWhiteSpace(item.Href)));
    }

    [Fact]
    public void ErpNavLinksPhpShellAreas()
    {
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Record to Report" && item.Href.Contains("area=finance", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Order to Cash" && item.Href.Contains("area=sales", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales orders list" && item.Href == "/erp/sales-orders-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales orders PHP" && item.Href.Contains("tab=sales_orders", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Purchase orders list" && item.Href == "/erp/purchase-orders-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Purchase orders PHP" && item.Href.Contains("tab=purchase_orders", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Invoices list" && item.Href == "/erp/invoices-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Invoices PHP" && item.Href.Contains("tab=invoices", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Dashboard summary KPIs" && item.Href == "/erp/dashboard-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Accounts summary KPIs" && item.Href == "/erp/accounts-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Cash & bank list" && item.Href == "/erp/cash-accounts-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Cash ledger entries" && item.Href == "/erp/cash-entries-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Cash & bank PHP" && item.Href.Contains("tab=cash_bank", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Chart of accounts list" && item.Href == "/erp/coa-accounts-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Chart of accounts PHP" && item.Href.Contains("tab=coa", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "GL journals list" && item.Href == "/erp/gl-journals-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "General ledger PHP" && item.Href.Contains("tab=gl", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Warehouses list" && item.Href == "/erp/warehouses-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Inventory stock KPIs" && item.Href == "/erp/inventory-stock-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Inventory PHP" && item.Href.Contains("tab=inventory", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Suppliers list" && item.Href == "/erp/suppliers-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Payables PHP" && item.Href.Contains("tab=payables", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Purchases list" && item.Href == "/erp/purchases-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Purchases PHP" && item.Href.Contains("tab=purchases", StringComparison.Ordinal));

        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Bank reconciliation" && item.Href == "/erp/bank-reconciliation-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Stock transfers" && item.Href == "/erp/stock-transfers-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales quotations" && item.Href == "/erp/sales-quotations-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Workspace favorites" && item.Href == "/erp/workspace-favorites-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Fixed assets" && item.Href == "/erp/fixed-assets-app");
    }

    [Fact]
    public void BosNavLinksPhpBosEntry()
    {
        Assert.Contains(LegacyChromeNavCatalog.Bos, item => item.Href.StartsWith("/BOS", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Audit log list" && item.Href == "/bos/audit-log-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Audit log PHP" && item.Href == "/CP/control/portal/epc_boc_audit_log");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet tenants list" && item.Href == "/bos/tenants-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Tenant control PHP" && item.Href == "/CP/control/portal/epc_tenant_control_center");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet health KPIs" && item.Href == "/bos/fleet-health-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet readiness KPIs" && item.Href == "/bos/fleet-readiness-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet summary KPIs" && item.Href == "/bos/fleet-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Platform health PHP" && item.Href == "/CP/control/portal/epc_platform_health_checkup");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Search parts" && item.Href == "/storefront/search-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Search PHP" && item.Href.Contains("part_search", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Cart" && item.Href == "/storefront/cart-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Cart PHP" && item.Href.Contains("/shop/cart", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "My orders" && item.Href == "/storefront/orders-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Orders PHP" && item.Href.Contains("/shop/orders", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Garage" && item.Href == "/storefront/garage-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Garage PHP" && item.Href.Contains("part_search", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Profile" && item.Href == "/storefront/profile-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Profile PHP" && item.Href.Contains("/users/profile", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Account summary" && item.Href == "/storefront/account-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Account PHP" && item.Href.Contains("/users/", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Checkout PHP" && item.Href.Contains("checkout", StringComparison.Ordinal));
    }
}
