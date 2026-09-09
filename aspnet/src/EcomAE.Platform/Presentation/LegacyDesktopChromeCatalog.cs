using System.Linq;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Mega-panel groupings that mirror PHP topnav structures for Batch 2 desktop chrome.
/// Links remain PHP deeplinks / hybrid workspace until vertical slices are ported.
/// </summary>
public static class LegacyDesktopChromeCatalog
{
    public sealed record MegaAreaColumn(
        string Id,
        string Label,
        string? Icon,
        IReadOnlyList<PhpModuleCatalog.ModuleLink> Tabs);

    public sealed record MegaGroup(
        string Id,
        string Label,
        IReadOnlyList<PhpModuleCatalog.ModuleLink> Links,
        string? Icon = null,
        string? ShortLabel = null,
        string? HubHref = null,
        IReadOnlyList<MegaAreaColumn>? Columns = null,
        string? Subtitle = null,
        bool IsAdvanced = false);

    /// <summary>
    /// Category → ERP area ids from php_module_catalog.json / erp_nav_areas.php category config.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> ErpCategoryAreas =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = ["overview"],
            ["record_to_report"] = ["finance", "budgeting", "consolidations", "cost_acct", "audit_wb", "fixed_assets"],
            ["procure_to_pay"] = ["purchasing", "ap", "landed_cost_area", "expense"],
            ["order_to_cash"] = ["sales", "ar", "credit_coll", "retail", "service_mgmt"],
            ["cash_treasury"] = ["banking"],
            ["inventory_fulfilment"] =
            [
                "inventory_mgmt", "pim", "cost_mgmt", "production", "warehouse", "mhei", "logistics",
                "master_planning_area", "asset_mgmt"
            ],
            ["hr_payroll"] = ["people", "payroll_area", "leave_abs", "projects"],
            ["compliance_tax"] = ["tax", "risk"],
            ["setup_admin"] = ["setup", "enterprise", "common"],
        };

    /// <summary>Short labels from <c>epc_erp_nav_categories_config</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> ErpCategoryShort =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = "Home",
            ["record_to_report"] = "R2R",
            ["procure_to_pay"] = "P2P",
            ["order_to_cash"] = "O2C",
            ["cash_treasury"] = "Cash",
            ["inventory_fulfilment"] = "Stock",
            ["hr_payroll"] = "HR",
            ["compliance_tax"] = "Tax",
            ["setup_admin"] = "Setup",
        };

    /// <summary>
    /// Explicit section → module ids from <c>epc_bos_*_items()</c> in epc_bos_unified.php
    /// (no keyword heuristics; no artificial per-section caps).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> BosSectionModuleIds =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fleet"] =
            [
                "command_center", "fleet_cp", "fleet_erp", "platform_health", "governance", "audit_log",
                "failover", "isolation_audit", "modern_auth", "tenant_email", "integrations", "credit_limit",
                "order_erp_pipeline", "po_approval", "api_clients", "fulfillment_queue", "power_bi",
                "auto_price", "industry_consol", "license_trends", "inventory_forecast", "multi_currency_gl",
                "wps_payroll", "collections_dunning", "warranty_rma", "customer_board", "config_edit",
                "sms_turning", "ai_copilot", "nl_reporting", "industry_packs", "multi_entity",
                "promotions_engine", "config_sandbox", "import_orchestrator", "document_vault",
                "subscription_billing", "soc2_compliance", "marketplace", "ai_service", "metabase_embed",
                "power_bi_guide", "isolation_anomaly"
            ],
            ["tenants"] =
            [
                "tenant_hub", "tenant_control", "tenant_features", "demo_tenants", "industry_packs",
                "customer_board", "integrations", "design_tokens"
            ],
            ["commerce"] =
            [
                "orders", "customers", "payments", "returns", "quotes", "channels", "pos", "statistics"
            ],
            ["catalogue"] =
            [
                "products", "sku_media", "prices_edit", "prices_upload", "multivendor", "prices_guide",
                "prices_send", "pricing"
            ],
            ["logistics"] = ["logistics", "procurement"],
            ["marketing_cp"] = ["marketing", "broadcast", "social", "seo"],
            ["marketing"] = ["marketing", "broadcast", "social", "seo"],
            ["professional"] = ["crm", "documents", "parts_agent"],
            ["erp"] =
            [
                "erp_home", "erp_gl", "erp_ap", "erp_ar", "erp_cash", "erp_tax", "erp_sales",
                "erp_purchasing", "erp_inventory", "erp_hr", "erp_payroll", "erp_production",
                "erp_projects", "erp_warehouse", "erp_fixed_assets", "erp_budgeting"
            ],
            ["auto_parts"] = ["crosses", "demand", "auto_price", "synonyms"],
            ["tax_advisory"] = ["tax_toolkit", "free_tools"],
            ["platform"] =
            [
                "portal_settings", "modern_auth", "communication", "data_policy", "api_docs",
                "operator_guide"
            ],
        };

    /// <summary>Section icons from <c>epc_bos_build_sections</c>.</summary>
    private static readonly IReadOnlyDictionary<string, string> BosSectionIcons =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fleet"] = "fa-tachometer",
            ["tenants"] = "fa-sitemap",
            ["commerce"] = "fa-shopping-cart",
            ["catalogue"] = "fa-cube",
            ["logistics"] = "fa-truck",
            ["marketing_cp"] = "fa-bullhorn",
            ["marketing"] = "fa-bullhorn",
            ["professional"] = "fa-briefcase",
            ["erp"] = "fa-university",
            ["auto_parts"] = "fa-car",
            ["tax_advisory"] = "fa-calculator",
            ["platform"] = "fa-cogs",
        };

    /// <summary>
    /// Explicit brochure category → CP topnav label (PHP control_groups / epc_cp_build_nav_tabs intent).
    /// Avoids keyword false-positives such as Label "Retail and commerce" landing under Commerce
    /// instead of the Shop / OMS order desk.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string[]> CpNavCategoryMap =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Commerce"] = ["Shop / OMS"],
            ["Catalogue"] = ["Prices & Catalogue"],
            ["Customers"] = ["Customers / CRM"],
            ["Users"] = [],
            ["Documents"] = [],
            ["Content"] = ["Content / CMS"],
            ["System"] = [],
            ["Modules"] = [],
            ["ERP"] = ["ERP / Modules", "ERP / Finance", "ERP / External Reporting", "ERP / Tax & VAT"],
            ["Purchase"] = ["Procurement"],
            ["Channels"] = ["Channels / Marketplace"],
            ["Logistics"] = ["Logistics"],
            ["AI"] = ["AI"],
            ["Marketing"] = ["Marketing"],
            ["Payments"] = ["Payments"],
            ["Integrations"] = ["Integrations"],
            ["Portal"] = ["Portal"],
            ["Platform"] = ["Super CP / Platform", "Super CP / BOC", "Industry Templates"],
            ["Operator"] = ["Super CP / Operator"],
        };

    /// <summary>
    /// CP topnav groups ≈ epc_cp_build_nav_tabs — multi-column mega panels (no artificial cap).
    /// When <paramref name="includeSuperOnly"/> is false (tenant hosts), mirrors PHP
    /// <c>epc_portal_cp_item_visible_enhanced</c>: hide Platform/Operator groups and
    /// Super brochure links (<c>epc_super_cp_*</c>, tenant_features, BOS).
    /// When <paramref name="industryCode"/> is not jewellery, jewellery / [JW] links are hidden
    /// (epartscart auto_parts must show Shop OMS, not jewellery retail).
    /// </summary>
    public static IReadOnlyList<MegaGroup> ControlPanelTopnav(bool includeSuperOnly = true, string? industryCode = null)
    {
        var jewelleryIndustry = ErpIndustryNav.IsJewelleryFromHostOrPack(industryCode, null, null);
        var groups = new List<MegaGroup>();
        foreach (var nav in LegacyChromeNavCatalog.ControlPanel)
        {
            // PHP brand link is "Control"; CONTROL is not a dropdown group.
            if (nav.Label.Equals("CONTROL", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!includeSuperOnly && IsSuperOnlyNavGroup(nav.Label))
            {
                continue;
            }

            var id = Slug(nav.Label);
            var links = PhpModuleCatalog.CpBrochureFeatures
                .Where(f => MatchesGroup(f, nav.Label, nav.Href))
                .Where(f => includeSuperOnly || !IsSuperOnlyCpLink(f.Href, f.Group))
                .Where(f => jewelleryIndustry || !IsJewelleryCpLink(f))
                .ToList();

            foreach (var extra in PhpLegacyGroupParityLinks(nav.Label))
            {
                if (!includeSuperOnly && IsSuperOnlyCpLink(extra.Href, extra.Group))
                {
                    continue;
                }

                if (!jewelleryIndustry && IsJewelleryCpLink(extra))
                {
                    continue;
                }

                if (links.Any(l => string.Equals(l.Href, extra.Href, StringComparison.OrdinalIgnoreCase)
                                   && string.Equals(l.Label, extra.Label, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                links.Add(extra);
            }

            if (string.Equals(nav.Label, "Commerce", StringComparison.OrdinalIgnoreCase))
            {
                links = PrioritizeCommerceOms(links, nav.Href);
            }

            if (links.Count == 0)
            {
                if (!includeSuperOnly && IsSuperOnlyCpLink(nav.Href, nav.Label))
                {
                    continue;
                }

                links.Add(new PhpModuleCatalog.ModuleLink(id, nav.Label, nav.Href, null, nav.Label));
                foreach (var qa in LegacyChromeNavCatalog.ControlPanelQuickActions.Take(8))
                {
                    if (!includeSuperOnly && IsSuperOnlyCpLink(qa.Href, nav.Label))
                    {
                        continue;
                    }

                    if (!jewelleryIndustry && IsJewelleryHrefOrLabel(qa.Href, qa.Label))
                    {
                        continue;
                    }

                    links.Add(new PhpModuleCatalog.ModuleLink(Slug(qa.Label), qa.Label, qa.Href, null, nav.Label));
                }

                if (string.Equals(nav.Label, "Commerce", StringComparison.OrdinalIgnoreCase))
                {
                    links = PrioritizeCommerceOms(links, nav.Href);
                }
            }

            if (links.Count == 0)
            {
                continue;
            }

            var columns = new List<MegaAreaColumn>();
            const int colSize = 8;
            for (var i = 0; i < links.Count; i += colSize)
            {
                var chunk = links.Skip(i).Take(colSize).ToList();
                var colIndex = (i / colSize) + 1;
                columns.Add(new MegaAreaColumn(
                    $"{id}-col-{colIndex}",
                    colIndex == 1 ? nav.Label : $"{nav.Label} · {colIndex}",
                    CpGroupIcon(id),
                    chunk));
            }

            var hub = string.Equals(nav.Label, "Commerce", StringComparison.OrdinalIgnoreCase)
                ? (links.FirstOrDefault(l => IsOmsOrdersLink(l))?.Href ?? nav.Href)
                : links[0].Href;

            groups.Add(new MegaGroup(
                id,
                nav.Label,
                links,
                CpGroupIcon(id),
                nav.Label,
                hub,
                columns,
                CpGroupSubtitle(nav.Label),
                CpGroupIsAdvanced(nav.Label)));
        }

        return groups;
    }

    /// <summary>Jewellery / JW brochure + app links — hidden on non-jewellery tenant CP (PHP industry filter).</summary>
    public static bool IsJewelleryCpLink(PhpModuleCatalog.ModuleLink link)
        => IsJewelleryHrefOrLabel(link.Href, link.Label)
           || ErpIndustryNav.IsJewelleryTab(link);

    public static bool IsJewelleryHrefOrLabel(string? href, string? label)
    {
        var h = href ?? string.Empty;
        var l = label ?? string.Empty;
        if (l.Contains("[JW]", StringComparison.OrdinalIgnoreCase)
            || l.Contains("jewellery", StringComparison.OrdinalIgnoreCase)
            || l.Contains("jewelry", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (h.Contains("/jewellery-", StringComparison.OrdinalIgnoreCase)
            || h.Contains("jewellery_", StringComparison.OrdinalIgnoreCase)
            || h.Contains("jewellery-", StringComparison.OrdinalIgnoreCase)
            || h.Contains("tab=jw_", StringComparison.OrdinalIgnoreCase)
            || h.Contains("jw_", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_jewel", StringComparison.OrdinalIgnoreCase)
            || h.Contains("gold_rate", StringComparison.OrdinalIgnoreCase)
            || h.Contains("jewellery_tag", StringComparison.OrdinalIgnoreCase)
            || h.Contains("gold_scheme", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // ERP retail/service_mgmt areas are jewellery POS packs in this product — not epartscart OMS.
        if (h.Contains("area=retail", StringComparison.OrdinalIgnoreCase)
            || h.Contains("area=service_mgmt", StringComparison.OrdinalIgnoreCase)
            || h.Contains("tab=retail_commerce", StringComparison.OrdinalIgnoreCase)
            || h.Contains("tab=jw_retail_sales", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsOmsOrdersLink(PhpModuleCatalog.ModuleLink link)
    {
        var id = link.Id ?? string.Empty;
        var href = link.Href ?? string.Empty;
        var label = link.Label ?? string.Empty;
        return id.Equals("oms-orders", StringComparison.OrdinalIgnoreCase)
               || href.Contains("/shop/orders/orders", StringComparison.OrdinalIgnoreCase)
               || href.Equals("/cp/orders", StringComparison.OrdinalIgnoreCase)
               || href.StartsWith("/cp/orders?", StringComparison.OrdinalIgnoreCase)
               || label.Contains("OMS · Orders", StringComparison.OrdinalIgnoreCase)
               || label.Equals("Orders (OMS)", StringComparison.OrdinalIgnoreCase);
    }

    private static List<PhpModuleCatalog.ModuleLink> PrioritizeCommerceOms(
        List<PhpModuleCatalog.ModuleLink> links,
        string commerceHref)
    {
        if (links.Count == 0)
        {
            return links;
        }

        var ordered = new List<PhpModuleCatalog.ModuleLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Take(PhpModuleCatalog.ModuleLink link)
        {
            var key = link.Id + "|" + link.Href;
            if (seen.Add(key))
            {
                ordered.Add(link);
            }
        }

        foreach (var link in links.Where(IsOmsOrdersLink))
        {
            Take(link);
        }

        if (!ordered.Any(IsOmsOrdersLink))
        {
            Take(new PhpModuleCatalog.ModuleLink("oms-orders", "OMS · Orders", commerceHref, "fa-shopping-cart", "Shop / OMS"));
        }

        foreach (var link in links)
        {
            Take(link);
        }

        return ordered;
    }

    /// <summary>PHP epc_portal_cp_item_visible_enhanced Super-only needles + brochure groups.</summary>
    public static bool IsSuperOnlyCpLink(string? href, string? group = null)
    {
        var h = href ?? string.Empty;
        var g = group ?? string.Empty;
        if (g.Contains("Super CP", StringComparison.OrdinalIgnoreCase)
            || g.Equals("Platform", StringComparison.OrdinalIgnoreCase)
            || g.Equals("Operator", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (h.StartsWith("/bos", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_super_cp_", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_tenant_features", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_pos_tenant_manage", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_sso_saml", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_event_bus", StringComparison.OrdinalIgnoreCase)
            || h.Contains("super_cp_fleet", StringComparison.OrdinalIgnoreCase)
            || h.Contains("super_erp_fleet", StringComparison.OrdinalIgnoreCase)
            || h.Contains("fleet-health-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/tenant-features-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/customer-board-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/sso-saml-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/event-bus-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/bos/", StringComparison.OrdinalIgnoreCase)
            // Super-gated apps / Boc paths that must not appear on tenant chrome.
            || h.Contains("epc_tax_toolkit", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/tax-toolkits-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("tenant_hub", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_tenant_control", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/tenants-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/demo-tenants-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_demo_tenants", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_boc_audit_log", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_free_tools", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/free-tools-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_platform_governance", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/platform-governance-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_platform_health", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/failover-status-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_dealer_portal", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_industry_license_trends", StringComparison.OrdinalIgnoreCase)
            // Super CP multi-site portal fleet / deploy inventory — never on tenant chrome.
            || h.Contains("/cp/portal-settings-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("control/portal/portal", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/platform-communication-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_super_cp_communication", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/info-blocks-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("epc_super_cp_info_blocks", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsSuperOnlyNavGroup(string label)
        => label.Equals("Platform", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Operator", StringComparison.OrdinalIgnoreCase);

    private static string CpGroupIcon(string id) => id switch
    {
        "dashboard" or "home" or "overview" => "fa-tachometer",
        "orders" or "commerce" or "shop" => "fa-shopping-cart",
        "catalogue" or "products" or "catalog" => "fa-cube",
        "customers" or "crm" => "fa-users",
        "documents" or "content" or "cms" => "fa-file-text-o",
        "erp" => "fa-university",
        "purchase" or "procurement" => "fa-truck",
        "channels" => "fa-share-alt",
        "logistics" or "shipping" => "fa-cubes",
        "ai" => "fa-magic",
        "marketing" => "fa-bullhorn",
        "finance" or "payments" => "fa-credit-card",
        "integrations" => "fa-plug",
        "portal" or "settings" or "setup" or "system" => "fa-cog",
        "modules" or "plugins" => "fa-cubes",
        "users" => "fa-users",
        "platform" => "fa-sitemap",
        "operator" => "fa-shield",
        _ => "fa-folder-o",
    };

    /// <summary>PHP <c>epc_portal_cp_group_subtitle_map</c> (English fallbacks).</summary>
    public static string? CpGroupSubtitle(string label) => label switch
    {
        "Commerce" => "Orders, catalogue & prices",
        "Catalogue" => "Products, stock & lists",
        "Customers" => "Clients, user accounts & CRM",
        "Users" => "Accounts, groups & registration",
        "Documents" => "Invoices & PDFs",
        "Content" => "Pages, menus & files",
        "System" => "Config, SMS & languages",
        "Modules" => "Modules, plugins & templates",
        "ERP" => "Finance, VAT & reports",
        "Purchase" => "Purchasing & suppliers",
        "Channels" => "Marketplaces & feeds",
        "Logistics" => "Shipping & delivery",
        "Payments" => "Cards & online pay",
        "Marketing" => "Campaigns & social",
        "AI" => "Pricing & assistants",
        "Integrations" => "Email, mobile, payments & more",
        "Portal" => "Site & industry settings",
        "Platform" => "Platform tools",
        "Operator" => "Cross-tenant platform tools",
        _ => null,
    };

    /// <summary>PHP <c>epc_portal_cp_advanced_group_keys</c>.</summary>
    public static bool CpGroupIsAdvanced(string label)
        => label.Equals("AI", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Marketing", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Payments", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Integrations", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Portal", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Platform", StringComparison.OrdinalIgnoreCase)
           || label.Equals("Operator", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PHP <c>control_items</c> from the epartscart / docpart leftover + primary groups.
    /// Catalog stores PHP <c>/CP/…</c> hrefs; chrome rewrites via <see cref="PhpSurfaceLinkMap.AspNetPrimaryHref"/>.
    /// Source: <c>content/files/epc_cache/epc_cp_menu_rows_v1_docpart.json</c>.
    /// </summary>
    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> PhpLegacyGroupParityLinks(string label)
    {
        if (label.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("cp-config", "Config", "/CP/control/config", "fa-wrench", "System"),
                new("cp-guideline", "CP guideline", "/CP/control/cp-guideline", "fa-book", "System"),
                new("sms-gateways", "SMS gateways", "/CP/control/sms-operatory", "fa-mobile-alt", "System"),
                new("email-settings", "Email settings", "/CP/control/config?need_config_group=3", "fa-envelope", "System"),
                new("communications", "Communications", "/CP/control/communications", "fa-mail-bulk", "System"),
                new("notifications", "Notifications", "/CP/control/notifications_settings", "fa-envelope-open-text", "System"),
                new("server-ip", "Server IP", "/content/usefull/ip.php", "fa-network-wired", "System"),
                new("languages", "Languages", "/CP/lang", "fa-language", "System"),
                new("umapi-settings", "UMAPI settings", "/CP/control/config?need_config_group=13", "fa-key", "System"),
            ];
        }

        if (label.Equals("Modules", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("modules-manager", "Modules manager", "/CP/modules/modules_manager", "fa-cubes", "Modules"),
                new("plugins-manager", "Plugins manager", "/CP/plugins/plugins_manager", "fa-puzzle-piece", "Modules"),
                new("templates-manager", "Templates manager", "/CP/templates/templates_manager", "fa-palette", "Modules"),
                new("debug-console", "Debug", "/CP/system/debug", "fa-bug", "Modules"),
                new("industry-packs", "Industry packs", "/CP/packs/packs_manager", "fa-compact-disc", "Modules"),
                new("pack-setup", "Pack setup", "/CP/packs/setup", "fa-upload", "Modules"),
            ];
        }

        if (label.Equals("Documents", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("document-control", "Document control", "/CP/shop/document_control/document_control", "fa-file-invoice", "Documents"),
            ];
        }

        if (label.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("user-groups", "User groups", "/CP/users/usergroups", "fa-users", "Users"),
                new("user-manager", "User manager", "/CP/users/usermanager", "fa-user-alt", "Users"),
                new("add-user", "Add user", "/CP/users/usermanager/user", "fa-user-plus", "Users"),
                new("customer-approvals", "Customer approvals", "/CP/users/customer_approvals", "fa-user-check", "Users"),
                new("registration-options", "Registration options", "/CP/users/registracionnye-varianty", "fa-users-cog", "Users"),
                new("registration-fields", "Registration fields", "/CP/users/polya-registracii", "fa-address-card", "Users"),
            ];
        }

        if (label.Equals("Content", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("content-tree", "Content tree", "/CP/content/content_tree", "fa-sitemap", "Content"),
                new("content-manager", "Content manager", "/CP/content/content_manager", "fa-copy", "Content"),
                new("additional-texts", "Additional texts", "/CP/content/dopolnitelnye-teksty", "fa-paperclip", "Content"),
                new("sitemap", "Sitemap", "/CP/content/sitemap", "fa-sitemap", "Content"),
                new("file-manager", "File manager", "/CP/filemanager", "fa-folder-open", "Content"),
                new("menus", "Menus", "/CP/menu/menu_manager", "fa-paste", "Content"),
                new("slider-banners", "Slider / banners", "/CP/content/slider", "fa-film", "Content"),
                new("structure-dumps", "Structure dumps", "/CP/content/structure_dumps", "fa-code-branch", "Content"),
            ];
        }

        if (label.Equals("Catalogue", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("catalogue-editor", "Catalogue editor", "/CP/shop/catalogue/catalogue_editor", "fa-sitemap", "Catalogue"),
                new("catalogue-products", "Products", "/CP/shop/catalogue/products", "fa-boxes", "Catalogue"),
                new("catalogue-stock", "Stock", "/CP/shop/logistics/stock", "fa-pallet", "Catalogue"),
                new("line-lists", "Line lists", "/CP/shop/catalogue/line_lists", "fa-list-alt", "Catalogue"),
                new("tree-lists", "Tree lists", "/CP/shop/catalogue/tree_lists", "fa-tree", "Catalogue"),
                new("special-searches", "Special searches", "/CP/shop/catalogue/specialnye-poiski", "fa-indent", "Catalogue"),
                new("homepage-products", "Homepage products", "/CP/shop/catalogue/tovary-na-glavnoj", "fa-tags", "Catalogue"),
                new("related-products", "Related products", "/CP/shop/catalogue/soputstvuyushhie-tovary", "fa-link", "Catalogue"),
                new("data-transfer", "Data transfer", "/CP/shop/perenos-dannyx", "fa-code", "Catalogue"),
                new("customer-reviews", "Customer reviews", "/CP/shop/catalogue/otzyvy-pokupatelej", "fa-comments", "Catalogue"),
            ];
        }

        if (label.Equals("Commerce", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("geo-nodes", "Geo / regions", "/CP/shop/geo/nodes", "fa-globe-americas", "Commerce"),
                new("offices", "Offices", "/CP/shop/logistics/offices", "fa-store", "Commerce"),
                new("storages", "Storages", "/CP/shop/logistics/storages", "fa-warehouse", "Commerce"),
                new("oms-orders-php", "Orders", "/CP/shop/orders/orders", "fa-shopping-cart", "Commerce"),
                new("order-statuses", "Order statuses", "/CP/shop/orders/statuses", "fa-exclamation-triangle", "Commerce"),
                new("order-items", "Order items", "/CP/shop/orders/items", "fa-shapes", "Commerce"),
                new("quote-requests", "Quote requests", "/CP/shop/quote-requests", "fa-file-text-o", "Commerce"),
                new("abandoned-carts", "Abandoned carts", "/CP/shop/orders/carts", "fa-shopping-cart", "Commerce"),
                new("account-operations", "Account operations", "/CP/shop/finance/account_operations", "fa-money-check-alt", "Commerce"),
                new("prices", "Prices", "/CP/shop/prices", "fa-file-excel", "Commerce"),
                new("price-management", "Price management", "/CP/shop/price-management", "fa-tags", "Commerce"),
                new("currency-rates", "Currency rates", "/CP/shop/finance/nastrojka-kursov-valyut", "fa-ruble-sign", "Commerce"),
                new("shop-statistics", "Shop statistics", "/CP/shop/statistika", "fa-chart-line", "Commerce"),
                new("sao-statuses", "SAO statuses", "/CP/shop/orders/sao_states_statuses_link", "fa-sliders-h", "Commerce"),
                new("manufacturer-synonyms", "Manufacturer synonyms", "/CP/shop/manufacturers_synonyms", "fa-equals", "Commerce"),
                new("crosses", "Crosses", "/CP/shop/crosses", "fa-random", "Commerce"),
                new("demand-countries", "Demand countries", "/CP/shop/demand_countries", "fa-globe-africa", "Commerce"),
                new("search-tabs", "Search tabs", "/CP/shop/taby-poiska", "fa-folder", "Commerce"),
                new("prices-send", "Send prices", "/CP/shop/prices_send", "fa-paper-plane", "Commerce"),
                new("downloadable-price-lists", "Downloadable price lists", "/CP/shop/prices/prajs-listy-dlya-skachivaniya", "fa-file-csv", "Commerce"),
                new("online-kassy", "Online cash registers", "/CP/shop/onlajn-kassy", "fa-receipt", "Commerce"),
                new("returns-manager", "Returns", "/CP/shop/returns-manager", "fa-arrow-left", "Commerce"),
                new("cash-book", "Cash book", "/CP/shop/cash", "fa-book", "Commerce"),
                new("system-requests", "System requests", "/CP/requests", "fa-bolt", "Commerce"),
                new("product-filters", "Product filters", "/CP/shop/filter", "fa-filter", "Commerce"),
            ];
        }

        if (label.Equals("Channels", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("channels", "Channels", "/CP/shop/channels/channels", "fa-plug", "Channels"),
                new("channels-guide", "Channels guide", "/CP/shop/channels/guide", "fa-book", "Channels"),
            ];
        }

        if (label.Equals("Payments", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("payments", "Payments", "/CP/shop/payments/payments", "fa-credit-card", "Payments"),
                new("payments-guide", "Payments guide", "/CP/shop/payments/payments/guide", "fa-book", "Payments"),
            ];
        }

        if (label.Equals("Logistics", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("logistics-hub", "Logistics", "/CP/shop/logistics", "fa-th-large", "Logistics"),
                new("carriers", "Carriers", "/CP/shop/logistics/carriers", "fa-shipping-fast", "Logistics"),
                new("logistics-guide", "Logistics guide", "/CP/shop/logistics/guide", "fa-book", "Logistics"),
                new("obtain-methods", "Obtain methods", "/CP/shop/logistics/sposoby-polucheniya", "fa-truck", "Logistics"),
                new("logistics-orders", "Orders", "/CP/shop/orders/orders", "fa-shopping-cart", "Logistics"),
                new("whatsapp-guide", "WhatsApp guide", "/CP/shop/orders/whatsapp-guide", "fab fa-whatsapp", "Logistics"),
            ];
        }

        if (label.Equals("ERP", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("erp-shell", "ERP", "/CP/shop/finance/erp?epc_erp_shell=1", "fa-ship", "ERP"),
                new("erp-guide", "ERP guide", "/CP/shop/finance/erp/guide?epc_erp_shell=1", "fa-book", "ERP"),
                new("custom-shipping-guide", "Custom shipping guide", "/CP/shop/finance/erp/custom-shipping-guide?epc_erp_shell=1", "fa-book", "ERP"),
                new("uae-tax", "UAE tax compliance", "/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", "fa-gavel", "ERP"),
            ];
        }

        if (label.Equals("AI", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("parts-agent-chats", "Parts Agent chats", "/CP/shop/parts_agent_chats", "fa-robot", "AI"),
            ];
        }

        if (label.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("procurement", "Procurement", "/CP/shop/procurement/procurement", "fa-truck-loading", "Purchase"),
            ];
        }

        if (label.Equals("Customers", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("customer-mgmt", "Customer management", "/CP/shop/customer_mgmt/customer_mgmt", "fa-address-book", "Customers"),
            ];
        }

        if (label.Equals("Marketing", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("marketing", "Marketing", "/CP/shop/marketing/marketing", "fa-bullhorn", "Marketing"),
            ];
        }

        if (label.Equals("Portal", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new("industry-settings", "Industry settings", "/CP/control/portal/industry_settings", "fa-sliders-h", "Portal"),
                new("auto-price", "Auto price", "/CP/control/portal/epc_auto_price_engine?tab=discover", "fa-magic", "Portal"),
                new("pos-terminal", "POS terminal", "/CP/shop/pos/terminal", "fa-cash-register", "Portal"),
                new("page-builder", "Visual page editor", "/CP/control/portal/epc_visual_page_editor", "fa-magic", "Portal"),
                new("social-hub", "Social media hub", "/CP/control/portal/epc_social_media_hub", "fa-share-alt", "Portal"),
                new("marketing-broadcast", "Marketing broadcast", "/CP/control/portal/epc_marketing_broadcast", "fa-bullhorn", "Portal"),
            ];
        }

        return [];
    }

    /// <summary>PHP leftover groups whose <c>control_items</c> we pin into tenant chrome.</summary>
    public static readonly IReadOnlyList<string> PhpLegacyParityGroupLabels =
    [
        "Commerce", "Customers", "Documents", "ERP", "Purchase", "Channels", "Logistics",
        "System", "Catalogue", "Content", "Users", "Modules",
        "AI", "Marketing", "Payments", "Portal",
    ];

    private static IReadOnlyDictionary<string, HashSet<string>>? _legacyNavKeyGroups;

    /// <summary>
    /// ASP.NET rewrite of a leftover PHP href → the topnav group(s) that own it.
    /// Shared leftovers (OMS orders in Commerce + Logistics) list both owners.
    /// </summary>
    public static IReadOnlyDictionary<string, HashSet<string>> PhpLegacyNavKeyGroups()
    {
        if (_legacyNavKeyGroups is not null)
        {
            return _legacyNavKeyGroups;
        }

        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupLabel in PhpLegacyParityGroupLabels)
        {
            foreach (var link in PhpLegacyGroupParityLinks(groupLabel))
            {
                var key = PhpSurfaceLinkMap.AspNetPrimaryHref(link.Href);
                if (string.IsNullOrWhiteSpace(key)
                    || key.Equals("/cp", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("/erp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!map.TryGetValue(key, out var owners))
                {
                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[key] = owners;
                }

                owners.Add(groupLabel);
            }
        }

        _legacyNavKeyGroups = map;
        return map;
    }

    public static bool PhpLegacyAllowsHrefInGroup(string? href, string label)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return true;
        }

        var key = PhpSurfaceLinkMap.AspNetPrimaryHref(href);
        if (!PhpLegacyNavKeyGroups().TryGetValue(key, out var owners) || owners.Count == 0)
        {
            return true;
        }

        return owners.Contains(label);
    }

    public static bool IsPhpUsersGroupHref(string? href)
    {
        var h = href ?? string.Empty;
        return h.Contains("/users/usergroups", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/users/usermanager", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/users/polya-registracii", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/users/registracionnye", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/users/customer_approvals", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/cp/users-app", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/cp/groups-app", StringComparison.OrdinalIgnoreCase)
               || h.Contains("user-groups", StringComparison.OrdinalIgnoreCase)
               || h.Contains("user-manager", StringComparison.OrdinalIgnoreCase)
               || h.Contains("registration-fields", StringComparison.OrdinalIgnoreCase)
               || h.Contains("registration-options", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPhpCatalogueHref(string? href)
    {
        var h = href ?? string.Empty;
        return h.Contains("/shop/catalogue", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/cp/product-catalogue", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/shop/logistics/stock", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/erp/inventory-stock", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/shop/perenos-dannyx", StringComparison.OrdinalIgnoreCase)
               || h.Contains("/cp/data-transfer", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPhpDocumentsHref(string? href)
    {
        var h = href ?? string.Empty;
        return h.Contains("document_control", StringComparison.OrdinalIgnoreCase)
               || h.Contains("document-control", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ERP topnav: categories → area columns → tabs (mirrors <c>epc_erp_render_top_nav</c>).
    /// No artificial tab caps — every catalogued tab appears under its area column.
    /// </summary>
    public static IReadOnlyList<MegaGroup> ErpTopnav()
    {
        var areasById = PhpModuleCatalog.ErpAreas.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

        return PhpModuleCatalog.ErpCategories.Select(cat =>
        {
            var areaIds = ErpCategoryAreas.TryGetValue(cat.Id, out var mapped)
                ? mapped
                : [ExtractQuery(cat.Href, "area") ?? "overview"];

            var columns = new List<MegaAreaColumn>();
            var allTabs = new List<PhpModuleCatalog.ModuleLink>();

            foreach (var areaId in areaIds)
            {
                var tabs = PhpModuleCatalog.ErpTabs
                    .Where(t => string.Equals(t.Group, areaId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (tabs.Count == 0)
                {
                    continue;
                }

                areasById.TryGetValue(areaId, out var area);
                columns.Add(new MegaAreaColumn(
                    areaId,
                    area?.Label ?? areaId,
                    area?.Icon ?? "fa-folder-o",
                    tabs));
                allTabs.AddRange(tabs);
            }

            if (columns.Count == 0)
            {
                var fallback = new PhpModuleCatalog.ModuleLink(cat.Id, cat.Label, cat.Href, cat.Icon, cat.Id);
                allTabs.Add(fallback);
                columns.Add(new MegaAreaColumn(cat.Id, cat.Label, cat.Icon, [fallback]));
            }

            var shortLabel = ErpCategoryShort.TryGetValue(cat.Id, out var s) ? s : cat.Label;
            var hubHref = allTabs[0].Href;

            return new MegaGroup(
                cat.Id,
                cat.Label,
                allTabs,
                cat.Icon,
                shortLabel,
                hubHref,
                columns);
        }).ToList();
    }

    /// <summary>
    /// BOS topnav sections with module flyouts from explicit PHP section→module maps
    /// (<c>epc_bos_unified.php</c>).
    /// </summary>
    public static IReadOnlyList<MegaGroup> BosTopnav()
    {
        var modulesById = PhpModuleCatalog.BosModules
            .GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var groups = new List<MegaGroup>();
        foreach (var section in PhpModuleCatalog.BosSections)
        {
            var ids = BosSectionModuleIds.TryGetValue(section.Id, out var mapped)
                ? mapped
                : Array.Empty<string>();

            var links = new List<PhpModuleCatalog.ModuleLink>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                if (!seen.Add(id))
                {
                    continue;
                }

                if (modulesById.TryGetValue(id, out var mod))
                {
                    links.Add(mod);
                }
            }

            if (links.Count == 0)
            {
                links.Add(new PhpModuleCatalog.ModuleLink(
                    section.Id, section.Label, section.Href, section.Icon, section.Id));
            }

            var icon = BosSectionIcons.TryGetValue(section.Id, out var ic) ? ic : (section.Icon ?? "fa-th-large");
            groups.Add(new MegaGroup(
                section.Id,
                section.Label,
                links,
                icon,
                section.Label,
                links[0].Href,
                [
                    new MegaAreaColumn(section.Id, "Modules", icon, links)
                ]));
        }

        return groups;
    }

    /// <summary>Structural CSS selectors Batch 2 desktop chrome must emit (probe / tests).</summary>
    public static IReadOnlyList<string> RequiredStructuralSelectors(string surface)
        => surface.Trim().ToLowerInvariant() switch
        {
            "cp" => ["#header", ".epc-cp-topnav", ".epc-cp-topnav-panel"],
            "erp" =>
            [
                ".epc-erp-topbar", ".epc-erp-topnav", ".epc-erp-topnav-panel",
                ".epc-erp-topnav-cols", ".epc-erp-topnav-col", ".epc-erp-topnav-panel-hub"
            ],
            "bos" =>
            [
                ".bos-topnav", ".bos-main", ".bos-topnav__panel",
                ".bos-topnav__panel-hub", ".bos-topnav__cols"
            ],
            "storefront" =>
            [
                ".top-menu-line", ".logo-line", ".schearch-line",
                ".header_search_form_1", ".header_search_form_attr", "#footer-widgets", "#header"
            ],
            _ => []
        };

    private static bool MatchesGroup(PhpModuleCatalog.ModuleLink f, string label, string href)
    {
        var g = f.Group ?? "";
        var linkHref = f.Href ?? string.Empty;

        // PHP leftover items stay in their DB group (offices → Commerce, stock → Catalogue).
        if (!PhpLegacyAllowsHrefInGroup(linkHref, label))
        {
            return false;
        }

        // Explicit brochure category map (Shop / OMS → Commerce). Do not use loose
        // Label.Contains("Commerce") — that incorrectly matched ERP "Retail and commerce".
        if (CpNavCategoryMap.TryGetValue(label, out var categories))
        {
            foreach (var cat in categories)
            {
                if (g.Equals(cat, StringComparison.OrdinalIgnoreCase))
                {
                    if (label.Equals("Customers", StringComparison.OrdinalIgnoreCase)
                        && IsPhpUsersGroupHref(linkHref))
                    {
                        return false;
                    }

                    return true;
                }
            }

            // Commerce also accepts any client shop/orders OMS deeplink by href.
            if (label.Equals("Commerce", StringComparison.OrdinalIgnoreCase)
                && (linkHref.Contains("/shop/orders", StringComparison.OrdinalIgnoreCase)
                    || linkHref.Contains("/cp/orders", StringComparison.OrdinalIgnoreCase)
                    || linkHref.Contains("/cp/abandoned-carts", StringComparison.OrdinalIgnoreCase)
                    || linkHref.Contains("/cp/returns-rma", StringComparison.OrdinalIgnoreCase)
                    || linkHref.Contains("/cp/quote-requests", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (label.Equals("Catalogue", StringComparison.OrdinalIgnoreCase)
                && IsPhpCatalogueHref(linkHref))
            {
                return true;
            }

            if (label.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                return IsPhpUsersGroupHref(linkHref);
            }

            if (label.Equals("Customers", StringComparison.OrdinalIgnoreCase)
                && g.Equals("Customers / CRM", StringComparison.OrdinalIgnoreCase))
            {
                return !IsPhpUsersGroupHref(linkHref);
            }

            if (label.Equals("Documents", StringComparison.OrdinalIgnoreCase)
                && IsPhpDocumentsHref(linkHref))
            {
                return true;
            }

            return false;
        }

        if (g.Contains(label, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (href.Contains("/portal", StringComparison.OrdinalIgnoreCase)
            && g.Contains("Portal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string? ExtractQuery(string href, string key)
    {
        var idx = href.IndexOf('?', StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        foreach (var part in href[(idx + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        return null;
    }

    private static string Slug(string s)
        => string.Concat(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'))
            .Trim('-');
}
