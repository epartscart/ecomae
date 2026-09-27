using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Cp;

/// <summary>One pin-able CP dashboard shortcut — PHP <c>epc_shortcuts_catalog_cp()</c> row.</summary>
public sealed record CpShortcutCatalogItem(
    string Key,
    string Label,
    string IconClass,
    string Color,
    string Url,
    string Tone)
{
    /// <summary>Icon without the leading <c>fa </c> — PHP tiles print <c>&lt;i class="fa {icon}"&gt;</c>.</summary>
    public string BareIcon => IconClass.StartsWith("fa ", StringComparison.Ordinal) ? IconClass[3..] : IconClass;
}

/// <summary>A rendered shortcut tile — PHP <c>epc_shortcuts_as_tiles()</c> shape.</summary>
public sealed record CpShortcutTile(
    long Id,
    string Key,
    string Label,
    string Icon,
    string Color,
    string Url,
    string Tone);

/// <summary>PHP <c>content/shop/finance/epc_erp_shortcut_icons.php</c> CP catalogue and industry defaults.</summary>
public static class CpShortcutCatalog
{
    private static readonly string[] Tones =
        ["red", "black", "crimson", "stone", "blue", "teal", "amber", "violet", "indigo", "emerald", "rose", "slate"];

    /// <summary>PHP CP dashboard tone overrides (red/black command centre).</summary>
    private static readonly Dictionary<string, string> CpToneMap = new(StringComparer.Ordinal)
    {
        ["orders"] = "red",
        ["catalogue"] = "black",
        ["prices"] = "crimson",
        ["clients"] = "stone",
        ["warehouses"] = "black",
        ["stock"] = "emerald",
        ["procurement"] = "indigo",
        ["erp"] = "red",
        ["documents"] = "stone",
        ["pos"] = "rose",
        ["multivendor"] = "teal",
        ["crosses"] = "blue",
        ["ai_chats"] = "violet",
        ["settings"] = "crimson",
        ["brochure"] = "indigo",
        ["accessories"] = "violet",
    };

    private static readonly string[] CpFallbackTones = ["red", "black", "crimson", "stone"];

    /// <summary>PHP: auto_parts tenants get the parts-trade default strip.</summary>
    public static readonly string[] AutoPartsDefaults =
        ["orders", "prices", "multivendor", "crosses", "procurement", "pos", "erp", "stock"];

    /// <summary>PHP: every other industry gets the generic commerce strip.</summary>
    public static readonly string[] GenericDefaults =
        ["orders", "catalogue", "prices", "clients", "accessories", "erp", "documents", "settings"];

    private static readonly IReadOnlyList<CpShortcutCatalogItem> Items = Build();

    public static IReadOnlyList<CpShortcutCatalogItem> All => Items;

    public static IReadOnlyList<string> DefaultsFor(string? industryCode)
        => string.Equals(industryCode, "auto_parts", StringComparison.OrdinalIgnoreCase)
            ? AutoPartsDefaults
            : GenericDefaults;

    public static CpShortcutCatalogItem? Find(string? key)
        => Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Default tiles shown before anything is persisted (PHP first-visit fallback).</summary>
    public static IReadOnlyList<CpShortcutTile> DefaultTiles(string? industryCode)
    {
        var tiles = new List<CpShortcutTile>();
        foreach (var key in DefaultsFor(industryCode))
        {
            var item = Find(key);
            if (item is not null)
            {
                tiles.Add(new CpShortcutTile(0, item.Key, item.Label, item.BareIcon, item.Color, item.Url, item.Tone));
            }
        }

        return tiles;
    }

    /// <summary>PHP tone cycle applied to persisted rows, then the CP tone overrides.</summary>
    public static string ToneFor(string? key, int index)
    {
        if (!string.IsNullOrEmpty(key) && CpToneMap.TryGetValue(key, out var tone))
        {
            return tone;
        }

        return CpFallbackTones[index % CpFallbackTones.Length];
    }

    public static string CycleTone(int index) => Tones[index % Tones.Length];

    private static IReadOnlyList<CpShortcutCatalogItem> Build()
    {
        // PHP paths resolved through the same map the CP menu uses, so no shortcut can point at an invented route.
        CpShortcutCatalogItem Item(string key, string label, string icon, string color, string phpPath, string tone)
            => new(key, label, "fa " + icon, color, PhpSurfaceLinkMap.MapCpPhpPath("/CP" + phpPath), CpToneMap.GetValueOrDefault(key, tone));

        return
        [
            Item("orders", "Orders (OMS)", "fa-shopping-cart", "#dc2626", "/shop/orders/orders", "red"),
            Item("catalogue", "Catalogue", "fa-th-large", "#2563eb", "/shop/catalogue/products", "blue"),
            Item("sku_media", "SKU photos & specs", "fa-picture-o", "#0f766e", "/shop/catalogue/sku_media", "teal"),
            Item("prices", "Prices", "fa-tags", "#d97706", "/shop/prices", "amber"),
            Item("clients", "Customers", "fa-address-book", "#0d9488", "/shop/customer_mgmt/customer_mgmt", "teal"),
            Item("warehouses", "Warehouses", "fa-building", "#7c3aed", "/shop/logistics/storages", "violet"),
            Item("stock", "Stock", "fa-cubes", "#059669", "/shop/logistics/stock", "emerald"),
            Item("procurement", "Procurement", "fa-truck", "#4f46e5", "/shop/procurement/procurement", "indigo"),
            new("erp", "ERP finance", "fa fa-university", "#e11d48", "/erp", CpToneMap["erp"]),
            new("insights", "Insights", "fa fa-lightbulb-o", "#0f766e", "/erp#epc-insights", "teal"),
            Item("documents", "Documents", "fa-file-text-o", "#64748b", "/shop/document_control/document_control", "slate"),
            Item("pos", "POS terminal", "fa-credit-card", "#db2777", "/shop/pos/terminal", "rose"),
            Item("multivendor", "Multivendor", "fa-handshake-o", "#0891b2", "/shop/prices/multivendor", "teal"),
            Item("crosses", "Crosses", "fa-exchange", "#2563eb", "/shop/crosses", "blue"),
            Item("ai_chats", "AI chats", "fa-comments", "#7c3aed", "/shop/parts_agent_chats", "violet"),
            Item("settings", "Settings", "fa-cog", "#475569", "/control/portal/industry_settings", "slate"),
            Item("brochure", "CP brochure", "fa-book", "#4f46e5", "/control/cp_brochure", "indigo"),
            Item("accessories", "Accessories", "fa-puzzle-piece", "#7c3aed", "/shop/accessories", "violet"),
        ];
    }
}
