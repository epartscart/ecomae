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
        IReadOnlyList<MegaAreaColumn>? Columns = null);

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

    /// <summary>CP topnav groups ≈ epc_cp_build_nav_tabs — multi-column mega panels (no artificial cap).</summary>
    public static IReadOnlyList<MegaGroup> ControlPanelTopnav()
    {
        var groups = new List<MegaGroup>();
        foreach (var nav in LegacyChromeNavCatalog.ControlPanel)
        {
            var id = Slug(nav.Label);
            var links = PhpModuleCatalog.CpBrochureFeatures
                .Where(f => MatchesGroup(f, nav.Label, nav.Href))
                .ToList();
            if (links.Count == 0)
            {
                links.Add(new PhpModuleCatalog.ModuleLink(id, nav.Label, nav.Href, null, nav.Label));
                foreach (var qa in LegacyChromeNavCatalog.ControlPanelQuickActions.Take(6))
                {
                    links.Add(new PhpModuleCatalog.ModuleLink(Slug(qa.Label), qa.Label, qa.Href, null, nav.Label));
                }
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

            groups.Add(new MegaGroup(
                id,
                nav.Label,
                links,
                CpGroupIcon(id),
                nav.Label,
                links[0].Href,
                columns));
        }

        return groups;
    }

    private static string CpGroupIcon(string id) => id switch
    {
        "dashboard" or "home" or "overview" => "fa-tachometer",
        "orders" or "commerce" or "shop" => "fa-shopping-cart",
        "catalogue" or "products" or "catalog" => "fa-cube",
        "customers" or "crm" => "fa-users",
        "logistics" or "shipping" => "fa-truck",
        "marketing" => "fa-bullhorn",
        "finance" or "payments" => "fa-credit-card",
        "settings" or "setup" or "system" => "fa-cogs",
        "content" or "cms" => "fa-file-text-o",
        _ => "fa-folder-o",
    };

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
            "storefront" => ["#header-full-top", ".header_search_form_1", "#header"],
            _ => []
        };

    private static bool MatchesGroup(PhpModuleCatalog.ModuleLink f, string label, string href)
    {
        var g = f.Group ?? "";
        if (g.Contains(label, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var key = label.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? label;
        if (g.Contains(key, StringComparison.OrdinalIgnoreCase) || f.Label.Contains(key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (href.Contains("/portal", StringComparison.OrdinalIgnoreCase)
            && g.Contains("Portal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (href.Contains("/shop/orders", StringComparison.OrdinalIgnoreCase)
            && (g.Contains("Order", StringComparison.OrdinalIgnoreCase) || g.Contains("Commerce", StringComparison.OrdinalIgnoreCase)))
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
