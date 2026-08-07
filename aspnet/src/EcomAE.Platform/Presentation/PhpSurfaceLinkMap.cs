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
        ("shop/orders/orders", "/cp/orders"),
        ("shop/orders/carts", "/cp/abandoned-carts-app"),
        ("control/shop/docpart/crosses", "/cp/crosses-app"),
        ("control/shop/catalogue/stock", "/erp/inventory-stock-app"),
        ("control/shop/procurement", "/cp/purchase-requests-app"),
        ("control/shop/multivendor", "/cp"),
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
        ("shop/parts_agent/parts_agent_chats", "/cp/parts-agent-chats-app"),
        ("shop/parts_agent_chats", "/cp/parts-agent-chats-app"),
        ("shop/parts_agent", "/cp/parts-agent-chats-app"),
        ("shop/document_control/document_control", "/cp/document-control-app"),
        ("shop/logistics/sposoby-polucheniya", "/cp/delivery-methods-app"),
        ("shop/logistics/custom_shipping", "/cp/carriers-app"),
        ("shop/logistics/storages", "/cp/storages-app"),
        ("shop/logistics/stock", "/erp/inventory-stock-app"),
        ("shop/logistics/logistics", "/cp/delivery-methods-app"),
        ("shop/logistics/carriers", "/cp/carriers-app"),
        ("shop/procurement/procurement", "/cp/purchase-requests-app"),
        ("shop/procurement", "/cp/purchase-requests-app"),
        ("shop/price-management", "/cp/price-lists-app"),
        ("shop/finance/nastrojka-kursov-valyut", "/cp/currencies-app"),
        ("shop/finance/epc_collections_dunning", "/cp/collections-dunning-app"),
        ("shop/finance/erp/uae-tax-compliance", "/cp/uae-tax-compliance-app"),
        ("uae-tax-compliance", "/cp/uae-tax-compliance-app"),
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
        ("shop/marketing/seo", "/cp/seo-app"),
        ("seo_main", "/cp/seo-app"),
        ("epc_social_media_hub", "/cp/social-hub-app"),
        ("epc_tenant_features", "/cp/tenant-features-app"),
        ("epc_super_cp_customer_board", "/cp/customer-board-app"),
        ("epc_fulfillment_queue", "/cp/fulfillment-queue-app"),
        ("epc_sso_saml", "/cp/sso-saml-app"),
        ("epc_event_bus", "/cp/event-bus-app"),
        ("shop/marketing/marketing", "/cp/marketing-growth-app"),
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
                    return absolute.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/erp";
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

        if (value.StartsWith("/shop/checkout", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Checkout;
        }

        if (value.StartsWith("/shop/orders", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Orders;
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

        if (IsStorefrontCatalogBrowsePath(value))
        {
            return StorefrontSurfaceLinks.ForCatalogBrowse(value);
        }

        if (value.Contains("garage", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.GarageLogin;
        }

        if (value.StartsWith("/users", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/vendor", StringComparison.OrdinalIgnoreCase))
        {
            return StorefrontSurfaceLinks.Login;
        }

        if (value.StartsWith("/shop/", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".php", StringComparison.OrdinalIgnoreCase))
        {
            // Product never navigates to bare PHP scripts — home or marketing compare.
            if (value.Contains("blockchain", StringComparison.OrdinalIgnoreCase))
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
    {
        var qIndex = value.IndexOf('?', StringComparison.Ordinal);
        var path = qIndex < 0 ? value : value[..qIndex];
        var query = qIndex < 0 ? string.Empty : value[qIndex..];
        foreach (var lang in new[] { "/en", "/me", "/ru" })
        {
            if (path.Equals(lang, StringComparison.OrdinalIgnoreCase))
            {
                return "/" + query;
            }

            if (path.StartsWith(lang + "/", StringComparison.OrdinalIgnoreCase))
            {
                return path[lang.Length..] + query;
            }
        }

        return value;
    }

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

            href = StorefrontAspNetCanonical.PartSearch + "?article=" + Uri.EscapeDataString(articleOnly);
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

            href = StorefrontAspNetCanonical.PartSearch
                   + "?article=" + Uri.EscapeDataString(article)
                   + "&brand=" + Uri.EscapeDataString(brand);
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

        foreach (var (marker, aspNet) in CpPathMap)
        {
            if (rest.Contains(marker, StringComparison.OrdinalIgnoreCase)
                || path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
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

        return areaKey switch
        {
            "overview" or "finance" or "common" or "setup" or "enterprise" => "/erp",
            "sales" or "ar" => "/erp/sales-orders-app",
            "purchasing" or "ap" => "/erp/purchase-orders-app",
            "banking" or "credit_coll" => "/erp/cash-accounts-app",
            "inventory_mgmt" or "pim" or "logistics" => "/erp/inventory-stock-app",
            "warehouse" => "/erp/warehouses-app",
            "people" or "payroll_area" or "leave_abs" => "/cp/hr-overview-app",
            "tax" or "risk" => "/cp/uae-tax-compliance-app",
            "production" => "/cp/production-overview-app",
            "projects" => "/cp/projects-overview-app",
            "retail" or "service_mgmt" => "/cp/jewellery-retail-app",
            "budgeting" => "/cp/budgets-app",
            "fixed_assets" or "asset_mgmt" => "/erp/fixed-assets-app",
            "cost_mgmt" or "cost_acct" => "/cp/cost-models-app",
            "landed_cost_area" => "/cp/landed-cost-app",
            "consolidations" => "/cp/consolidations-app",
            _ => "/erp",
        };
    }

    private static string MapBosPhpPath(string value)
    {
        var path = value.Split('?', 2)[0].ToLowerInvariant();
        var module = ExtractQuery(value, "m")?.Trim().ToLowerInvariant();

        if (module is "fleet_cp" or "tenant" or "tenants")
        {
            return "/bos/tenants-app";
        }

        if (module is "command_center" or "command")
        {
            return "/bos/app";
        }

        if (path.Contains("tenant", StringComparison.Ordinal) || module?.Contains("tenant", StringComparison.Ordinal) == true)
        {
            return "/bos/tenants-app";
        }

        if (path.Contains("health", StringComparison.Ordinal) || module?.Contains("health", StringComparison.Ordinal) == true)
        {
            return "/bos/fleet-health-app";
        }

        if (path.Contains("ready", StringComparison.Ordinal)
            || path.Contains("readiness", StringComparison.Ordinal)
            || module?.Contains("ready", StringComparison.Ordinal) == true)
        {
            return "/bos/fleet-readiness-app";
        }

        if (path.Contains("audit", StringComparison.Ordinal) || module?.Contains("audit", StringComparison.Ordinal) == true)
        {
            return "/bos/audit-log-app";
        }

        if (path.Contains("summary", StringComparison.Ordinal) || module?.Contains("summary", StringComparison.Ordinal) == true)
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
