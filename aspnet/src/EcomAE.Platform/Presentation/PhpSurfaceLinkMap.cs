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
        ("control/portal/epc_tenant_control_center", "/cp/tenants-app"),
        ("shop/tenant_hub/tenant_hub", "/cp/tenants-app"),
        ("control/portal/epc_platform_health_checkup", "/cp/failover-status-app"),
        ("control/portal/epc_boc_audit_log", "/bos/audit-log-app"),
        ("control/portal/epc_boc_channel_control", "/cp/marketplace-channels-app"),
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
        ("shop/parts_agent/parts_agent_chats", "/cp/parts-agent-chats-app"),
        ("shop/document_control/document_control", "/cp/document-control-app"),
        ("shop/logistics/sposoby-polucheniya", "/cp/delivery-methods-app"),
        ("shop/logistics/storages", "/cp/storages-app"),
        ("shop/logistics/carriers", "/cp/carriers-app"),
        ("shop/finance/nastrojka-kursov-valyut", "/cp/currencies-app"),
        ("shop/finance/epc_collections_dunning", "/cp/collections-dunning-app"),
        ("shop/finance/erp/uae-tax-compliance", "/cp/uae-tax-compliance-app"),
        ("shop/catalogue/catalogue_editor", "/cp/product-catalogue-app"),
        ("shop/marketing/marketing", "/cp/marketing-growth-app"),
        ("shop/payments/payments", "/cp/payment-gateways-app"),
        ("shop/returns-manager", "/cp/returns-rma-app"),
        ("shop/crm/crm_main", "/cp/crm-board-app"),
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
        ("industry_settings", "/cp/industry-packs-app"),
        ("sposoby-polucheniya", "/cp/delivery-methods-app"),
        ("tenant_control", "/cp/tenants-app"),
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
                    return absolute.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/cp";
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

    private static string MapCpPhpPath(string value)
    {
        // CP embeds ERP tabs under /CP/shop/finance/erp — route those via ERP map.
        if (value.Contains("epc_erp_shell=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/finance/erp", StringComparison.OrdinalIgnoreCase))
        {
            return MapErpPhpPath(value);
        }

        var path = value.Split('?', 2)[0];
        var rest = path.StartsWith("/CP/", StringComparison.Ordinal)
            ? path["/CP/".Length..]
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

        if (ContainsAny(path, "ops", "guide"))
        {
            return "/cp/ops-guides-app";
        }

        return "/cp";
    }

    private static string MapErpPhpPath(string value)
    {
        var tab = ExtractQuery(value, "tab");
        var area = ExtractQuery(value, "area");
        var key = (tab ?? string.Empty).Trim().ToLowerInvariant();
        var areaKey = (area ?? string.Empty).Trim().ToLowerInvariant();

        var fromTab = key switch
        {
            "dashboard" or "overview" => "/erp",
            "processflow" or "process_flow" or "workflow" => "/erp/process-flow-tasks-app",
            "aging" or "ar_aging" or "ap_aging" => "/erp/aging-app",
            "report_center" or "reports" or "reportcenter" or "rc_finance" or "pl" or "balance_sheet" => "/erp/report-center-app",
            "sales_orders" or "salesorders" => "/erp/sales-orders-app",
            "sales_quotations" or "quotations" or "proposals" => "/erp/sales-quotations-app",
            "purchase_orders" or "purchaseorders" => "/erp/purchase-orders-app",
            "purchase_requisitions" => "/cp/purchase-requests-app",
            "purchases" or "payables" => "/erp/purchases-app",
            "invoices" or "receivables" => "/erp/invoices-app",
            "cash_bank" or "cash" or "banking" => "/erp/cash-accounts-app",
            "cash_entries" or "bank_entries" => "/erp/cash-entries-app",
            "coa" or "chart_of_accounts" => "/erp/coa-accounts-app",
            "gl" or "journals" or "general_journal" => "/erp/gl-journals-app",
            "inventory" or "stock" or "inventory_stock" => "/erp/inventory-stock-app",
            "stock_movements" or "movements" or "ledger" => "/erp/stock-movements-app",
            "stock_transfers" or "transfers" or "inv_groups" => "/erp/stock-transfers-app",
            "warehouses" or "warehouse" or "wms" => "/erp/warehouses-app",
            "suppliers" or "vendors" => "/erp/suppliers-app",
            "fixed_assets" or "assets" => "/erp/fixed-assets-app",
            "bank_reconciliation" or "reconciliation" or "bank_recon" => "/erp/bank-reconciliation-app",
            "on_premises" or "onpremises" => "/erp/on-premises-app",
            "favorites" or "workspace" => "/erp/workspace-favorites-app",
            "accounts" => "/erp/accounts-summary-app",
            "hr" => "/cp/hr-overview-app",
            "manufacturing" => "/cp/production-overview-app",
            "projects" => "/cp/projects-overview-app",
            "retail_commerce" => "/cp/jewellery-retail-app",
            "budgeting" => "/cp/budgets-app",
            "workflow_automation" => "/cp/workflows-app",
            "opportunities" => "/cp/crm-opportunities-app",
            "einvoice" => "/cp/einvoice-documents-app",
            "jw_repairs" => "/cp/jewellery-repairs-app",
            "crm" => "/cp/crm-tickets-app",
            "cost_models" => "/cp/cost-models-app",
            "fin_advanced" => "/cp/fin-advanced-app",
            "blockchain_proofs" => "/cp/blockchain-proofs-app",
            "landed_cost" => "/cp/landed-cost-app",
            "aml_compliance" => "/cp/aml-compliance-app",
            "jw_karat" => "/cp/jewellery-masters-app",
            "consolidation_bu" => "/cp/consolidations-app",
            "elec_reporting" => "/cp/electronic-reporting-app",
            _ => null,
        };

        if (fromTab is not null)
        {
            return fromTab;
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
