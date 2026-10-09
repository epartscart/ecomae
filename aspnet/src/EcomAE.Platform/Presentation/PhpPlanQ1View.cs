using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-view helpers. PHP identifiers kept for the inventory:
/// <c>epc_sku_media_cp_lang</c>, <c>epc_sku_media_cp_install</c>,
/// <c>epc_sku_media_storefront_load</c>, <c>epc_sku_media_render_spec_groups_html</c>,
/// <c>epc_sku_media_emit_storefront_css</c>, <c>epc_sku_media_render_storefront</c>.
/// </summary>
public static class PhpPlanQ1View
{
    public const string SkuMediaCpInstallPath = "content/shop/catalogue/epc_sku_media_cp_install.php";
    public const string SkuMediaStorefrontPath = "content/shop/catalogue/epc_sku_media_storefront.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<PhpPlanQ1Open.OpenStore, int>? MenuApply { get; set; }
    private static bool _cssDone;

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        MenuApply = null;
        _cssDone = false;
    }

    public sealed class ContentRow
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
        public int Level { get; set; }
        public string Alias { get; set; } = "";
        public string Value { get; set; } = "";
        public int Parent { get; set; }
        public string Description { get; set; } = "";
        public int IsFrontend { get; set; }
        public string ContentType { get; set; } = "";
        public string Content { get; set; } = "";
        public string TitleTag { get; set; } = "";
        public int PublishedFlag { get; set; }
        public long TimeCreated { get; set; }
        public long TimeEdited { get; set; }
        public int Order { get; set; } = 55;
    }

    public sealed class AccessRow
    {
        public int ContentId { get; set; }
        public int GroupId { get; set; }
    }

    public sealed class LangRow
    {
        public string StrKey { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class LangTrRow
    {
        public string StrKey { get; set; } = "";
        public string LangCode { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public static string ManagerContentPath()
        => "/<backend_dir>/content/shop/catalogue/" + "epc_sku_media_manager" + ".php";

    public static void EpcSkuMediaCpLang(PhpPlanQ1Open.OpenStore db, List<LangRow> langs, List<LangTrRow> tr, string key, string en, string ru)
    {
        _ = db;
        if (!langs.Any(l => l.StrKey == key))
        {
            langs.Add(new LangRow { StrKey = key, Description = en });
        }

        UpsertTr(tr, key, "en", en);
        UpsertTr(tr, key, "ru", ru);
    }

    public static Dictionary<string, object?> EpcSkuMediaCpInstall(
        PhpPlanQ1Open.OpenStore db,
        List<ContentRow> contents,
        List<AccessRow> access,
        List<LangRow> langs,
        List<LangTrRow> tr,
        string backendDir = "cp",
        bool apply = true)
    {
        backendDir = backendDir.Trim('/');
        if (backendDir == "")
        {
            backendDir = "cp";
        }

        _ = backendDir;
        PhpPlanQ1Open.EpcSkuMediaEnsureSchema(db);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["content_id"] = 0,
            ["menu_item_id"] = 0
        };
        if (!apply)
        {
            return result;
        }

        EpcSkuMediaCpLang(db, langs, tr, "epc_sku_media_manager", "SKU photos & specs", "Фото и характеристики SKU");
        const string contentUrl = "shop/catalogue/sku_media";
        var phpPath = ManagerContentPath();
        var now = Clock();
        var parentRow = contents.FirstOrDefault(c => c.Url == "shop/catalogue/products" && c.IsFrontend == 0)
            ?? contents.FirstOrDefault(c => c.Url == "shop/catalogue" && c.IsFrontend == 0)
            ?? contents.FirstOrDefault(c => c.Url == "shop" && c.IsFrontend == 0);
        if (parentRow is null)
        {
            throw new InvalidOperationException("Parent content not found for SKU media CP route");
        }

        var parentId = parentRow.Id;
        var level = parentRow.Level + 1;
        var existing = contents.FirstOrDefault(c => c.Url == contentUrl && c.IsFrontend == 0);
        int contentId;
        if (existing is not null)
        {
            contentId = existing.Id;
            existing.PublishedFlag = 1;
            existing.ContentType = "php";
            existing.Content = phpPath;
            existing.TitleTag = "epc_sku_media_manager";
            existing.Value = "epc_sku_media_manager";
            existing.Parent = parentId;
            existing.Level = level;
            existing.Alias = "sku_media";
            existing.TimeEdited = now;
        }
        else
        {
            contentId = contents.Count == 0 ? 1 : contents.Max(c => c.Id) + 1;
            contents.Add(new ContentRow
            {
                Id = contentId,
                Url = contentUrl,
                Level = level,
                Alias = "sku_media",
                Value = "epc_sku_media_manager",
                Parent = parentId,
                Description = "SKU photos & multi-type specifications for any product",
                IsFrontend = 0,
                ContentType = "php",
                Content = phpPath,
                TitleTag = "epc_sku_media_manager",
                PublishedFlag = 1,
                TimeCreated = now,
                TimeEdited = now,
                Order = 55
            });
        }

        result["content_id"] = contentId;
        var refId = contents.FirstOrDefault(c => c.Url == "shop/catalogue/products" && c.IsFrontend == 0)?.Id ?? 0;
        if (refId > 0 && contentId > 0)
        {
            access.RemoveAll(a => a.ContentId == contentId);
            var groupIds = access.Where(a => a.ContentId == refId).Select(a => a.GroupId).Distinct().ToList();
            foreach (var gid in groupIds)
            {
                if (!access.Any(a => a.ContentId == contentId && a.GroupId == gid))
                {
                    access.Add(new AccessRow { ContentId = contentId, GroupId = gid });
                }
            }
        }

        if (MenuApply is not null)
        {
            result["menu_item_id"] = MenuApply(db);
        }

        return result;
    }

    public static Dictionary<string, object?>? EpcSkuMediaStorefrontLoad(PhpPlanQ1Open.OpenStore db, Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var productId = IntVal(Field(opts, "product_id"));
        var brand = Str(Field(opts, "brand"));
        var article = Str(Field(opts, "article"));
        var profile = PhpPlanQ1Open.EpcSkuMediaResolveForProduct(db, productId, brand, article);
        if (profile is null || Str(Field(profile, "status")) is "hidden" or "draft")
        {
            return null;
        }

        var payload = PhpPlanQ1Open.EpcSkuMediaFullPayload(db, IntVal(Field(profile, "id")));
        if (payload is null)
        {
            return null;
        }

        var photos = payload["photos"] is IEnumerable<object?> p ? p.OfType<Dictionary<string, object?>>().ToList() : [];
        var rawGroups = payload["spec_groups"] is IEnumerable<object?> g ? g.OfType<Dictionary<string, object?>>().ToList() : [];
        var groups = rawGroups.Where(row => !Empty(Field(row, "rows"))).ToList();
        if (photos.Count == 0 && groups.Count == 0)
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["profile"] = profile,
            ["photos"] = photos,
            ["groups"] = groups
        };
    }

    public static string EpcSkuMediaRenderSpecGroupsHtml(IEnumerable<object?> groups)
    {
        var list = groups.OfType<Dictionary<string, object?>>().ToList();
        if (list.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-sku-storefront__specs\">");
        foreach (var g in list)
        {
            sb.Append("<section class=\"epc-sku-storefront__group\">");
            sb.Append("<h3><i class=\"fa ").Append(H(Field(g, "icon") ?? "fa-list")).Append("\"></i> ").Append(H(Field(g, "name") ?? "Specifications")).Append("</h3>");
            sb.Append("<table><tbody>");
            if (Field(g, "rows") is IEnumerable<object?> rows)
            {
                foreach (var item in rows)
                {
                    if (item is not Dictionary<string, object?> row)
                    {
                        continue;
                    }

                    var label = Str(Field(row, "label"));
                    var display = Str(Field(row, "display") ?? Field(row, "value"));
                    var type = Str(Field(row, "value_type") ?? "text");
                    sb.Append("<tr><th scope=\"row\">").Append(H(label)).Append("</th><td>");
                    sb.Append(type == "rich" ? display : H(display));
                    sb.Append("</td></tr>");
                }
            }

            sb.Append("</tbody></table></section>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    public static string EpcSkuMediaEmitStorefrontCss()
    {
        if (_cssDone)
        {
            return "";
        }

        _cssDone = true;
        var cssPath = Path.Combine(DocumentRoot.TrimEnd('/', '\\'), "content", "shop", "catalogue", "epc_sku_media.css");
        var stamp = File.Exists(cssPath)
            ? ((DateTimeOffset)File.GetLastWriteTimeUtc(cssPath)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
            : "";
        var href = "/content/shop/catalogue/epc_sku_media.css?v=" + stamp;
        return "<link rel=\"stylesheet\" href=\"" + H(href) + "\">";
    }

    public static string EpcSkuMediaRenderStorefront(PhpPlanQ1Open.OpenStore db, Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var showPhotos = !opts.ContainsKey("show_photos") || !Empty(Field(opts, "show_photos"));
        var showSpecs = !opts.ContainsKey("show_specs") || !Empty(Field(opts, "show_specs"));
        var data = EpcSkuMediaStorefrontLoad(db, opts);
        if (data is null)
        {
            return "";
        }

        var photos = showPhotos && data["photos"] is IEnumerable<object?> p
            ? p.OfType<Dictionary<string, object?>>().ToList()
            : [];
        var groups = showSpecs && data["groups"] is IEnumerable<object?> g
            ? g.OfType<Dictionary<string, object?>>().ToList()
            : [];
        if (photos.Count == 0 && groups.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append(EpcSkuMediaEmitStorefrontCss());
        var profile = (Dictionary<string, object?>)data["profile"]!;
        var uid = "epcSkuGal" + IntVal(Field(profile, "id"));
        sb.Append("<div class=\"epc-sku-storefront\" id=\"").Append(H(uid)).Append("\">");
        if (photos.Count > 0)
        {
            var primary = photos[0];
            foreach (var ph in photos)
            {
                if (!Empty(Field(ph, "is_primary")))
                {
                    primary = ph;
                    break;
                }
            }

            sb.Append("<div class=\"epc-sku-storefront__gallery\">");
            sb.Append("<div><div class=\"epc-sku-storefront__main\"><img id=\"").Append(H(uid)).Append("_main\" src=\"")
                .Append(H(Field(primary, "url"))).Append("\" alt=\"")
                .Append(H(Field(primary, "alt") ?? Field(profile, "title"))).Append("\"></div>");
            var cap = Str(Field(primary, "caption"));
            sb.Append("<div class=\"epc-sku-storefront__caption\" id=\"").Append(H(uid)).Append("_cap\">").Append(H(cap)).Append("</div></div>");
            if (photos.Count > 1)
            {
                sb.Append("<div class=\"epc-sku-storefront__thumbs\" role=\"list\">");
                for (var i = 0; i < photos.Count; i++)
                {
                    var ph = photos[i];
                    var active = EqualsId(Field(ph, "id"), Field(primary, "id")) ? " is-active" : "";
                    sb.Append("<button type=\"button\" class=\"").Append(active).Append("\" data-src=\"")
                        .Append(H(Field(ph, "url"))).Append("\" data-alt=\"").Append(H(Field(ph, "alt")))
                        .Append("\" data-cap=\"").Append(H(Field(ph, "caption"))).Append("\" aria-label=\"Photo ")
                        .Append(i + 1).Append("\">");
                    sb.Append("<img src=\"").Append(H(Field(ph, "url"))).Append("\" alt=\"\">");
                    sb.Append("</button>");
                }

                sb.Append("</div>");
            }

            sb.Append("</div>");
            if (photos.Count > 1)
            {
                sb.Append("<script>(function(){var r=document.getElementById(")
                    .Append(JsonSerializer.Serialize(uid, JsonOpts))
                    .Append(");if(!r)return;var m=document.getElementById(")
                    .Append(JsonSerializer.Serialize(uid + "_main", JsonOpts))
                    .Append(");var c=document.getElementById(")
                    .Append(JsonSerializer.Serialize(uid + "_cap", JsonOpts))
                    .Append(");r.querySelectorAll(\".epc-sku-storefront__thumbs button\").forEach(function(b){b.addEventListener(\"click\",function(){r.querySelectorAll(\".epc-sku-storefront__thumbs button\").forEach(function(x){x.classList.remove(\"is-active\")});b.classList.add(\"is-active\");if(m){m.src=b.getAttribute(\"data-src\");m.alt=b.getAttribute(\"data-alt\")||\"\";}if(c){c.textContent=b.getAttribute(\"data-cap\")||\"\";}});});})();</script>");
            }
        }

        if (groups.Count > 0)
        {
            sb.Append(EpcSkuMediaRenderSpecGroupsHtml(groups));
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void UpsertTr(List<LangTrRow> tr, string key, string lang, string value)
    {
        var row = tr.FirstOrDefault(t => t.StrKey == key && t.LangCode == lang);
        if (row is null)
        {
            tr.Add(new LangTrRow { StrKey = key, LangCode = lang, Value = value });
        }
        else
        {
            row.Value = value;
        }
    }

    private static bool EqualsId(object? left, object? right)
        => IntVal(left) == IntVal(right) && left is not null && right is not null
            || (left is null && right is null);

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);
    }

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
            JsonElement je => je.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static int IntVal(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is bool b)
        {
            return b ? 1 : 0;
        }

        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static bool Empty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            string s => s is "" or "0",
            System.Collections.ICollection c => c.Count == 0,
            _ => Str(value) is "" or "0"
        };
}
