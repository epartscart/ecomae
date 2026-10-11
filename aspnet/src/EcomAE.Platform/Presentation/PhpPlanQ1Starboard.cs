using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-starboard full CP brochure. PHP identifiers kept for the inventory:
/// <c>epc_cp_brochure_load_inventory</c>, <c>epc_cp_brochure_dedupe_items</c>,
/// <c>epc_cp_brochure_filtered_bundle</c>, <c>epc_cp_brochure_filtered_inventory</c>,
/// <c>epc_cp_brochure_css</c>, <c>epc_cp_full_brochure_render_html</c>,
/// <c>epc_cp_brochure_render_deck_body</c>, <c>epc_cp_brochure_render_catalog_body</c>,
/// <c>epc_cp_full_brochure_render_and_exit</c>.
/// Path: <c>content/general_pages/epc_cp_full_brochure.php</c>.
/// GET never mints a session cookie. Live inventory / marketing brochure parents stay injectable.
/// </summary>
public static class PhpPlanQ1Starboard
{
    public const string FullBrochurePath = "content/general_pages/epc_cp_full_brochure.php";

    public static Func<Dictionary<string, object?>>? LiveInventory { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? AreaVisuals { get; set; }
    public static Func<Dictionary<string, object?>, string, Dictionary<string, object?>>? ItemPhoto { get; set; }
    public static Func<Dictionary<string, object?>, string, string>? ItemImage { get; set; }
    public static Func<string, Dictionary<string, object?>>? BrochureProfile { get; set; }
    public static Func<Dictionary<string, object?>, string>? BrochureCss { get; set; }
    public static Func<object?, string>? BrochureH { get; set; }
    public static string QueryView { get; set; } = "deck";
    public static Func<long>? Clock { get; set; }
    public static bool HeadersSent { get; set; }
    public static List<string> ResponseHeaders { get; set; } = [];
    public static bool ExitCalled { get; set; }

    public static void Reset()
    {
        LiveInventory = PhpPlanQ1Port.EpcCpBrochureBuildLiveInventory;
        AreaVisuals = PhpPlanQ1Port.EpcCpBrochureAreaVisuals;
        ItemPhoto = PhpPlanQ1Port.EpcCpBrochureItemPhotoMeta;
        ItemImage = PhpPlanQ1Port.EpcCpBrochureItemImage;
        BrochureProfile = PhpPlanQ1Sprit.EpcBrochureProfile;
        BrochureCss = PhpPlanQ1Sprit.EpcBrochureCss;
        BrochureH = PhpPlanQ1Sprit.EpcBrochureH;
        QueryView = "deck";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        HeadersSent = false;
        ResponseHeaders = [];
        ExitCalled = false;
    }

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcCpBrochureLoadInventory()
    {
        var live = LiveInventory != null ? LiveInventory() : new Dictionary<string, object?>(StringComparer.Ordinal);
        return AreasOf(live);
    }

    public static List<Dictionary<string, object?>> EpcCpBrochureDedupeItems(IEnumerable<Dictionary<string, object?>> items)
    {
        var byUrl = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var noUrl = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            var url = Str(item, "url").Trim();
            var name = Str(item, "name").Trim();
            var does = Str(item, "does").Trim();
            var isStub = name.StartsWith("Epc ", StringComparison.Ordinal) || does == "Open from left CP menu.";
            if (url == "")
            {
                noUrl.Add(item);
                continue;
            }

            if (!byUrl.TryGetValue(url, out var prev))
            {
                byUrl[url] = item;
                continue;
            }

            var prevStub = Str(prev, "name").StartsWith("Epc ", StringComparison.Ordinal) || Str(prev, "does") == "Open from left CP menu.";
            if (prevStub && !isStub)
            {
                byUrl[url] = item;
            }
        }

        var output = byUrl.Values.ToList();
        output.AddRange(noUrl);
        output.Sort((a, b) => string.Compare(Convert.ToString(a.TryGetValue("name", out var an) ? an : ""), Convert.ToString(b.TryGetValue("name", out var bn) ? bn : ""), StringComparison.OrdinalIgnoreCase));
        return output;
    }

    public static Dictionary<string, object?> EpcCpBrochureFilteredBundle(string scope)
    {
        scope = Regex.Replace(scope.ToLowerInvariant(), "[^a-z]", "");
        if (scope is not ("client" or "super" or "all"))
        {
            scope = "client";
        }

        var live = LiveInventory != null ? LiveInventory() : new Dictionary<string, object?>(StringComparer.Ordinal);
        var output = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        foreach (var (area, items) in AreasOf(live))
        {
            var keep = new List<Dictionary<string, object?>>();
            foreach (var item in items)
            {
                var s = item.TryGetValue("scope", out var sc) && Convert.ToString(sc) is { Length: > 0 } sv ? sv : "client";
                if (scope == "all" || (scope == "client" && s is "client" or "both") || (scope == "super" && s is "super" or "both"))
                {
                    keep.Add(item);
                }
            }

            keep = EpcCpBrochureDedupeItems(keep);
            if (keep.Count > 0)
            {
                output[area] = keep;
            }
        }

        var total = output.Values.Sum(v => v.Count);
        var meta = live.TryGetValue("meta", out var m) && m is Dictionary<string, object?> md
            ? new Dictionary<string, object?>(md, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        meta["total"] = total;
        meta["area_count"] = output.Count;
        meta["scope"] = scope;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["areas"] = output, ["meta"] = meta };
    }

    public static Dictionary<string, List<Dictionary<string, object?>>> EpcCpBrochureFilteredInventory(string scope)
        => AreasOf(EpcCpBrochureFilteredBundle(scope));

    public static string EpcCpBrochureCss(Dictionary<string, object?> profile)
    {
        var baseCss = BrochureCss != null ? BrochureCss(profile) : "";
        return baseCss + ExtraCss;
    }

    public static string EpcCpFullBrochureRenderHtml(Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var brand = Regex.Replace((Convert.ToString(opts.TryGetValue("brand", out var b) ? b : "epartscart") ?? "epartscart").ToLowerInvariant(), "[^a-z0-9_]", "");
        if (brand != "ecomae")
        {
            brand = "epartscart";
        }

        var scope = Regex.Replace((Convert.ToString(opts.TryGetValue("scope", out var s) ? s : "client") ?? "client").ToLowerInvariant(), "[^a-z]", "");
        if (scope is not ("client" or "super" or "all"))
        {
            scope = "client";
        }

        var viewRaw = opts.TryGetValue("view", out var v) && !string.IsNullOrEmpty(Convert.ToString(v))
            ? Convert.ToString(v)
            : QueryView;
        var view = Regex.Replace((viewRaw ?? "deck").ToLowerInvariant(), "[^a-z]", "");
        if (view != "catalog")
        {
            view = "deck";
        }

        var p = BrochureProfile != null ? BrochureProfile(brand) : new Dictionary<string, object?>(StringComparer.Ordinal);
        var bundle = EpcCpBrochureFilteredBundle(scope);
        var inv = AreasOf(bundle);
        var meta = bundle["meta"] is Dictionary<string, object?> md ? md : new Dictionary<string, object?>(StringComparer.Ordinal);
        var total = ToInt(meta, "total");
        var areaCount = ToInt(meta, "area_count");
        var generated = meta.TryGetValue("generated_at", out var ga) ? ToLong(ga) : (Clock != null ? Clock() : DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var syncedAt = PhpDate(generated);
        var sources = meta.TryGetValue("sources", out var src) && src is IEnumerable<string> sl ? sl.ToList()
            : src is IEnumerable<object?> ol ? ol.Select(x => Convert.ToString(x) ?? "").ToList()
            : [];

        var basePath = Convert.ToString(opts.TryGetValue("base_path", out var bp) ? bp : "/brochure/cp") ?? "/brochure/cp";
        string Link(string sc, string vw = "")
        {
            var q = "scope=" + Uri.EscapeDataString(sc);
            if (vw != "")
            {
                q += "&view=" + Uri.EscapeDataString(vw);
            }

            return H(basePath + "?" + q);
        }

        var scopeLabel = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["client"] = "Client CP (tenant)",
            ["super"] = "Super CP (platform)",
            ["all"] = "All CP functions"
        };
        var visuals = AreaVisuals != null ? AreaVisuals() : new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        const string productBrochure = "/brochure";
        var css = EpcCpBrochureCss(p);
        var title = H(Str(p, "name") + " — Full Control Panel brochure");
        var desc = H("Graphical map of every Control Panel function (" + total + " items) — auto-synced. " + (scopeLabel.TryGetValue(scope, out var slb) ? slb : scope) + ".");
        var cover = H(Str(p, "cover"));
        var autoPrint = !PhpEmpty(opts.TryGetValue("print", out var pr) ? pr : null)
            ? "<script>window.addEventListener(\"load\",function(){setTimeout(function(){window.print()},400)});</script>"
            : "";

        var filters = "<div class=\"epc-br__filters\">"
            + "<a class=\"" + (scope == "client" ? "is-on" : "") + "\" href=\"" + Link("client", view) + "\">Client CP</a>"
            + "<a class=\"" + (scope == "super" ? "is-on" : "") + "\" href=\"" + Link("super", view) + "\">Super CP</a>"
            + "<a class=\"" + (scope == "all" ? "is-on" : "") + "\" href=\"" + Link("all", view) + "\">All</a>"
            + "</div>";

        var viewToggle = "<div class=\"epc-br__viewtoggle epc-br__filters\">"
            + "<a class=\"" + (view == "deck" ? "is-on" : "") + "\" href=\"" + Link(scope, "deck") + "\"><i class=\"fa fa-th-large\"></i> Graphical</a>"
            + "<a class=\"" + (view == "catalog" ? "is-on" : "") + "\" href=\"" + Link(scope, "catalog") + "\"><i class=\"fa fa-list\"></i> Catalogue</a>"
            + "</div>";

        var bodyInner = view == "catalog"
            ? EpcCpBrochureRenderCatalogBody(inv, scopeLabel.TryGetValue(scope, out var sl2) ? sl2 : scope, total)
            : EpcCpBrochureRenderDeckBody(inv, visuals, total, areaCount, syncedAt, sources);

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>" + title + "</title>"
            + "<meta name=\"description\" content=\"" + desc + "\">"
            + "<meta name=\"robots\" content=\"index,follow\">"
            + "<meta property=\"og:title\" content=\"" + title + "\">"
            + "<meta property=\"og:image\" content=\"" + H(Str(p, "url").TrimEnd('/') + Str(p, "cover")) + "\">"
            + "<style>" + css + "</style></head><body>"
            + "<div class=\"epc-br epc-br--deck\">"
            + "<div class=\"epc-br__bar\"><a class=\"epc-br__brand\" href=\"" + H(Str(p, "url")) + "\">" + H(Str(p, "name")) + "</a>"
            + "<div class=\"epc-br__actions\">"
            + "<a class=\"epc-br__btn epc-br__btn--ghost\" href=\"" + H(productBrochure) + "\">Product brochure</a>"
            + "<button type=\"button\" class=\"epc-br__btn epc-br__btn--ghost\" onclick=\"window.print()\">Print / PDF</button>"
            + "<a class=\"epc-br__btn epc-br__btn--pri\" href=\"" + H(Str(p, "cp_url")) + "\">Open Control Panel</a>"
            + "</div></div>"
            + "<header class=\"epc-br__hero epc-br__hero--deck\">"
            + "<img class=\"epc-br__hero-media\" src=\"" + cover + "\" alt=\"" + H(Str(p, "name") + " Control Panel") + "\" width=\"1600\" height=\"900\">"
            + "<div class=\"epc-br__hero-veil\" aria-hidden=\"true\"></div>"
            + "<div class=\"epc-br__hero-inner\">"
            + "<div class=\"epc-br__live\"><i class=\"fa fa-circle\"></i> Live synced · " + H(syncedAt) + "</div>"
            + "<div class=\"epc-br__eyebrow\">Control Panel · illustrated operations book</div>"
            + "<h1>" + H(Str(p, "name")) + "</h1>"
            + "<p>A complete book of every operator capability — <strong>" + total + "</strong> processes across "
            + areaCount + " chapters, each with a topic-related photo and full detail. Auto-updates when ERP modules or platform capabilities change.</p>"
            + "<div class=\"epc-br__hero-ctas\">"
            + "<a class=\"epc-br__btn epc-br__btn--pri\" href=\"#areas\">Explore areas</a>"
            + "<a class=\"epc-br__btn epc-br__btn--ghost\" href=\"" + H(Str(p, "cp_url")) + "\">Open /cp</a>"
            + "</div></div></header>"
            + "<div class=\"epc-br__toolbar\" style=\"margin-top:28px\">"
            + filters + viewToggle
            + "</div>"
            + bodyInner
            + "<footer class=\"epc-br__foot\">"
            + "<div><strong>" + H(Str(p, "name")) + " Control Panel</strong><br>"
            + "<span style=\"color:var(--br-muted)\">" + H(Str(p, "legal")) + " · " + total + " functions · auto-synced</span>"
            + "<p class=\"epc-br__sources\">Sources: " + H(string.Join(" · ", sources)) + "</p></div>"
            + "<div style=\"text-align:right\">"
            + "<a href=\"" + H(productBrochure) + "\">Product overview brochure →</a><br>"
            + "<a href=\"" + H(Str(p, "cp_url")) + "\">Open /cp →</a><br>"
            + "<a href=\"mailto:" + H(Str(p, "contact_email")) + "\">" + H(Str(p, "contact_email")) + "</a>"
            + "</div></footer></div>"
            + "<div class=\"epc-br__modal\" id=\"epc-br-modal\" aria-hidden=\"true\" role=\"dialog\">"
            + "<div class=\"epc-br__modal-panel\">"
            + "<img class=\"epc-br__modal-photo\" id=\"epc-br-modal-photo\" alt=\"\" width=\"960\" height=\"540\">"
            + "<div class=\"epc-br__modal-copy\">"
            + "<h3 id=\"epc-br-modal-title\"></h3>"
            + "<p id=\"epc-br-modal-body\"></p>"
            + "<code id=\"epc-br-modal-url\"></code>"
            + "<button type=\"button\" class=\"epc-br__btn epc-br__btn--pri\" id=\"epc-br-modal-close\">Close</button>"
            + "</div></div></div>"
            + "<script>" + PageJs + "</script>"
            + autoPrint
            + "</body></html>";
    }

    public static string EpcCpBrochureRenderDeckBody(
        Dictionary<string, List<Dictionary<string, object?>>> inv,
        Dictionary<string, Dictionary<string, object?>> visuals,
        int total,
        int areaCount,
        string syncedAt,
        IReadOnlyList<string> sources)
    {
        _ = syncedAt;
        _ = sources;
        var stats = "<div class=\"epc-br__stats\">"
            + "<div><strong>" + total + "</strong><span>Functions mapped</span></div>"
            + "<div><strong>" + areaCount + "</strong><span>Visual areas</span></div>"
            + "<div><strong>Live</strong><span>Auto-sync on open</span></div>"
            + "<div><strong>PDF</strong><span>Print-ready deck</span></div>"
            + "</div>";

        var mosaic = "<nav class=\"epc-br__mosaic\" id=\"areas\" aria-label=\"CP areas\">";
        var i = 0;
        foreach (var (area, items) in inv)
        {
            var aid = "area-" + Regex.Replace(area.ToLowerInvariant(), "[^a-z0-9]+", "-");
            var v = visuals.TryGetValue(area, out var vis) ? vis : DefaultVisual();
            var img = H(Str(v, "image"));
            var icon = H(Str(v, "icon"));
            var delay = PhpFloat(Math.Min(0.08 * i, 0.6));
            mosaic += "<a class=\"epc-br__tile\" href=\"#" + H(aid) + "\" style=\"animation:epc-br-fade .5s " + delay + "s both\">"
                + "<span class=\"epc-br__tile-bg\" style=\"background-image:url(" + img + ")\"></span>"
                + "<span class=\"epc-br__tile-veil\" aria-hidden=\"true\"></span>"
                + "<em>" + items.Count + " functions</em>"
                + "<i class=\"fa " + icon + "\" aria-hidden=\"true\"></i>"
                + "<strong>" + H(area) + "</strong>"
                + "<span>" + H(Str(v, "blurb")) + "</span>"
                + "</a>";
            i++;
        }

        mosaic += "</nav>";

        var toolbar = "<div class=\"epc-br__toolbar\">"
            + "<input type=\"search\" class=\"epc-br__search\" id=\"epc-br-search\" placeholder=\"Search functions…\" autocomplete=\"off\">"
            + "<p class=\"epc-br__legend\" id=\"epc-br-status\" style=\"margin:0\">Showing all " + total + " functions</p>"
            + "</div>";

        var areasHtml = "";
        foreach (var (area, items) in inv)
        {
            var aid = "area-" + Regex.Replace(area.ToLowerInvariant(), "[^a-z0-9]+", "-");
            var v = visuals.TryGetValue(area, out var vis) ? vis : DefaultVisual();
            var img = H(Str(v, "image"));
            var blurb = Str(v, "blurb");
            areasHtml += "<section class=\"epc-br__area epc-br__chapter\" id=\"" + H(aid) + "\" data-area=\"" + H(area.ToLowerInvariant()) + "\">";
            areasHtml += "<div class=\"epc-br__area-banner\">"
                + "<div class=\"epc-br__area-banner-media\" style=\"background-image:url(" + img + ")\"></div>"
                + "<div class=\"epc-br__area-banner-copy\">"
                + "<div class=\"epc-br__chapter-no\">" + H(area) + "</div>"
                + "<h2><i class=\"fa " + H(Str(v, "icon")) + "\"></i> " + H(area) + "</h2>"
                + "<p>" + H(blurb != "" ? blurb : "Operator capabilities in this area.") + "</p>"
                + "<span class=\"epc-br__count\">" + items.Count + " capabilities</span>"
                + "</div></div>";
            areasHtml += "<div class=\"epc-br__cards\">";
            foreach (var item in items)
            {
                var sc = item.TryGetValue("scope", out var sco) && Convert.ToString(sco) is { Length: > 0 } scv ? scv : "client";
                var scopeClass = sc == "super" ? " epc-br__scope--super" : (sc == "both" ? " epc-br__scope--both" : "");
                var url = Str(item, "url").Trim();
                var name = Str(item, "name");
                var does = Str(item, "does");
                var icon = Regex.Replace(Str(item, "icon").ToLowerInvariant(), "[^a-z0-9\\-]", "");
                if (!icon.StartsWith("fa-", StringComparison.Ordinal))
                {
                    icon = "fa-cube";
                }

                var photoMeta = ItemPhoto != null
                    ? ItemPhoto(item, area)
                    : new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["photo"] = ItemImage != null ? ItemImage(item, area) : "/content/general_pages/marketing_screens/og_cover.png",
                        ["label"] = "Operations",
                        ["topic"] = "default"
                    };
                var photo = Str(photoMeta, "photo");
                var topicLabel = Str(photoMeta, "label");
                if (topicLabel == "")
                {
                    topicLabel = "Operations";
                }

                var q = (name + " " + does + " " + url + " " + area + " " + topicLabel).ToLowerInvariant();
                areasHtml += "<article class=\"epc-br__card\" tabindex=\"0\" role=\"button\""
                    + " data-q=\"" + H(q) + "\""
                    + " data-name=\"" + H(name) + "\""
                    + " data-does=\"" + H(does) + "\""
                    + " data-url=\"" + H(url) + "\""
                    + " data-photo=\"" + H(photo) + "\""
                    + " data-topic=\"" + H(topicLabel) + "\">"
                    + "<span class=\"epc-br__card-photo\">"
                    + "<img src=\"" + H(photo) + "\" alt=\"" + H(topicLabel + " — " + name) + "\" loading=\"lazy\" width=\"480\" height=\"270\">"
                    + "<span class=\"epc-br__topic\">" + H(topicLabel) + "</span>"
                    + "<span class=\"epc-br__card-photo-ico\" aria-hidden=\"true\"><i class=\"fa " + H(icon) + "\"></i></span>"
                    + "</span>"
                    + "<div class=\"epc-br__card-body\">"
                    + "<h3>" + H(name) + "</h3>"
                    + "<p>" + H(does) + "</p>"
                    + "<div class=\"epc-br__card-foot\">"
                    + "<span class=\"epc-br__scope" + scopeClass + "\">" + H(sc) + "</span>"
                    + (url != "" ? "<code>" + H(url) + "</code>" : "<span></span>")
                    + "</div></div></article>";
            }

            areasHtml += "</div></section>";
        }

        return stats
            + "<section class=\"epc-br__sec\"><h2>Complete illustrated book</h2>"
            + "<p class=\"epc-br__pagehint\">Every process has its own <strong>unique topic photo</strong> — no repeats. Inventory shows warehouse shelves, currency shows money, orders show parcels, catalogue shows auto parts.</p></section>"
            + mosaic
            + toolbar
            + "<div class=\"epc-br__book\">" + areasHtml + "</div>";
    }

    public static string EpcCpBrochureRenderCatalogBody(Dictionary<string, List<Dictionary<string, object?>>> inv, string scopeLabel, int total)
    {
        var toc = "";
        foreach (var (area, items) in inv)
        {
            var aid = "area-" + Regex.Replace(area.ToLowerInvariant(), "[^a-z0-9]+", "-");
            toc += "<a href=\"#" + H(aid) + "\">" + H(area)
                + "<span>" + items.Count + " functions</span></a>";
        }

        var html = "<p class=\"epc-br__legend\">Catalogue view — <strong>" + H(scopeLabel) + "</strong> — "
            + total + " functions. Switch to Graphical for the visual deck.</p>"
            + "<section class=\"epc-br__sec\"><h2>Jump to area</h2></section>"
            + "<nav class=\"epc-br__toc\">" + toc + "</nav>";
        foreach (var (area, items) in inv)
        {
            var aid = "area-" + Regex.Replace(area.ToLowerInvariant(), "[^a-z0-9]+", "-");
            html += "<section class=\"epc-br__area\" id=\"" + H(aid) + "\">";
            html += "<div class=\"epc-br__area-head\"><h2>" + H(area) + "</h2><em>" + items.Count + " items</em></div>";
            foreach (var item in items)
            {
                var sc = item.TryGetValue("scope", out var sco) && Convert.ToString(sco) is { Length: > 0 } scv ? scv : "client";
                var scopeClass = sc == "super" ? " epc-br__scope--super" : (sc == "both" ? " epc-br__scope--both" : "");
                var url = Str(item, "url").Trim();
                var name = Str(item, "name");
                var photo = ItemImage != null ? ItemImage(item, area) : "/content/general_pages/marketing_screens/og_cover.png";
                html += "<div class=\"epc-br__fn\">";
                html += "<div style=\"display:flex;gap:10px;align-items:flex-start\">"
                    + "<img class=\"epc-br__fn-photo\" src=\"" + H(photo) + "\" alt=\"" + H(name) + "\" loading=\"lazy\" width=\"72\" height=\"40\">"
                    + "<div><strong>" + H(name) + "</strong><br>"
                    + "<span class=\"epc-br__scope" + scopeClass + "\">" + H(sc) + "</span></div></div>";
                html += "<p>" + H(Str(item, "does")) + "</p>";
                html += "<div>" + (url != "" ? "<code>" + H(url) + "</code>" : "—") + "</div>";
                html += "</div>";
            }

            html += "</section>";
        }

        return html;
    }

    public static string EpcCpFullBrochureRenderAndExit(Dictionary<string, object?>? opts = null)
    {
        ExitCalled = true;
        if (!HeadersSent)
        {
            ResponseHeaders =
            [
                "Content-Type: text/html; charset=utf-8",
                "X-Robots-Tag: index, follow",
                "Cache-Control: no-store, no-cache, must-revalidate, max-age=0"
            ];
        }

        return EpcCpFullBrochureRenderHtml(opts);
    }

    private static Dictionary<string, List<Dictionary<string, object?>>> AreasOf(Dictionary<string, object?> live)
    {
        if (live.TryGetValue("areas", out var a) && a is Dictionary<string, List<Dictionary<string, object?>>> typed)
        {
            return typed;
        }

        var output = new Dictionary<string, List<Dictionary<string, object?>>>(StringComparer.Ordinal);
        if (a is Dictionary<string, object?> loose)
        {
            foreach (var (k, v) in loose)
            {
                if (v is List<Dictionary<string, object?>> list)
                {
                    output[k] = list;
                }
            }
        }

        return output;
    }

    private static Dictionary<string, object?> DefaultVisual()
        => new(StringComparer.Ordinal)
        {
            ["icon"] = "fa-cube",
            ["image"] = "/content/general_pages/marketing_screens/og_cover.png",
            ["blurb"] = ""
        };

    private static string PhpDate(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("dd MMM yyyy · HH:mm", CultureInfo.InvariantCulture);

    private static string PhpFloat(double value)
    {
        if (Math.Abs(value) < 0.0000001)
        {
            return "0";
        }

        var s = value.ToString("0.################", CultureInfo.InvariantCulture);
        return s;
    }

    private static string H(string value)
        => BrochureH != null ? BrochureH(value) : PhpPlanQ1Sprit.EpcBrochureH(value);

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";

    private static int ToInt(Dictionary<string, object?> row, string key)
        => int.TryParse(Convert.ToString(row.TryGetValue(key, out var v) ? v : 0, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static long ToLong(object? value)
        => long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"));

    private const string ExtraCss = """
@import url('https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css');
.epc-br--deck{max-width:1200px}
.epc-br__live{display:inline-flex;align-items:center;gap:8px;padding:6px 12px;border-radius:999px;background:rgba(255,255,255,.12);border:1px solid rgba(255,255,255,.22);font-size:.78rem;font-weight:700;letter-spacing:.04em;text-transform:uppercase;color:#fff;margin:0 0 14px}
.epc-br__live i{color:var(--br-accent);animation:epc-br-pulse 1.8s ease-in-out infinite}
@keyframes epc-br-pulse{0%,100%{opacity:1}50%{opacity:.35}}
.epc-br__hero--deck{min-height:min(78vh,620px);margin:0 0 0;width:100vw;margin-left:calc(50% - 50vw);margin-right:calc(50% - 50vw)}
.epc-br__hero--deck .epc-br__hero-inner{max-width:720px;padding:clamp(48px,8vw,96px) clamp(20px,6vw,72px)}
.epc-br__hero-media{animation:epc-br-ken 22s ease-in-out infinite alternate}
@keyframes epc-br-ken{from{transform:scale(1)}to{transform:scale(1.06)}}
.epc-br__stats{display:grid;grid-template-columns:repeat(4,1fr);gap:1px;background:rgba(15,23,42,.1);margin:0 0 36px;border-radius:0;overflow:hidden}
.epc-br__stats div{background:var(--br-white);padding:22px 18px;position:relative;overflow:hidden}
.epc-br__stats div::after{content:'';position:absolute;left:0;bottom:0;height:3px;width:100%;background:linear-gradient(90deg,var(--br-accent),var(--br-accent2));transform:scaleX(0);transform-origin:left;animation:epc-br-bar .8s .2s ease forwards}
@keyframes epc-br-bar{to{transform:scaleX(1)}}
.epc-br__stats strong{display:block;font-family:Syne,sans-serif;font-size:1.65rem;letter-spacing:-.03em;line-height:1}
.epc-br__stats span{display:block;margin-top:6px;font-size:.82rem;color:var(--br-muted);font-weight:600}
.epc-br__mosaic{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:14px;margin:0 0 48px}
.epc-br__tile{position:relative;min-height:160px;overflow:hidden;color:#fff;text-decoration:none;display:flex;flex-direction:column;justify-content:flex-end;padding:16px;isolation:isolate;transition:transform .25s ease}
.epc-br__tile:hover{transform:translateY(-3px)}
.epc-br__tile-bg{position:absolute;inset:0;background-size:cover;background-position:center;opacity:.45;z-index:0;transform:scale(1.02);transition:transform .5s ease}
.epc-br__tile:hover .epc-br__tile-bg{transform:scale(1.08)}
.epc-br__tile-veil{position:absolute;inset:0;background:linear-gradient(160deg,rgba(0,0,0,.15),rgba(0,0,0,.88));z-index:1}
.epc-br__tile > *{position:relative;z-index:2}
.epc-br__tile i{font-size:1.35rem;margin-bottom:10px;color:var(--br-accent)}
.epc-br__tile strong{font-family:Syne,sans-serif;font-size:1.05rem;letter-spacing:-.02em;display:block}
.epc-br__tile span{font-size:.78rem;opacity:.85;margin-top:4px}
.epc-br__tile em{font-style:normal;font-size:.72rem;font-weight:700;letter-spacing:.08em;text-transform:uppercase;opacity:.7;margin-bottom:6px}
.epc-br__toolbar{display:flex;flex-wrap:wrap;gap:10px;align-items:center;justify-content:space-between;margin:0 0 18px}
.epc-br__search{flex:1;min-width:200px;max-width:360px;padding:10px 14px;border:1px solid rgba(15,23,42,.14);background:#fff;font:inherit;border-radius:8px}
.epc-br__filters{display:flex;flex-wrap:wrap;gap:8px;margin:0}
.epc-br__filters a{padding:7px 13px;border-radius:8px;border:1px solid rgba(15,23,42,.15);text-decoration:none;color:var(--br-ink);font-size:.85rem;font-weight:600}
.epc-br__filters a.is-on{background:var(--br-accent);color:#fff;border-color:transparent}
.epc-br__viewtoggle a{font-size:.85rem}
.epc-br__area{margin:0 0 48px;scroll-margin-top:72px}
.epc-br__area-banner{position:relative;min-height:180px;margin:0 0 18px;overflow:hidden;color:#fff;display:grid;grid-template-columns:1.2fr .8fr;gap:0}
.epc-br__area-banner-copy{position:relative;z-index:2;padding:28px 24px;background:linear-gradient(110deg,var(--br-ink) 0%,var(--br-ink2) 70%,transparent 100%)}
.epc-br__area-banner-copy h2{margin:0 0 8px;font-family:Syne,sans-serif;font-size:1.7rem;letter-spacing:-.02em}
.epc-br__area-banner-copy p{margin:0;color:rgba(255,255,255,.82);max-width:36em;font-size:.95rem}
.epc-br__area-banner-copy .epc-br__count{display:inline-block;margin-top:12px;padding:4px 10px;background:var(--br-accent);font-size:.75rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase}
.epc-br__area-banner-media{position:absolute;inset:0;background-size:cover;background-position:center;opacity:.55}
.epc-br__cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:14px}
.epc-br__card{background:#fff;border:1px solid rgba(15,23,42,.08);padding:0;cursor:pointer;transition:border-color .2s,transform .2s;min-height:280px;display:flex;flex-direction:column;overflow:hidden}
.epc-br__card:hover,.epc-br__card:focus{border-color:var(--br-accent);transform:translateY(-2px);outline:none}
.epc-br__card-photo{position:relative;display:block;width:100%;aspect-ratio:16/9;background:#0f172a;overflow:hidden}
.epc-br__card-photo img{width:100%;height:100%;object-fit:cover;display:block;transform:scale(1.02);transition:transform .45s ease}
.epc-br__card:hover .epc-br__card-photo img{transform:scale(1.07)}
.epc-br__card-photo-ico{position:absolute;left:10px;bottom:10px;width:34px;height:34px;display:inline-flex;align-items:center;justify-content:center;background:rgba(0,0,0,.55);color:#fff;border:1px solid rgba(255,255,255,.25);font-size:.95rem;backdrop-filter:blur(4px)}
.epc-br__topic{position:absolute;right:10px;top:10px;padding:4px 8px;border-radius:4px;background:rgba(0,0,0,.55);color:#fff;font-size:.65rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;backdrop-filter:blur(4px)}
.epc-br__card-body{padding:14px 14px 14px;display:flex;flex-direction:column;flex:1}
.epc-br__book{counter-reset:epc-br-chapter}
.epc-br__chapter{counter-increment:epc-br-chapter}
.epc-br__chapter-no{display:inline-block;font-family:Syne,sans-serif;font-size:.75rem;font-weight:800;letter-spacing:.12em;text-transform:uppercase;color:var(--br-accent);margin:0 0 6px}
.epc-br__chapter-no::before{content:"Chapter " counter(epc-br-chapter) " · "}
.epc-br__pagehint{font-size:.8rem;color:var(--br-muted);margin:0 0 28px}
.epc-br__card h3{margin:0 0 8px;font-family:Syne,sans-serif;font-size:1rem;letter-spacing:-.01em;line-height:1.25}
.epc-br__card p{margin:0;flex:1;font-size:.86rem;color:var(--br-muted);line-height:1.45;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
.epc-br__card-foot{display:flex;justify-content:space-between;align-items:center;gap:8px;margin-top:12px;font-size:.72rem}
.epc-br__scope{display:inline-block;font-size:.65rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase;padding:2px 6px;border-radius:4px;background:rgba(15,23,42,.06)}
.epc-br__scope--super{background:rgba(2,132,199,.12);color:#075985}
.epc-br__scope--both{background:rgba(220,38,38,.1);color:#991b1b}
.epc-br__card code{font-size:.68rem;color:var(--br-accent);max-width:55%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.epc-br__modal{position:fixed;inset:0;z-index:40;display:none;align-items:center;justify-content:center;padding:20px;background:rgba(10,10,10,.55);backdrop-filter:blur(4px)}
.epc-br__modal.is-open{display:flex;animation:epc-br-fade .2s ease}
@keyframes epc-br-fade{from{opacity:0}to{opacity:1}}
.epc-br__modal-panel{background:#fff;max-width:560px;width:100%;padding:0;border-left:4px solid var(--br-accent);max-height:85vh;overflow:auto}
.epc-br__modal-photo{display:block;width:100%;aspect-ratio:16/9;object-fit:cover;background:#0f172a}
.epc-br__modal-copy{padding:22px 22px 24px}
.epc-br__modal-panel h3{font-family:Syne,sans-serif;margin:0 0 10px;font-size:1.35rem}
.epc-br__modal-panel p{margin:0 0 14px;color:var(--br-muted);line-height:1.55}
.epc-br__modal-panel code{display:block;font-size:.8rem;color:var(--br-accent);word-break:break-all;margin-bottom:16px}
.epc-br__fn-photo{width:72px;height:40px;object-fit:cover;border-radius:4px;background:#0f172a;display:block}
.epc-br__sources{font-size:.78rem;color:var(--br-muted);margin:8px 0 0}
/* catalogue (text) view */
.epc-br__toc{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:8px;margin:0 0 36px}
.epc-br__toc a{display:block;padding:10px 12px;background:#fff;border-left:3px solid var(--br-accent);text-decoration:none;color:var(--br-ink);font-size:.9rem;font-weight:600}
.epc-br__toc a span{display:block;font-weight:500;color:var(--br-muted);font-size:.75rem;margin-top:2px}
.epc-br__area-head{display:flex;justify-content:space-between;align-items:baseline;gap:12px;border-bottom:2px solid var(--br-ink);padding-bottom:8px;margin-bottom:12px}
.epc-br__area-head h2{margin:0;font-family:Syne,sans-serif;font-size:1.35rem}
.epc-br__fn{display:grid;grid-template-columns:minmax(140px,220px) 1fr minmax(90px,120px);gap:8px 14px;padding:10px 0;border-bottom:1px solid rgba(15,23,42,.08);font-size:.9rem}
.epc-br__fn p{margin:0;color:var(--br-muted)}
.epc-br__fn code{font-size:.72rem;color:var(--br-accent);word-break:break-all}
.epc-br__legend{font-size:.85rem;color:var(--br-muted);margin:0 0 24px}
.epc-br__card.is-hidden,.epc-br__area.is-hidden,.epc-br__tile.is-hidden{display:none!important}
@media (max-width:860px){
  .epc-br__stats{grid-template-columns:1fr 1fr}
  .epc-br__area-banner{grid-template-columns:1fr;min-height:150px}
  .epc-br__fn{grid-template-columns:1fr}
}
@media print{
  .epc-br__filters,.epc-br__search,.epc-br__toolbar,.epc-br__modal,.epc-br__viewtoggle{display:none!important}
  .epc-br__hero-media{animation:none}
  .epc-br__card{break-inside:avoid;box-shadow:none}
  .epc-br__tile{break-inside:avoid}
}
""";

    private const string PageJs = """
(function(){
  var search=document.getElementById('epc-br-search');
  var status=document.getElementById('epc-br-status');
  var modal=document.getElementById('epc-br-modal');
  var mTitle=document.getElementById('epc-br-modal-title');
  var mBody=document.getElementById('epc-br-modal-body');
  var mUrl=document.getElementById('epc-br-modal-url');
  var mPhoto=document.getElementById('epc-br-modal-photo');
  var mClose=document.getElementById('epc-br-modal-close');
  function filter(){
    var q=(search&&search.value||'').toLowerCase().trim();
    var cards=document.querySelectorAll('.epc-br__card[data-q]');
    var shown=0;
    cards.forEach(function(c){
      var hit=!q||(c.getAttribute('data-q')||'').indexOf(q)!==-1;
      c.classList.toggle('is-hidden',!hit);
      if(hit) shown++;
    });
    document.querySelectorAll('.epc-br__area').forEach(function(area){
      var any=area.querySelector('.epc-br__card:not(.is-hidden)');
      area.classList.toggle('is-hidden',!any);
    });
    if(status) status.textContent=q?('Showing '+shown+' matches'):('Showing all '+shown+' functions');
  }
  if(search){search.addEventListener('input',filter);filter();}
  function openCard(card){
    if(!modal||!card) return;
    mTitle.textContent=card.getAttribute('data-name')||'';
    mBody.textContent=card.getAttribute('data-does')||'';
    var u=card.getAttribute('data-url')||'';
    mUrl.textContent=u||'Route available inside Control Panel';
    mUrl.style.display=u?'block':'none';
    var photo=card.getAttribute('data-photo')||'';
    if(mPhoto){
      if(photo){mPhoto.src=photo;mPhoto.alt=mTitle.textContent||'Process';mPhoto.style.display='block';}
      else{mPhoto.removeAttribute('src');mPhoto.style.display='none';}
    }
    modal.classList.add('is-open');
    modal.setAttribute('aria-hidden','false');
  }
  document.addEventListener('click',function(e){
    var card=e.target.closest('.epc-br__card[data-name]');
    if(card){openCard(card);return;}
    if(e.target===modal||e.target===mClose){modal.classList.remove('is-open');modal.setAttribute('aria-hidden','true');}
  });
  document.addEventListener('keydown',function(e){
    if(e.key==='Escape'&&modal){modal.classList.remove('is-open');}
  });
})();
""";
}
