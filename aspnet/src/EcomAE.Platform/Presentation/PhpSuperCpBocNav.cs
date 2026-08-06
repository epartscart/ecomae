namespace EcomAE.Platform.Presentation;

/// <summary>
/// Port of the PHP BOC area registry (content/general_pages/epc_boc_kernel.php:
/// epc_boc_groups + epc_boc_areas) — the Super CP top mega-menu structure.
/// Every area keeps its PHP label/icon/hint; the href maps the PHP CP path into
/// the best current ASP.NET module via <see cref="PhpSurfaceLinkMap"/>.
/// </summary>
public static class PhpSuperCpBocNav
{
    public sealed record BocGroup(string Key, string Label, string Icon, string Blurb);

    public sealed record BocArea(string Key, string Label, string Group, string Icon, string PhpPath, string Hint)
    {
        public string Href => PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/" + PhpPath.TrimStart('/'));
    }

    public static readonly IReadOnlyList<BocGroup> Groups =
    [
        new("command", "Command", "fa-tachometer", "Fleet health & KPIs"),
        new("lifecycle", "Tenants", "fa-rocket", "Onboard, control, demos"),
        new("reliability", "Ops", "fa-heartbeat", "Health, governance, incidents"),
        new("supply", "Supply", "fa-cubes", "Vendors, warehouses, channels"),
        new("commerce", "Pricing", "fa-tags", "Pricing AI, API, POS"),
        new("shop", "Commerce", "fa-shopping-cart", "Orders, customers, payments — CP"),
        new("catalogue", "Catalogue", "fa-th-large", "Products, prices, SKU media"),
        new("logistics", "Logistics", "fa-truck", "Delivery, procurement, stock"),
        new("erp", "ERP", "fa-university", "Finance, GL, AR/AP, inventory"),
        new("growth", "Growth", "fa-bullhorn", "Marketing, CMS, mobile"),
        new("professional", "Pro", "fa-briefcase", "CRM, documents, parts agent"),
        new("finance", "Compliance", "fa-balance-scale", "Tax & compliance kits"),
        new("identity", "Integrations", "fa-plug", "Auth, email, connectors"),
        new("platform", "Platform", "fa-cog", "BI, AI, workflows, security"),
        new("knowledge", "Guides", "fa-book", "Operator & product docs"),
    ];

    public static readonly IReadOnlyList<BocArea> Areas =
    [
        // Command
        new("command_center", "Command Center", "command", "fa-tachometer", "control/portal/epc_boc_command_center", "Live fleet health"),
        new("fleet_cp", "CP fleet dashboard", "command", "fa-th", "control/portal/epc_super_cp_fleet_dashboard", "Cross-tenant CP KPIs"),
        new("fleet_erp", "ERP fleet dashboard", "command", "fa-university", "control/portal/epc_super_erp_fleet_dashboard", "Cross-tenant ERP KPIs"),
        new("insights_erp", "Insights hub", "command", "fa-lightbulb-o", "shop/finance/erp?epc_erp_shell=1&area=overview&tab=dashboard", "Financial / business / CP insights"),

        // Tenants
        new("tenant_hub", "Tenant hub / onboard", "lifecycle", "fa-sitemap", "shop/tenant_hub/tenant_hub", "Provision tenants"),
        new("tenant_control", "Tenant control", "lifecycle", "fa-sliders", "control/portal/epc_tenant_control_center", "Credentials & on/off"),
        new("tenant_features", "Feature matrix", "lifecycle", "fa-th", "control/portal/epc_tenant_features", "Per-tenant flags"),
        new("demo_tenants", "Demo tenants", "lifecycle", "fa-flask", "control/portal/epc_demo_tenants_manage", "Sandbox lifecycle"),
        new("industry_settings", "Industry / ERP packs", "lifecycle", "fa-cubes", "control/portal/industry_settings", "Module presets"),
        new("industry_consol", "Industry consolidation", "lifecycle", "fa-sitemap", "control/portal/industry_consolidation", "Industry roll-up"),
        new("license_trends", "License trends", "lifecycle", "fa-line-chart", "control/portal/epc_industry_license_trends", "License usage trends"),
        new("customer_board", "Customer board", "lifecycle", "fa-users", "control/portal/epc_super_cp_customer_board", "Cross-tenant search"),
        new("erp_only_guide", "ERP-only onboard", "lifecycle", "fa-rocket", "control/portal/epc_erp_only_onboard_guide", "ERP-only client guide"),
        new("tenant_config", "Tenant config", "lifecycle", "fa-cog", "control/portal/epc_tenant_config", "Tenant configuration"),
        new("design_tokens", "Design tokens", "lifecycle", "fa-paint-brush", "control/portal/epc_design_tokens", "Brand tokens"),

        // Ops / reliability
        new("platform_health", "Platform health", "reliability", "fa-heartbeat", "control/portal/epc_platform_health_checkup", "SSL/DB/nginx probes"),
        new("governance", "Governance", "reliability", "fa-gavel", "control/portal/epc_platform_governance", "Policies & rules"),
        new("audit_log", "Audit log", "reliability", "fa-history", "control/portal/epc_boc_audit_log", "Who did what"),
        new("failover", "Failover runbook", "reliability", "fa-life-ring", "control/portal/epc_platform_failover_guide", "Incident steps"),
        new("isolation_audit", "Isolation audit", "reliability", "fa-shield", "control/portal/epc_commerce_isolation_audit", "Data isolation"),
        new("readiness_score", "Readiness score", "reliability", "fa-trophy", "control/portal/epc_readiness_score", "Go-live readiness"),
        new("notifications", "Notifications", "reliability", "fa-bell", "control/portal/epc_notifications", "Platform alerts"),
        new("db_migrations", "DB migrations", "reliability", "fa-database", "control/portal/epc_db_migrations", "Schema migrations"),
        new("soc2_compliance", "SOC 2", "reliability", "fa-certificate", "control/portal/epc_soc2_compliance", "Compliance checklist"),

        // Supply fleet
        new("vendor_control", "Vendor & sourcing", "supply", "fa-truck", "control/portal/epc_boc_vendor_control", "Fleet suppliers"),
        new("warehouse_control", "Warehouse & inventory", "supply", "fa-cubes", "control/portal/epc_boc_warehouse_control", "Stock risk"),
        new("channel_control", "Channels & OMS", "supply", "fa-sitemap", "control/portal/epc_boc_channel_control", "Web/POS/API"),
        new("fulfillment_queue", "Fulfilment queue", "supply", "fa-truck", "shop/finance/epc_fulfillment_queue", "Ship queue"),

        // Pricing / commerce platform
        new("auto_price", "Auto Price AI", "commerce", "fa-line-chart", "control/portal/epc_auto_price_engine", "Multi-source compare"),
        new("price_configs", "Price configs", "commerce", "fa-percent", "control/portal/epc_super_cp_price_configs", "Markup rules"),
        new("api_clients", "API clients", "commerce", "fa-key", "control/portal/epc_api_clients_manage", "Keys & quotas"),
        new("pos_overview", "POS overview", "commerce", "fa-credit-card", "control/portal/epc_pos_tenant_manage", "POS per tenant"),
        new("promotions", "Promotions", "commerce", "fa-tags", "control/portal/epc_promotions_engine", "Promo engine"),
        new("marketplace", "Marketplace", "commerce", "fa-shopping-cart", "control/portal/epc_marketplace", "Marketplace hub"),

        // Tenant CP — Commerce (shop)
        new("cp_orders", "OMS · Orders", "shop", "fa-shopping-cart", "shop/orders/orders", "Order management"),
        new("cp_customers", "Customers", "shop", "fa-users", "shop/customer_mgmt/customer_mgmt", "Customer management"),
        new("cp_payments", "Payments", "shop", "fa-credit-card", "shop/payments/payments", "Payment gateways"),
        new("cp_returns", "Returns", "shop", "fa-undo", "shop/returns-manager", "Returns manager"),
        new("cp_quotes", "Quote requests", "shop", "fa-file-text", "shop/quote_requests", "RFQs"),
        new("cp_channels", "Channels", "shop", "fa-share-alt", "shop/channels/channels", "Sales channels"),
        new("cp_pos", "POS terminal", "shop", "fa-calculator", "shop/pos/terminal", "Point of sale"),
        new("cp_statistics", "Statistics", "shop", "fa-bar-chart", "shop/statistics/statistics", "Commerce stats"),
        new("cp_web_tracker", "Web tracker", "shop", "fa-line-chart", "shop/statistics/web_tracker", "Website traffic"),

        // Catalogue / prices
        new("cp_products", "Products", "catalogue", "fa-cube", "shop/catalogue/products", "Product catalogue"),
        new("cp_sku_media", "SKU photos & specs", "catalogue", "fa-picture-o", "shop/catalogue/sku_media", "Media & specs"),
        new("cp_prices", "Price lists", "catalogue", "fa-upload", "shop/prices", "Price list manager"),
        new("cp_multivendor", "Multivendor upload", "catalogue", "fa-handshake-o", "shop/prices/multivendor", "One-file vendor prices"),
        new("cp_prices_guide", "Price upload guide", "catalogue", "fa-book", "shop/prices/guide", "Upload guide"),
        new("cp_prices_edit", "Prices edit", "catalogue", "fa-tag", "shop/prices/prices_edit", "Inline price edit"),
        new("cp_prices_send", "Prices send", "catalogue", "fa-paper-plane", "shop/prices_send", "Send price lists"),
        new("cp_price_mgmt", "Pricing rules", "catalogue", "fa-percent", "shop/price-management", "Pricing rules"),
        new("cp_crosses", "Crosses", "catalogue", "fa-exchange", "shop/crosses", "OEM crosses"),
        new("cp_synonyms", "Brand synonyms", "catalogue", "fa-random", "shop/manufacturers_synonyms", "Brand synonyms"),
        new("cp_accessories", "Accessories", "catalogue", "fa-puzzle-piece", "shop/accessories", "Accessory links"),

        // Logistics
        new("cp_logistics", "Delivery & shipping", "logistics", "fa-truck", "shop/logistics/logistics", "Logistics hub"),
        new("cp_storages", "Warehouses", "logistics", "fa-building", "shop/logistics/storages", "Warehouse list"),
        new("cp_stock", "Stock", "logistics", "fa-cubes", "shop/logistics/stock", "Stock levels"),
        new("cp_procurement", "Procurement", "logistics", "fa-shopping-basket", "shop/procurement/procurement", "Purchasing hub"),
        new("cp_custom_ship", "Custom & shipping", "logistics", "fa-ship", "shop/logistics/custom_shipping", "Customs & freight"),

        // ERP deep links
        new("erp_home", "ERP home", "erp", "fa-th-large", "shop/finance/erp?epc_erp_shell=1&area=overview&tab=dashboard", "ERP command centre"),
        new("erp_gl", "General ledger", "erp", "fa-book", "shop/finance/erp?epc_erp_shell=1&area=gl", "GL journals"),
        new("erp_ap", "Accounts payable", "erp", "fa-credit-card", "shop/finance/erp?epc_erp_shell=1&area=ap", "AP"),
        new("erp_ar", "Accounts receivable", "erp", "fa-users", "shop/finance/erp?epc_erp_shell=1&area=ar", "AR"),
        new("erp_cash", "Cash & bank", "erp", "fa-university", "shop/finance/erp?epc_erp_shell=1&area=banking&tab=cash_bank", "Treasury"),
        new("erp_tax", "Tax & VAT", "erp", "fa-balance-scale", "shop/finance/erp?epc_erp_shell=1&area=tax", "Tax compliance"),
        new("erp_sales", "Sales & CRM", "erp", "fa-line-chart", "shop/finance/erp?epc_erp_shell=1&area=sales", "Sales orders"),
        new("erp_purchasing", "Purchasing", "erp", "fa-shopping-basket", "shop/finance/erp?epc_erp_shell=1&area=purchasing", "POs"),
        new("erp_inventory", "Inventory", "erp", "fa-cubes", "shop/finance/erp?epc_erp_shell=1&area=inventory_mgmt", "Stock & WMS"),
        new("erp_hr", "Human resources", "erp", "fa-id-card", "shop/finance/erp?epc_erp_shell=1&area=people", "HR"),
        new("erp_payroll", "Payroll", "erp", "fa-money", "shop/finance/erp?epc_erp_shell=1&area=payroll", "Payroll"),
        new("erp_pl", "Profit & loss", "erp", "fa-line-chart", "shop/finance/erp?epc_erp_shell=1&area=finance&tab=pl", "P&L"),
        new("erp_aging", "AR/AP aging", "erp", "fa-hourglass-half", "shop/finance/erp?epc_erp_shell=1&area=finance&tab=aging", "Aging"),
        new("erp_guide", "ERP guide", "erp", "fa-book", "shop/finance/erp?epc_erp_shell=1&area=overview&tab=guide", "Operator guide"),

        // Growth
        new("marketing", "Marketing broadcast", "growth", "fa-envelope", "control/portal/epc_marketing_broadcast", "Bulk email"),
        new("social", "Social media hub", "growth", "fa-share-alt", "control/portal/epc_social_media_hub", "Social + AI"),
        new("info_blocks", "Info blocks (CMS)", "growth", "fa-newspaper-o", "control/portal/epc_super_cp_info_blocks", "CMS blocks"),
        new("visual_editor", "Visual editor", "growth", "fa-magic", "control/portal/epc_visual_page_editor", "Page layouts"),
        new("mobile_apps", "Mobile apps", "growth", "fa-mobile", "control/portal/epc_mobile_apps", "PWA / apps"),
        new("free_tools", "Free Tools", "growth", "fa-wrench", "control/portal/epc_free_tools_admin", "Public tools"),
        new("cp_marketing", "Campaigns", "growth", "fa-bullhorn", "shop/marketing/marketing", "CP marketing"),
        new("cp_seo", "SEO", "growth", "fa-search", "shop/marketing/seo", "SEO tools"),

        // Professional
        new("cp_crm", "CRM", "professional", "fa-handshake-o", "shop/crm/crm", "CRM pipeline"),
        new("cp_documents", "Documents", "professional", "fa-folder-open", "shop/document_control/document_control", "Document control"),
        new("cp_parts_agent", "Parts agent", "professional", "fa-comments", "shop/parts_agent_chats", "AI parts chats"),
        new("dealer_portal", "Dealer portal", "professional", "fa-handshake-o", "control/portal/epc_dealer_portal", "Dealer access"),

        // Compliance
        new("tax_toolkit", "Tax Toolkit", "finance", "fa-balance-scale", "control/portal/epc_tax_toolkit_manage", "VAT/GST kits"),
        new("erp_finance", "ERP & finance shell", "finance", "fa-university", "shop/finance/erp?epc_erp_shell=1", "Full ERP"),
        new("uae_tax", "UAE tax compliance", "finance", "fa-gavel", "shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", "UAE VAT"),

        // Identity / integrations
        new("integrations", "Integrations hub", "identity", "fa-plug", "control/portal/epc_integrations_hub", "Connectors"),
        new("modern_auth", "Modern auth", "identity", "fa-sign-in", "control/portal/epc_cp_auth_settings", "OAuth / OTP / MFA"),
        new("communication", "Communication", "identity", "fa-comments", "control/portal/epc_super_cp_communication", "Email policy"),
        new("tenant_email", "Tenant email / SMTP", "identity", "fa-envelope", "control/portal/epc_tenant_email_settings", "Per-tenant SMTP"),
        new("mfa_management", "MFA / 2FA", "identity", "fa-lock", "control/portal/epc_mfa_management", "2FA admin"),
        new("sso_saml", "SSO / SAML", "identity", "fa-key", "control/portal/epc_sso_saml", "Enterprise SSO"),
        new("event_bus", "Event bus", "identity", "fa-bolt", "control/portal/epc_event_bus", "Events"),
        new("webhooks", "Webhooks", "identity", "fa-plug", "control/portal/epc_webhooks", "Outbound hooks"),
        new("rest_api_v2", "API v2", "identity", "fa-code", "control/portal/epc_rest_api_v2", "REST API"),

        // Platform tools
        new("bi_metrics", "BI metrics", "platform", "fa-bar-chart", "control/portal/epc_bi_metrics", "Fleet BI"),
        new("power_bi", "Power BI", "platform", "fa-bar-chart", "control/portal/epc_power_bi", "Power BI embed"),
        new("power_bi_guide", "Power BI guide", "platform", "fa-book", "control/portal/epc_power_bi_guide", "BI guide"),
        new("ai_copilot", "AI Copilot", "platform", "fa-commenting", "control/portal/epc_ai_copilot", "AI assistant"),
        new("ai_classify", "AI classify", "platform", "fa-magic", "control/portal/epc_ai_classification", "AI classification"),
        new("nl_reporting", "NL reports", "platform", "fa-file-text", "control/portal/epc_nl_reporting", "Natural-language reports"),
        new("workflow_builder", "Workflows", "platform", "fa-random", "control/portal/epc_workflow_builder", "Workflow builder"),
        new("import_orch", "Imports", "platform", "fa-upload", "control/portal/epc_import_orchestrator", "Import jobs"),
        new("doc_vault", "Doc vault", "platform", "fa-archive", "control/portal/epc_document_vault", "Document vault"),
        new("cp_roles", "CP roles", "platform", "fa-users", "control/portal/epc_cp_role_home", "Role homes"),
        new("config_sandbox", "Sandbox", "platform", "fa-flask", "control/portal/epc_config_sandbox", "Config sandbox"),
        new("industry_packs", "Industry packs", "platform", "fa-industry", "control/portal/epc_industry_packs", "Industry packs"),
        new("portal_settings", "Portal settings", "platform", "fa-cog", "control/portal/portal", "Portal config"),
        new("data_policy", "Data policy", "platform", "fa-lock", "control/portal/epc_tenant_data_policy", "Data policy"),
        new("config_edit", "Site config", "platform", "fa-wrench", "control/config_edit", "DP config editor"),
        new("sms_turning", "SMS settings", "platform", "fa-mobile", "control/sms_turning", "SMS gateway"),

        // Knowledge
        new("operator_guide", "Operator guide", "knowledge", "fa-book", "control/portal/epc_super_cp_operator_guide", "Who uses what"),
        new("cp_brochure", "Full CP brochure", "knowledge", "fa-book", "control/cp_brochure", "Every CP function"),
        new("product_brochure", "Product brochure", "knowledge", "fa-file-text-o", "control/portal/epc_boc_product_brochure", "Marketing brochure"),
        new("api_docs", "API docs", "knowledge", "fa-file-code-o", "control/portal/epc_api_documentation_guide", "API guide"),
        new("cp_guideline", "CP guideline", "knowledge", "fa-list-alt", "control/cp-guideline", "CP UX guideline"),
        new("auto_price_guide", "Auto Price guide", "knowledge", "fa-book", "control/portal/epc_auto_price_guide", "Pricing AI guide"),
        new("custom_ship_guide", "Custom & shipping guide", "knowledge", "fa-ship", "control/portal/epc_custom_shipping_guide", "Customs guide"),
        new("workshop_guide", "Autoworkshop guide", "knowledge", "fa-wrench", "control/portal/epc_autoworkshop_guide", "Workshop vertical"),
    ];

    public static IEnumerable<(BocGroup Group, IReadOnlyList<BocArea> Areas)> Nav()
    {
        foreach (var group in Groups)
        {
            var areas = Areas.Where(a => a.Group == group.Key).ToList();
            if (areas.Count > 0)
            {
                yield return (group, areas);
            }
        }
    }
}
