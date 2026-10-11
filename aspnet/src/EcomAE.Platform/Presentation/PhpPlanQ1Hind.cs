using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hind EParts Mod catalog page.
/// PHP identifiers kept for the inventory: <c>text</c>,
/// <c>categoriesWithSubcategories</c>, <c>filterRowsBySubcategory</c>,
/// <c>esc</c>, <c>apiItems</c>, <c>isSubscriptionErr</c>, <c>shopUrlFor</c>,
/// <c>subscriptionMessage</c>, <c>applyCatalogGate</c>,
/// <c>renderSubscriptionsPanel</c>, <c>showSubscriptionBanner</c>,
/// <c>loading</c>, <c>done</c>, <c>showSubscriptionErr</c>, <c>showErr</c>,
/// <c>ui</c>, <c>renderStepPickerUi</c>, <c>applyFilter</c>,
/// <c>applyFilterFromOutput</c>, <c>partSearchUrl</c>,
/// <c>productDetailUrl</c>, <c>bindArticleRowNavigation</c>, <c>cartUrl</c>,
/// <c>renderDemoDashboard</c>, <c>fallbackDemoPayload</c>,
/// <c>loadDemoMode</c>, <c>pendingCategoryBanner</c>,
/// <c>consumePendingCategory</c>, <c>renderMakeDashboard</c>,
/// <c>loadModelsForMake</c>, <c>loadCarsForModel</c>,
/// <c>renderArticleRow</c>, <c>renderFlowBar</c>, <c>cataApi</c>,
/// <c>api</c>, <c>partsApi</c>, <c>setStep</c>,
/// <c>renderVehicleBar</c>, <c>renderCategories</c>,
/// <c>renderArticlesList</c>, <c>loadArticles</c>,
/// <c>loadCategories</c>, <c>loadManufacturers</c>.
/// Path: <c>content/eparts_mod_catalog.php</c>.
/// GET never mints a session cookie. Leftover unique portal / user helper
/// stay unnamed. Auto-parts site is injected. ePartsCart host uses Dock.
/// Parents already closed: Spar (partsapi), Hint (cata config), Dock
/// (tenant hostname), TinyPages (VIN label + product route + cata bridge).
/// </summary>
public static class PhpPlanQ1Hind
{
    public const string EpartsModCatalogPath = "content/eparts_mod_catalog.php";
    public const string Denied =
        "<div class=\"alert alert-warning\">EParts Mod is not available on this site.</div>";
    public const string DefaultShopUrl = "https://partsapi.ru/account/shop";
    public const string DefaultSubscriptionMsg =
        "An EParts API subscription is required for vehicle models (getModels). Add the method key to config.epc-partsapi.php (method_keys) after subscribing.";

    public static bool AutoPartsSite { get; set; }
    public static string LangHref { get; set; } = "/en";
    public static bool ChpuOn { get; set; }
    public static string ChpuPartsUrl { get; set; } = "parts";
    public static string ChpuBrandsUrl { get; set; } = "brands";
    public static string ChpuSlashCode { get; set; } = "%2F";

    public static void Reset()
    {
        AutoPartsSite = false;
        LangHref = "/en";
        ChpuOn = false;
        ChpuPartsUrl = "parts";
        ChpuBrandsUrl = "brands";
        ChpuSlashCode = "%2F";
        PhpPlanQ1Spar.Reset();
        PhpPlanQ1Hint.Reset();
        PhpPlanQ1Dock.Server = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public static string NormalizeLangHref(string? lang)
    {
        if (string.IsNullOrEmpty(lang))
        {
            return "/en";
        }

        return lang.TrimEnd('/');
    }

    public static string Text(object? value)
        => value is null ? "" : Convert.ToString(value) ?? "";

    public static string Esc(object? value)
        => Text(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static List<object?> ApiItems(object? data)
    {
        if (data is IList<object?> list && data is not string)
        {
            return list.ToList();
        }

        if (data is Dictionary<string, object?> map
            && map.TryGetValue("data", out var inner)
            && inner is IList<object?> innerList
            && inner is not string)
        {
            return innerList.ToList();
        }

        return [];
    }

    public static bool IsSubscriptionErr(object? err)
    {
        if (err is not Dictionary<string, object?> map)
        {
            return false;
        }

        if (!PhpPlanQ1Hint.PhpEmpty(map.TryGetValue("subscription_required", out var sub) ? sub : null))
        {
            return true;
        }

        var status = map.TryGetValue("http_status", out var raw) ? raw : 0;
        var code = Convert.ToInt32(status ?? 0);
        return code is 401 or 403;
    }

    public static string NormArticle(object? article)
        => Regex.Replace(Text(article), @"[\s\-_`/'""\\.,#]", "").ToUpperInvariant();

    public static string PartSearchUrl(
        string lang,
        bool chpu,
        string parts,
        string brands,
        string slash,
        string brand,
        string article)
    {
        var a = NormArticle(article);
        var b = Text(brand).Trim();
        if (a == "")
        {
            return lang + "/shop/part_search";
        }

        if (chpu)
        {
            if (b == "")
            {
                return lang + "/" + parts + "/" + brands + "/" + Uri.EscapeDataString(a);
            }

            return lang + "/" + parts + "/" + Uri.EscapeDataString(b.Replace("/", slash, StringComparison.Ordinal)) + "/" + Uri.EscapeDataString(a);
        }

        var u = lang + "/shop/part_search?article=" + Uri.EscapeDataString(a);
        if (b != "")
        {
            u += "&brend=" + Uri.EscapeDataString(b);
        }

        return u;
    }

    public static string ProductDetailUrl(string lang, string brand, string article)
    {
        var a = NormArticle(article);
        var slug = Regex.Replace(Text(brand).ToUpperInvariant(), "[^A-Z0-9]", "");
        if (slug == "" || a == "")
        {
            return lang + "/eparts-product";
        }

        return lang + "/product/" + Uri.EscapeDataString(slug) + "/" + Uri.EscapeDataString(a);
    }

    public static string CartUrl(string lang)
        => lang + "/shop/cart";

    public static List<Dictionary<string, object?>> FilterRowsBySubcategory(
        IEnumerable<Dictionary<string, object?>> rows,
        string subName)
    {
        var list = rows.ToList();
        if (subName == "")
        {
            return list;
        }

        var term = subName.ToLowerInvariant();
        var filtered = new List<Dictionary<string, object?>>();
        foreach (var row in list)
        {
            var group = Text(
                    row.TryGetValue("PRODUCT_GROUP", out var g) ? g
                    : row.TryGetValue("name", out var n) ? n
                    : row.TryGetValue("ART_PRODUCT_NAME", out var a) ? a
                    : row.TryGetValue("partname", out var p) ? p
                    : "")
                .ToLowerInvariant();
            if (group.Contains(term, StringComparison.Ordinal) || term.Contains(group, StringComparison.Ordinal))
            {
                filtered.Add(row);
            }
        }

        return filtered.Count > 0 ? filtered : list;
    }

    public static List<Dictionary<string, object?>> CategoriesWithSubcategories(
        IEnumerable<Dictionary<string, object?>> cats,
        Dictionary<string, object?> map)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var c in cats)
        {
            var copy = new Dictionary<string, object?>(c, StringComparer.Ordinal);
            var sid = Text(
                copy.TryGetValue("STR_ID", out var s) ? s
                : copy.TryGetValue("CATEGORY_ID", out var id) ? id
                : copy.TryGetValue("id", out var i) ? i
                : "");
            var hasChildren = copy.TryGetValue("children", out var ch) && !PhpPlanQ1Hint.PhpEmpty(ch);
            if (!hasChildren && map.TryGetValue(sid, out var kids) && !PhpPlanQ1Hint.PhpEmpty(kids))
            {
                copy["children"] = kids;
            }

            outRows.Add(copy);
        }

        return outRows;
    }

    public static string ShopUrlFor(
        object? action,
        object? err,
        Dictionary<string, Dictionary<string, object?>> methodShops,
        Dictionary<string, Dictionary<string, object?>> actionShops,
        string fallback)
    {
        if (err is Dictionary<string, object?> emap)
        {
            if (!PhpPlanQ1Hint.PhpEmpty(emap.TryGetValue("shop_url", out var shop) ? shop : null))
            {
                return Text(shop);
            }

            if (!PhpPlanQ1Hint.PhpEmpty(emap.TryGetValue("method", out var method) ? method : null)
                && methodShops.TryGetValue(Text(method), out var methodMeta)
                && methodMeta.TryGetValue("shop_url", out var methodShop)
                && !PhpPlanQ1Hint.PhpEmpty(methodShop))
            {
                return Text(methodShop);
            }
        }

        var actionKey = Text(action);
        if (actionKey != ""
            && actionShops.TryGetValue(actionKey, out var actionMeta)
            && actionMeta.TryGetValue("shop_url", out var actionShop)
            && !PhpPlanQ1Hint.PhpEmpty(actionShop))
        {
            return Text(actionShop);
        }

        return fallback;
    }

    public static string SubscriptionMessage(
        object? err,
        object? action,
        Dictionary<string, Dictionary<string, object?>> actionShops)
    {
        if (err is Dictionary<string, object?> emap
            && !PhpPlanQ1Hint.PhpEmpty(emap.TryGetValue("error", out var error) ? error : null))
        {
            return Text(error);
        }

        var actionKey = Text(action);
        if (actionKey != "" && actionShops.TryGetValue(actionKey, out var meta))
        {
            return "An EParts API subscription is required for "
                + Text(meta.TryGetValue("label", out var label) ? label : "")
                + " ("
                + Text(meta.TryGetValue("method", out var method) ? method : "")
                + "). Add the key to config.epc-partsapi.php under method_keys after subscribing.";
        }

        return DefaultSubscriptionMsg;
    }

    public static string PendingCategoryBanner(string name)
        => "<div class=\"epc-vc-message\" style=\"margin-bottom:12px;\">Category <strong>"
           + Esc(name)
           + "</strong> selected. Choose make, model and engine to browse parts.</div>";

    public static Dictionary<string, object?> PageFlags(string html)
        => new(StringComparer.Ordinal)
        {
            ["denied"] = html.Contains("EParts Mod is not available on this site.", StringComparison.Ordinal) ? 1 : 0,
            ["root"] = html.Contains("id=\"epc-em-root\"", StringComparison.Ordinal) ? 1 : 0,
            ["cart"] = html.Contains("/shop/cart", StringComparison.Ordinal) ? 1 : 0,
            ["help"] = html.Contains("/contacts", StringComparison.Ordinal) ? 1 : 0,
            ["key_alert"] = html.Contains("config.epc-partsapi.php", StringComparison.Ordinal) ? 1 : 0,
            ["vin"] = html.Contains("id=\"epc-em-vin\"", StringComparison.Ordinal) ? 1 : 0,
            ["plate"] = html.Contains("id=\"epc-em-plate\"", StringComparison.Ordinal) ? 1 : 0,
            ["version"] = html.Contains(PhpPlanQ1Hint.EpcCataVersion, StringComparison.Ordinal) ? 1 : 0,
            ["session"] = html.Contains("PHPSESSID", StringComparison.Ordinal) ? 1 : 0,
            ["prefix"] = html.Length <= 80 ? html : html[..80]
        };

    public static bool EnabledForRequest()
    {
        PhpPlanQ1Spar.IsEpartsHost = PhpPlanQ1Dock.EpcPortalIsEpartscartHostname();
        PhpPlanQ1Spar.IsAutoPartsSite = AutoPartsSite;
        return PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
    }

    public static string RenderPage(string? langHref = null)
    {
        var lang = NormalizeLangHref(langHref ?? LangHref);
        if (!EnabledForRequest())
        {
            return Denied;
        }

        var pres = PhpPlanQ1Hint.EpcCataPresentationJsConfig();
        var vinOn = !PhpPlanQ1Hint.PhpEmpty(pres.TryGetValue("enableVinSearch", out var vin) ? vin : null);
        var plateOn = !PhpPlanQ1Hint.PhpEmpty(pres.TryGetValue("enablePlateSearch", out var plate) ? plate : null);
        var configured = PhpPlanQ1Spar.EpcPartsapiCredentialsConfigured();
        var vinLabel = StorefrontTinyPages.VinLabel();
        var v = Uri.EscapeDataString(PhpPlanQ1Hint.EpcCataVersion);
        var hLang = Esc(lang);
        var html =
            "<link rel=\"stylesheet\" href=\"/content/general_pages/epc_vc_catalog.css?v=" + v + "\">\n"
            + "<link rel=\"stylesheet\" href=\"/content/general_pages/epc_car_mod_theme.css?v=" + v + "\">\n"
            + "<script src=\"/content/general_pages/epc_vc_catalog_ui.js?v=" + v + "\"></script>\n"
            + "\n"
            + "<div class=\"epc-vc epc-cm\" id=\"epc-em-root\" data-lang-href=\"" + hLang + "\">\n"
            + "\t<div class=\"epc-cm-toolbar\">\n"
            + "\t\t<a href=\"" + hLang + "/shop/cart\" class=\"epc-cm-cart\" title=\"Cart\"><i class=\"fa fa-shopping-cart\"></i> Cart</a>\n"
            + "\t\t<a href=\"#\" onclick=\"return false;\" title=\"My Garage (coming soon)\"><i class=\"fa fa-car\"></i> My Garage</a>\n"
            + "\t</div>\n"
            + "\t<div class=\"epc-vc-hero-grid epc-vc-hero-grid--single\">\n"
            + "\t\t<div class=\"epc-cm-dashboard\">\n"
            + "\t\t\t<h2 class=\"epc-cm-dashboard-title\">Select a vehicle</h2>\n"
            + "\t\t\t<div id=\"epc-em-step-picker\"></div>\n"
            + "\t\t\t<div class=\"epc-cm-select-hidden\">\n"
            + "\t\t\t\t<select class=\"form-control\" id=\"epc-em-make\"><option value=\"\">Make</option></select>\n"
            + "\t\t\t\t<select class=\"form-control\" id=\"epc-em-model\" disabled><option value=\"\">Model</option></select>\n"
            + "\t\t\t\t<select class=\"form-control\" id=\"epc-em-car\" disabled><option value=\"\">Engine / modification</option></select>\n"
            + "\t\t\t</div>\n"
            + "\t\t\t<div class=\"epc-cm-vin-row\" id=\"epc-em-vin-row\""
            + (vinOn || plateOn ? "" : " style=\"display:none\"")
            + ">\n";
        if (vinOn)
        {
            html += "\t\t\t\t<input type=\"search\" class=\"form-control\" id=\"epc-em-vin\" placeholder=\""
                + Esc(vinLabel)
                + " / Engine / Chassis\" maxlength=\"17\" autocomplete=\"off\" style=\"text-transform:uppercase;\">\n";
        }

        if (plateOn)
        {
            html += "\t\t\t\t<div class=\"epc-cm-vin-plate\"><span>GB</span><input type=\"text\" id=\"epc-em-plate\" placeholder=\"AA12 BBB\" maxlength=\"8\" autocomplete=\"off\"></div>\n";
        }

        html +=
            "\t\t\t</div>\n"
            + "\t\t\t<div class=\"epc-cm-part-search\">\n"
            + "\t\t\t\t<button type=\"button\" class=\"epc-cm-part-search-btn\" id=\"epc-em-search-go\" aria-label=\"Search\"><i class=\"fa fa-search\"></i></button>\n"
            + "\t\t\t\t<input type=\"search\" class=\"form-control\" id=\"epc-em-part\" placeholder=\"Number, Article, OE, 5w30….\" autocomplete=\"off\">\n"
            + "\t\t\t</div>\n"
            + "\t\t\t<div class=\"epc-cm-help\"><a href=\"" + hLang + "/contacts\">Need help?</a></div>\n";
        if (!configured)
        {
            html +=
                "\t\t\t\t<div class=\"alert alert-info\" style=\"margin-top:12px;font-size:12px;\">Add your EParts API key in <code>config.epc-partsapi.php</code> on the server.</div>\n";
        }

        html +=
            "\t\t\t<div class=\"epc-vc-message\" id=\"epc-em-subscription-banner\" style=\"display:none;margin-top:12px;font-size:12px;\"></div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<div id=\"epc-em-flow\" class=\"epc-vc-flow\" style=\"display:none;\"></div>\n"
            + "\t<div id=\"epc-em-vehicle-bar\" style=\"display:none;\"></div>\n"
            + "\t<div id=\"epc-em-demo-banner\" style=\"display:none;margin:0 0 12px;padding:10px 14px;border-radius:10px;background:#fffbeb;border:1px solid #fcd34d;font-size:12px;font-weight:700;color:#92400e;\"></div>\n"
            + "\t<div id=\"epc-em-output\"><div class=\"epc-vc-loader\">Select a vehicle type, then choose a make…</div></div>\n"
            + "</div>\n";
        if (!configured)
        {
            html += "<script>output.innerHTML = '<div class=\"epc-vc-message\">EParts API key not configured on server.</div>';</script>\n";
        }

        return html;
    }
}
