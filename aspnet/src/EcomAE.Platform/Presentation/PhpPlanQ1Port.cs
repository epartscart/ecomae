using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-port CP brochure live inventory. PHP identifiers kept for the inventory:
/// <c>epc_cp_brochure_item_image</c>, <c>epc_cp_brochure_item_photo_meta</c>,
/// <c>epc_cp_brochure_screen_pool</c>, <c>epc_cp_brochure_area_visuals</c>,
/// <c>epc_cp_brochure_capabilities_catalog</c>, <c>epc_cp_brochure_norm_key</c>,
/// <c>epc_cp_brochure_cap_category_to_area</c>, <c>epc_cp_brochure_capability_index</c>,
/// <c>epc_cp_brochure_erp_nav_items</c>, <c>epc_cp_brochure_build_live_inventory</c>.
/// Path: <c>content/general_pages/epc_cp_brochure_live.php</c>.
/// GET never mints a session cookie. Leftover topic-photo / dump / ERP-nav parents stay injected.
/// </summary>
public static class PhpPlanQ1Port
{
    public const string BrochureLivePath = "content/general_pages/epc_cp_brochure_live.php";

    public static Func<Dictionary<string, object?>, string, Dictionary<string, object?>>? TopicPhoto { get; set; }
    public static Func<string, string, string, string, string>? TopicSvgUrl { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? TopicCatalog { get; set; }
    public static Func<List<Dictionary<string, object?>>>? CapsLoad { get; set; }
    public static Func<Dictionary<string, List<Dictionary<string, object?>>>>? InventoryLoad { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? ErpNavLoad { get; set; }
    public static Func<Dictionary<string, List<Dictionary<string, object?>>>, Dictionary<string, object?>>? AssignUniquePhotos { get; set; }
    public static Func<long>? Clock { get; set; }

    private static List<Dictionary<string, object?>>? CapsCache;
    private static Dictionary<string, object?>? BuiltCache;

    public static void Reset()
    {
        TopicPhoto = null;
        TopicSvgUrl = null;
        TopicCatalog = null;
        CapsLoad = () => [];
        InventoryLoad = () => new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        ErpNavLoad = () => new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        AssignUniquePhotos = null;
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CapsCache = null;
        BuiltCache = null;
    }

    public static string EpcCpBrochureItemImage(Dictionary<string, object?> item, string area)
    {
        if (TopicPhoto != null)
        {
            var meta = TopicPhoto(item, area);
            return Str(meta, "photo");
        }

        var name = Str(item, "name").Trim();
        if (name == "")
        {
            name = "Process";
        }

        var icon = Str(item, "icon").Trim();
        if (icon == "")
        {
            icon = "fa-cube";
        }

        return "/content/general_pages/epc_brochure_process_photo.php?" + HttpBuildQuery(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["t"] = name,
            ["a"] = area,
            ["i"] = icon
        });
    }

    public static Dictionary<string, object?> EpcCpBrochureItemPhotoMeta(Dictionary<string, object?> item, string area)
    {
        if (TopicPhoto != null)
        {
            return TopicPhoto(item, area);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["topic"] = "default",
            ["label"] = "Operations",
            ["photo"] = EpcCpBrochureItemImage(item, area)
        };
    }

    public static List<string> EpcCpBrochureScreenPool()
    {
        const string basePath = "/content/general_pages/marketing_screens/";
        return
        [
            basePath + "pf_workforce.png",
            basePath + "pf_orgmap.png",
            basePath + "pf_tracker.png",
            basePath + "pf_location.png",
            basePath + "og_cover.png",
            basePath + "epartscart-brochure-cover.jpg",
            basePath + "ecomae-brochure-cover.jpg"
        ];
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCpBrochureAreaVisuals()
    {
        string Pick(string topic)
        {
            if (TopicSvgUrl != null)
            {
                return TopicSvgUrl(topic, "chapter-" + topic, Ucfirst(topic), "Chapter");
            }

            if (TopicCatalog == null)
            {
                return "/content/general_pages/marketing_screens/og_cover.png";
            }

            var cat = TopicCatalog();
            object? photosObj = null;
            if (cat.TryGetValue(topic, out var topicRow) && topicRow.TryGetValue("photos", out var topicPhotos))
            {
                photosObj = topicPhotos;
            }
            else if (cat.TryGetValue("default", out var defRow) && defRow.TryGetValue("photos", out var defPhotos))
            {
                photosObj = defPhotos;
            }

            var first = FirstString(photosObj);
            return first != "" ? first : "/content/general_pages/marketing_screens/og_cover.png";
        }

        return new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["Super CP / Platform"] = Visual("fa-cloud", Pick("platform"), "Host every tenant from one operator console."),
            ["Super CP / Operator"] = Visual("fa-user-secret", Pick("platform"), "Cross-tenant tools for platform operators."),
            ["Super CP / BOC"] = Visual("fa-shield", Pick("platform"), "Business operations control and audit."),
            ["Portal"] = Visual("fa-sliders", Pick("settings"), "Industry packs, branding, and site settings."),
            ["Integrations"] = Visual("fa-plug", Pick("platform"), "APIs, webhooks, and partner connections."),
            ["Shop / OMS"] = Visual("fa-shopping-cart", Pick("orders"), "Orders, fulfilment, and daily desk work."),
            ["Prices & Catalogue"] = Visual("fa-tags", Pick("inventory"), "Price lists, warehouses, and catalogue truth."),
            ["Payments"] = Visual("fa-credit-card", Pick("money"), "Gateways, currency, and reconciliation."),
            ["Logistics"] = Visual("fa-truck", Pick("logistics"), "Carriers, branches, and shipping methods."),
            ["Channels / Marketplace"] = Visual("fa-share-alt", Pick("orders"), "Sell beyond your own storefront."),
            ["Procurement"] = Visual("fa-clipboard", Pick("procurement"), "Suppliers, POs, and three-way match."),
            ["ERP / Finance"] = Visual("fa-university", Pick("money"), "GL, AR/AP, treasury, and close."),
            ["ERP / Modules"] = Visual("fa-th-large", Pick("erp"), "Every ERP area and tab — auto-synced from live nav."),
            ["ERP / Tax & VAT"] = Visual("fa-percent", Pick("money"), "VAT, tax toolkit, and jurisdiction rules."),
            ["ERP / External Reporting"] = Visual("fa-file-text-o", Pick("documents"), "Statutory returns and export packs."),
            ["Documents"] = Visual("fa-folder-open", Pick("documents"), "Print packs, PDFs, and document control."),
            ["Customers / CRM"] = Visual("fa-users", Pick("customers"), "Customers, profiles, and CRM pipelines."),
            ["AI"] = Visual("fa-magic", Pick("ai"), "Parts Expert chat and agent review."),
            ["Marketing"] = Visual("fa-bullhorn", Pick("marketing"), "Broadcast, social hub, and campaigns."),
            ["Content / CMS"] = Visual("fa-pencil", Pick("content"), "Pages, blocks, and storefront content."),
            ["System & Admin"] = Visual("fa-cogs", Pick("settings"), "Users, roles, and platform admin."),
            ["Industry Templates"] = Visual("fa-industry", Pick("platform"), "Vertical packs ready to deploy.")
        };
    }

    public static List<Dictionary<string, object?>> EpcCpBrochureCapabilitiesCatalog()
    {
        if (CapsCache != null)
        {
            return CapsCache;
        }

        var caps = CapsLoad != null ? CapsLoad() : [];
        CapsCache = caps;
        return caps;
    }

    public static string EpcCpBrochureNormKey(string s)
    {
        s = s.Trim().ToLowerInvariant();
        s = Regex.Replace(s, "[^a-z0-9]+", " ");
        return Regex.Replace(s, "\\s+", " ").Trim();
    }

    public static string EpcCpBrochureCapCategoryToArea(string cat)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Platform & Super CP"] = "Super CP / Platform",
            ["Commerce — Pricing & catalog"] = "Prices & Catalogue",
            ["Commerce — Orders & fulfilment"] = "Shop / OMS",
            ["Payments"] = "Payments",
            ["Logistics & shipping"] = "Logistics",
            ["Marketplace channels"] = "Channels / Marketplace",
            ["Procurement & suppliers"] = "Procurement",
            ["Finance & ERP"] = "ERP / Finance",
            ["UAE e-invoicing & VAT"] = "ERP / Tax & VAT",
            ["External Reporting & statutory returns"] = "ERP / External Reporting",
            ["Document control"] = "Documents",
            ["CRM & customer management"] = "Customers / CRM",
            ["AI & automation"] = "AI",
            ["Marketing & growth"] = "Marketing",
            ["System & admin"] = "System & Admin",
            ["Industry templates"] = "Industry Templates"
        };
        return map.TryGetValue(cat, out var area) ? area : "Portal";
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcCpBrochureCapabilityIndex()
    {
        var idx = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var cap in EpcCpBrochureCapabilitiesCatalog())
        {
            var title = Str(cap, "title");
            if (title == "")
            {
                continue;
            }

            idx[EpcCpBrochureNormKey(title)] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["icon"] = Str(cap, "icon") != "" ? Str(cap, "icon") : "fa-cube",
                ["summary"] = Str(cap, "summary"),
                ["id"] = Str(cap, "id"),
                ["category"] = Str(cap, "category")
            };
        }

        return idx;
    }

    public static List<Dictionary<string, object?>> EpcCpBrochureErpNavItems()
    {
        var items = new List<Dictionary<string, object?>>();
        var cfg = ErpNavLoad != null ? ErpNavLoad() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var (areaKey, area) in cfg)
        {
            var areaLabel = Str(area, "label");
            if (areaLabel == "")
            {
                areaLabel = areaKey;
            }

            var areaIcon = Str(area, "icon");
            if (areaIcon == "")
            {
                areaIcon = "fa-cube";
            }

            var areaDesc = Str(area, "desc");
            Dictionary<string, object?>? tabs = null;
            if (area.TryGetValue("tabs", out var tabsObj) && tabsObj is Dictionary<string, object?> loose)
            {
                tabs = loose;
            }
            else if (tabsObj is Dictionary<string, Dictionary<string, object?>> typed)
            {
                tabs = typed.ToDictionary(kv => kv.Key, kv => (object?)kv.Value, StringComparer.Ordinal);
            }
            if (tabs == null || tabs.Count == 0)
            {
                items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = areaLabel,
                    ["does"] = areaDesc != "" ? areaDesc : ("ERP area: " + areaLabel),
                    ["url"] = "/cp/shop/finance/erp?area=" + RawUrl(areaKey),
                    ["scope"] = "client",
                    ["icon"] = areaIcon,
                    ["id"] = "erp-" + Regex.Replace(areaKey, "[^a-z0-9_\\-]", "")
                });
                continue;
            }

            foreach (var (tabKey, tabObj) in tabs)
            {
                if (tabObj is not Dictionary<string, object?> tab)
                {
                    continue;
                }

                var tabLabel = Str(tab, "label");
                if (tabLabel == "")
                {
                    tabLabel = tabKey;
                }

                var tabIcon = Str(tab, "icon");
                if (tabIcon == "")
                {
                    tabIcon = areaIcon;
                }

                items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = areaLabel + " · " + tabLabel,
                    ["does"] = areaDesc != "" ? (areaDesc + " — " + tabLabel) : (areaLabel + " / " + tabLabel),
                    ["url"] = "/cp/shop/finance/erp?area=" + RawUrl(areaKey) + "&tab=" + RawUrl(tabKey),
                    ["scope"] = "client",
                    ["icon"] = tabIcon,
                    ["id"] = "erp-" + Regex.Replace(areaKey + "-" + tabKey, "[^a-z0-9_\\-]", "")
                });
            }
        }

        return items;
    }

    public static Dictionary<string, object?> EpcCpBrochureBuildLiveInventory()
    {
        if (BuiltCache != null)
        {
            return BuiltCache;
        }

        var sources = new List<string> { "curated inventory" };
        var raw = InventoryLoad != null ? InventoryLoad() : new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        var capIdx = EpcCpBrochureCapabilityIndex();
        sources.Add("capabilities catalog (" + capIdx.Count + ")");
        var areaVisuals = EpcCpBrochureAreaVisuals();

        var output = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        var seenUrl = new Dictionary<string, bool>(StringComparer.Ordinal);
        var seenName = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var (areaKey, items) in raw)
        {
            var area = areaKey;
            output[area] = [];
            foreach (var item in items)
            {
                var name = Str(item, "name").Trim();
                var does = Str(item, "does").Trim();
                var url = Str(item, "url").Trim();
                var scope = item.TryGetValue("scope", out var sc) && Convert.ToString(sc) is { Length: > 0 } scopeVal ? scopeVal : "client";
                var isStub = name.StartsWith("Epc ", StringComparison.Ordinal) || does == "Open from left CP menu.";
                if (isStub)
                {
                    continue;
                }

                var key = EpcCpBrochureNormKey(name);
                var icon = "fa-cube";
                var id = "";
                if (capIdx.TryGetValue(key, out var cap))
                {
                    var capIcon = Str(cap, "icon");
                    icon = capIcon != "" ? capIcon : icon;
                    id = Str(cap, "id");
                    var summary = Str(cap, "summary");
                    if (PhpStrlen(summary) > PhpStrlen(does))
                    {
                        does = summary;
                    }
                }
                else if (areaVisuals.TryGetValue(area, out var visual) && visual.TryGetValue("icon", out var visIcon) && Convert.ToString(visIcon) is { Length: > 0 } vi)
                {
                    icon = vi;
                }

                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = name,
                    ["does"] = does,
                    ["url"] = url,
                    ["scope"] = scope,
                    ["icon"] = icon,
                    ["id"] = id != "" ? id : ("fn-" + Md5Hex(area + "|" + name + "|" + url)[..10])
                };
                if (url != "")
                {
                    seenUrl[url] = true;
                }

                seenName[key] = true;
                output[area].Add(row);
            }
        }

        foreach (var cap in EpcCpBrochureCapabilitiesCatalog())
        {
            var title = Str(cap, "title").Trim();
            if (title == "")
            {
                continue;
            }

            var key = EpcCpBrochureNormKey(title);
            if (seenName.ContainsKey(key))
            {
                continue;
            }

            var area = EpcCpBrochureCapCategoryToArea(Str(cap, "category"));
            if (!output.ContainsKey(area))
            {
                output[area] = [];
            }

            output[area].Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = title,
                ["does"] = Str(cap, "summary"),
                ["url"] = "",
                ["scope"] = area.StartsWith("Super CP", StringComparison.Ordinal) ? "super" : "both",
                ["icon"] = Str(cap, "icon") != "" ? Str(cap, "icon") : "fa-cube",
                ["id"] = Str(cap, "id")
            });
            seenName[key] = true;
        }

        sources.Add("capability append");

        var erpItems = EpcCpBrochureErpNavItems();
        if (erpItems.Count > 0)
        {
            output["ERP / Modules"] = erpItems;
            sources.Add("ERP nav live (" + erpItems.Count + " tabs)");
        }

        var drop = new List<string>();
        foreach (var (area, items) in output)
        {
            if (items.Count == 0)
            {
                drop.Add(area);
                continue;
            }

            items.Sort((a, b) => string.Compare(Convert.ToString(a["name"]), Convert.ToString(b["name"]), StringComparison.OrdinalIgnoreCase));
        }

        foreach (var area in drop)
        {
            output.Remove(area);
        }

        var total = 0;
        foreach (var items in output.Values)
        {
            total += items.Count;
        }

        if (AssignUniquePhotos != null)
        {
            var map = AssignUniquePhotos(output);
            sources.Add("unique topic photos (" + map.Count + ")");
        }

        BuiltCache = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["areas"] = output,
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["generated_at"] = Clock != null ? Clock() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["sources"] = sources,
                ["total"] = total,
                ["area_count"] = output.Count
            }
        };
        return BuiltCache;
    }

    private static Dictionary<string, object?> Visual(string icon, string image, string blurb)
        => new(StringComparer.Ordinal) { ["icon"] = icon, ["image"] = image, ["blurb"] = blurb };

    private static string FirstString(object? photosObj)
    {
        if (photosObj is IList<string> list && list.Count > 0)
        {
            return list[0] ?? "";
        }

        if (photosObj is IList<object?> obj && obj.Count > 0)
        {
            return Convert.ToString(obj[0]) ?? "";
        }

        return "";
    }

    private static string Ucfirst(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";

    private static int PhpStrlen(string value) => Encoding.UTF8.GetByteCount(value);

    private static string Md5Hex(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string RawUrl(string value) => Uri.EscapeDataString(value);

    private static string HttpBuildQuery(Dictionary<string, string> pairs)
        => string.Join("&", pairs.Select(kv => PhpUrlencode(kv.Key) + "=" + PhpUrlencode(kv.Value)));

    private static string PhpUrlencode(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var sb = new StringBuilder(bytes.Length * 3);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c is '-' or '_' or '.')
            {
                sb.Append(c);
            }
            else if (c == ' ')
            {
                sb.Append('+');
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2"));
            }
        }

        return sb.ToString();
    }
}
