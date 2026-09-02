namespace EcomAE.Platform.Presentation;

/// <summary>
/// Rewrites PHP product hrefs to ASP.NET browse routes.
/// PHP stays available only under /php-reference/* (never as a primary click target).
/// </summary>
public static class PhpSurfaceLinkMap
{
    /// <summary>Longest-first CP path fragments (under /CP/) → ASP.NET apps.</summary>
    private static readonly (string Marker, string AspNet)[] CpPathMap =
    [
        ("shop/logistics/stock", "/erp/inventory-stock-app"),
        ("shop/logistics/sposoby-polucheniya", "/cp/delivery-methods-app"),
        ("shop/logistics/custom_shipping", "/cp/carriers-app"),
        ("shop/logistics/carriers", "/cp/carriers-app"),
        ("shop/logistics/storages", "/cp/storages-app"),
        ("shop/logistics/offices", "/cp/offices-app"),
        ("shop/logistics/logistics", "/cp/delivery-methods-app"),
        ("shop/logistics", "/cp/delivery-methods-app"),
        ("shop/marketing/seo", "/cp/seo-app"),
        ("shop/marketing/marketing", "/cp/marketing-growth-app"),
        ("shop/marketing", "/cp/marketing-growth-app"),
        ("shop/parts_agent/parts_agent_chats", "/cp/parts-agent-chats-app"),
        ("shop/parts_agent_chats", "/cp/parts-agent-chats-app"),
        ("shop/parts_agent", "/cp/parts-agent-chats-app"),
        ("shop/prices/multivendor", "/cp/prices-upload-app"),
        ("shop/prices_upload", "/cp/prices-upload-app"),
        ("shop/prices_edit", "/cp/prices-edit-app"),
        ("shop/prices_send", "/cp/prices-send-app"),
        ("prices_upload", "/cp/prices-upload-app"),
        ("prices_edit", "/cp/prices-edit-app"),
        ("prices_send", "/cp/prices-send-app"),
        ("shop/order_process", "/cp/orders"),
        ("shop/orders/sao_states_statuses_link", "/cp/sao-app"),
        ("shop/orders/items", "/cp/orders"),
        ("shop/orders/statuses", "/cp/orders"),
        ("shop/orders/orders", "/cp/orders"),
        ("shop/orders/carts", "/cp/abandoned-carts-app"),
        // Hub before ops|guide catch-all so oms-guide / whatsapp-guide stay on OMS.
        ("shop/orders", "/cp/orders"),
        ("shop/onlajn-kassy", "/cp/kkt-app"),
        ("shop/perenos-dannyx", "/cp/data-transfer-app"),
        ("shop/cash", "/erp/cash-accounts-app"),
        // Longer catalogue / statistics markers BEFORE short hubs (Contains match).
        ("control/shop/catalogue/stock", "/erp/inventory-stock-app"),
        ("shop/catalogue/catalogue_editor", "/cp/product-catalogue-app"),
        ("shop/catalogue/sku_media", "/cp/product-catalogue-app"),
        ("shop/catalogue/products", "/cp/product-catalogue-app"),
        ("shop/catalogue", "/cp/product-catalogue-app"),
        ("shop/payments", "/cp/payment-gateways-app"),
        ("shop/document_control", "/cp/document-control-app"),
        ("shop/customer_mgmt", "/cp/users-app"),
        ("shop/quote_requests", "/cp/quote-requests-app"),
        ("shop/statistics/web_tracker", "/cp/web-tracker-app"),
        ("shop/statistics/statistics", "/cp/statistics-app"),
        ("shop/statistics", "/cp/statistics-app"),
        ("shop/accessories", "/cp/accessories-app"),
        ("shop/manufacturers_synonyms", "/cp/synonyms-app"),
        ("shop/crosses", "/cp/crosses-app"),
        ("shop/crm", "/cp/crm-board-app"),
        ("shop/pos", "/cp/pos-overview-app"),
        ("shop/procurement", "/cp/purchase-requests-app"),
        ("shop/eparts-cata", "/cp/product-catalogue-app"),
        ("shop/eparts-mod", "/cp/product-catalogue-app"),

        ("order_process", "/cp/orders"),
        ("shop/workshop", "/cp/workshop-app"),
        ("shop/sao", "/cp/sao-app"),
        ("sao_states_statuses_link", "/cp/sao-app"),
        ("shop/print_docs", "/cp/print-docs-app"),
        ("shop/data_transfer", "/cp/data-transfer-app"),
        ("shop/bulk_upload", "/cp/bulk-upload-app"),
        ("shop/kkt", "/cp/kkt-app"),
        ("onlajn-kassy", "/cp/kkt-app"),
        ("perenos-dannyx", "/cp/data-transfer-app"),
        ("shop/search_tabs", "/cp/search-tabs-app"),
        ("shop/geo", "/cp/geo-regions-app"),
        ("shop/filter", "/cp/product-filters-app"),
        ("shop/demand_countries", "/cp/demand-intelligence-app"),
        ("shop/pricing", "/cp/price-lists-app"),
        ("shop/returns", "/cp/returns-rma-app"),
        ("shop/channels", "/cp/marketplace-channels-app"),
        ("filemanager", "/cp/file-manager-app"),
        ("file_manager", "/cp/file-manager-app"),
        ("plugins_control", "/cp/plugins-manager-app"),
        ("plugins/plugins_manager", "/cp/plugins-manager-app"),
        ("plugins/", "/cp/plugins-manager-app"),
        ("templates_control", "/cp/templates-manager-app"),
        ("templates/templates_manager", "/cp/templates-manager-app"),
        ("templates/", "/cp/templates-manager-app"),
        ("packs_control", "/cp/industry-packs-app"),
        ("packs/packs_manager", "/cp/industry-packs-app"),
        ("packs/setup", "/cp/industry-packs-app"),
        ("packs/", "/cp/industry-packs-app"),
        ("lang/page_lang", "/cp/languages-app"),
        ("control/lang", "/cp/languages-app"),
        ("content/dopolnitelnye-teksty", "/cp/additional-texts-app"),
        ("dopolnitelnye-teksty", "/cp/additional-texts-app"),
        ("content/structure_dumps", "/cp/structure-dumps-app"),
        ("structure_dumps", "/cp/structure-dumps-app"),
        ("content/slider", "/cp/slider-banners-app"),
        ("content/sitemap", "/cp/sitemap-app"),
        ("content/content_tree", "/cp/pages-app"),
        ("content_tree", "/cp/pages-app"),
        ("content/content_manager", "/cp/pages-app"),
        ("modules_control", "/cp/modules-app"),
        ("modules/modules_manager", "/cp/modules-app"),
        ("control/communications", "/cp/communications-test-app"),
        ("control/sms-operatory", "/cp/sms-whatsapp-app"),
        ("sms-operatory", "/cp/sms-whatsapp-app"),
        ("sms_turning", "/cp/sms-whatsapp-app"),
        ("system/debug", "/cp/debug-console-app"),
        // Tenant SMTP is independent of Super CP portal fleet inventory.
        ("epc_tenant_email_settings", "/cp/tenant-email-app"),
        ("users/customer_approvals", "/cp/users-app"),
        ("customer_approvals", "/cp/users-app"),
        ("users/polya-registracii", "/cp/users-app"),
        ("polya-registracii", "/cp/users-app"),
        ("users/registracionnye-varianty", "/cp/users-app"),
        ("registracionnye-varianty", "/cp/users-app"),
        ("users/usergroups", "/cp/groups-app"),
        ("users/user_manager", "/cp/users-app"),
        // After usergroups — "users/user" would otherwise steal usergroups.
        ("users/user", "/cp/users-app"),
        ("users/approvals", "/cp/users-app"),
        ("control/version_control", "/cp/ops-guides-app"),
        ("shop/taby-poiska", "/cp/search-tabs-app"),
        ("taby-poiska", "/cp/search-tabs-app"),
        ("control/shop/docpart/crosses", "/cp/crosses-app"),
        ("control/shop/procurement", "/cp/purchase-requests-app"),
        ("control/shop/multivendor", "/cp/prices-upload-app"),
        ("multivendor", "/cp/prices-upload-app"),
        // BocNav / brochure holdouts that previously collapsed to bare /cp.
        ("epc_webhooks", "/cp/integrations-app"),
        ("epc_rest_api_v2", "/cp/api-clients-app"),
        ("epc_dealer_portal", "/cp/tenants-app"),
        ("epc_industry_license_trends", "/cp/industry-packs-app"),
        ("epc_cp_role_home", "/cp/groups-app"),
        ("epc_tenant_data_policy", "/cp/platform-governance-app"),
        ("epc_boc_product_brochure", "/brochure/cp"),
        ("epc_isolation_anomaly", "/cp/isolation-audit-app"),
        ("usefull/ip", "/cp/server-ip-app"),
        ("content/usefull/ip", "/cp/server-ip-app"),
        ("control/shop/prices", "/cp/price-lists-app"),
        ("control/shop/pos", "/cp/pos-overview-app"),
        ("control/portal/epc_super_cp_fleet_dashboard", "/bos/fleet-health-app"),
        ("control/portal/epc_super_erp_fleet_dashboard", "/erp/fleet-app"),
        ("control/portal/epc_super_cp_info_blocks", "/cp/info-blocks-app"),
        ("control/portal/epc_super_cp_communication", "/cp/platform-communication-app"),
        ("control/portal/epc_tenant_control_center", "/cp/tenants-app"),
        ("control/portal/epc_industry_packs", "/cp/industry-packs-app"),
        ("control/portal/epc_notifications", "/cp/notifications-app"),
        ("control/portal/epc_workflow_builder", "/cp/workflows-app"),
        ("shop/tenant_hub/tenant_hub", "/cp/tenants-app"),
        ("control/portal/epc_platform_health_checkup", "/cp/failover-status-app"),
        ("control/portal/epc_boc_audit_log", "/bos/audit-log-app"),
        ("control/portal/epc_boc_channel_control", "/cp/marketplace-channels-app"),
        ("control/portal/epc_boc_warehouse_control", "/cp/warehouse-wms-app"),
        ("control/portal/epc_boc_vendor_control", "/erp/suppliers-app"),
        ("control/portal/epc_boc_command_center", "/cp/control"),
        ("control/portal/epc_super_cp_price_configs", "/cp/price-lists-app"),
        ("control/portal/epc_tenant_config", "/cp/tenant-config-app"),
        ("control/portal/epc_design_tokens", "/cp/design-tokens-app"),
        ("control/portal/epc_config_sandbox", "/cp/config-sandbox-app"),
        ("control/portal/epc_db_migrations", "/cp/data-migrations-app"),
        ("control/portal/epc_import_orchestrator", "/cp/data-migrations-app"),
        ("control/portal/epc_mfa_management", "/cp/auth-mfa-app"),
        ("control/portal/epc_marketplace", "/cp/marketplace-apps-app"),
        ("control/portal/epc_ai_copilot", "/cp/ai-service-app"),
        ("control/portal/epc_ai_classification", "/cp/ai-service-app"),
        ("control/portal/epc_document_vault", "/cp/document-control-app"),
        ("control/portal/epc_bi_metrics", "/cp/metabase-app"),
        ("control/portal/epc_readiness_score", "/bos/fleet-readiness-app"),
        ("control/portal/industry_consolidation", "/cp/industry-packs-app"),
        ("control/cp_brochure", "/brochure/cp"),
        ("control/portal/epc_api_clients_manage", "/cp/api-clients-app"),
        ("control/portal/epc_power_bi", "/cp/power-bi-app"),
        ("control/portal/epc_mobile_apps", "/cp/mobile-apps-app"),
        ("control/portal/epc_nl_reporting", "/cp/nl-reporting-app"),
        ("control/portal/epc_marketing_broadcast", "/cp/marketing-broadcast-app"),
        ("control/portal/epc_demo_tenants_manage", "/cp/demo-tenants-app"),
        ("control/portal/epc_tax_toolkit_manage", "/cp/tax-toolkits-app"),
        ("control/portal/epc_auto_price_engine", "/cp/auto-price-app"),
        ("control/portal/epc_promotions_engine", "/cp/promotions-app"),
        ("control/portal/epc_integrations_hub", "/cp/integrations-app"),
        ("control/portal/epc_visual_page_editor", "/cp/page-builder-app"),
        ("control/portal/epc_platform_governance", "/cp/platform-governance-app"),
        ("control/portal/epc_soc2_compliance", "/cp/soc2-compliance-app"),
        ("control/portal/epc_commerce_isolation_audit", "/cp/isolation-audit-app"),
        ("control/portal/epc_cp_auth_settings", "/cp/auth-mfa-app"),
        ("control/portal/epc_web_tracker", "/cp/web-tracker-app"),
        ("control/portal/industry_settings", "/cp/industry-packs-app"),
        ("control/portal/epc_free_tools", "/cp/free-tools-app"),
        ("control/portal/epc_pos_tenant", "/cp/pos-overview-app"),
        ("control/portal/tenant_control", "/cp/tenants-app"),
        ("control/portal/portal", "/cp/portal-settings-app"),
        ("shop/document_control/document_control", "/cp/document-control-app"),
        ("shop/procurement/procurement", "/cp/purchase-requests-app"),
        ("shop/procurement", "/cp/purchase-requests-app"),
        ("shop/price-management", "/cp/price-lists-app"),
        ("shop/finance/nastrojka-kursov-valyut", "/cp/currencies-app"),
        ("shop/finance/epc_collections_dunning", "/cp/collections-dunning-app"),
        ("shop/finance/epc_fulfillment_queue", "/cp/fulfillment-queue-app"),
        ("shop/finance/erp/uae-tax-compliance", "/cp/uae-tax-compliance-app"),
        ("uae-tax-compliance", "/cp/uae-tax-compliance-app"),
        // Catch-all after specific shop/finance/* CP modules (prefix Contains match).
        ("shop/finance", "/erp"),
        ("shop/catalogue/catalogue_editor", "/cp/product-catalogue-app"),
        ("shop/catalogue/sku_media", "/cp/product-catalogue-app"),
        ("shop/catalogue/products", "/cp/product-catalogue-app"),
        ("products/catalogue", "/cp/product-catalogue-app"),
        ("shop/customer_mgmt", "/cp/users-app"),
        ("customer_mgmt", "/cp/users-app"),
        ("shop/quote_requests", "/cp/quote-requests-app"),
        ("shop/quote-requests", "/cp/quote-requests-app"),
        ("quote_requests", "/cp/quote-requests-app"),
        ("shop/statistics/web_tracker", "/cp/web-tracker-app"),
        ("web_tracker", "/cp/web-tracker-app"),
        ("shop/statistics/statistics", "/cp/statistics-app"),
        ("shop/statistics", "/cp/statistics-app"),
        ("statistika", "/cp/statistics-app"),
        ("shop/accessories", "/cp/accessories-app"),
        ("shop/manufacturers_synonyms", "/cp/synonyms-app"),
        ("manufacturers_synonyms", "/cp/synonyms-app"),
        ("seo_main", "/cp/seo-app"),
        ("epc_social_media_hub", "/cp/social-hub-app"),
        ("epc_tenant_features", "/cp/tenant-features-app"),
        ("epc_super_cp_customer_board", "/cp/customer-board-app"),
        ("epc_fulfillment_queue", "/cp/fulfillment-queue-app"),
        ("epc_sso_saml", "/cp/sso-saml-app"),
        ("epc_event_bus", "/cp/event-bus-app"),
        ("shop/payments/payments", "/cp/payment-gateways-app"),
        ("shop/returns-manager", "/cp/returns-rma-app"),
        ("shop/crm/crm_main", "/cp/crm-board-app"),
        ("shop/crm", "/cp/crm-board-app"),
        ("shop/pos", "/cp/pos-overview-app"),
        ("shop/crosses", "/cp/crosses-app"),
        ("shop/prices", "/cp/price-lists-app"),
        ("modules/modules_manager", "/cp/modules-app"),
        ("content/content_manager", "/cp/pages-app"),
        ("menu/menu_manager", "/cp/menus-app"),
        ("general_pages/epc_metabase_embed", "/cp/metabase-app"),
        ("general_pages/epc_ai_service", "/cp/ai-service-app"),
        ("control/sms_turning", "/cp/sms-whatsapp-app"),
        ("control/config_edit", "/cp/config-items-app"),
        // After config_edit so /CP/control/config_edit is not swallowed.
        ("control/config", "/cp/config-items-app"),
        ("requests", "/cp/system-requests-app"),
        ("control/users", "/cp/users-app"),
        ("users/usergroups", "/cp/groups-app"),
        ("channels_main", "/cp/marketplace-channels-app"),
        ("epc_integrations", "/cp/integrations-app"),
        ("epc_api_clients", "/cp/api-clients-app"),
        ("epc_demo_tenants", "/cp/demo-tenants-app"),
        ("epc_platform_governance", "/cp/platform-governance-app"),
        ("epc_free_tools", "/cp/free-tools-app"),
        ("epc_tenant_control", "/cp/tenants-app"),
        ("epc_tenant_config", "/cp/tenant-config-app"),
        ("epc_design_tokens", "/cp/design-tokens-app"),
        ("epc_config_sandbox", "/cp/config-sandbox-app"),
        ("epc_db_migrations", "/cp/data-migrations-app"),
        ("epc_mfa_management", "/cp/auth-mfa-app"),
        ("epc_marketplace", "/cp/marketplace-apps-app"),
        ("epc_ai_copilot", "/cp/ai-service-app"),
        ("epc_ai_classification", "/cp/ai-service-app"),
        ("epc_document_vault", "/cp/document-control-app"),
        ("epc_bi_metrics", "/cp/metabase-app"),
        ("epc_readiness_score", "/bos/fleet-readiness-app"),
        ("epc_boc_command_center", "/cp/control"),
        ("industry_consolidation", "/cp/industry-packs-app"),
        ("super_cp_fleet", "/bos/fleet-health-app"),
        ("super_erp_fleet", "/erp/fleet-app"),
        ("fleet_dashboard", "/bos/fleet-health-app"),
        ("industry_packs", "/cp/industry-packs-app"),
        ("industry_settings", "/cp/industry-packs-app"),
        ("tenant_hub", "/cp/tenants-app"),
        ("sposoby-polucheniya", "/cp/delivery-methods-app"),
        ("tenant_control", "/cp/tenants-app"),
        ("workflows", "/cp/workflows-app"),
        ("notifications", "/cp/notifications-app"),
        ("carriers", "/cp/carriers-app"),
        ("payments", "/cp/payment-gateways-app"),
        ("channels", "/cp/marketplace-channels-app"),
        ("failover", "/cp/failover-status-app"),
    ];

    public static string AspNetPrimaryHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            if (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase))
            {
                value = string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
                if (!string.IsNullOrEmpty(absolute.Query))
                {
                    value += absolute.Query;
                }
            }
            else if (absolute.Host.Equals("www.ecomae.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.Equals("ecomae.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".ecomae.com", StringComparison.OrdinalIgnoreCase))
            {
                // Industry showcase /CP /ERP /BOS on *.ecomae.com → same-host ASP.NET shells.
                // Product Super BOS is bare /bos (and /BOS) — never the marketing knowledge article.
                var path = absolute.AbsolutePath ?? "/";
                var absHash = string.IsNullOrEmpty(absolute.Fragment) ? "" : absolute.Fragment;
                var absQuery = string.IsNullOrEmpty(absolute.Query) ? "" : absolute.Query;
                if (IsUpperPhpShell(path, "CP") || path.Equals("/cp", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase))
                {
                    // Deep /CP/... modules must survive — never collapse every ecomae CP URL to host+/cp.
                    var mappedCp = MapCpPhpPath(path + absQuery);
                    if (mappedCp.Equals("/cp", StringComparison.OrdinalIgnoreCase)
                        || mappedCp.Equals("/cp/", StringComparison.OrdinalIgnoreCase))
                    {
                        return absolute.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/cp";
                    }

                    return mappedCp + absHash;
                }

                if (IsUpperPhpShell(path, "ERP") || path.Equals("/erp", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase))
                {
                    // Deep /ERP/?tab=… must survive — never collapse every ecomae ERP URL to host+/erp.
                    var mappedErp = MapErpPhpPath(path + absQuery);
                    if (mappedErp.Equals("/erp", StringComparison.OrdinalIgnoreCase)
                        || mappedErp.Equals("/erp/", StringComparison.OrdinalIgnoreCase))
                    {
                        return absolute.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/erp";
                    }

                    return mappedErp + absHash;
                }

                if (IsUpperPhpShell(path, "BOS")
                    || path.Equals("/bos", StringComparison.OrdinalIgnoreCase)
                    || path.Equals("/bos/", StringComparison.OrdinalIgnoreCase))
                {
                    return MapBosPhpPath(path + absQuery) + absHash;
                }

                // Product BOS digests (/bos/tenants-app, …) stay on-host; do not rewrite to marketing.
                if (path.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase))
                {
                    return path.TrimEnd('/') + absQuery + absHash;
                }

                return MapMarketingPath(path, absolute.Fragment);
            }
        }

        // Uppercase PHP shells / deep modules → concrete ASP.NET apps when known (never leave /CP/ /ERP/ /BOS/).
        if (IsUpperPhpShell(value, "CP"))
        {
            return MapCpPhpPath(value);
        }

        if (IsUpperPhpShell(value, "ERP")
            || value.Contains("epc_erp_shell=", StringComparison.OrdinalIgnoreCase))
        {
            return MapErpPhpPath(value);
        }

        if (IsUpperPhpShell(value, "BOS"))
        {
            return MapBosPhpPath(value);
        }

        value = StripStorefrontLangPrefix(value);

        if (value.StartsWith("/php-reference/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/marketing/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal))
        {
            return value;
        }

        // Interim: PHP /en/… pages unless TemporarilyDeactivatePhpServing (PreferAspNetApps).
        if (value.StartsWith("/shop/cart", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Cart;
        }

        if (value.Contains("checkout/how_get", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.CheckoutHowGet;
        }

        if (value.Contains("checkout/login_offer", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.CheckoutLoginOffer;
        }

        if (value.Contains("checkout/confirm", StringComparison.OrdinalIgnoreCase)
            || value.Contains("checkout_confirm", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.CheckoutConfirm;
        }

        if (value.StartsWith("/shop/checkout", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Checkout;
        }

        if (value.Equals("/shop/orders/guest", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/orders/guest?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.GuestOrder;
        }

        if (value.StartsWith("/shop/orders", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Orders;
        }

        if (value.Equals("/shop/pay", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/pay?", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/pay/", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Payment;
        }

        if (value.StartsWith("/shop/catalogue/product", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/product", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/product?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Product;
        }

        if (value.Equals("/shop/catalogue", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/catalogue?", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/catalogue/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/katalog", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/katalog?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.OwnCatalog;
        }

        if (value.Equals("/sitemap", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/sitemap?", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/sitemap", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/sitemap?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Sitemap;
        }

        if (value.Equals("/ofisy", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/ofisy?", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/offices", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/offices?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Offices;
        }

        if (value.Contains("/shop/quotes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/quotes", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Quotes;
        }

        if (value.Contains("zakladki", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Wishlist;
        }

        if (value.Contains("sravneniya", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/compare", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/compare?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Compare;
        }

        if (value.Contains("/shop/balans", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/balans", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Balance;
        }

        if (value.Contains("bulk-upload", StringComparison.OrdinalIgnoreCase)
            || value.Contains("bulk_upload", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.BulkUpload;
        }

        if (value.Contains("warehouse-search", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.ForWarehouseSearch(value);
        }

        if (value.StartsWith("/shop/part_search", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.ForPartSearch(value);
        }

        if (value.StartsWith("/shop/search", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.NameSearch + (value.Contains('?', StringComparison.Ordinal)
                ? value[value.IndexOf('?', StringComparison.Ordinal)..]
                : "");
        }

        if (value.Contains("katalog-laximo", StringComparison.OrdinalIgnoreCase)
            || value.Contains("identString=", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.ForVinSearch(value);
        }

        if (value.Contains("vehicle-catalog", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.ForVehicleCatalog(value);
        }

        // /parts with a search query is an article search, not a catalog browse
        // (home widget deep links: /en/parts?article=… / ?man=…&article=…).
        if ((value.StartsWith("/parts?", StringComparison.OrdinalIgnoreCase)
             || value.StartsWith("/parts/", StringComparison.OrdinalIgnoreCase))
            && value.Contains('?', StringComparison.Ordinal))
        {
            return StorefrontSurfaceLinks.ForPartSearch(value);
        }

        // PHP CHPU /parts/{BRAND}/{ARTICLE} → ASP.NET brand+article search result.
        if (TryMapPartsBrandArticlePath(value, out var brandArticleHref))
        {
            return brandArticleHref;
        }

        if (value.Equals("/zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/zapros-prodavczu?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.SellerRequest;
        }

        if (value.Equals("/requests", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/requests/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/requests?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.CustomerRequests;
        }

        if (value.Equals("/shop/print", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/print?", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/print_docs", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/print_docs", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.CustomerPrint;
        }

        if (value.Equals("/shop/orders/guest", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/orders/guest?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.GuestOrder;
        }

        if (value.Equals("/shop/pay", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/pay?", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/pay/", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Payment;
        }

        if (value.Equals("/novosti", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/novosti/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/novosti?", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/news", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/news/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/news?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.News;
        }

        if (IsStorefrontCatalogBrowsePath(value))
        {
            return StorefrontSurfaceLinks.ForCatalogBrowse(value);
        }

        if (value.Contains("/garage/manager", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/garage/manager", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.GarageManager;
        }

        if (value.Contains("garage", StringComparison.OrdinalIgnoreCase)
            || value.Contains("garazh", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.GarageLogin;
        }

        if (value.Contains("auto-workshop", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.AutoWorkshop;
        }

        // Account/profile before blanket /users → login.
        if (value.Contains("/users/profile", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.PreferAspNetApps
                ? "/storefront/profile-app"
                : StorefrontPhpCanonical.LangPrefix + "/users/profile";
        }

        if (value.Contains("/users/registration", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/users/register", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Registration;
        }

        if (value.Contains("/users/login", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Login;
        }

        if (value.Contains("/users/account", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/users/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/users/?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.PreferAspNetApps
                ? StorefrontAspNetCanonical.Balance
                : StorefrontPhpCanonical.Balance;
        }

        if (value.StartsWith("/vendor/register", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.VendorRegister;
        }

        if (value.StartsWith("/vendor/upload", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.VendorUpload;
        }

        if (value.StartsWith("/vendor", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.VendorPortal;
        }

        if (value.StartsWith("/users/forgot", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/users/new_password", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.ForgotPassword;
        }

        if (value.StartsWith("/users/confirm", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.ConfirmContact;
        }

        if (value.StartsWith("/shop/returns", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.CustomerReturns;
        }

        if (value.Equals("/zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/zapros-prodavczu?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.SellerRequest;
        }

        if (value.Equals("/requests", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/requests/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/requests?", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.CustomerRequests;
        }

        if (value.Equals("/shop/print", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/print?", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/shop/print_docs", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/shop/print_docs", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.CustomerPrint;
        }

        if (value.StartsWith("/auto-workshop", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.AutoWorkshop;
        }

        if (value.StartsWith("/garage/manager", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.GarageManager;
        }

        if (value.StartsWith("/garazh", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.GarageLogin;
        }

        if (value.StartsWith("/newsletter", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/subscribe", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontAspNetCanonical.Newsletter;
        }

        if (value.StartsWith("/users", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Login;
        }

        // Server IP utility (catalog href is content/usefull/ip.php, not under /CP).
        if (value.Contains("usefull/ip", StringComparison.OrdinalIgnoreCase)
            || value.Contains("useful/ip", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/server-ip-app";
        }

        var phpPathOnly = value;
        var phpQ = phpPathOnly.IndexOf('?', StringComparison.Ordinal);
        if (phpQ >= 0)
        {
            phpPathOnly = phpPathOnly[..phpQ];
        }

        var phpHash = phpPathOnly.IndexOf('#', StringComparison.Ordinal);
        if (phpHash >= 0)
        {
            phpPathOnly = phpPathOnly[..phpHash];
        }

        if (phpPathOnly.Equals("/shop/erp", StringComparison.OrdinalIgnoreCase)
            || phpPathOnly.Equals("/shop/erp/", StringComparison.OrdinalIgnoreCase))
        {
            return "/erp";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || phpPathOnly.Equals("/index.php", StringComparison.OrdinalIgnoreCase)
            || phpPathOnly.EndsWith(".php", StringComparison.OrdinalIgnoreCase))
        {
            // Product never navigates to bare PHP scripts — ASP.NET routes only.
            // (PHP compare remains under /php-reference/*; asset bridges keep /epc-static.php.)
            if (phpPathOnly.Contains("blockchain-verify", StringComparison.OrdinalIgnoreCase)
                || phpPathOnly.Contains("epc-blockchain-verify", StringComparison.OrdinalIgnoreCase))
            {
                return phpQ >= 0 ? "/blockchain/verify" + value[phpQ..] : "/blockchain/verify";
            }

            if (phpPathOnly.Contains("blockchain", StringComparison.OrdinalIgnoreCase))
            {
                return "/blockchain";
            }

            return "/";
        }

        // Relative PHP marketing paths — keep canonical full pages (do not collapse to "/").
        var qIndex = value.IndexOf('?', StringComparison.Ordinal);
        var hashIndex = value.IndexOf('#', StringComparison.Ordinal);
        var pathEnd = value.Length;
        if (qIndex >= 0)
        {
            pathEnd = Math.Min(pathEnd, qIndex);
        }

        if (hashIndex >= 0)
        {
            pathEnd = Math.Min(pathEnd, hashIndex);
        }

        var pathOnly = pathEnd == value.Length ? value : value[..pathEnd];
        var query = "";
        if (qIndex >= 0)
        {
            var queryEnd = hashIndex > qIndex ? hashIndex : value.Length;
            query = value[qIndex..queryEnd];
        }

        var frag = hashIndex >= 0 ? value[hashIndex..] : "";
        if (EcomaeMarketingPages.IsMarketingPhpPath(pathOnly))
        {
            return MapMarketingPath(pathOnly, "") + query + frag;
        }

        return "/";
    }

    private static string StripStorefrontLangPrefix(string value)
        => StorefrontLangPrefix.Strip(value);

    private static string AppendQuery(string aspNetPath, string original, string requiredPair)
    {
        var qIndex = original.IndexOf('?', StringComparison.Ordinal);
        var incoming = qIndex < 0 || qIndex >= original.Length - 1
            ? string.Empty
            : original[(qIndex + 1)..];
        if (string.IsNullOrEmpty(incoming))
        {
            return aspNetPath + "?" + requiredPair;
        }

        if (incoming.Contains(requiredPair.Split('=')[0] + "=", StringComparison.OrdinalIgnoreCase))
        {
            return aspNetPath + "?" + incoming;
        }

        return aspNetPath + "?" + requiredPair + "&" + incoming;
    }

    /// <summary>
    /// PHP <c>/CP/control/users?user_id=</c> / <c>/users/usermanager/user?user_id=</c>
    /// → ASP.NET users console with detail query preserved.
    /// </summary>
    private static string MapCpUsersAppHref(string original)
    {
        var raw = ExtractQuery(original, "user_id");
        if (!string.IsNullOrWhiteSpace(raw)
            && int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var userId)
            && userId > 0)
        {
            return "/cp/users-app?user_id=" + userId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return "/cp/users-app";
    }

    /// <summary>
    /// PHP CHPU <c>/parts/{BRAND}/{ARTICLE}</c> (and <c>/parts/brands/{ARTICLE}</c>) → search-app query.
    /// </summary>
    private static bool TryMapPartsBrandArticlePath(string value, out string href)
    {
        href = string.Empty;
        var path = value;
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            path = value[..q];
        }

        path = StripStorefrontLangPrefix(path).TrimEnd('/');
        if (!path.StartsWith("/parts/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = path["/parts/".Length..];
        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        static string Decode(string segment)
        {
            try
            {
                return Uri.UnescapeDataString(segment.Replace('+', ' ')).Trim();
            }
            catch (UriFormatException)
            {
                return segment.Trim();
            }
        }

        if (segments.Length == 1
            && segments[0].Equals("brands", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length >= 2
            && segments[0].Equals("brands", StringComparison.OrdinalIgnoreCase))
        {
            var articleOnly = Decode(segments[1]);
            if (articleOnly.Length == 0)
            {
                return false;
            }

            // Same-URL ASP.NET brand picker (PHP /en/parts/brands/{ARTICLE}).
            href = "/en/parts/brands/" + Uri.EscapeDataString(articleOnly);
            return true;
        }

        if (segments.Length >= 2)
        {
            var brand = Decode(segments[0]);
            var article = Decode(segments[1]);
            if (brand.Length == 0 || article.Length == 0)
            {
                return false;
            }

            // Same-URL ASP.NET CHPU result (PHP /en/parts/{BRAND}/{ARTICLE}).
            href = "/en/parts/"
                   + Uri.EscapeDataString(brand.ToUpperInvariant())
                   + "/"
                   + Uri.EscapeDataString(article);
            return true;
        }

        return false;
    }

    private static bool IsStorefrontCatalogBrowsePath(string value)
    {
        var path = value;
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            path = value[..q];
        }

        path = path.TrimEnd('/');
        return path.Equals("/product-family", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/available-brands", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/parts", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/accessories-spare-parts", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/accessories", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/eparts-cata", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/eparts-mod", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/partsapi-catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/levam-oem", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/umapi_catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/original-catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/demand-intelligence", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
            || path.Contains("katalogi-ucats", StringComparison.OrdinalIgnoreCase)
            || path.Contains("bulk-upload", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapMarketingPath(string path, string fragment)
    {
        var value = string.IsNullOrEmpty(path) ? "/" : path;
        var frag = string.IsNullOrEmpty(fragment) ? "" : fragment;
        if (value.Equals("/", StringComparison.Ordinal) || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return EcomaeMarketingPages.AspNetHome + frag;
        }

        if (value.StartsWith("/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp";
        }

        if (value.StartsWith("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "/erp";
        }

        // Bare /bos is product Super BOS (Super-CP host only). Marketing knowledge
        // lives at /bos/what-is-a-business-operating-system (and other article slugs).
        if (value.Equals("/bos", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos/", StringComparison.OrdinalIgnoreCase))
        {
            return "/bos" + frag;
        }

        if (value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("/BOS", StringComparison.Ordinal))
        {
            return value.TrimEnd('/') + frag;
        }

        // Interim: secondary marketing stays on PHP canonical full pages (not thin /marketing/* stubs).
        // Home alone is ASP.NET (/marketing/app). Nginx still serves /platform, /documentation, etc. via PHP.
        if (value.StartsWith("/platform", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/brochure", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/documentation", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/compare", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/blockchain", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/solutions", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/legal", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/privacy", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/terms", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/cookie-policy", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/security-policy", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/right-to-use", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/trademark", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/copyright", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/data-protection", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/acceptable-use", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/confidentiality", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/intellectual-property", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/blockchain-disclaimer", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/dmca", StringComparison.OrdinalIgnoreCase))
        {
            return value.TrimEnd('/') + frag;
        }

        var slug = value.Trim('/');
        if (EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/" + slug, out var fromStub))
        {
            return fromStub;
        }

        return value.TrimEnd('/') + frag;
    }

    public static string PhpReferenceOnlyHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return "/php-reference/home";
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            value = absolute.AbsolutePath;
        }

        if (IsUpperPhpShell(value, "CP")
            || value.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/cp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/cp";
        }

        if (IsUpperPhpShell(value, "ERP")
            || value.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/erp", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/erp";
        }

        if (IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/bos/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/bos", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/bos";
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/storefront", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return "/php-reference/home";
        }

        return "/php-reference/storefront";
    }

    public static bool IsPhpProductHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Host.Equals("epartscart.com", StringComparison.OrdinalIgnoreCase)
                || absolute.Host.EndsWith(".epartscart.com", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return IsUpperPhpShell(value, "CP")
            || IsUpperPhpShell(value, "ERP")
            || IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// True when the request path is an uppercase PHP product shell (deep /CP /ERP /BOS)
    /// that must redirect into ASP.NET — not serve PHP in the product environment.
    /// Exact shell roots /CP /CP/ are already ASP.NET classic-entry aliases.
    /// </summary>
    public static bool TryMapIncomingPhpProductPath(string pathAndQuery, out string aspNetHref)
    {
        aspNetHref = "/";
        if (string.IsNullOrWhiteSpace(pathAndQuery))
        {
            return false;
        }

        var value = pathAndQuery.Trim();
        var stripped = StripStorefrontLangPrefix(value);
        // CHPU /parts/{brand}/{article} always stays on Blazor (with or without /en).
        // Lang-prefixed aliases (/en/shop/part_search, /en/umapi_catalog, …) stay too.
        // Bare /shop/part_search still remaps to /en/… when PreferAspNet is off.
        if (IsBlazorOwnedChpuPartsPath(stripped)
            || (IncomingHasStorefrontLangPrefix(value) && IsBlazorOwnedStorefrontSameUrlPath(stripped))
            || (StorefrontSurfaceLinks.PreferAspNetApps && IsBlazorOwnedStorefrontSameUrlPath(stripped)))
        {
            return false;
        }

        if (!(IsUpperPhpShell(value, "CP")
            || IsUpperPhpShell(value, "ERP")
            || IsUpperPhpShell(value, "BOS")
            || value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || stripped.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || stripped.Contains("warehouse-search", StringComparison.OrdinalIgnoreCase)
            || stripped.Contains("katalog-laximo", StringComparison.OrdinalIgnoreCase)
            || stripped.Contains("vehicle-catalog", StringComparison.OrdinalIgnoreCase)
            // Home catalog widgets deep-link /en/parts, /en/umapi_catalog, /en/available-brands,
            // /en/product-family, … — map them into the ASP.NET apps too (kills the splash
            // when PHP serving is paused).
            || IsStorefrontCatalogBrowsePath(stripped)
            || stripped.StartsWith("/parts", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Exact /CP /ERP /BOS (+ trailing slash) with no query already have Blazor aliases — leave them.
        // Deep queries (/ERP/?epc_erp_shell=1&area=…, /BOS/?m=…) must remap into ASP.NET apps.
        var qIndex = value.IndexOf('?', StringComparison.Ordinal);
        var pathOnly = qIndex < 0 ? value : value[..qIndex];
        var hasQuery = qIndex >= 0 && qIndex < value.Length - 1;
        if (!hasQuery
            && (pathOnly.Equals("/CP", StringComparison.Ordinal)
                || pathOnly.Equals("/CP/", StringComparison.Ordinal)
                || pathOnly.Equals("/ERP", StringComparison.Ordinal)
                || pathOnly.Equals("/ERP/", StringComparison.Ordinal)
                || pathOnly.Equals("/BOS", StringComparison.Ordinal)
                || pathOnly.Equals("/BOS/", StringComparison.Ordinal)))
        {
            return false;
        }

        aspNetHref = AspNetPrimaryHref(value);
        return true;
    }

    /// <summary>
    /// Paths that <c>StorefrontSearchApp</c> (and peers) already serve at the PHP-canonical URL.
    /// Incoming remap must not 302 these away after classic-entry proxies them to Kestrel.
    /// </summary>
    private static bool IsBlazorOwnedStorefrontSameUrlPath(string strippedPathAndQuery)
    {
        var qIndex = strippedPathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = (qIndex < 0 ? strippedPathAndQuery : strippedPathAndQuery[..qIndex]).TrimEnd('/');
        if (path.Equals("/shop/part_search", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/warehouse-search", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/search", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/quotes", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/zakladki", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/sravneniya", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/balans", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/bulk-upload", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/garage/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/garage", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/garage/manager", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/garazh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/garazh/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/auto-workshop", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/newsletter", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/subscribe", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/profile", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/cart", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/orders", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/orders/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/checkout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/checkout/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/checkout_confirm", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/registration", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/register", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/forgot", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/forgot_password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/new_password", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/confirm", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/users/confirm_contact", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/vendor", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/vendor/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/vendor/register", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/vendor/upload", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/returns", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/returns/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/zapros-prodavczu", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/requests", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/requests/request", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/requests/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/print", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/print_docs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/print/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/print_docs/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/orders/guest", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/pay", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/pay/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/novosti", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/novosti/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/news", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/news/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/kontakty", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/o-dostavke", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ob-oplate", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/o-vozvrate", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/polzovatelskoe-soglashenie", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/politika-konfidencialnosti", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/sitemap", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/sitemap", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/catalogue", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/catalogue/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/katalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/product", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/compare", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/o-kompanii", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/chastye-voprosy", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ofisy", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/shop/offices", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/katalog-laximo", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/vehicle-catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/umapi_catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/product-family", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/available-brands", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/original-catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/eparts-cata", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/eparts-mod", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/partsapi-catalog", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/levam-oem", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/demand-intelligence", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/accessories-spare-parts", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/accessories", StringComparison.OrdinalIgnoreCase)
            // Exact /parts is brand-in-stock. /parts?article= remains part search (not this page).
            || (path.Equals("/parts", StringComparison.OrdinalIgnoreCase) && qIndex < 0)
            || path.Equals("/shop/katalogi-ucats", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/shop/katalogi-ucats/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsBlazorOwnedChpuPartsPath(path);
    }

    private static bool IsBlazorOwnedChpuPartsPath(string strippedPath)
    {
        var qIndex = strippedPath.IndexOf('?', StringComparison.Ordinal);
        var path = (qIndex < 0 ? strippedPath : strippedPath[..qIndex]).TrimEnd('/');
        if (!path.StartsWith("/parts/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = path["/parts/".Length..];
        var segments = rest.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 1;
    }

    private static bool IncomingHasStorefrontLangPrefix(string pathAndQuery)
    {
        var q = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = (q < 0 ? pathAndQuery : pathAndQuery[..q]);
        foreach (var lang in new[] { "/en", "/me", "/ru", "/ar" })
        {
            if (path.Equals(lang, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(lang + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUpperPhpShell(string value, string shell)
    {
        // Product PHP chrome uses uppercase /CP /ERP /BOS (catalog + legacy nav).
        var prefix = "/" + shell;
        return value.StartsWith(prefix, StringComparison.Ordinal)
            || value.StartsWith(prefix + "/", StringComparison.Ordinal)
            || value.StartsWith(prefix + "?", StringComparison.Ordinal);
    }

    /// <summary>Maps a PHP /CP/… (or /cp/…) path to an ASP.NET Control app.</summary>
    public static string MapCpPhpPath(string value)
    {
        // UAE tax lives under /finance/erp/… but must NOT be swallowed by the ERP shell remap.
        if (value.Contains("uae-tax-compliance", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/uae-tax-compliance-app";
        }

        // CP embeds ERP tabs under /CP/shop/finance/erp — route those via ERP map.
        if (value.Contains("epc_erp_shell=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/finance/erp", StringComparison.OrdinalIgnoreCase))
        {
            return MapErpPhpPath(value);
        }

        var path = value.Split('?', 2)[0];
        var rest = path.StartsWith("/CP/", StringComparison.Ordinal)
            ? path["/CP/".Length..]
            : path.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase)
                ? path["/cp/".Length..]
                : path.TrimStart('/');

        // Bare PHP /cp/control (and /CP/control) → same ASP.NET Command Centre as /cp.
        if (rest.Equals("control", StringComparison.OrdinalIgnoreCase)
            || rest.Equals("control/", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/control";
        }

        // Top-level PHP CP areas (exact rest) — too short for safe Contains markers.
        var topLevel = rest.TrimEnd('/');
        if (topLevel.Equals("lang", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/languages-app";
        }

        if (topLevel.Equals("requests", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/system-requests-app";
        }

        if (topLevel.Equals("content", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/pages-app";
        }

        if (topLevel.Equals("users", StringComparison.OrdinalIgnoreCase))
        {
            return MapCpUsersAppHref(value);
        }

        if (topLevel.Equals("menu", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/menus-app";
        }

        if (topLevel.Equals("filemanager", StringComparison.OrdinalIgnoreCase)
            || topLevel.Equals("file_manager", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/file-manager-app";
        }

        if (topLevel.Equals("modules_control", StringComparison.OrdinalIgnoreCase)
            || topLevel.Equals("modules", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/modules-app";
        }

        if (topLevel.Equals("packs_control", StringComparison.OrdinalIgnoreCase)
            || topLevel.Equals("packs", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/industry-packs-app";
        }

        if (topLevel.Equals("plugins_control", StringComparison.OrdinalIgnoreCase)
            || topLevel.Equals("plugins", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/plugins-manager-app";
        }

        if (topLevel.Equals("templates_control", StringComparison.OrdinalIgnoreCase)
            || topLevel.Equals("templates", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp/templates-manager-app";
        }

        if (topLevel.Equals("shop", StringComparison.OrdinalIgnoreCase))
        {
            return "/cp";
        }

        foreach (var (marker, aspNet) in CpPathMap)
        {
            if (rest.Contains(marker, StringComparison.OrdinalIgnoreCase)
                || path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                // Preserve ?user_id= so Open / PHP user.php deep links open the detail pane.
                if (aspNet.Equals("/cp/users-app", StringComparison.OrdinalIgnoreCase))
                {
                    return MapCpUsersAppHref(value);
                }

                return aspNet;
            }
        }

        // Specific *guide* / ops paths → ops-guides (keep after concrete module markers).
        if (ContainsAny(path, "ops", "guide"))
        {
            return "/cp/ops-guides-app";
        }

        return "/cp";
    }

    private static string MapErpPhpPath(string value)
    {
        var pathOnly = value.Split('?', 2)[0];
        var path = pathOnly.ToLowerInvariant();
        if (path.Contains("erp/guide", StringComparison.Ordinal)
            || path.Equals("/erp/guide", StringComparison.Ordinal)
            || path.EndsWith("/guide", StringComparison.Ordinal)
            || path.Contains("epc_erp_guide", StringComparison.Ordinal)
            || path.Contains("erp_guide", StringComparison.Ordinal))
        {
            return "/erp/guide-app";
        }

        var tab = ExtractQuery(value, "tab");
        var area = ExtractQuery(value, "area");
        var key = (tab ?? string.Empty).Trim().ToLowerInvariant();
        var areaKey = (area ?? string.Empty).Trim().ToLowerInvariant();

        if (ErpPhpTabRouteMap.TryMapTab(key, out var fromTab))
        {
            return fromTab;
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            return ErpPhpTabRouteMap.MapTabOrModuleApp(key);
        }

        // Area-only hubs must land on the module named by that area (not a sibling process).
        // AP ≠ purchasing; AR ≠ sales; credit_coll ≠ treasury.
        return areaKey switch
        {
            "overview" or "common" or "setup" or "enterprise" => "/erp",
            "finance" or "gl" or "general_ledger" => "/erp/gl-journals-app",
            "ap" => "/erp/payables-app",
            "ar" => "/erp/receivables-app",
            "sales" => "/erp/sales-orders-app",
            "purchasing" => "/erp/purchase-orders-app",
            "banking" => "/erp/cash-accounts-app",
            "credit_coll" => "/cp/collections-dunning-app",
            "inventory_mgmt" or "inventory" or "pim" or "logistics" => "/erp/inventory-stock-app",
            "warehouse" => "/erp/warehouses-app",
            "people" or "payroll" or "payroll_area" or "leave_abs" => "/cp/hr-overview-app",
            "tax" or "risk" => "/cp/uae-tax-compliance-app",
            "production" or "master_planning_area" or "mhei" => "/cp/production-overview-app",
            "projects" => "/cp/projects-overview-app",
            "retail" or "service_mgmt" => "/cp/jewellery-retail-app",
            "budgeting" => "/cp/budgets-app",
            "fixed_assets" or "asset_mgmt" => "/erp/fixed-assets-app",
            "cost_mgmt" or "cost_acct" => "/cp/cost-models-app",
            "landed_cost_area" => "/cp/landed-cost-app",
            "consolidations" => "/cp/consolidations-app",
            "audit_wb" or "audit" => "/cp/audit-trail-app",
            "expense" => "/erp/expense-reports-app",
            _ => "/erp",
        };
    }

    private static string MapBosPhpPath(string value)
    {
        var path = value.Split('?', 2)[0].ToLowerInvariant();
        var module = ExtractQuery(value, "m")?.Trim().ToLowerInvariant() ?? string.Empty;
        var section = ExtractQuery(value, "section")?.Trim().ToLowerInvariant() ?? string.Empty;

        // Exact module match first — never let substring Contains steal (isolation_audit, tenant_email).
        if (module.Length > 0)
        {
            var fromModule = module switch
            {
                "fleet_cp" or "tenant" or "tenants" => "/bos/tenants-app",
                "command_center" or "command" => "/bos/app",
                "fleet_health" or "health" => "/bos/fleet-health-app",
                "fleet_readiness" or "readiness" or "ready" => "/bos/fleet-readiness-app",
                "fleet_summary" or "summary" => "/bos/fleet-summary-app",
                "audit_log" or "boc_audit" => "/bos/audit-log-app",
                "isolation_audit" => "/cp/isolation-audit-app",
                "tenant_email" => "/cp/tenant-email-app",
                "license_trends" or "industry_license_trends" => "/cp/industry-packs-app",
                "data_policy" or "tenant_data_policy" => "/cp/platform-governance-app",
                _ => null,
            };
            if (fromModule is not null)
            {
                return fromModule;
            }

            // Known CP Boc modules under /BOS/?m= → CP apps via MapCpPhpPath markers.
            var asCp = MapCpPhpPath("/CP/control/portal/" + module);
            if (!asCp.Equals("/cp", StringComparison.OrdinalIgnoreCase))
            {
                return asCp;
            }
        }

        if (section is "tenants" or "tenant")
        {
            return "/bos/tenants-app";
        }

        if (path.Contains("/bos/tenants", StringComparison.Ordinal)
            || path.Contains("fleet_cp", StringComparison.Ordinal))
        {
            return "/bos/tenants-app";
        }

        if (path.Contains("fleet-health", StringComparison.Ordinal) || path.Contains("fleet_health", StringComparison.Ordinal))
        {
            return "/bos/fleet-health-app";
        }

        if (path.Contains("fleet-readiness", StringComparison.Ordinal) || path.Contains("readiness", StringComparison.Ordinal))
        {
            return "/bos/fleet-readiness-app";
        }

        if (path.Contains("audit-log", StringComparison.Ordinal) || path.Contains("audit_log", StringComparison.Ordinal))
        {
            return "/bos/audit-log-app";
        }

        if (path.Contains("fleet-summary", StringComparison.Ordinal) || path.Contains("fleet_summary", StringComparison.Ordinal))
        {
            return "/bos/fleet-summary-app";
        }

        return "/bos";
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ExtractQuery(string href, string key)
    {
        var qIndex = href.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0 || qIndex >= href.Length - 1)
        {
            return null;
        }

        var query = href[(qIndex + 1)..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }
}
