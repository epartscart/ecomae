using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp;

/// <summary>Row of PHP <c>control_groups</c>.</summary>
public sealed record CpNavRawGroup(int Id, string Caption, int Order);

/// <summary>Row of PHP <c>control_items</c> (URL still carries the <c>&lt;backend&gt;</c> placeholder).</summary>
public sealed record CpNavRawItem(
    int Id,
    int GroupId,
    string Caption,
    string Url,
    int Order,
    string Icon,
    bool ShowAnyway);

/// <summary>
/// Per-request visibility policy — the inputs PHP <c>epc_cp_build_nav_tabs</c> /
/// <c>epc_portal_cp_item_visible_enhanced</c> read from host, session, site settings and feature flags.
/// </summary>
public sealed record CpNavPolicy(
    bool IsSuperHost,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> EnabledPacks,
    IReadOnlySet<int> HiddenGroups,
    IReadOnlySet<int> HiddenItems,
    IReadOnlySet<string> DisabledFeatures,
    Func<string, bool> AclAllows)
{
    public static readonly IReadOnlyCollection<string> TenantDefaultPacks = ["core"];

    /// <summary>Super-CP operator: every pack, no tenant hides, ACL bypass (PHP <c>$epcCpSuperAdmin</c>).</summary>
    public static CpNavPolicy SuperOperator() => new(
        IsSuperHost: true,
        IsSuperAdmin: true,
        EnabledPacks: CpNavTree.AllPackKeys,
        HiddenGroups: new HashSet<int>(),
        HiddenItems: new HashSet<int>(),
        DisabledFeatures: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        AclAllows: static _ => true);

    /// <summary>Tenant host with the given packs and no per-item ACL knowledge (snapshot mode).</summary>
    public static CpNavPolicy Tenant(IReadOnlyCollection<string>? packs = null, Func<string, bool>? acl = null) => new(
        IsSuperHost: false,
        IsSuperAdmin: false,
        EnabledPacks: packs ?? CpNavTree.AllTenantPackKeys,
        HiddenGroups: new HashSet<int>(),
        HiddenItems: new HashSet<int>(),
        DisabledFeatures: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        AclAllows: acl ?? (static _ => true));
}

public sealed record CpNavItem(
    int Id,
    string Caption,
    string Label,
    string Url,
    string Icon,
    bool ShowAnyway);

public sealed record CpNavGroup(
    string Key,
    string CaptionKey,
    string Caption,
    string Short,
    string Subtitle,
    string Icon,
    string Tier,
    IReadOnlyList<CpNavItem> Items)
{
    public bool IsAdvanced => Tier == "advanced";
}

/// <summary>
/// Port of PHP <c>content/general_pages/epc_cp_nav_tree.php</c> + <c>epc_portal_cp_menu.php</c>:
/// DB-driven CP menu (groups → items), ACL + portal/tenant/feature visibility, URL dedupe,
/// primary → remaining → advanced ordering, icons/short labels/subtitles. No invented links.
/// </summary>
public static class CpNavTree
{
    public const string BackendDir = "cp";

    public static readonly IReadOnlyList<string> PrimaryGroupKeys =
    [
        "744",
        "epc_cp_group_customers",
        "epc_cp_group_documents",
        "epc_cp_group_erp",
        "epc_cp_group_procurement",
        "epc_cp_group_channels",
        "epc_cp_group_logistics",
    ];

    public static readonly IReadOnlyList<string> AdvancedGroupKeys =
    [
        "epc_cp_group_ai",
        "epc_cp_group_marketing",
        "epc_cp_group_payments",
        "epc_cp_group_integrations",
        "epc_cp_group_portal",
        "epc_cp_group_tenant_hub",
        "epc_cp_group_operator",
    ];

    public static readonly IReadOnlyDictionary<string, string> GroupIcons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["744"] = "fa-shopping-cart",
        ["741"] = "fa-users",
        ["epc_cp_group_customers"] = "fa-users",
        ["epc_cp_group_documents"] = "fa-file-text-o",
        ["epc_cp_group_erp"] = "fa-university",
        ["epc_cp_group_procurement"] = "fa-truck",
        ["epc_cp_group_channels"] = "fa-share-alt",
        ["epc_cp_group_logistics"] = "fa-cubes",
        ["epc_cp_group_ai"] = "fa-magic",
        ["epc_cp_group_marketing"] = "fa-bullhorn",
        ["epc_cp_group_payments"] = "fa-credit-card",
        ["epc_cp_group_integrations"] = "fa-plug",
        ["epc_cp_group_portal"] = "fa-cog",
        ["epc_cp_group_tenant_hub"] = "fa-sitemap",
        ["epc_cp_group_operator"] = "fa-shield",
    };

    public static readonly IReadOnlyDictionary<string, string> GroupShortLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["744"] = "Commerce",
        ["741"] = "Users",
        ["epc_cp_group_customers"] = "Customers",
        ["epc_cp_group_documents"] = "Documents",
        ["epc_cp_group_erp"] = "ERP",
        ["epc_cp_group_procurement"] = "Purchase",
        ["epc_cp_group_channels"] = "Channels",
        ["epc_cp_group_logistics"] = "Logistics",
        ["epc_cp_group_ai"] = "AI",
        ["epc_cp_group_marketing"] = "Marketing",
        ["epc_cp_group_payments"] = "Payments",
        ["epc_cp_group_integrations"] = "Integrations",
        ["epc_cp_group_portal"] = "Portal",
        ["epc_cp_group_tenant_hub"] = "Platform",
        ["epc_cp_group_operator"] = "Operator",
    };

    public static readonly IReadOnlyDictionary<string, string> GroupSubtitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["744"] = "Orders, catalogue & prices",
        ["epc_cp_group_customers"] = "Clients, user accounts & CRM",
        ["epc_cp_group_documents"] = "Invoices & PDFs",
        ["epc_cp_group_erp"] = "Finance, VAT & reports",
        ["epc_cp_group_procurement"] = "Purchasing & suppliers",
        ["epc_cp_group_channels"] = "Marketplaces & feeds",
        ["epc_cp_group_logistics"] = "Shipping & delivery",
        ["epc_cp_group_payments"] = "Cards & online pay",
        ["epc_cp_group_marketing"] = "Campaigns & social",
        ["epc_cp_group_ai"] = "Pricing & assistants",
        ["epc_cp_group_integrations"] = "epc_cp_group_integrations_desc",
        ["epc_cp_group_portal"] = "Site & industry settings",
        ["epc_cp_group_tenant_hub"] = "Platform tools",
        ["epc_cp_group_operator"] = "epc_cp_group_operator_desc",
    };

    /// <summary>PHP <c>epc_portal_cp_pack_routes()</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> PackRoutes = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["core"] =
        [
            "/content/", "/users/", "/control/", "/templates", "/modules", "/plugins",
            "/shop/modul-pechati", "/shop/print", "/cp-guideline", "/guideline", "/control/portal",
        ],
        ["commerce"] =
        [
            "/shop/orders", "/shop/cart", "/shop/catalogue", "/shop/payments", "/shop/channels",
            "/shop/marketing", "/shop/customer", "/customers", "/shop/bulk", "/shop/pos",
            "/shop/accessories", "/shop/quote-requests", "/shop/returns-manager",
        ],
        ["auto_parts"] =
        [
            "/shop/prices", "/shop/crosses", "/shop/procurement", "/shop/demand",
            "/shop/parts", "/shop/docpart", "/shop/price-management", "/shop/logistics",
            "/shop/vehicle", "/shop/product-family", "/shop/umapi", "/shop/agent",
            "/shop/accessories", "/shop/workshop", "/shop/statistics",
            "/shop/eparts-cata", "/shop/eparts-mod", "/shop/manufacturers_synonyms", "/shop/quote-requests",
        ],
        ["logistics"] = ["/shop/logistics", "/shop/storages", "/shop/offices", "/shop/warehouse", "/shop/geo"],
        ["catalogue"] = ["/shop/catalogue", "/shop/bulk-upload", "/shop/product-family", "/shop/accessories"],
        ["professional"] =
        [
            "/shop/finance", "/shop/erp", "/shop/customer_mgmt", "/shop/einvoice",
            "/shop/customer-approval", "/shop/demand_countries", "/shop/document_control",
            "/shop/returns-manager", "/shop/statistics",
        ],
        ["crm"] = [],
        ["erp"] = ["/shop/finance", "/shop/erp", "/shop/einvoice", "/shop/modul-pechati", "/shop/document_control"],
        ["marketing"] = ["/shop/marketing"],
        ["tax_advisory"] =
        [
            "/shop/finance", "/shop/erp", "/shop/customer_mgmt", "/shop/einvoice",
            "/shop/marketing", "/shop/payments", "/shop/print",
        ],
        ["super_platform"] = ["/shop/tenant_hub", "/control/portal"],
    };

    public static readonly IReadOnlyCollection<string> AllPackKeys = PackRoutes.Keys.ToArray();

    public static readonly IReadOnlyCollection<string> AllTenantPackKeys =
        PackRoutes.Keys.Where(k => k != "super_platform").ToArray();

    /// <summary>PHP <c>epc_integrations_catalog()[*]['menu_patterns']</c> — feature key → URL needles.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> FeatureMenuPatterns = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["whatsapp"] = ["whatsapp"],
        ["payment_gateways"] = ["/shop/payments/"],
        ["pos"] = ["/shop/pos/"],
        ["tax_toolkit"] = ["epc_tax_toolkit", "uae-tax-compliance"],
        ["custom_shipping"] = ["custom_shipping", "custom-shipping"],
        ["social_media_hub"] = ["epc_social_media_hub"],
        ["marketing_broadcast"] = ["epc_marketing_broadcast", "/shop/marketing/"],
        ["web_tracker"] = ["epc_web_tracker"],
        ["visual_page_editor"] = ["epc_visual_page_editor"],
        ["auto_price_ai"] = ["epc_auto_price", "/shop/parts_agent"],
        ["parts_agent"] = ["parts_agent"],
        ["api_integrations"] = ["epc_api_clients"],
        ["power_bi"] = ["epc_power_bi", "powerbi", "epc_power_bi_guide"],
        ["tenant_registry"] = ["tenant_hub"],
    };

    /// <summary>PHP <c>epc_cp_system_menu_hidden_url_patterns()</c>.</summary>
    public static readonly IReadOnlyList<string> LegacyHiddenUrlPatterns =
    [
        "/control/o-programme",
        "/control/obnovleniya",
        "/control/izmeneniya",
        "/content/usefull/changes_fc",
        "changes_fc.php",
        "/version_control/about_program",
        "/version_control/updates",
    ];

    /// <summary>PHP <c>epc_cp_system_menu_hidden_labels()</c>.</summary>
    public static readonly IReadOnlyList<string> LegacyHiddenLabels =
    [
        "about program", "docpart changes", "updates",
        "история изменений", "изменения docpart", "изменения", "о программе", "обновления",
    ];

    private static readonly string[] SuperOnlyNeedles = ["epc_super_cp_", "epc_pos_tenant_manage"];

    private static readonly Regex NamedKeyRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex DigitsRegex = new("^\\d+$", RegexOptions.Compiled);
    private static readonly Regex GeneratedKeyRegex = new("^[0-9]+_[0-9]+_", RegexOptions.Compiled);
    private static readonly Regex LettersRegex = new("[A-Za-z\\u0400-\\u04FF]", RegexOptions.Compiled);
    private static readonly Regex NonSlugRegex = new("[^a-z0-9_-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SpacesRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex NoisyPrefixRegex = new("^(epc|oms|crm)_?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Replace the <c>&lt;backend&gt;</c> placeholder (PHP <c>$DP_Config-&gt;backend_dir</c>).</summary>
    public static string ResolveUrl(string url) =>
        (url ?? string.Empty).Replace("<backend>", BackendDir, StringComparison.Ordinal);

    /// <summary>PHP <c>epc_cp_acl_content_url</c>: strip query, backend prefix and leading slash → <c>content.url</c>.</summary>
    public static string ContentUrl(string resolvedUrl)
    {
        var url = (resolvedUrl ?? string.Empty).Split('?', 2)[0];
        url = url.Replace("/" + BackendDir + "/", string.Empty, StringComparison.Ordinal);
        return url.TrimStart('/');
    }

    /// <summary>PHP <c>epc_portal_cp_menu_dedupe_items</c> key: lower-case path without query.</summary>
    public static string DedupeKey(string resolvedUrl) =>
        (resolvedUrl ?? string.Empty).Split('?', 2)[0].ToLowerInvariant();

    public static bool IsLegacyHidden(string resolvedUrl, string caption)
    {
        var url = DedupeKey(resolvedUrl);
        foreach (var pat in LegacyHiddenUrlPatterns)
        {
            if (url.Contains(pat, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var label = (caption ?? string.Empty).Trim().ToLowerInvariant();
        return label.Length > 0 && LegacyHiddenLabels.Contains(label, StringComparer.Ordinal);
    }

    /// <summary>PHP <c>epc_portal_cp_item_visible</c> (pack routes) — operator bypass, portal/pos always, packs, bare /cp.</summary>
    public static bool PackVisible(string resolvedUrl, CpNavPolicy policy)
    {
        if (policy.IsSuperHost)
        {
            return true;
        }

        var url = resolvedUrl.ToLowerInvariant();
        if (url.Contains("control/portal", StringComparison.Ordinal) || url.Contains("/shop/pos", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var pack in policy.EnabledPacks)
        {
            if (!PackRoutes.TryGetValue(pack, out var prefixes))
            {
                continue;
            }

            foreach (var prefix in prefixes)
            {
                if (url.Contains(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return url.Contains("/cp", StringComparison.Ordinal) && url.Count(c => c == '/') <= 2;
    }

    /// <summary>PHP <c>epc_integrations_menu_blocked_by_feature</c> — tenant host only.</summary>
    public static bool FeatureBlocked(string resolvedUrl, CpNavPolicy policy)
    {
        if (policy.IsSuperHost || policy.DisabledFeatures.Count == 0)
        {
            return false;
        }

        var url = DedupeKey(resolvedUrl);
        foreach (var feature in policy.DisabledFeatures)
        {
            if (!FeatureMenuPatterns.TryGetValue(feature, out var patterns))
            {
                continue;
            }

            foreach (var pattern in patterns)
            {
                if (url.Contains(pattern.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>PHP <c>epc_portal_cp_item_visible_enhanced</c>.</summary>
    public static bool ItemVisible(CpNavRawItem item, string resolvedUrl, CpNavPolicy policy, bool inOperatorGroup)
    {
        var url = resolvedUrl.ToLowerInvariant();
        if (IsLegacyHidden(url, item.Caption))
        {
            return false;
        }

        if (!PackVisible(url, policy))
        {
            return false;
        }

        if (!policy.IsSuperHost && (policy.HiddenItems.Contains(item.Id) || policy.HiddenGroups.Contains(item.GroupId)))
        {
            return false;
        }

        if (FeatureBlocked(url, policy))
        {
            return false;
        }

        if (!policy.IsSuperHost && url.Contains("epc_tenant_features", StringComparison.Ordinal))
        {
            return false;
        }

        if (policy.IsSuperHost && url.Contains("epc_tenant_email_settings", StringComparison.Ordinal))
        {
            return false;
        }

        if (!policy.IsSuperHost)
        {
            foreach (var needle in SuperOnlyNeedles)
            {
                if (url.Contains(needle, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            if (inOperatorGroup)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>PHP <c>epc_portal_cp_menu_resolve_label</c> without the DB lookups (caller supplies translations).</summary>
    public static string ResolveLabel(string caption, string url, Func<string, string?> translate)
    {
        caption = (caption ?? string.Empty).Trim();
        url = (url ?? string.Empty).Trim();
        var label = string.Empty;

        if (caption.Length > 0)
        {
            var tr = translate(caption);
            if (!string.IsNullOrWhiteSpace(tr) && tr.Trim() != caption)
            {
                label = tr.Trim();
            }

            if (label.Length == 0
                && LettersRegex.IsMatch(caption)
                && !DigitsRegex.IsMatch(caption)
                && !GeneratedKeyRegex.IsMatch(caption)
                && !caption.StartsWith("epc_", StringComparison.Ordinal))
            {
                label = caption;
            }
        }

        if (label.Length == 0
            || DigitsRegex.IsMatch(label)
            || GeneratedKeyRegex.IsMatch(label)
            || (label.StartsWith("epc_", StringComparison.Ordinal) && !label.Contains(' ')))
        {
            var fromUrl = HumanizeUrl(url);
            if (fromUrl.Length > 0)
            {
                label = fromUrl;
            }
            else if (caption.Length > 0 && !DigitsRegex.IsMatch(caption) && !GeneratedKeyRegex.IsMatch(caption))
            {
                label = HumanizeKey(caption);
            }
            else
            {
                label = "Menu item";
            }
        }

        return label;
    }

    /// <summary>PHP <c>epc_portal_cp_menu_humanize_url</c>: shop/orders/orders → Orders.</summary>
    public static string HumanizeUrl(string url)
    {
        url = (url ?? string.Empty).Replace('\\', '/');
        url = Regex.Replace(url, "^/<backend>/", string.Empty);
        url = Regex.Replace(url, "^/?(cp|backend)/", string.Empty);
        url = url.Split('?', 2)[0].Trim('/');
        if (url.Length == 0)
        {
            return string.Empty;
        }

        var part = url.Split('/').Last();
        part = NonSlugRegex.Replace(part, " ");
        part = SpacesRegex.Replace(part, " ").Trim();
        if (part.Length == 0)
        {
            return string.Empty;
        }

        part = NoisyPrefixRegex.Replace(part, string.Empty);
        return UcWords(part.Replace('-', ' ').Replace('_', ' ').ToLowerInvariant());
    }

    /// <summary>PHP <c>epc_portal_cp_menu_humanize_key</c>.</summary>
    public static string HumanizeKey(string key)
    {
        key = (key ?? string.Empty).Trim();
        key = Regex.Replace(key, "^(epc_|EPC_)", string.Empty);
        key = Regex.Replace(key, "_(cp|group)$", string.Empty, RegexOptions.IgnoreCase);
        key = key.Replace('-', ' ').Replace('_', ' ');
        key = SpacesRegex.Replace(key, " ").Trim();
        return key.Length > 0 ? UcWords(key.ToLowerInvariant()) : "Menu item";
    }

    public static string GroupIcon(string captionKey) =>
        GroupIcons.TryGetValue(captionKey, out var icon) ? icon : "fa-folder-o";

    /// <summary>PHP <c>epc_cp_nav_group_short</c> — map or 14-char truncation of the caption.</summary>
    public static string GroupShort(string captionKey, string fallback)
    {
        if (GroupShortLabels.TryGetValue(captionKey, out var s))
        {
            return s;
        }

        fallback = (fallback ?? string.Empty).Trim();
        if (fallback.Length == 0)
        {
            return "Module";
        }

        return fallback.Length > 14 ? fallback[..13].TrimEnd() + "…" : fallback;
    }

    public static string GroupSubtitle(string captionKey, Func<string, string?> translate)
    {
        if (!GroupSubtitles.TryGetValue(captionKey, out var sub))
        {
            return string.Empty;
        }

        if (NamedKeyRegex.IsMatch(sub) && sub.StartsWith("epc_", StringComparison.Ordinal))
        {
            var tr = translate(sub);
            return string.IsNullOrWhiteSpace(tr) ? HumanizeKey(sub) : tr.Trim();
        }

        return sub;
    }

    /// <summary>
    /// PHP <c>epc_cp_build_nav_tabs</c>: ACL/show_anyway gate, enhanced visibility, dedupe by path,
    /// primary keys → remaining non-advanced → advanced keys → remaining advanced; empty groups dropped.
    /// </summary>
    public static IReadOnlyList<CpNavGroup> Build(
        IReadOnlyList<CpNavRawGroup> groups,
        IReadOnlyList<CpNavRawItem> items,
        CpNavPolicy policy,
        Func<string, string?> translate)
    {
        var operatorGroupId = groups
            .Where(g => g.Caption == "epc_cp_group_operator")
            .Select(g => g.Id)
            .FirstOrDefault();

        var tabs = new List<(CpNavRawGroup Group, List<CpNavItem> Items)>();
        var byGroup = new Dictionary<int, List<CpNavItem>>();
        foreach (var group in groups.OrderBy(g => g.Order).ThenBy(g => g.Id))
        {
            var list = new List<CpNavItem>();
            tabs.Add((group, list));
            byGroup[group.Id] = list;
        }

        foreach (var item in items.OrderBy(i => i.Order).ThenBy(i => i.Id))
        {
            var url = ResolveUrl(item.Url);
            var aclOk = policy.IsSuperAdmin || policy.AclAllows(ContentUrl(url));
            if (!aclOk && !item.ShowAnyway)
            {
                continue;
            }

            if (!ItemVisible(item, url, policy, operatorGroupId > 0 && item.GroupId == operatorGroupId))
            {
                continue;
            }

            if (!byGroup.TryGetValue(item.GroupId, out var list))
            {
                continue;
            }

            list.Add(new CpNavItem(
                item.Id,
                item.Caption,
                ResolveLabel(item.Caption, url, translate),
                url,
                item.Icon.Trim(),
                item.ShowAnyway));
        }

        for (var i = 0; i < tabs.Count; i++)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var deduped = new List<CpNavItem>();
            foreach (var it in tabs[i].Items)
            {
                var key = DedupeKey(it.Url);
                if (key.Length == 0 || !seen.Add(key))
                {
                    continue;
                }

                deduped.Add(it);
            }

            tabs[i].Items.Clear();
            tabs[i].Items.AddRange(deduped);
        }

        var ordered = new List<CpNavGroup>();
        var rendered = new HashSet<int>();

        void Push((CpNavRawGroup Group, List<CpNavItem> Items) tab, string tier)
        {
            if (rendered.Contains(tab.Group.Id) || tab.Items.Count == 0)
            {
                return;
            }

            var ck = tab.Group.Caption;
            var caption = ResolveLabel(ck, string.Empty, translate);
            ordered.Add(new CpNavGroup(
                tab.Group.Id.ToString(CultureInfo.InvariantCulture),
                ck,
                caption,
                GroupShort(ck, caption),
                GroupSubtitle(ck, translate),
                GroupIcon(ck),
                tier,
                tab.Items.ToArray()));
            rendered.Add(tab.Group.Id);
        }

        foreach (var pkey in PrimaryGroupKeys)
        {
            var match = tabs.FirstOrDefault(t => t.Group.Caption == pkey || t.Group.Id.ToString(CultureInfo.InvariantCulture) == pkey);
            if (match.Group is not null)
            {
                Push(match, "primary");
            }
        }

        foreach (var tab in tabs)
        {
            if (rendered.Contains(tab.Group.Id) || AdvancedGroupKeys.Contains(tab.Group.Caption, StringComparer.Ordinal))
            {
                continue;
            }

            Push(tab, "primary");
        }

        var advanced = tabs.Where(t => !rendered.Contains(t.Group.Id) && t.Items.Count > 0).ToList();
        foreach (var akey in AdvancedGroupKeys)
        {
            var idx = advanced.FindIndex(t => t.Group.Caption == akey || t.Group.Id.ToString(CultureInfo.InvariantCulture) == akey);
            if (idx >= 0)
            {
                Push(advanced[idx], "advanced");
                advanced.RemoveAt(idx);
            }
        }

        foreach (var tab in advanced)
        {
            Push(tab, "advanced");
        }

        return ordered;
    }

    /// <summary>PHP top-nav chunking: columns of eight links.</summary>
    public static IReadOnlyList<IReadOnlyList<CpNavItem>> Chunk(IReadOnlyList<CpNavItem> items, int size = 8)
    {
        var cols = new List<IReadOnlyList<CpNavItem>>();
        for (var i = 0; i < items.Count; i += size)
        {
            cols.Add(items.Skip(i).Take(size).ToArray());
        }

        return cols;
    }

    private static string UcWords(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
        }

        return string.Join(' ', parts);
    }
}
