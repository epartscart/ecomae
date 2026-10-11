using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-holt ePartsCart accessories marketplace page.
/// PHP identifiers kept for the inventory: <c>esc</c>,
/// <c>money</c>, <c>getSaved</c>, <c>setSaved</c>, <c>toggleSaved</c>,
/// <c>isSaved</c>, <c>updateSavedBar</c>, <c>relativeUpdated</c>,
/// <c>listingHref</c>, <c>listingPhotos</c>, <c>browseBackHref</c>,
/// <c>clearDetail</c>, <c>renderDetail</c>, <c>syncUrl</c>,
/// <c>readUrl</c>, <c>facetList</c>, <c>fillVehicleMakes</c>,
/// <c>renderFacets</c>, <c>renderCards</c>, <c>renderPager</c>,
/// <c>load</c>, <c>saveNotify</c>, <c>setView</c>.
/// Path: <c>content/general_pages/epc_epartscart_accessories.php</c>.
/// GET never mints a session cookie. Leftover unique portal / user helper
/// stay unnamed. Parents already closed: Stem (catalog), Foam (db).
/// </summary>
public static class PhpPlanQ1Holt
{
    public const string AccessoriesPath = "content/general_pages/epc_epartscart_accessories.php";
    public const string Version = "20260721accDetail2";
    public const string SearchApi = "/content/shop/docpart/ajax_epc_accessories_search.php";
    public const string DefaultSort = "updated-desc";
    public const string SaveKey = "epc_acc_saved_ads";
    public const string NotifyKey = "epc_acc_notify_alerts";
    public const string EmDash = "\u2014";
    public const string MidDot = " \u00b7 ";

    public static string LangHref { get; set; } = "/en";
    public static long NowUnix { get; set; } = 1_700_000_000;
    public static long NowMs { get; set; } = 1_700_000_000_000;
    public static List<string> SavedIds { get; set; } = [];
    public static bool ShowSavedOnly { get; set; }

    public static void Reset()
    {
        LangHref = "/en";
        NowUnix = 1_700_000_000;
        NowMs = 1_700_000_000_000;
        SavedIds = [];
        ShowSavedOnly = false;
    }

    public static string NormalizeLangHref(string? lang)
    {
        if (string.IsNullOrEmpty(lang))
        {
            return "/en";
        }

        return lang.TrimEnd('/');
    }

    public static string PageBase(string? lang = null)
    {
        var href = NormalizeLangHref(lang ?? LangHref);
        if (href == "")
        {
            href = "/en";
        }

        return href + "/accessories-spare-parts";
    }

    public static string Text(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    public static string Esc(object? value)
        => Text(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    public static string HtmlEsc(object? value)
        => Esc(value).Replace("'", "&#039;", StringComparison.Ordinal);

    public static bool JsTruthy(object? value)
    {
        if (value is null || value is false)
        {
            return false;
        }

        if (value is true)
        {
            return true;
        }

        if (value is string s)
        {
            return s != "";
        }

        if (value is int i)
        {
            return i != 0;
        }

        if (value is long l)
        {
            return l != 0;
        }

        if (value is double d)
        {
            return d != 0 && !double.IsNaN(d);
        }

        if (value is decimal m)
        {
            return m != 0;
        }

        return true;
    }

    public static double JsNumber(object? value)
    {
        if (value is null || value is false)
        {
            return 0;
        }

        if (value is true)
        {
            return 1;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return l;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is decimal m)
        {
            return (double)m;
        }

        var s = Text(value).Trim();
        if (s == "")
        {
            return 0;
        }

        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? n
            : double.NaN;
    }

    public static string Money(object? amount, object? currency = null)
    {
        var n = JsNumber(amount);
        if (double.IsNaN(n) || double.IsInfinity(n) || n <= 0)
        {
            return "Price on request";
        }

        var cur = Text(currency);
        if (cur == "")
        {
            cur = "AED";
        }

        return cur + " " + Math.Round(n, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);
    }

    public static List<string> GetSaved()
        => SavedIds.Select(Text).ToList();

    public static void SetSaved(IEnumerable<string>? ids)
    {
        SavedIds = (ids ?? []).Select(Text).ToList();
        UpdateSavedBar();
    }

    public static bool ToggleSaved(object? id)
    {
        var key = Text(id);
        var ids = GetSaved();
        var i = ids.IndexOf(key);
        if (i >= 0)
        {
            ids.RemoveAt(i);
        }
        else
        {
            ids.Add(key);
        }

        SetSaved(ids);
        return ids.IndexOf(key) >= 0;
    }

    public static bool IsSaved(object? id)
        => GetSaved().IndexOf(Text(id)) >= 0;

    public static Dictionary<string, object?> UpdateSavedBar()
    {
        var n = GetSaved().Count;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["count"] = n,
            ["hidden"] = n < 1 && !ShowSavedOnly ? 1 : 0
        };
    }

    public static string RelativeUpdated(object? ts)
    {
        var t = JsNumber(ts);
        if (double.IsNaN(t) || t == 0)
        {
            return "";
        }

        var unix = (long)t;
        var diff = Math.Max(0, NowUnix - unix);
        if (diff < 3600)
        {
            return Math.Max(1, diff / 60) + " min ago";
        }

        if (diff < 86400)
        {
            return (diff / 3600) + " hours ago";
        }

        if (diff < 604800)
        {
            return (diff / 86400) + " days ago";
        }

        return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
    }

    public static string ListingHref(Dictionary<string, object?>? item, string? pageBase = null)
    {
        var baseHref = pageBase ?? PageBase();
        if (item is null || !JsTruthy(Item(item, "id")))
        {
            return baseHref;
        }

        if (JsTruthy(Item(item, "detail_url")))
        {
            return Text(Item(item, "detail_url"));
        }

        var url = Text(Item(item, "url"));
        if (url.Contains("id=", StringComparison.Ordinal))
        {
            return url;
        }

        var href = baseHref + (baseHref.Contains('?', StringComparison.Ordinal) ? "&" : "?") + "id=" + Uri.EscapeDataString(Text(Item(item, "id")));
        if (JsTruthy(Item(item, "category")))
        {
            href += "&category=" + Uri.EscapeDataString(Text(Item(item, "category")));
        }

        if (JsTruthy(Item(item, "subcategory")))
        {
            href += "&subcategory=" + Uri.EscapeDataString(Text(Item(item, "subcategory")));
        }

        return href;
    }

    public static List<string> ListingPhotos(Dictionary<string, object?>? item)
    {
        var photos = new List<string>();
        if (item is not null && item.TryGetValue("photos", out var raw) && raw is IEnumerable<object?> list && raw is not string)
        {
            foreach (var p in list)
            {
                var u = p is string s
                    ? s
                    : p is Dictionary<string, object?> map && map.TryGetValue("url", out var url)
                        ? Text(url)
                        : "";
                if (u != "")
                {
                    photos.Add(u);
                }
            }
        }

        if (photos.Count == 0 && item is not null && JsTruthy(Item(item, "image_url")))
        {
            photos.Add(Text(Item(item, "image_url")));
        }

        return photos;
    }

    public static string BrowseBackHref(Dictionary<string, object?>? item, string? pageBase = null)
    {
        var href = pageBase ?? PageBase();
        var qs = new List<string>();
        if (item is not null && JsTruthy(Item(item, "category")))
        {
            qs.Add("category=" + Uri.EscapeDataString(Text(Item(item, "category"))));
        }

        if (item is not null && JsTruthy(Item(item, "subcategory")))
        {
            qs.Add("subcategory=" + Uri.EscapeDataString(Text(Item(item, "subcategory"))));
        }

        if (qs.Count == 0)
        {
            return href;
        }

        return href + (href.Contains('?', StringComparison.Ordinal) ? "&" : "?") + string.Join("&", qs);
    }

    public static Dictionary<string, object?> ClearDetail()
        => new(StringComparer.Ordinal)
        {
            ["detailHidden"] = 1,
            ["toolbarHidden"] = 0,
            ["resultsHidden"] = 0,
            ["pagerHidden"] = 0,
            ["isDetail"] = 0
        };

    public static string RenderDetail(Dictionary<string, object?>? item, string? pageBase = null)
    {
        if (item is null)
        {
            return "";
        }

        var photos = ListingPhotos(item);
        var main = photos.Count > 0 ? photos[0] : "";
        var saved = IsSaved(Item(item, "id"));
        var vehicleBits = string.Join(MidDot, new[] { Text(Item(item, "make")), Text(Item(item, "model")), Text(Item(item, "year")) }.Where(JsTruthy));
        var compareHtml = JsTruthy(Item(item, "compare_price")) && JsNumber(Item(item, "compare_price")) > JsNumber(Item(item, "price"))
            ? "<span class=\"epc-acc__compare\">" + Esc(Money(Item(item, "compare_price"), Item(item, "currency"))) + "</span>"
            : "";
        var thumbs = photos.Count > 1
            ? "<div class=\"epc-acc__detail-thumbs\" role=\"list\">" + string.Concat(photos.Select((u, i) =>
                "<button type=\"button\" class=\"" + (i == 0 ? "is-active" : "") + "\" data-src=\"" + Esc(u) + "\" aria-label=\"Photo " + (i + 1) + "\">"
                + "<img src=\"" + Esc(u) + "\" alt=\"\" loading=\"lazy\" /></button>")) + "</div>"
            : "";
        var desc = Text(Item(item, "description")).Trim();
        var catBits = string.Join(" / ", new[] { Text(Item(item, "category_label")), Text(Item(item, "subcategory_label")) }.Where(JsTruthy));
        return
            "<div class=\"epc-acc__detail-bar\">"
            + "<a class=\"epc-acc__detail-back\" href=\"" + Esc(BrowseBackHref(item, pageBase)) + "\" id=\"epc-acc-detail-back\"><i class=\"fa fa-arrow-left\" aria-hidden=\"true\"></i> Back to listings</a>"
            + "<span class=\"epc-acc__detail-id\">Ad #" + Esc(Item(item, "id")) + "</span>"
            + "</div>"
            + "<div class=\"epc-acc__detail-grid\">"
            + "<div class=\"epc-acc__detail-gallery\">"
            + "<div class=\"epc-acc__detail-main\">"
            + (main != ""
                ? "<img id=\"epc-acc-detail-main-img\" src=\"" + Esc(main) + "\" alt=\"" + Esc(Item(item, "title")) + "\" />"
                : "<div class=\"epc-acc__detail-placeholder\"><i class=\"fa fa-image\" aria-hidden=\"true\"></i><span>No photo yet</span></div>")
            + "</div>"
            + thumbs
            + "</div>"
            + "<div class=\"epc-acc__detail-info\">"
            + "<div class=\"epc-acc__meta\">"
            + (JsTruthy(Item(item, "category_label")) ? "<span>" + Esc(Item(item, "category_label")) + "</span>" : "")
            + (JsTruthy(Item(item, "subcategory_label")) ? "<span>" + Esc(Item(item, "subcategory_label")) + "</span>" : "")
            + (JsTruthy(Item(item, "city")) ? "<span>" + Esc(Item(item, "city")) + "</span>" : "")
            + "<span>" + Esc(Text(Item(item, "condition")) == "used" ? "Used" : "New") + "</span>"
            + "</div>"
            + "<h2 class=\"epc-acc__detail-title\">" + Esc(JsTruthy(Item(item, "title")) ? Item(item, "title") : "Listing") + "</h2>"
            + (vehicleBits != "" ? "<p class=\"epc-acc__article\">" + Esc(vehicleBits) + (JsTruthy(Item(item, "updated_at")) ? " · " + Esc(RelativeUpdated(Item(item, "updated_at"))) : "") + "</p>" : "")
            + "<div class=\"epc-acc__price\">" + Esc(Money(Item(item, "price"), Item(item, "currency"))) + compareHtml + "</div>"
            + (JsNumber(Item(item, "stock_qty")) > 0 ? "<div class=\"epc-acc__stock\">" + Esc(Item(item, "stock_qty")) + " in stock</div>" : "")
            + "<div class=\"epc-acc__detail-specs\"><table><tbody>"
            + "<tr><th scope=\"row\">Make</th><td>" + Esc(JsTruthy(Item(item, "make")) ? Item(item, "make") : EmDash) + "</td></tr>"
            + "<tr><th scope=\"row\">Model</th><td>" + Esc(JsTruthy(Item(item, "model")) ? Item(item, "model") : EmDash) + "</td></tr>"
            + "<tr><th scope=\"row\">Year</th><td>" + Esc(JsTruthy(Item(item, "year")) ? Item(item, "year") : EmDash) + "</td></tr>"
            + "<tr><th scope=\"row\">City</th><td>" + Esc(JsTruthy(Item(item, "city")) ? Item(item, "city") : EmDash) + "</td></tr>"
            + "<tr><th scope=\"row\">Condition</th><td>" + Esc(Text(Item(item, "condition")) == "used" ? "Used" : "New") + "</td></tr>"
            + "<tr><th scope=\"row\">Category</th><td>" + Esc(catBits != "" ? catBits : EmDash) + "</td></tr>"
            + "</tbody></table></div>"
            + (desc != "" ? "<div class=\"epc-acc__detail-desc\"><h3>Details</h3><p>" + Esc(desc) + "</p></div>" : "")
            + "<div class=\"epc-acc__actions epc-acc__detail-actions\">"
            + "<button type=\"button\" class=\"secondary epc-acc__save" + (saved ? " is-saved" : "") + "\" data-id=\"" + Esc(Item(item, "id")) + "\" id=\"epc-acc-detail-save\">"
            + (saved ? "Saved" : "Save Ad") + "</button>"
            + (JsTruthy(Item(item, "external_url")) ? "<a class=\"primary\" href=\"" + Esc(Item(item, "external_url")) + "\" target=\"_blank\" rel=\"noopener\">Seller link</a>" : "")
            + "</div>"
            + "</div></div>";
    }

    public static string SyncUrl(string path, Dictionary<string, object?> state)
    {
        var keys = new[] { "q", "category", "subcategory", "make", "model", "city", "condition", "price_min", "price_max", "id", "sort", "page" };
        var parts = new List<string>();
        foreach (var k in keys)
        {
            var v = Text(Item(state, k));
            if (v != "" && !(k == "page" && v == "1") && !(k == "sort" && v == DefaultSort))
            {
                parts.Add(Uri.EscapeDataString(k) + "=" + Uri.EscapeDataString(v));
            }
        }

        return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
    }

    public static Dictionary<string, object?> ReadUrl(string url)
    {
        var state = DefaultState();
        var q = url.IndexOf('?', StringComparison.Ordinal);
        if (q < 0)
        {
            return state;
        }

        var query = url[(q + 1)..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var k = Uri.UnescapeDataString(eq < 0 ? part : part[..eq]);
            var v = eq < 0 ? "" : Uri.UnescapeDataString(part[(eq + 1)..].Replace("+", " ", StringComparison.Ordinal));
            if (state.ContainsKey(k))
            {
                state[k] = v;
            }
        }

        return state;
    }

    public static string FacetList(
        IEnumerable<Dictionary<string, object?>>? rows,
        string valueKey,
        string labelKey,
        object? active,
        string? allLabel = null)
    {
        var html = "<li><button type=\"button\" class=\"" + (!JsTruthy(active) ? "is-active" : "") + "\" data-value=\"\">" + Esc(allLabel is null or "" ? "All" : allLabel) + "</button></li>";
        foreach (var row in rows ?? [])
        {
            var value = Item(row, valueKey);
            var label = JsTruthy(Item(row, labelKey)) ? Item(row, labelKey) : value;
            var hasCount = row.TryGetValue("count", out var count) && count is not null;
            var cls = Text(value) == Text(active) ? " is-active" : "";
            html += "<li><button type=\"button\" class=\"" + cls + "\" data-value=\"" + Esc(value) + "\"><span>" + Esc(label) + "</span>"
                + (hasCount ? "<span class=\"count\">" + Esc(count) + "</span>" : "") + "</button></li>";
        }

        return html;
    }

    public static string FillVehicleMakes(IEnumerable<object?>? makes, object? current)
    {
        var cur = Text(current);
        var opts = "<option value=\"\">Any make</option>";
        foreach (var m in makes ?? [])
        {
            var name = m is string s ? s : m is Dictionary<string, object?> map ? Text(Item(map, "make")) : Text(m);
            if (name == "")
            {
                continue;
            }

            opts += "<option value=\"" + Esc(name) + "\"" + (name == cur ? " selected" : "") + ">" + Esc(name) + "</option>";
        }

        return opts;
    }

    public static Dictionary<string, object?> RenderFacets(
        Dictionary<string, object?>? facets,
        Dictionary<string, object?>? state = null)
    {
        state ??= DefaultState();
        var cats = AsMaps(facets is not null && facets.TryGetValue("categories", out var c) ? c : null);
        var subs = new List<Dictionary<string, object?>>();
        foreach (var cat in cats)
        {
            if (JsTruthy(Item(state, "category")) && Text(Item(cat, "slug")) != Text(Item(state, "category")))
            {
                continue;
            }

            foreach (var s in AsMaps(Item(cat, "subs") ?? Item(cat, "children")))
            {
                subs.Add(s);
            }
        }

        var makes = AsMaps(facets is not null && facets.TryGetValue("makes", out var mk) ? mk : null);
        var cities = AsMaps(facets is not null && facets.TryGetValue("cities", out var ct) ? ct : null);
        var conditions = AsMaps(facets is not null && facets.TryGetValue("conditions", out var cd) ? cd : null);
        if (conditions.Count == 0)
        {
            conditions =
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "new", ["label"] = "New" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "used", ["label"] = "Used" }
            ];
        }

        var browseCats = string.Concat(cats.Select(cat =>
            "<button type=\"button\" data-cat=\"" + Esc(Item(cat, "slug")) + "\">" + Esc(Item(cat, "label"))
            + (JsTruthy(Item(cat, "count")) ? " (" + Text(Item(cat, "count")) + ")" : "") + "</button>"));
        var allSubs = new List<Dictionary<string, object?>>();
        foreach (var cat in cats)
        {
            foreach (var s in AsMaps(Item(cat, "subs") ?? Item(cat, "children")).Take(8))
            {
                allSubs.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["slug"] = Item(s, "slug"),
                    ["label"] = Item(s, "label"),
                    ["parent"] = Item(cat, "slug"),
                    ["count"] = Item(s, "count")
                });
            }
        }

        var browseSubs = string.Concat(allSubs.Take(40).Select(s =>
            "<button type=\"button\" data-cat=\"" + Esc(Item(s, "parent")) + "\" data-sub=\"" + Esc(Item(s, "slug")) + "\">" + Esc(Item(s, "label")) + "</button>"));
        var browseMakes = string.Concat(makes.Select(m =>
            "<button type=\"button\" data-make=\"" + Esc(Item(m, "make")) + "\">" + Esc(Item(m, "make")) + " Parts and Accessories"
            + (JsTruthy(Item(m, "count")) ? " (" + Text(Item(m, "count")) + ")" : "") + "</button>"));
        var browseCities = string.Concat(cities.Select(city =>
            "<button type=\"button\" data-city=\"" + Esc(Item(city, "city")) + "\">Parts and Accessories in " + Esc(Item(city, "city"))
            + (JsTruthy(Item(city, "count")) ? " (" + Text(Item(city, "count")) + ")" : "") + "</button>"));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["cats"] = FacetList(cats, "slug", "label", Item(state, "category"), "All categories"),
            ["subs"] = FacetList(subs, "slug", "label", Item(state, "subcategory"), "All sub categories"),
            ["makes"] = FacetList(makes, "make", "make", Item(state, "make"), "All makes"),
            ["cities"] = FacetList(cities, "city", "city", Item(state, "city"), "All cities"),
            ["condition"] = FacetList(conditions, "value", "label", Item(state, "condition"), "Any condition"),
            ["vehicle"] = FillVehicleMakes(makes.Cast<object?>(), Item(state, "make")),
            ["browseCats"] = browseCats,
            ["browseSubs"] = browseSubs,
            ["browseMakes"] = browseMakes,
            ["browseCities"] = browseCities
        };
    }

    public static string RenderCards(
        IEnumerable<Dictionary<string, object?>>? items,
        Dictionary<string, object?>? state = null,
        bool emptyCatalog = false,
        string? pageBase = null)
    {
        state ??= DefaultState();
        var list = (items ?? []).ToList();
        var savedIds = GetSaved();
        if (JsTruthy(Item(state, "showSavedOnly")) || ShowSavedOnly)
        {
            list = list.Where(it => savedIds.IndexOf(Text(Item(it, "id"))) >= 0).ToList();
        }

        if (JsTruthy(Item(state, "id")))
        {
            var focused = list.FirstOrDefault(it => Text(Item(it, "id")) == Text(Item(state, "id")));
            if (focused is not null)
            {
                return RenderDetail(focused, pageBase);
            }

            return
                "<div class=\"epc-acc__empty\">"
                + "<strong>Listing not found</strong>"
                + "<p>This ad is unavailable or not published. <a href=\"" + Esc(BrowseBackHref(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["category"] = Item(state, "category"),
                    ["subcategory"] = Item(state, "subcategory")
                }, pageBase)) + "\">Back to listings</a></p>"
                + "</div>";
        }

        if (list.Count == 0)
        {
            var savedOnly = JsTruthy(Item(state, "showSavedOnly")) || ShowSavedOnly;
            return
                "<div class=\"epc-acc__empty\">"
                + "<strong>" + (savedOnly ? "No saved ads yet" : "No ads in this search yet") + "</strong>"
                + "<p>Browse by category, make, model, and city. Listings will appear here as they are added category by category.</p>"
                + (emptyCatalog && !savedOnly ? "<p class=\"epc-acc__empty-hint\">Catalog is ready — start by adding products under Car Care, Interior, Exterior, Brakes, and other categories.</p>" : "")
                + "</div>";
        }

        return string.Concat(list.Select((item, idx) =>
        {
            var href = ListingHref(item, pageBase);
            var saved = IsSaved(Item(item, "id"));
            var photos = ListingPhotos(item);
            var cover = photos.Count > 0 ? photos[0] : Text(Item(item, "image_url"));
            var img = cover != ""
                ? "<a class=\"epc-acc__media-link\" href=\"" + Esc(href) + "\"><img src=\"" + Esc(cover) + "\" alt=\"\" loading=\"lazy\" /></a>"
                : "<a class=\"epc-acc__media-link\" href=\"" + Esc(href) + "\"><span class=\"epc-acc__media-mark\">" + Esc(Text(JsTruthy(Item(item, "make")) ? Item(item, "make") : (JsTruthy(Item(item, "category_label")) ? Item(item, "category_label") : "AD")).PadRight(3).Substring(0, Math.Min(3, Text(JsTruthy(Item(item, "make")) ? Item(item, "make") : (JsTruthy(Item(item, "category_label")) ? Item(item, "category_label") : "AD")).Length)).ToUpperInvariant()) + "</span></a>";
            var compareHtml = JsTruthy(Item(item, "compare_price")) && JsNumber(Item(item, "compare_price")) > JsNumber(Item(item, "price"))
                ? "<span class=\"epc-acc__compare\">" + Esc(Money(Item(item, "compare_price"), Item(item, "currency"))) + "</span>"
                : "";
            var vehicleBits = string.Join(MidDot, new[] { Text(Item(item, "make")), Text(Item(item, "model")), Text(Item(item, "year")) }.Where(JsTruthy));
            var photoCount = JsTruthy(Item(item, "photo_count")) ? Item(item, "photo_count") : (object)(photos.Count > 0 ? photos.Count : 1);
            var markSrc = Text(JsTruthy(Item(item, "make")) ? Item(item, "make") : (JsTruthy(Item(item, "category_label")) ? Item(item, "category_label") : "AD"));
            var mark = markSrc.Length <= 3 ? markSrc.ToUpperInvariant() : markSrc[..3].ToUpperInvariant();
            img = cover != ""
                ? "<a class=\"epc-acc__media-link\" href=\"" + Esc(href) + "\"><img src=\"" + Esc(cover) + "\" alt=\"\" loading=\"lazy\" /></a>"
                : "<a class=\"epc-acc__media-link\" href=\"" + Esc(href) + "\"><span class=\"epc-acc__media-mark\">" + Esc(mark) + "</span></a>";
            var delay = (Math.Min(idx, 8) * 0.03d).ToString(CultureInfo.InvariantCulture) + "s";
            return "<article class=\"epc-acc__card" + (JsTruthy(Item(item, "featured")) ? " is-featured" : "") + "\" id=\"epc-acc-listing-" + Esc(Item(item, "id")) + "\" data-listing-id=\"" + Esc(Item(item, "id")) + "\" style=\"animation-delay:" + delay + "\">"
                + "<div class=\"epc-acc__media\">"
                + (JsTruthy(Item(item, "featured")) ? "<span class=\"epc-acc__featured\">Featured</span>" : "")
                + "<span class=\"epc-acc__media-badge\">" + Esc(Text(Item(item, "condition")) == "used" ? "Used" : "New") + "</span>"
                + "<span class=\"epc-acc__photos\"><i class=\"fa fa-camera\" aria-hidden=\"true\"></i> " + Esc(photoCount) + "</span>"
                + img
                + "</div><div class=\"epc-acc__body\">"
                + "<div class=\"epc-acc__meta\">"
                + (JsTruthy(Item(item, "category_label")) ? "<span>" + Esc(Item(item, "category_label")) + "</span>" : "")
                + (JsTruthy(Item(item, "subcategory_label")) ? "<span>" + Esc(Item(item, "subcategory_label")) + "</span>" : "")
                + (JsTruthy(Item(item, "city")) ? "<span>" + Esc(Item(item, "city")) + "</span>" : "")
                + "</div>"
                + "<h3 class=\"epc-acc__title\"><a href=\"" + Esc(href) + "\">" + Esc(Item(item, "title")) + "</a></h3>"
                + "<p class=\"epc-acc__article\">" + Esc(vehicleBits) + (JsTruthy(Item(item, "updated_at")) ? " · " + Esc(RelativeUpdated(Item(item, "updated_at"))) : "") + "</p>"
                + "<div class=\"epc-acc__price-row-card\"><div class=\"epc-acc__price\">" + Esc(Money(Item(item, "price"), Item(item, "currency"))) + compareHtml + "</div></div>"
                + "<div class=\"epc-acc__actions\">"
                + "<a class=\"primary\" href=\"" + Esc(href) + "\">View ad</a>"
                + "<button type=\"button\" class=\"secondary epc-acc__save" + (saved ? " is-saved" : "") + "\" data-id=\"" + Esc(Item(item, "id")) + "\">"
                + (saved ? "Saved" : "Save Ad") + "</button>"
                + "</div></div></article>";
        }));
    }

    public static string RenderPager(int page, int pages, bool showSavedOnly = false)
    {
        if (pages <= 1 || showSavedOnly)
        {
            return "";
        }

        var html = "";
        var start = Math.Max(1, page - 2);
        var end = Math.Min(pages, page + 2);
        if (page > 1)
        {
            html += "<button type=\"button\" data-page=\"" + (page - 1) + "\">Prev</button>";
        }

        for (var p = start; p <= end; p++)
        {
            html += "<button type=\"button\" class=\"" + (p == page ? "is-active" : "") + "\" data-page=\"" + p + "\">" + p + "</button>";
        }

        if (page < pages)
        {
            html += "<button type=\"button\" data-page=\"" + (page + 1) + "\">Next</button>";
        }

        return html;
    }

    public static string Load(Dictionary<string, object?> state, string? api = null)
    {
        var keys = new[] { "q", "category", "subcategory", "make", "model", "city", "condition", "price_min", "price_max", "id", "sort", "page" };
        var parts = new List<string>();
        foreach (var k in keys)
        {
            var v = Item(state, k);
            if (v is not null && Text(v) != "")
            {
                parts.Add(Uri.EscapeDataString(k) + "=" + Uri.EscapeDataString(Text(v)));
            }
        }

        parts.Add("per_page=24");
        return (api ?? SearchApi) + "?" + string.Join("&", parts);
    }

    public static string CountLabel(Dictionary<string, object?> data, Dictionary<string, object?>? state = null)
    {
        state ??= DefaultState();
        var total = JsNumber(Item(data, "total"));
        var label = total != 0 && !double.IsNaN(total)
            ? "<strong>" + Esc(Item(data, "from")) + " – " + Esc(Item(data, "to")) + "</strong> of <strong>" + Esc(Item(data, "total")) + "</strong> results"
            : "<strong>0</strong> results — categories ready, ads will be added category by category";
        if (JsTruthy(Item(state, "showSavedOnly")) || ShowSavedOnly)
        {
            label = "Showing <strong>saved</strong> ads from this search";
        }

        if (JsTruthy(Item(state, "id")) && JsNumber(Item(data, "total")) == 1)
        {
            label = "Showing listing <strong>#" + Esc(Item(state, "id")) + "</strong>";
        }
        else if (JsTruthy(Item(state, "id")) && !JsTruthy(Item(data, "total")))
        {
            label = "Listing <strong>#" + Esc(Item(state, "id")) + "</strong> not found or not published";
        }

        return label;
    }

    public static Dictionary<string, object?> SaveNotify(string email, Dictionary<string, object?>? state = null)
    {
        state ??= DefaultState();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["email"] = email,
            ["created_at"] = NowMs,
            ["filters"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["q"] = Item(state, "q"),
                ["category"] = Item(state, "category"),
                ["subcategory"] = Item(state, "subcategory"),
                ["make"] = Item(state, "make"),
                ["model"] = Item(state, "model"),
                ["city"] = Item(state, "city"),
                ["condition"] = Item(state, "condition"),
                ["price_min"] = Item(state, "price_min"),
                ["price_max"] = Item(state, "price_max")
            }
        };
    }

    public static Dictionary<string, object?> SetView(string mode)
        => new(StringComparer.Ordinal)
        {
            ["view"] = mode,
            ["resultsList"] = mode == "list" ? 1 : 0,
            ["gridActive"] = mode == "grid" ? 1 : 0,
            ["listActive"] = mode == "list" ? 1 : 0
        };

    public static Dictionary<string, object?> PageFlags(string html)
        => new(StringComparer.Ordinal)
        {
            ["root"] = html.Contains("id=\"epc-accessories\"", StringComparison.Ordinal) ? 1 : 0,
            ["api"] = html.Contains(SearchApi, StringComparison.Ordinal) ? 1 : 0,
            ["hero"] = html.Contains("Car spare parts and accessories", StringComparison.Ordinal) ? 1 : 0,
            ["search"] = html.Contains("id=\"epc-acc-search-form\"", StringComparison.Ordinal) ? 1 : 0,
            ["notify"] = html.Contains("id=\"epc-acc-notify-dialog\"", StringComparison.Ordinal) ? 1 : 0,
            ["saved"] = html.Contains("id=\"epc-acc-saved-bar\"", StringComparison.Ordinal) ? 1 : 0,
            ["version"] = html.Contains(Version, StringComparison.Ordinal) ? 1 : 0,
            ["css"] = html.Contains("/content/general_pages/epc_accessories.css?v=", StringComparison.Ordinal) ? 1 : 0,
            ["session"] = html.Contains("PHPSESSID", StringComparison.Ordinal) ? 1 : 0,
            ["lang_en"] = html.Contains("data-lang=\"/en\"", StringComparison.Ordinal) ? 1 : 0,
            ["lang_ar"] = html.Contains("data-lang=\"/ar\"", StringComparison.Ordinal) ? 1 : 0,
            ["prefix"] = html.Length <= 80 ? html : html[..80]
        };

    public static string RenderPage(string? langHref = null)
    {
        var lang = NormalizeLangHref(langHref ?? LangHref);
        return PageShell.Replace("__LANG__", HtmlEsc(lang), StringComparison.Ordinal) + PageScript;
    }

    public static Dictionary<string, object?> DefaultState()
        => new(StringComparer.Ordinal)
        {
            ["q"] = "",
            ["category"] = "",
            ["subcategory"] = "",
            ["make"] = "",
            ["model"] = "",
            ["city"] = "",
            ["condition"] = "",
            ["price_min"] = "",
            ["price_max"] = "",
            ["id"] = "",
            ["sort"] = DefaultSort,
            ["page"] = 1,
            ["view"] = "grid",
            ["year"] = "",
            ["showSavedOnly"] = false
        };

    private static object? Item(Dictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    private static List<Dictionary<string, object?>> AsMaps(object? value)
    {
        if (value is IEnumerable<Dictionary<string, object?>> typed)
        {
            return typed.ToList();
        }

        if (value is IEnumerable<object?> list && value is not string)
        {
            return list.OfType<Dictionary<string, object?>>().ToList();
        }

        return [];
    }

    private const string PageScript = """
<script>
(function () {
	var root = document.getElementById('epc-accessories');
	if (!root) { return; }
	var api = root.getAttribute('data-api') || '';
	var lang = root.getAttribute('data-lang') || '/en';
	var pageBase = root.getAttribute('data-base') || ((lang || '/en') + '/accessories-spare-parts');
	var SAVE_KEY = 'epc_acc_saved_ads';
	var NOTIFY_KEY = 'epc_acc_notify_alerts';
	var els = {
		q: document.getElementById('epc-acc-q'),
		form: document.getElementById('epc-acc-search-form'),
		cats: document.getElementById('epc-acc-cats'),
		subs: document.getElementById('epc-acc-subs'),
		makes: document.getElementById('epc-acc-makes'),
		cities: document.getElementById('epc-acc-cities'),
		condition: document.getElementById('epc-acc-condition'),
		model: document.getElementById('epc-acc-model'),
		priceMin: document.getElementById('epc-acc-price-min'),
		priceMax: document.getElementById('epc-acc-price-max'),
		applyPrice: document.getElementById('epc-acc-apply-price'),
		reset: document.getElementById('epc-acc-reset'),
		sort: document.getElementById('epc-acc-sort'),
		count: document.getElementById('epc-acc-count'),
		detail: document.getElementById('epc-acc-detail'),
		toolbar: document.getElementById('epc-acc-toolbar'),
		results: document.getElementById('epc-acc-results'),
		pager: document.getElementById('epc-acc-pager'),
		viewGrid: document.getElementById('epc-acc-view-grid'),
		viewList: document.getElementById('epc-acc-view-list'),
		browseCats: document.getElementById('epc-acc-browse-cats'),
		browseSubs: document.getElementById('epc-acc-browse-subs'),
		browseMakes: document.getElementById('epc-acc-browse-makes'),
		browseCities: document.getElementById('epc-acc-browse-cities'),
		vMake: document.getElementById('epc-acc-vehicle-make'),
		vModel: document.getElementById('epc-acc-vehicle-model'),
		vYear: document.getElementById('epc-acc-vehicle-year'),
		vApply: document.getElementById('epc-acc-vehicle-apply'),
		vClear: document.getElementById('epc-acc-vehicle-clear'),
		savedBar: document.getElementById('epc-acc-saved-bar'),
		savedCount: document.getElementById('epc-acc-saved-count'),
		showSaved: document.getElementById('epc-acc-show-saved'),
		notifyOpen: document.getElementById('epc-acc-notify-open'),
		notifyDialog: document.getElementById('epc-acc-notify-dialog'),
		notifyForm: document.getElementById('epc-acc-notify-form'),
		notifyEmail: document.getElementById('epc-acc-notify-email'),
		notifyNote: document.getElementById('epc-acc-notify-note')
	};

	var state = {
		q: '', category: '', subcategory: '', make: '', model: '', city: '',
		condition: '', price_min: '', price_max: '', id: '', sort: 'updated-desc', page: 1, view: 'grid',
		year: '', showSavedOnly: false,
		taxonomy: [], makes: [], cities: []
	};

	function esc(s) {
		return String(s == null ? '' : s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
	}
	function money(amount, currency) {
		var n = Number(amount);
		if (!isFinite(n) || n <= 0) { return 'Price on request'; }
		return String(currency || 'AED') + ' ' + n.toLocaleString(undefined, { maximumFractionDigits: 0 });
	}
	function getSaved() {
		try {
			var raw = localStorage.getItem(SAVE_KEY);
			var arr = raw ? JSON.parse(raw) : [];
			return Array.isArray(arr) ? arr.map(String) : [];
		} catch (e) { return []; }
	}
	function setSaved(ids) {
		try { localStorage.setItem(SAVE_KEY, JSON.stringify(ids)); } catch (e) {}
		updateSavedBar();
	}
	function toggleSaved(id) {
		id = String(id);
		var ids = getSaved();
		var i = ids.indexOf(id);
		if (i >= 0) { ids.splice(i, 1); } else { ids.push(id); }
		setSaved(ids);
		return ids.indexOf(id) >= 0;
	}
	function isSaved(id) { return getSaved().indexOf(String(id)) >= 0; }
	function updateSavedBar() {
		var n = getSaved().length;
		if (els.savedCount) els.savedCount.textContent = String(n);
		if (els.savedBar) els.savedBar.hidden = n < 1 && !state.showSavedOnly;
	}
	function relativeUpdated(ts) {
		var t = Number(ts) || 0;
		if (!t) return '';
		var diff = Math.max(0, Math.floor(Date.now() / 1000) - t);
		if (diff < 3600) return Math.max(1, Math.floor(diff / 60)) + ' min ago';
		if (diff < 86400) return Math.floor(diff / 3600) + ' hours ago';
		if (diff < 604800) return Math.floor(diff / 86400) + ' days ago';
		return new Date(t * 1000).toLocaleDateString();
	}
	/** Canonical storefront detail URL — never use category browse external_url. */
	function listingHref(item) {
		if (!item || !item.id) return pageBase;
		if (item.detail_url) return String(item.detail_url);
		if (item.url && String(item.url).indexOf('id=') >= 0) return String(item.url);
		var href = pageBase + (pageBase.indexOf('?') >= 0 ? '&' : '?') + 'id=' + encodeURIComponent(item.id);
		if (item.category) href += '&category=' + encodeURIComponent(item.category);
		if (item.subcategory) href += '&subcategory=' + encodeURIComponent(item.subcategory);
		return href;
	}
	function listingPhotos(item) {
		var photos = [];
		if (item && Array.isArray(item.photos)) {
			item.photos.forEach(function (p) {
				var u = typeof p === 'string' ? p : (p && p.url ? p.url : '');
				if (u) photos.push(u);
			});
		}
		if (!photos.length && item && item.image_url) {
			photos.push(String(item.image_url));
		}
		return photos;
	}
	function browseBackHref(item) {
		var href = pageBase;
		var qs = [];
		if (item && item.category) qs.push('category=' + encodeURIComponent(item.category));
		if (item && item.subcategory) qs.push('subcategory=' + encodeURIComponent(item.subcategory));
		if (!qs.length) return href;
		return href + (href.indexOf('?') >= 0 ? '&' : '?') + qs.join('&');
	}
	function clearDetail() {
		if (els.detail) {
			els.detail.hidden = true;
			els.detail.innerHTML = '';
		}
		if (els.toolbar) els.toolbar.hidden = false;
		if (els.results) els.results.hidden = false;
		if (els.pager) els.pager.hidden = false;
		root.classList.remove('is-detail');
	}
	function renderDetail(item) {
		if (!els.detail || !item) { clearDetail(); return; }
		var photos = listingPhotos(item);
		var main = photos[0] || '';
		var saved = isSaved(item.id);
		var vehicleBits = [item.make, item.model, item.year].filter(Boolean).join(' · ');
		var compareHtml = (item.compare_price && item.compare_price > item.price)
			? '<span class="epc-acc__compare">' + esc(money(item.compare_price, item.currency)) + '</span>'
			: '';
		var thumbs = photos.length > 1
			? '<div class="epc-acc__detail-thumbs" role="list">' + photos.map(function (u, i) {
				return '<button type="button" class="' + (i === 0 ? 'is-active' : '') + '" data-src="' + esc(u) + '" aria-label="Photo ' + (i + 1) + '">'
					+ '<img src="' + esc(u) + '" alt="" loading="lazy" /></button>';
			}).join('') + '</div>'
			: '';
		var desc = String(item.description || '').trim();
		els.detail.hidden = false;
		els.detail.innerHTML =
			'<div class="epc-acc__detail-bar">'
			+ '<a class="epc-acc__detail-back" href="' + esc(browseBackHref(item)) + '" id="epc-acc-detail-back"><i class="fa fa-arrow-left" aria-hidden="true"></i> Back to listings</a>'
			+ '<span class="epc-acc__detail-id">Ad #' + esc(item.id) + '</span>'
			+ '</div>'
			+ '<div class="epc-acc__detail-grid">'
			+ '<div class="epc-acc__detail-gallery">'
			+ '<div class="epc-acc__detail-main">'
			+ (main
				? '<img id="epc-acc-detail-main-img" src="' + esc(main) + '" alt="' + esc(item.title || '') + '" />'
				: '<div class="epc-acc__detail-placeholder"><i class="fa fa-image" aria-hidden="true"></i><span>No photo yet</span></div>')
			+ '</div>'
			+ thumbs
			+ '</div>'
			+ '<div class="epc-acc__detail-info">'
			+ '<div class="epc-acc__meta">'
			+ (item.category_label ? '<span>' + esc(item.category_label) + '</span>' : '')
			+ (item.subcategory_label ? '<span>' + esc(item.subcategory_label) + '</span>' : '')
			+ (item.city ? '<span>' + esc(item.city) + '</span>' : '')
			+ '<span>' + esc(item.condition === 'used' ? 'Used' : 'New') + '</span>'
			+ '</div>'
			+ '<h2 class="epc-acc__detail-title">' + esc(item.title || 'Listing') + '</h2>'
			+ (vehicleBits ? '<p class="epc-acc__article">' + esc(vehicleBits) + (item.updated_at ? ' · ' + esc(relativeUpdated(item.updated_at)) : '') + '</p>' : '')
			+ '<div class="epc-acc__price">' + esc(money(item.price, item.currency)) + compareHtml + '</div>'
			+ (item.stock_qty > 0 ? '<div class="epc-acc__stock">' + esc(item.stock_qty) + ' in stock</div>' : '')
			+ '<div class="epc-acc__detail-specs"><table><tbody>'
			+ '<tr><th scope="row">Make</th><td>' + esc(item.make || '—') + '</td></tr>'
			+ '<tr><th scope="row">Model</th><td>' + esc(item.model || '—') + '</td></tr>'
			+ '<tr><th scope="row">Year</th><td>' + esc(item.year || '—') + '</td></tr>'
			+ '<tr><th scope="row">City</th><td>' + esc(item.city || '—') + '</td></tr>'
			+ '<tr><th scope="row">Condition</th><td>' + esc(item.condition === 'used' ? 'Used' : 'New') + '</td></tr>'
			+ '<tr><th scope="row">Category</th><td>' + esc([item.category_label, item.subcategory_label].filter(Boolean).join(' / ') || '—') + '</td></tr>'
			+ '</tbody></table></div>'
			+ (desc ? '<div class="epc-acc__detail-desc"><h3>Details</h3><p>' + esc(desc) + '</p></div>' : '')
			+ '<div class="epc-acc__actions epc-acc__detail-actions">'
			+ '<button type="button" class="secondary epc-acc__save' + (saved ? ' is-saved' : '') + '" data-id="' + esc(item.id) + '" id="epc-acc-detail-save">'
			+ (saved ? 'Saved' : 'Save Ad') + '</button>'
			+ (item.external_url ? '<a class="primary" href="' + esc(item.external_url) + '" target="_blank" rel="noopener">Seller link</a>' : '')
			+ '</div>'
			+ '</div></div>';
		if (els.toolbar) els.toolbar.hidden = true;
		if (els.results) els.results.hidden = true;
		if (els.pager) els.pager.hidden = true;
		root.classList.add('is-detail');
		var mainImg = document.getElementById('epc-acc-detail-main-img');
		Array.prototype.forEach.call(els.detail.querySelectorAll('.epc-acc__detail-thumbs button'), function (btn) {
			btn.addEventListener('click', function () {
				Array.prototype.forEach.call(els.detail.querySelectorAll('.epc-acc__detail-thumbs button'), function (b) {
					b.classList.remove('is-active');
				});
				btn.classList.add('is-active');
				if (mainImg) {
					mainImg.src = btn.getAttribute('data-src') || mainImg.src;
				}
			});
		});
		var saveBtn = document.getElementById('epc-acc-detail-save');
		if (saveBtn) {
			saveBtn.addEventListener('click', function () {
				var on = toggleSaved(saveBtn.getAttribute('data-id'));
				saveBtn.textContent = on ? 'Saved' : 'Save Ad';
				saveBtn.classList.toggle('is-saved', on);
			});
		}
		var back = document.getElementById('epc-acc-detail-back');
		if (back) {
			back.addEventListener('click', function (ev) {
				// Soft navigate back to category list without full reload when possible.
				ev.preventDefault();
				state.id = '';
				state.category = item.category || state.category || '';
				state.subcategory = item.subcategory || state.subcategory || '';
				state.page = 1;
				clearDetail();
				load();
			});
		}
		try { window.scrollTo({ top: Math.max(0, root.offsetTop - 12), behavior: 'smooth' }); } catch (e) {}
	}

	function syncUrl() {
		try {
			var u = new URL(window.location.href);
			['q','category','subcategory','make','model','city','condition','price_min','price_max','id','sort','page'].forEach(function (k) {
				var v = state[k];
				if (v && String(v) !== '' && !(k === 'page' && String(v) === '1') && !(k === 'sort' && v === 'updated-desc')) {
					u.searchParams.set(k, v);
				} else {
					u.searchParams.delete(k);
				}
			});
			window.history.replaceState({}, '', u.pathname + u.search);
		} catch (e) {}
	}
	function readUrl() {
		try {
			var u = new URL(window.location.href);
			['q','category','subcategory','make','model','city','condition','price_min','price_max','id','sort','page'].forEach(function (k) {
				if (u.searchParams.has(k)) { state[k] = u.searchParams.get(k) || ''; }
			});
			if (els.q) els.q.value = state.q;
			if (els.sort) els.sort.value = state.sort || 'updated-desc';
			if (els.model) els.model.value = state.model;
			if (els.vModel) els.vModel.value = state.model;
			if (els.vMake) els.vMake.value = state.make;
			if (els.priceMin) els.priceMin.value = state.price_min;
			if (els.priceMax) els.priceMax.value = state.price_max;
		} catch (e) {}
	}

	function facetList(target, rows, valueKey, labelKey, active, onPick, allLabel) {
		if (!target) return;
		var html = '<li><button type="button" class="' + (!active ? 'is-active' : '') + '" data-value="">' + esc(allLabel || 'All') + '</button></li>';
		(rows || []).forEach(function (row) {
			var value = row[valueKey];
			var label = row[labelKey] || value;
			var count = row.count != null ? row.count : null;
			var cls = String(value) === String(active) ? ' is-active' : '';
			html += '<li><button type="button" class="' + cls + '" data-value="' + esc(value) + '"><span>' + esc(label) + '</span>'
				+ (count != null ? '<span class="count">' + esc(count) + '</span>' : '') + '</button></li>';
		});
		target.innerHTML = html;
		Array.prototype.forEach.call(target.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () { onPick(btn.getAttribute('data-value') || ''); });
		});
	}

	function fillVehicleMakes(makes) {
		if (!els.vMake) return;
		var cur = state.make || els.vMake.value || '';
		var opts = '<option value="">Any make</option>';
		(makes || []).forEach(function (m) {
			var name = typeof m === 'string' ? m : (m.make || '');
			if (!name) return;
			opts += '<option value="' + esc(name) + '"' + (name === cur ? ' selected' : '') + '>' + esc(name) + '</option>';
		});
		els.vMake.innerHTML = opts;
	}

	function renderFacets(facets) {
		var cats = (facets && facets.categories) || state.taxonomy || [];
		facetList(els.cats, cats, 'slug', 'label', state.category, function (v) {
			state.category = (state.category === v) ? '' : v;
			state.subcategory = '';
			state.page = 1;
			load();
		}, 'All categories');

		var subs = [];
		cats.forEach(function (c) {
			if (state.category && c.slug !== state.category) return;
			(c.subs || c.children || []).forEach(function (s) { subs.push(s); });
		});
		facetList(els.subs, subs, 'slug', 'label', state.subcategory, function (v) {
			state.subcategory = (state.subcategory === v) ? '' : v;
			state.page = 1;
			load();
		}, 'All sub categories');

		var makes = (facets && facets.makes) || [];
		facetList(els.makes, makes, 'make', 'make', state.make, function (v) {
			state.make = (state.make === v) ? '' : v;
			if (els.vMake) els.vMake.value = state.make;
			state.page = 1;
			load();
		}, 'All makes');
		fillVehicleMakes(makes);

		facetList(els.cities, (facets && facets.cities) || [], 'city', 'city', state.city, function (v) {
			state.city = (state.city === v) ? '' : v;
			state.page = 1;
			load();
		}, 'All cities');

		facetList(els.condition, (facets && facets.conditions) || [
			{value:'new', label:'New'}, {value:'used', label:'Used'}
		], 'value', 'label', state.condition, function (v) {
			state.condition = (state.condition === v) ? '' : v;
			state.page = 1;
			load();
		}, 'Any condition');

		if (els.browseCats) {
			els.browseCats.innerHTML = cats.map(function (c) {
				return '<button type="button" data-cat="' + esc(c.slug) + '">' + esc(c.label) + (c.count ? ' (' + c.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseCats.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.category = btn.getAttribute('data-cat') || '';
					state.subcategory = '';
					state.page = 1;
					load();
					root.scrollIntoView({behavior:'smooth', block:'start'});
				});
			});
		}
		if (els.browseSubs) {
			var allSubs = [];
			cats.forEach(function (c) {
				(c.subs || c.children || []).slice(0, 8).forEach(function (s) {
					allSubs.push({ slug: s.slug, label: s.label, parent: c.slug, count: s.count });
				});
			});
			els.browseSubs.innerHTML = allSubs.slice(0, 40).map(function (s) {
				return '<button type="button" data-cat="' + esc(s.parent) + '" data-sub="' + esc(s.slug) + '">' + esc(s.label) + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseSubs.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.category = btn.getAttribute('data-cat') || '';
					state.subcategory = btn.getAttribute('data-sub') || '';
					state.page = 1;
					load();
					root.scrollIntoView({behavior:'smooth', block:'start'});
				});
			});
		}
		if (els.browseMakes) {
			els.browseMakes.innerHTML = makes.map(function (m) {
				return '<button type="button" data-make="' + esc(m.make) + '">' + esc(m.make) + ' Parts and Accessories' + (m.count ? ' (' + m.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseMakes.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.make = btn.getAttribute('data-make') || '';
					if (els.vMake) els.vMake.value = state.make;
					state.page = 1;
					load();
				});
			});
		}
		if (els.browseCities) {
			var cities = (facets && facets.cities) || [];
			els.browseCities.innerHTML = cities.map(function (c) {
				return '<button type="button" data-city="' + esc(c.city) + '">Parts and Accessories in ' + esc(c.city) + (c.count ? ' (' + c.count + ')' : '') + '</button>';
			}).join('');
			Array.prototype.forEach.call(els.browseCities.querySelectorAll('button'), function (btn) {
				btn.addEventListener('click', function () {
					state.city = btn.getAttribute('data-city') || '';
					state.page = 1;
					load();
				});
			});
		}
	}

	function renderCards(items, emptyCatalog) {
		if (!els.results) return;
		var savedIds = getSaved();
		if (state.showSavedOnly) {
			items = (items || []).filter(function (it) { return savedIds.indexOf(String(it.id)) >= 0; });
		}
		if (state.id) {
			var focused = (items || []).filter(function (it) { return String(it.id) === String(state.id); })[0];
			if (focused) {
				renderDetail(focused);
				return;
			}
			clearDetail();
			els.results.hidden = false;
			els.results.innerHTML =
				'<div class="epc-acc__empty">'
				+ '<strong>Listing not found</strong>'
				+ '<p>This ad is unavailable or not published. <a href="' + esc(browseBackHref({ category: state.category, subcategory: state.subcategory })) + '">Back to listings</a></p>'
				+ '</div>';
			return;
		}
		clearDetail();
		if (!items || !items.length) {
			els.results.innerHTML =
				'<div class="epc-acc__empty">'
				+ '<strong>' + (state.showSavedOnly ? 'No saved ads yet' : 'No ads in this search yet') + '</strong>'
				+ '<p>Browse by category, make, model, and city. Listings will appear here as they are added category by category.</p>'
				+ (emptyCatalog && !state.showSavedOnly ? '<p class="epc-acc__empty-hint">Catalog is ready — start by adding products under Car Care, Interior, Exterior, Brakes, and other categories.</p>' : '')
				+ '</div>';
			return;
		}
		els.results.innerHTML = items.map(function (item, idx) {
			var href = listingHref(item);
			var saved = isSaved(item.id);
			var cover = listingPhotos(item)[0] || item.image_url || '';
			var img = cover
				? '<a class="epc-acc__media-link" href="' + esc(href) + '"><img src="' + esc(cover) + '" alt="" loading="lazy" /></a>'
				: '<a class="epc-acc__media-link" href="' + esc(href) + '"><span class="epc-acc__media-mark">' + esc((item.make || item.category_label || 'AD').toString().slice(0, 3).toUpperCase()) + '</span></a>';
			var compareHtml = (item.compare_price && item.compare_price > item.price)
				? '<span class="epc-acc__compare">' + esc(money(item.compare_price, item.currency)) + '</span>'
				: '';
			var vehicleBits = [item.make, item.model, item.year].filter(Boolean).join(' · ');
			return '<article class="epc-acc__card' + (item.featured ? ' is-featured' : '') + '" id="epc-acc-listing-' + esc(item.id) + '" data-listing-id="' + esc(item.id) + '" style="animation-delay:' + (Math.min(idx, 8) * 0.03) + 's">'
				+ '<div class="epc-acc__media">'
				+ (item.featured ? '<span class="epc-acc__featured">Featured</span>' : '')
				+ '<span class="epc-acc__media-badge">' + esc(item.condition === 'used' ? 'Used' : 'New') + '</span>'
				+ '<span class="epc-acc__photos"><i class="fa fa-camera" aria-hidden="true"></i> ' + esc(item.photo_count || listingPhotos(item).length || 1) + '</span>'
				+ img
				+ '</div><div class="epc-acc__body">'
				+ '<div class="epc-acc__meta">'
				+ (item.category_label ? '<span>' + esc(item.category_label) + '</span>' : '')
				+ (item.subcategory_label ? '<span>' + esc(item.subcategory_label) + '</span>' : '')
				+ (item.city ? '<span>' + esc(item.city) + '</span>' : '')
				+ '</div>'
				+ '<h3 class="epc-acc__title"><a href="' + esc(href) + '">' + esc(item.title) + '</a></h3>'
				+ '<p class="epc-acc__article">' + esc(vehicleBits) + (item.updated_at ? ' · ' + esc(relativeUpdated(item.updated_at)) : '') + '</p>'
				+ '<div class="epc-acc__price-row-card"><div class="epc-acc__price">' + esc(money(item.price, item.currency)) + compareHtml + '</div></div>'
				+ '<div class="epc-acc__actions">'
				+ '<a class="primary" href="' + esc(href) + '">View ad</a>'
				+ '<button type="button" class="secondary epc-acc__save' + (saved ? ' is-saved' : '') + '" data-id="' + esc(item.id) + '">'
				+ (saved ? 'Saved' : 'Save Ad') + '</button>'
				+ '</div></div></article>';
		}).join('');
		Array.prototype.forEach.call(els.results.querySelectorAll('.epc-acc__save'), function (btn) {
			btn.addEventListener('click', function () {
				var on = toggleSaved(btn.getAttribute('data-id'));
				btn.textContent = on ? 'Saved' : 'Save Ad';
				btn.classList.toggle('is-saved', on);
				if (state.showSavedOnly) { renderCards(items, emptyCatalog); }
			});
		});
	}

	function renderPager(page, pages) {
		if (!els.pager) return;
		if (pages <= 1 || state.showSavedOnly) { els.pager.innerHTML = ''; return; }
		var html = '';
		var start = Math.max(1, page - 2), end = Math.min(pages, page + 2);
		if (page > 1) html += '<button type="button" data-page="' + (page - 1) + '">Prev</button>';
		for (var p = start; p <= end; p++) {
			html += '<button type="button" class="' + (p === page ? 'is-active' : '') + '" data-page="' + p + '">' + p + '</button>';
		}
		if (page < pages) html += '<button type="button" data-page="' + (page + 1) + '">Next</button>';
		els.pager.innerHTML = html;
		Array.prototype.forEach.call(els.pager.querySelectorAll('button'), function (btn) {
			btn.addEventListener('click', function () {
				state.page = parseInt(btn.getAttribute('data-page'), 10) || 1;
				load();
			});
		});
	}

	function load() {
		if (!api || !els.results) return;
		els.results.innerHTML = '<div class="epc-acc__loading"><i class="fa fa-spinner fa-spin"></i> Loading…</div>';
		els.count.textContent = 'Loading…';
		syncUrl();
		var params = new URLSearchParams();
		['q','category','subcategory','make','model','city','condition','price_min','price_max','id','sort','page'].forEach(function (k) {
			if (state[k] !== '' && state[k] != null) params.set(k, state[k]);
		});
		params.set('per_page', '24');
		fetch(api + '?' + params.toString(), { credentials: 'same-origin', cache: 'no-store' })
			.then(function (r) { return r.json(); })
			.then(function (data) {
				if (!data || !data.status) {
					els.results.innerHTML = '<div class="epc-acc__empty">Could not load accessories catalog.</div>';
					els.count.textContent = '0 results';
					return;
				}
				state.taxonomy = data.taxonomy || data.facets && data.facets.categories || [];
				var countLabel = data.total
					? ('<strong>' + esc(data.from) + ' – ' + esc(data.to) + '</strong> of <strong>' + esc(data.total) + '</strong> results')
					: '<strong>0</strong> results — categories ready, ads will be added category by category';
				if (state.showSavedOnly) {
					countLabel = 'Showing <strong>saved</strong> ads from this search';
				}
				if (state.id && data.total === 1) {
					countLabel = 'Showing listing <strong>#' + esc(state.id) + '</strong>';
				} else if (state.id && !data.total) {
					countLabel = 'Listing <strong>#' + esc(state.id) + '</strong> not found or not published';
				}
				els.count.innerHTML = countLabel;
				renderFacets(data.facets || {});
				renderCards(data.items || [], !!data.empty_catalog);
				renderPager(data.page || 1, data.pages || 1);
			})
			.catch(function () {
				els.results.innerHTML = '<div class="epc-acc__empty">Network error loading accessories.</div>';
			});
	}

	if (els.form) {
		els.form.addEventListener('submit', function (e) {
			e.preventDefault();
			state.q = els.q ? els.q.value.trim() : '';
			state.page = 1;
			load();
		});
	}
	if (els.sort) {
		els.sort.addEventListener('change', function () {
			state.sort = els.sort.value || 'updated-desc';
			state.page = 1;
			load();
		});
	}
	if (els.applyPrice) {
		els.applyPrice.addEventListener('click', function () {
			state.model = els.model ? els.model.value.trim() : '';
			if (els.vModel) els.vModel.value = state.model;
			state.price_min = els.priceMin ? els.priceMin.value : '';
			state.price_max = els.priceMax ? els.priceMax.value : '';
			state.page = 1;
			load();
		});
	}
	if (els.reset) {
		els.reset.addEventListener('click', function () {
			state = { q:'', category:'', subcategory:'', make:'', model:'', city:'', condition:'', price_min:'', price_max:'', id:'', sort:'updated-desc', page:1, view:state.view, year:'', showSavedOnly:false, taxonomy:state.taxonomy, makes:state.makes, cities:state.cities };
			if (els.q) els.q.value = '';
			if (els.model) els.model.value = '';
			if (els.vMake) els.vMake.value = '';
			if (els.vModel) els.vModel.value = '';
			if (els.vYear) els.vYear.value = '';
			if (els.sort) els.sort.value = 'updated-desc';
			if (els.priceMin) els.priceMin.value = '';
			if (els.priceMax) els.priceMax.value = '';
			load();
		});
	}
	if (els.vApply) {
		els.vApply.addEventListener('click', function () {
			state.make = els.vMake ? els.vMake.value : '';
			state.model = els.vModel ? els.vModel.value.trim() : '';
			state.year = els.vYear ? els.vYear.value.trim() : '';
			if (els.model) els.model.value = state.model;
			if (state.year) {
				state.q = state.year;
				if (els.q) els.q.value = state.year;
			}
			state.page = 1;
			load();
		});
	}
	if (els.vClear) {
		els.vClear.addEventListener('click', function () {
			state.make = '';
			state.model = '';
			state.year = '';
			if (state.q && /^\d{4}$/.test(state.q)) {
				state.q = '';
				if (els.q) els.q.value = '';
			}
			if (els.vMake) els.vMake.value = '';
			if (els.vModel) els.vModel.value = '';
			if (els.vYear) els.vYear.value = '';
			if (els.model) els.model.value = '';
			state.page = 1;
			load();
		});
	}
	if (els.showSaved) {
		els.showSaved.addEventListener('click', function () {
			state.showSavedOnly = !state.showSavedOnly;
			els.showSaved.textContent = state.showSavedOnly ? 'Show all' : 'Show saved';
			load();
		});
	}
	if (els.notifyOpen && els.notifyDialog) {
		els.notifyOpen.addEventListener('click', function () {
			if (els.notifyNote) els.notifyNote.hidden = true;
			if (typeof els.notifyDialog.showModal === 'function') {
				els.notifyDialog.showModal();
			} else {
				var email = window.prompt('Email for Notify Me alerts');
				if (email) saveNotify(email);
			}
		});
	}
	function saveNotify(email) {
		var alert = {
			email: email,
			created_at: Date.now(),
			filters: {
				q: state.q, category: state.category, subcategory: state.subcategory,
				make: state.make, model: state.model, city: state.city,
				condition: state.condition, price_min: state.price_min, price_max: state.price_max
			}
		};
		try {
			var raw = localStorage.getItem(NOTIFY_KEY);
			var arr = raw ? JSON.parse(raw) : [];
			if (!Array.isArray(arr)) arr = [];
			arr.push(alert);
			localStorage.setItem(NOTIFY_KEY, JSON.stringify(arr));
		} catch (e) {}
		if (els.notifyNote) {
			els.notifyNote.hidden = false;
			els.notifyNote.textContent = 'Alert saved for ' + email + '. We will use this when notifications go live.';
		}
	}
	if (els.notifyForm) {
		els.notifyForm.addEventListener('submit', function (e) {
			var submitter = e.submitter || null;
			var val = submitter ? submitter.value : 'ok';
			if (val === 'cancel') return;
			e.preventDefault();
			var email = els.notifyEmail ? els.notifyEmail.value.trim() : '';
			if (!email) return;
			saveNotify(email);
			setTimeout(function () {
				if (els.notifyDialog && els.notifyDialog.open) els.notifyDialog.close();
			}, 900);
		});
	}

	function setView(mode) {
		state.view = mode;
		if (els.results) els.results.classList.toggle('is-list', mode === 'list');
		if (els.viewGrid) els.viewGrid.classList.toggle('is-active', mode === 'grid');
		if (els.viewList) els.viewList.classList.toggle('is-active', mode === 'list');
	}
	if (els.viewGrid) els.viewGrid.addEventListener('click', function () { setView('grid'); });
	if (els.viewList) els.viewList.addEventListener('click', function () { setView('list'); });

	readUrl();
	updateSavedBar();
	setView(state.view || 'grid');
	load();
})();
</script>
""";

    private const string PageShell = """
<link rel="stylesheet" href="/content/general_pages/epc_accessories.css?v=20260721accDetail2">

<section class="epc-acc" id="epc-accessories"
	data-api="/content/shop/docpart/ajax_epc_accessories_search.php"
	data-lang="__LANG__">

	<div class="epc-acc__hero">
		<div class="container">
			<div class="epc-acc__brand">eParts<span>Cart</span></div>
			<h1>Car spare parts and accessories</h1>
			<p>Browse categories and filters. Listings are added into each category over time.</p>
			<div class="epc-acc__hero-cta">
				<form class="epc-acc__search" id="epc-acc-search-form" role="search">
					<label class="sr-only" for="epc-acc-q">Search accessories</label>
					<input id="epc-acc-q" name="q" type="search" placeholder="Search accessories and spare parts" maxlength="80" />
					<button type="submit"><i class="fa fa-search" aria-hidden="true"></i> Search</button>
				</form>
				<button type="button" class="epc-acc__btn epc-acc__btn--ghost" id="epc-acc-notify-open">Notify Me</button>
			</div>
		</div>
	</div>

	<div class="container">
		<div class="epc-acc__vehicle" id="epc-acc-vehicle">
			<div class="epc-acc__vehicle-head">
				<strong>Select Vehicle</strong>
				<span>Filter ads by make and model</span>
			</div>
			<div class="epc-acc__vehicle-row">
				<label>
					<span>Make</span>
					<select id="epc-acc-vehicle-make" aria-label="Vehicle make">
						<option value="">Any make</option>
					</select>
				</label>
				<label>
					<span>Model</span>
					<input type="text" id="epc-acc-vehicle-model" placeholder="e.g. Corolla, Civic" />
				</label>
				<label>
					<span>Year</span>
					<input type="text" id="epc-acc-vehicle-year" placeholder="e.g. 2018" maxlength="4" />
				</label>
				<button type="button" class="epc-acc__btn" id="epc-acc-vehicle-apply">Apply</button>
				<button type="button" class="epc-acc__btn epc-acc__btn-muted" id="epc-acc-vehicle-clear">Clear</button>
			</div>
			<div class="epc-acc__saved-bar" id="epc-acc-saved-bar" hidden>
				Saved ads: <strong id="epc-acc-saved-count">0</strong>
				<button type="button" id="epc-acc-show-saved">Show saved</button>
			</div>
		</div>

		<div class="epc-acc__layout">
			<aside class="epc-acc__side" aria-label="Filters">
				<div class="epc-acc__facet">
					<h3>Category</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-cats"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Sub category</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-subs"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Make</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-makes"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Model</h3>
					<input type="text" id="epc-acc-model" class="epc-acc-input" placeholder="e.g. Corolla, Civic" />
				</div>
				<div class="epc-acc__facet">
					<h3>City</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-cities"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Condition</h3>
					<ul class="epc-acc__facet-list" id="epc-acc-condition"></ul>
				</div>
				<div class="epc-acc__facet">
					<h3>Price range</h3>
					<div class="epc-acc__price-row">
						<input type="number" id="epc-acc-price-min" min="0" step="1" placeholder="Min" />
						<input type="number" id="epc-acc-price-max" min="0" step="1" placeholder="Max" />
					</div>
					<button type="button" class="epc-acc__btn" id="epc-acc-apply-price">Apply filters</button>
					<button type="button" class="epc-acc__btn epc-acc__btn-muted" id="epc-acc-reset">Reset</button>
				</div>
			</aside>

			<div class="epc-acc__main">
				<div id="epc-acc-detail" class="epc-acc__detail" hidden aria-live="polite"></div>
				<div class="epc-acc__toolbar" id="epc-acc-toolbar">
					<div class="epc-acc__count" id="epc-acc-count">Loading…</div>
					<div class="epc-acc__tools">
						<label>
							<span class="sr-only">Sort by</span>
							<select id="epc-acc-sort" aria-label="Sort by">
								<option value="updated-desc">Updated Date: Recent First</option>
								<option value="updated-asc">Updated Date: Oldest First</option>
								<option value="price-asc">Price: Low to High</option>
								<option value="price-desc">Price: High to Low</option>
								<option value="top-sales">Top Sales</option>
							</select>
						</label>
						<div class="epc-acc__view-toggle" role="group" aria-label="View mode">
							<button type="button" id="epc-acc-view-list" title="List">LIST</button>
							<button type="button" id="epc-acc-view-grid" class="is-active" title="Grid">GRID</button>
						</div>
					</div>
				</div>
				<div id="epc-acc-results" class="epc-acc__grid" aria-live="polite"></div>
				<div class="epc-acc__pager" id="epc-acc-pager"></div>
			</div>
		</div>

		<div class="epc-acc__browse">
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by category</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-cats"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View by sub category</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-subs"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by make</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-makes"></div>
			</div>
			<div class="epc-acc__browse-block">
				<h2>View spare parts and accessories by city</h2>
				<div class="epc-acc__chips" id="epc-acc-browse-cities"></div>
			</div>
		</div>
	</div>

	<dialog class="epc-acc__dialog" id="epc-acc-notify-dialog">
		<form method="dialog" class="epc-acc__dialog-inner" id="epc-acc-notify-form">
			<h3>Notify Me</h3>
			<p>Get an alert when new ads match your current filters.</p>
			<label>
				<span>Email</span>
				<input type="email" id="epc-acc-notify-email" required placeholder="you@example.com" />
			</label>
			<div class="epc-acc__dialog-actions">
				<button type="submit" class="epc-acc__btn" value="ok">Save alert</button>
				<button type="submit" class="epc-acc__btn epc-acc__btn-muted" value="cancel">Cancel</button>
			</div>
			<p class="epc-acc__dialog-note" id="epc-acc-notify-note" hidden></p>
		</form>
	</dialog>
</section>


""";
}
