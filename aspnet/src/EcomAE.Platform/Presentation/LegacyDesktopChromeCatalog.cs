using System.Linq;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Mega-panel groupings that mirror PHP topnav structures for Batch 2 desktop chrome.
/// Links remain PHP deeplinks / hybrid workspace until vertical slices are ported.
/// </summary>
public static class LegacyDesktopChromeCatalog
{
    public sealed record MegaGroup(string Id, string Label, IReadOnlyList<PhpModuleCatalog.ModuleLink> Links);

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

    /// <summary>Keyword buckets for BOS topnav sections (modules have no section field in catalog).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> BosSectionKeywords =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["fleet-command"] = ["command_center", "fleet_cp", "fleet_erp", "platform_health", "failover"],
            ["tenant-operations"] = ["tenant", "isolation", "governance", "customer_board", "industry"],
            ["commerce"] =
            [
                "order", "fulfillment", "multivendor", "pos", "credit_limit", "promotions", "subscription"
            ],
            ["catalogue"] = ["catalogue", "catalog", "stock", "price", "cross", "import", "pim"],
            ["logistics"] = ["logistics", "warehouse", "shipping", "fulfillment_queue", "landed"],
            ["marketing"] = ["marketing", "promo", "pixel", "tracker", "sms"],
            ["professional"] = ["ai_", "nl_", "power_bi", "copilot", "document_vault", "sandbox"],
            ["erp-finance"] =
            [
                "finance", "payroll", "currency", "collections", "warranty", "gl", "invoice", "po_approval",
                "order_erp"
            ],
            ["auto-parts"] = ["auto_parts", "parts", "vin", "umapi", "laximo", "article"],
            ["tax-advisory"] = ["tax", "soc2", "compliance", "advisory"],
            ["platform"] =
            [
                "api_clients", "config", "integrations", "auth", "license", "audit_log", "modern_auth",
                "industry_packs"
            ],
        };

    /// <summary>CP topnav groups ≈ epc_cp_build_nav_tabs short labels + brochure samples.</summary>
    public static IReadOnlyList<MegaGroup> ControlPanelTopnav()
    {
        var groups = new List<MegaGroup>();
        foreach (var nav in LegacyChromeNavCatalog.ControlPanel)
        {
            var id = Slug(nav.Label);
            var links = PhpModuleCatalog.CpBrochureFeatures
                .Where(f => MatchesGroup(f, nav.Label, nav.Href))
                .Take(12)
                .ToList();
            if (links.Count == 0)
            {
                links.Add(new PhpModuleCatalog.ModuleLink(id, nav.Label, nav.Href, null, nav.Label));
                foreach (var qa in LegacyChromeNavCatalog.ControlPanelQuickActions.Take(4))
                {
                    links.Add(new PhpModuleCatalog.ModuleLink(Slug(qa.Label), qa.Label, qa.Href, null, nav.Label));
                }
            }

            groups.Add(new MegaGroup(id, nav.Label, links));
        }

        return groups;
    }

    /// <summary>ERP topnav: categories → tabs for that category's areas (erp_main topnav-only).</summary>
    public static IReadOnlyList<MegaGroup> ErpTopnav()
    {
        return PhpModuleCatalog.ErpCategories.Select(cat =>
        {
            var areaIds = ErpCategoryAreas.TryGetValue(cat.Id, out var mapped)
                ? mapped
                : [ExtractQuery(cat.Href, "area") ?? "overview"];

            var tabs = PhpModuleCatalog.ErpTabs
                .Where(t => areaIds.Contains(t.Group ?? "", StringComparer.OrdinalIgnoreCase))
                .Take(36)
                .ToList();

            if (tabs.Count == 0)
            {
                tabs = PhpModuleCatalog.ErpAreas
                    .Where(a => areaIds.Contains(a.Id, StringComparer.OrdinalIgnoreCase))
                    .Select(a => new PhpModuleCatalog.ModuleLink(a.Id, a.Label, a.Href, a.Icon, cat.Id))
                    .ToList();
            }

            if (tabs.Count == 0)
            {
                tabs =
                [
                    new PhpModuleCatalog.ModuleLink(cat.Id, cat.Label, cat.Href, cat.Icon, cat.Id)
                ];
            }

            return new MegaGroup(cat.Id, cat.Label, tabs);
        }).ToList();
    }

    /// <summary>BOS topnav sections with module flyouts (keyword + remainder fill).</summary>
    public static IReadOnlyList<MegaGroup> BosTopnav()
    {
        var sections = LegacyChromeNavCatalog.Bos;
        var remaining = PhpModuleCatalog.BosModules.ToList();
        var groups = new List<MegaGroup>();

        foreach (var section in sections)
        {
            var slug = Slug(section.Label);
            var keywords = BosSectionKeywords.TryGetValue(slug, out var keys) ? keys : [slug];
            var matched = remaining
                .Where(m => keywords.Any(k =>
                    m.Id.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || m.Label.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || m.Href.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .Take(16)
                .ToList();

            foreach (var m in matched)
            {
                remaining.Remove(m);
            }

            if (matched.Count == 0)
            {
                matched.Add(new PhpModuleCatalog.ModuleLink(slug, section.Label, section.Href, null, section.Label));
            }

            groups.Add(new MegaGroup(slug, section.Label, matched));
        }

        // Append leftovers into Platform so nothing is orphaned from mega-nav.
        if (remaining.Count > 0 && groups.Count > 0)
        {
            var platform = groups[^1];
            var merged = platform.Links.Concat(remaining.Take(24)).ToList();
            groups[^1] = new MegaGroup(platform.Id, platform.Label, merged);
        }

        return groups;
    }

    /// <summary>Structural CSS selectors Batch 2 desktop chrome must emit (probe / tests).</summary>
    public static IReadOnlyList<string> RequiredStructuralSelectors(string surface)
        => surface.Trim().ToLowerInvariant() switch
        {
            "cp" => ["#header", ".epc-cp-topnav", ".epc-cp-topnav-panel"],
            "erp" => [".epc-erp-topbar", ".epc-erp-topnav", ".epc-erp-topnav-panel"],
            "bos" => [".bos-topnav", ".bos-main", ".bos-topnav__panel"],
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
