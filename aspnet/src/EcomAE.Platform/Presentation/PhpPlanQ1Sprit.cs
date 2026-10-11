using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-sprit marketing brochure. PHP identifiers kept for the inventory:
/// <c>epc_brochure_h</c>, <c>epc_brochure_profile</c>, <c>epc_brochure_sections</c>,
/// <c>epc_brochure_stats</c>, <c>epc_brochure_journey</c>, <c>epc_brochure_css</c>,
/// <c>epc_brochure_render_html</c>, <c>epc_brochure_render_and_exit</c>.
/// GET never mints a session cookie. Leftover live-deck parents stay injected.
/// </summary>
public static class PhpPlanQ1Sprit
{
    public const string MarketingBrochurePath = "content/general_pages/epc_marketing_brochure.php";

    private const string FallbackCover = "/content/general_pages/marketing_screens/og_cover.png";

    private static readonly Regex BrandKeep = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Func<Dictionary<string, object?>, string, string>? ItemImage { get; set; }
    public static Func<Dictionary<string, object?>, string, Dictionary<string, object?>>? PhotoMeta { get; set; }
    public static Func<Dictionary<string, object?>>? LiveInventory { get; set; }
    public static bool HeadersSent { get; set; }
    public static List<string> ResponseHeaders { get; set; } = [];

    public static void Reset()
    {
        ItemImage = (_, _) => FallbackCover;
        PhotoMeta = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "" };
        LiveInventory = () => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["total"] = 0 }
        };
        HeadersSent = false;
        ResponseHeaders = [];
    }

    public static string EpcBrochureH(object? value)
        => H(Convert.ToString(value) ?? "");

    public static Dictionary<string, object?> EpcBrochureProfile(string brand)
    {
        brand = BrandKeep.Replace((brand ?? "").ToLowerInvariant(), "");
        if (brand is "epartscart" or "auto_parts")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = "epartscart",
                ["name"] = "eParts Cart",
                ["legal"] = "Electronic World Group",
                ["tagline"] = "Spare parts commerce — storefront, OMS, and Control Panel",
                ["domain"] = "www.epartscart.com",
                ["url"] = "https://www.epartscart.com",
                ["cp_url"] = "https://www.epartscart.com/cp",
                ["cover"] = "/content/general_pages/marketing_screens/epartscart-brochure-cover.jpg",
                ["contact_email"] = "hello@epartscart.com",
                ["contact_phone"] = "+971-567607011",
                ["accent"] = "#dc2626",
                ["accent2"] = "#f97316",
                ["ink"] = "#0f172a",
                ["ink2"] = "#1e293b",
                ["paper"] = "#f8fafc",
                ["muted"] = "#64748b",
                ["hero_sub"] = "One system for parts search, pricing, warehouses, orders, WhatsApp, AI chat, and ERP — so buyers and your team always see the same truth.",
                ["cta_primary"] = Cta("Open storefront", "https://www.epartscart.com"),
                ["cta_secondary"] = Cta("Full CP brochure", "/brochure-cp"),
                ["cp_brochure"] = "/brochure-cp"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = "ecomae",
            ["name"] = "ECOM AE",
            ["legal"] = "Electronic World Group",
            ["tagline"] = "Blockchain BOS Enterprise System — commerce, ERP, CRM, compliance",
            ["domain"] = "www.ecomae.com",
            ["url"] = "https://www.ecomae.com",
            ["cp_url"] = "https://www.ecomae.com/cp",
            ["cover"] = "/content/general_pages/marketing_screens/ecomae-brochure-cover.jpg",
            ["contact_email"] = "hello@ecomae.com",
            ["contact_phone"] = "+971-567607011",
            ["accent"] = "#0ea5e9",
            ["accent2"] = "#0284c7",
            ["ink"] = "#0a0a0a",
            ["ink2"] = "#141414",
            ["paper"] = "#f4f6f8",
            ["muted"] = "#64748b",
            ["hero_sub"] = "Hosted multi-tenant platform for UAE & GCC businesses: storefronts, Super CP, Client CP, ERP finance, Peppol e-invoice, and industry packs — one database, one operator story.",
            ["cta_primary"] = Cta("Book a demo", "https://www.ecomae.com/platform/demo"),
            ["cta_secondary"] = Cta("Full CP brochure", "/brochure/cp"),
            ["cp_brochure"] = "/brochure/cp"
        };
    }

    public static List<Dictionary<string, object?>> EpcBrochureSections(string brand)
    {
        if (brand == "epartscart")
        {
            return
            [
                Sec("Storefront buyers understand",
                    "Customers search by part number, brand, VIN, and crosses — then buy with clear stock, price, and delivery options.",
                    "Live part search across warehouses & price lists",
                    "OEM / aftermarket crosses",
                    "Multi-currency display (AED, USD, …)",
                    "WhatsApp quote & share from product/cart",
                    "AI Parts Expert chat on the website",
                    "Accessories & marketplace listings"),
                Sec("Control Panel (Client CP) — daily work",
                    "Your operations team runs the business from /cp — not five disconnected apps. Same data as the storefront.",
                    "OMS · Orders — tabs, filters, messages, print, fulfilment",
                    "Warehouses & price lists — upload, history, storefront toggles",
                    "Multivendor supplier pipelines",
                    "Customers / CRM & documents",
                    "AI agent chats — review what buyers asked",
                    "ERP & finance shell — ledger, VAT, reports",
                    "Marketing broadcast, social hub, POS"),
                Sec("Orders & fulfilment (OMS)",
                    "From cart to courier without spreadsheet chaos. Statuses, supplier lines, VAT, and customer messaging stay on the order.",
                    "Today / pending-ship filters & keyboard shortcuts",
                    "Line items, margin, brand/part edit",
                    "Supplier fulfilment pipeline",
                    "Courier VAT mapping",
                    "WhatsApp share templates (EN + AR)",
                    "Daily OMS guide for new staff"),
                Sec("Pricing & warehouses",
                    "Price lists attach to storages. Temporary storefront disable hides a supplier without deleting data. Uploads keep history.",
                    "CSV / file price upload with history download",
                    "Warehouse list search & currency per storage",
                    "Storefront ON/OFF toggles for WH & price lists",
                    "Crosses for OEM ↔ aftermarket",
                    "Auto Price AI (platform module)"),
                Sec("AI, marketing & growth",
                    "Help buyers find parts; help your team publish and broadcast without leaving CP.",
                    "AI Parts Expert — VIN, article, country-aware replies",
                    "Chat review + CSV export in CP",
                    "Email + WhatsApp marketing broadcast",
                    "Social hub — IG / FB / TikTok publish from drafts",
                    "First-party web tracker + UTM"),
                Sec("ERP, tax & compliance",
                    "Trading data flows into finance modules — VAT-aware for UAE operations, with document control for PDFs and e-invoice paths.",
                    "ERP Suite (GL, AR, inventory views)",
                    "Tax toolkit / VAT workflows",
                    "Document control & print packs",
                    "Customer TRN / Peppol buyer profiles",
                    "POS terminal for counter sales")
            ];
        }

        return
        [
            Sec("One Blockchain BOS for the enterprise",
                "ECOM AE is a hosted Business Operating System: commerce + ERP + CRM + compliance on one tenant model — with cryptographic proof hooks for critical documents.",
                "Multi-tenant Super CP for operators",
                "Client CP for each storefront business",
                "Industry packs (auto parts, jewellery, fashion, …)",
                "Shared database — no CSV sync between modules",
                "UAE / GCC ready: VAT, Peppol e-invoice paths"),
            Sec("Commerce that sells",
                "Storefronts, catalogues, pricing, carts, payments, and omnichannel listings — built for distribution and retail traders.",
                "Catalogue + CSV import",
                "Supplier price lists & live search",
                "Order desk / OMS & fulfilment",
                "Payments, multi-currency, credit limits",
                "Warehouses, carriers, RMA",
                "Marketplace / accessories channels"),
            Sec("ERP & finance",
                "Dynamics-style module coverage without bolting a second ERP beside the shop.",
                "General ledger & chart of accounts",
                "AR / treasury / inventory views",
                "Procurement & supplier payables",
                "Tax toolkit & e-invoice readiness",
                "Document control & print designer",
                "ERP-only mode for finance tenants"),
            Sec("AI & automation",
                "Assist buyers and operators with domain-aware agents — not generic chat widgets.",
                "AI parts / product agents",
                "VIN & demand intelligence",
                "Auto Price AI",
                "Marketing broadcast & social publish",
                "Web tracker + GA4 / Clarity hooks"),
            Sec("Operator Control Panels",
                "Super CP runs the platform. Client CP runs each tenant’s daily trading — orders, prices, warehouses, CRM, ERP shell.",
                "Tenant hub, DNS, health, onboard",
                "Client CP guideline & daily dashboards",
                "Roles, groups, backend access",
                "Integrations vault (SMTP, WhatsApp Cloud API, social)",
                "Business continuity & failover tooling"),
            Sec("Who it is for",
                "Distributors, retailers, and multi-brand groups across GCC industries — with templates that adapt language, tax, and catalogue shape.",
                "Automotive & spare parts",
                "Electronics & fashion",
                "Jewellery & medical supply",
                "Hospitality, beauty, construction packs",
                "Platform host for SaaS operators")
        ];
    }

    public static List<Dictionary<string, object?>> EpcBrochureStats(string brand)
        => brand == "epartscart"
            ?
            [
                Stat("Focus", "Auto parts B2B/B2C"),
                Stat("Workspace", "Storefront + /cp"),
                Stat("Core", "OMS · Prices · WH"),
                Stat("AI", "Parts Expert chat"),
                Stat("Region", "UAE · GCC")
            ]
            :
            [
                Stat("Model", "Multi-tenant BOS"),
                Stat("Panels", "Super CP + Client CP"),
                Stat("Modules", "100+ capabilities"),
                Stat("Compliance", "VAT · Peppol path"),
                Stat("HQ", "Dubai, UAE")
            ];

    public static List<Dictionary<string, object?>> EpcBrochureJourney(string brand)
        => brand == "epartscart"
            ?
            [
                Step("01", "Customer finds a part", "Search, VIN, AI chat, or WhatsApp — prices from live warehouses."),
                Step("02", "Order lands in OMS", "Staff confirm lines, margin, supplier, and ship status in one desk."),
                Step("03", "Fulfil & message", "Print, courier VAT, WhatsApp updates — customer stays informed."),
                Step("04", "Finance closes the loop", "Documents and ERP views reflect the same order truth.")
            ]
            :
            [
                Step("01", "Onboard a tenant", "Super CP provisions industry pack, branding, and modules."),
                Step("02", "Trade on storefront", "Catalogue, prices, cart, and payments go live."),
                Step("03", "Operate in Client CP", "Orders, warehouses, CRM, marketing — daily workspace."),
                Step("04", "Govern in ERP", "Ledger, tax, documents, and proof-ready workflows.")
            ];

    public static string EpcBrochureCss(Dictionary<string, object?> p)
    {
        var a = H(Str(p, "accent"));
        var a2 = H(Str(p, "accent2"));
        var ink = H(Str(p, "ink"));
        var ink2 = H(Str(p, "ink2"));
        var paper = H(Str(p, "paper"));
        var muted = H(Str(p, "muted"));
        return CssTemplate
            .Replace("__A2__", a2, StringComparison.Ordinal)
            .Replace("__A__", a, StringComparison.Ordinal)
            .Replace("__INK2__", ink2, StringComparison.Ordinal)
            .Replace("__INK__", ink, StringComparison.Ordinal)
            .Replace("__PAPER__", paper, StringComparison.Ordinal)
            .Replace("__MUTED__", muted, StringComparison.Ordinal);
    }

    public static string EpcBrochureRenderHtml(string brand, Dictionary<string, object?>? opts = null)
    {
        opts ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var p = EpcBrochureProfile(brand);
        var id = Str(p, "id");
        var sections = EpcBrochureSections(id);
        var stats = EpcBrochureStats(id);
        var journey = EpcBrochureJourney(id);
        var css = EpcBrochureCss(p);
        var autoPrint = !Empty(opts.TryGetValue("print", out var print) ? print : null)
            ? "<script>window.addEventListener(\"load\",function(){setTimeout(function(){window.print()},400)});</script>"
            : "";

        string[] cpItems = id == "epartscart"
            ? ["OMS · Orders", "Warehouses", "Prices & toggles", "AI chats", "Customers", "ERP & VAT", "Marketing", "Settings"]
            : ["Super CP tenants", "Client CP home", "Capabilities", "Industries", "ERP modules", "Integrations", "Tax toolkit", "Health"];

        var statsHtml = "";
        foreach (var s in stats)
        {
            statsHtml += "<div><strong>" + H(Str(s, "value")) + "</strong><span>" + H(Str(s, "label")) + "</span></div>";
        }

        var journeyHtml = "";
        foreach (var j in journey)
        {
            var photo = ItemImage != null
                ? ItemImage(Item(Str(j, "title"), Str(j, "body"), "fa-play"), "How work flows")
                : FallbackCover;
            journeyHtml += "<div class=\"epc-br__j\">"
                + "<img class=\"epc-br__j-photo\" src=\"" + H(photo) + "\" alt=\"" + H(Str(j, "title")) + "\" loading=\"lazy\" width=\"640\" height=\"400\">"
                + "<div class=\"epc-br__j-copy\"><em>" + H(Str(j, "step")) + "</em><strong>" + H(Str(j, "title")) + "</strong><span>" + H(Str(j, "body")) + "</span></div>"
                + "</div>";
        }

        var secHtml = "";
        foreach (var sec in sections)
        {
            var secPhoto = ItemImage != null
                ? ItemImage(Item(Str(sec, "title"), Str(sec, "body"), "fa-image"), Str(sec, "title"))
                : FallbackCover;
            secHtml += "<section class=\"epc-br__sec\">"
                + "<img class=\"epc-br__sec-photo\" src=\"" + H(secPhoto) + "\" alt=\"" + H(Str(sec, "title")) + "\" loading=\"lazy\" width=\"1100\" height=\"220\">"
                + "<h2>" + H(Str(sec, "title")) + "</h2><p>" + H(Str(sec, "body")) + "</p>"
                + "<ul class=\"epc-br__points-grid\">";
            foreach (var pt in Points(sec))
            {
                var ptPhoto = ItemImage != null
                    ? ItemImage(Item(pt, "", "fa-check"), Str(sec, "title"))
                    : FallbackCover;
                var ptMeta = PhotoMeta != null
                    ? PhotoMeta(Item(pt, "", ""), Str(sec, "title"))
                    : new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "" };
                secHtml += "<li class=\"epc-br__point-card\">"
                    + "<img src=\"" + H(ptPhoto) + "\" alt=\"" + H(pt) + "\" loading=\"lazy\" width=\"480\" height=\"270\">"
                    + "<span>" + H(pt)
                    + (!Empty(Str(ptMeta, "label"))
                        ? " <em style=\"display:block;margin-top:4px;font-style:normal;font-size:.72rem;color:var(--br-muted);text-transform:uppercase;letter-spacing:.06em\">" + H(Str(ptMeta, "label")) + "</em>"
                        : "")
                    + "</span></li>";
            }

            secHtml += "</ul></section>";
        }

        var menuHtml = "";
        for (var i = 0; i < cpItems.Length; i++)
        {
            menuHtml += "<span" + (i == 0 ? " class=\"is-on\"" : "") + ">" + H(cpItems[i]) + "</span>";
        }

        var shots = new (string Src, string Cap)[]
        {
            ("/content/general_pages/marketing_screens/pf_workforce.png", "Operations desk"),
            ("/content/general_pages/marketing_screens/pf_orgmap.png", "Organisation map"),
            ("/content/general_pages/marketing_screens/pf_tracker.png", "Live tracking"),
            ("/content/general_pages/marketing_screens/pf_location.png", "Locations & logistics")
        };
        var galleryHtml = "<div class=\"epc-br__gallery\">";
        foreach (var shot in shots)
        {
            galleryHtml += "<figure><img src=\"" + H(shot.Src) + "\" alt=\"" + H(shot.Cap) + "\" loading=\"lazy\" width=\"640\" height=\"400\">"
                + "<figcaption>" + H(shot.Cap) + "</figcaption></figure>";
        }

        galleryHtml += "</div>";

        var cpCount = 0;
        if (LiveInventory != null)
        {
            var live = LiveInventory();
            if (live.TryGetValue("meta", out var metaObj) && metaObj is Dictionary<string, object?> meta)
            {
                cpCount = ToInt(meta, "total");
            }
        }

        var cpBrochureHref = Str(p, "cp_brochure");
        if (cpBrochureHref == "")
        {
            cpBrochureHref = "/brochure-cp";
        }

        var ctaBand = "<aside class=\"epc-br__cta-band\">"
            + "<div><h2>Full Control Panel — graphical deck</h2>"
            + "<p>" + (cpCount > 0 ? cpCount + " functions" : "Every CP function")
            + " — each with a presentation photo, area visuals, and live sync when modules are added.</p></div>"
            + "<a class=\"epc-br__btn epc-br__btn--pri\" href=\"" + H(cpBrochureHref) + "\">Open graphical CP brochure</a>"
            + "</aside>";

        var title = H(Str(p, "name") + " — Product brochure");
        var desc = H(Str(p, "tagline"));
        var cover = H(Str(p, "cover"));
        var primary = CtaOf(p, "cta_primary");
        var secondary = CtaOf(p, "cta_secondary");

        return "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>" + title + "</title>"
            + "<meta name=\"description\" content=\"" + desc + "\">"
            + "<meta name=\"robots\" content=\"index,follow\">"
            + "<meta property=\"og:title\" content=\"" + title + "\">"
            + "<meta property=\"og:description\" content=\"" + desc + "\">"
            + "<meta property=\"og:image\" content=\"" + H(Str(p, "url").TrimEnd('/') + Str(p, "cover")) + "\">"
            + "<style>" + css + "</style></head><body>"
            + "<div class=\"epc-br\">"
            + "<div class=\"epc-br__bar\"><a class=\"epc-br__brand\" href=\"" + H(Str(p, "url")) + "\">" + H(Str(p, "name")) + "</a>"
            + "<div class=\"epc-br__actions\">"
            + "<button type=\"button\" class=\"epc-br__btn epc-br__btn--ghost\" onclick=\"window.print()\">Print / PDF</button>"
            + "<a class=\"epc-br__btn epc-br__btn--pri\" href=\"" + H(Str(primary, "href")) + "\">" + H(Str(primary, "label")) + "</a>"
            + "</div></div>"
            + "<header class=\"epc-br__hero\">"
            + "<img class=\"epc-br__hero-media\" src=\"" + cover + "\" alt=\"" + H(Str(p, "name") + " brochure cover") + "\" width=\"1600\" height=\"900\">"
            + "<div class=\"epc-br__hero-veil\" aria-hidden=\"true\"></div>"
            + "<div class=\"epc-br__hero-inner\">"
            + "<div class=\"epc-br__eyebrow\">Product brochure</div>"
            + "<h1>" + H(Str(p, "name")) + "</h1>"
            + "<p>" + H(Str(p, "hero_sub")) + "</p>"
            + "<div class=\"epc-br__hero-ctas\">"
            + "<a class=\"epc-br__btn epc-br__btn--pri\" href=\"" + H(Str(primary, "href")) + "\">" + H(Str(primary, "label")) + "</a>"
            + "<a class=\"epc-br__btn epc-br__btn--ghost\" href=\"" + H(Str(secondary, "href")) + "\">" + H(Str(secondary, "label")) + "</a>"
            + "</div></div></header>"
            + "<div class=\"epc-br__strip\">" + statsHtml + "</div>"
            + "<section class=\"epc-br__sec\"><h2>See the product in context</h2><p>Real operational surfaces — not a text wall.</p></section>"
            + galleryHtml
            + "<section class=\"epc-br__sec\"><h2>How work flows</h2><p>A simple journey customers and staff can follow.</p></section>"
            + "<div class=\"epc-br__journey\">" + journeyHtml + "</div>"
            + ctaBand
            + "<div class=\"epc-br__cp\"><div class=\"epc-br__cp-copy\">"
            + "<h2>" + (id == "epartscart" ? "Inside the Control Panel" : "Inside the Control Panels") + "</h2>"
            + "<p>" + (id == "epartscart"
                ? "Client CP at /cp is the daily workspace: OMS first, then warehouses, prices, AI chats, customers, and ERP. Left-menu search jumps to any module."
                : "Super CP governs tenants and platform health. Each customer gets a Client CP tailored by industry pack — commerce and finance without a second login maze.") + "</p>"
            + "<ul class=\"epc-br__points\">"
            + (id == "epartscart"
                ? "<li>Dashboard: OMS, Warehouses, Prices, Multivendor, Crosses, AI chats</li><li>Secure vaults for WhatsApp & social tokens</li><li>Printable OMS daily guide for training</li>"
                : "<li>Tenant hub & industry templates</li><li>Capabilities catalog (100+ modules)</li><li>Demo & pricing paths for sales</li>")
            + "</ul></div>"
            + "<div class=\"epc-br__panel\" aria-hidden=\"true\"><div class=\"epc-br__panel-top\"><i class=\"epc-br__dot\"></i><i class=\"epc-br__dot\"></i><i class=\"epc-br__dot\"></i></div>"
            + "<div class=\"epc-br__menu\">" + menuHtml + "</div></div></div>"
            + secHtml
            + "<footer class=\"epc-br__foot\">"
            + "<div><strong>" + H(Str(p, "name")) + "</strong><br><span style=\"color:var(--br-muted)\">" + H(Str(p, "legal")) + " · Dubai, UAE</span></div>"
            + "<div style=\"text-align:right\">"
            + "<a href=\"mailto:" + H(Str(p, "contact_email")) + "\">" + H(Str(p, "contact_email")) + "</a><br>"
            + "<a href=\"tel:" + H(Spaces.Replace(Str(p, "contact_phone"), "")) + "\">" + H(Str(p, "contact_phone")) + "</a><br>"
            + "<a href=\"" + H(Str(p, "url")) + "\">" + H(Str(p, "domain")) + "</a>"
            + "<br><a href=\"" + H(Str(p, "cp_brochure") == "" ? "/brochure-cp" : Str(p, "cp_brochure")) + "\">Full CP brochure (every function) →</a>"
            + (id == "epartscart" ? "<br><a href=\"" + H(Str(p, "cp_url")) + "\">Control Panel →</a>" : "")
            + "</div></footer></div>"
            + autoPrint
            + "</body></html>";
    }

    public static string EpcBrochureRenderAndExit(string brand, Dictionary<string, object?>? opts = null)
    {
        if (!HeadersSent)
        {
            ResponseHeaders =
            [
                "Content-Type: text/html; charset=utf-8",
                "X-Robots-Tag: index, follow"
            ];
        }

        return EpcBrochureRenderHtml(brand, opts);
    }

    private static Dictionary<string, object?> Cta(string label, string href)
        => new(StringComparer.Ordinal) { ["label"] = label, ["href"] = href };

    private static Dictionary<string, object?> Sec(string title, string body, params string[] points)
        => new(StringComparer.Ordinal)
        {
            ["title"] = title,
            ["body"] = body,
            ["points"] = points.ToList()
        };

    private static Dictionary<string, object?> Stat(string label, string value)
        => new(StringComparer.Ordinal) { ["label"] = label, ["value"] = value };

    private static Dictionary<string, object?> Step(string step, string title, string body)
        => new(StringComparer.Ordinal) { ["step"] = step, ["title"] = title, ["body"] = body };

    private static Dictionary<string, object?> Item(string name, string does, string icon)
        => new(StringComparer.Ordinal) { ["name"] = name, ["does"] = does, ["icon"] = icon };

    private static Dictionary<string, object?> CtaOf(Dictionary<string, object?> p, string key)
        => p.TryGetValue(key, out var raw) && raw is Dictionary<string, object?> d
            ? d
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    private static List<string> Points(Dictionary<string, object?> sec)
        => sec.TryGetValue("points", out var raw) && raw is List<string> list ? list : [];

    private static bool Empty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is bool flag)
        {
            return !flag;
        }

        var text = Convert.ToString(value) ?? "";
        return text == "" || text == "0";
    }

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var raw) && raw != null ? Convert.ToString(raw) ?? "" : "";

    private static int ToInt(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return int.TryParse(Convert.ToString(raw), out var n) ? n : 0;
    }

    private static string H(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private const string CssTemplate = """
@import url('https://fonts.googleapis.com/css2?family=Syne:wght@600;700;800&family=Source+Sans+3:wght@400;500;600;700&display=swap');
:root{
  --br-accent:__A__;--br-accent2:__A2__;--br-ink:__INK__;--br-ink2:__INK2__;
  --br-paper:__PAPER__;--br-muted:__MUTED__;--br-white:#fff;
}
*{box-sizing:border-box}
html,body{margin:0;padding:0;background:var(--br-paper);color:var(--br-ink);
  font-family:'Source Sans 3',system-ui,sans-serif;line-height:1.5;-webkit-font-smoothing:antialiased}
.epc-br{max-width:1100px;margin:0 auto;padding:0 20px 64px}
.epc-br__bar{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:14px 0;position:sticky;top:0;z-index:5;
  background:rgba(248,250,252,.92);backdrop-filter:blur(10px);border-bottom:1px solid rgba(15,23,42,.08)}
.epc-br__brand{font-family:Syne,sans-serif;font-weight:800;font-size:1.15rem;letter-spacing:-.02em;text-decoration:none;color:var(--br-ink)}
.epc-br__actions{display:flex;gap:8px;flex-wrap:wrap}
.epc-br__btn{display:inline-flex;align-items:center;gap:6px;padding:8px 14px;border-radius:8px;font-weight:600;font-size:.9rem;text-decoration:none;border:0;cursor:pointer}
.epc-br__btn--pri{background:var(--br-accent);color:#fff}
.epc-br__btn--ghost{background:transparent;color:var(--br-ink);border:1px solid rgba(15,23,42,.15)}
.epc-br__hero{position:relative;margin:18px 0 28px;border-radius:0;overflow:hidden;min-height:min(72vh,560px);
  background:linear-gradient(135deg,var(--br-ink) 0%,var(--br-ink2) 55%,#000 100%);color:var(--br-white)}
.epc-br__hero-media{position:absolute;inset:0;width:100%;height:100%;object-fit:cover;opacity:.55}
.epc-br__hero-veil{position:absolute;inset:0;background:linear-gradient(105deg,rgba(0,0,0,.82) 0%,rgba(0,0,0,.35) 55%,rgba(0,0,0,.55) 100%)}
.epc-br__hero-inner{position:relative;z-index:1;padding:clamp(36px,6vw,72px);max-width:640px}
.epc-br__eyebrow{display:inline-block;font-size:.75rem;font-weight:700;letter-spacing:.14em;text-transform:uppercase;color:var(--br-accent);margin:0 0 12px}
.epc-br__hero h1{font-family:Syne,sans-serif;font-weight:800;font-size:clamp(2.4rem,5.5vw,3.8rem);line-height:1.05;letter-spacing:-.03em;margin:0 0 14px}
.epc-br__hero p{font-size:1.05rem;color:rgba(255,255,255,.88);margin:0 0 22px;max-width:34em}
.epc-br__hero-ctas{display:flex;gap:10px;flex-wrap:wrap}
.epc-br__hero-ctas .epc-br__btn--ghost{color:#fff;border-color:rgba(255,255,255,.35)}
.epc-br__strip{display:grid;grid-template-columns:repeat(5,1fr);gap:1px;background:rgba(15,23,42,.08);border-radius:12px;overflow:hidden;margin:0 0 40px}
.epc-br__strip div{background:var(--br-white);padding:16px 14px}
.epc-br__strip strong{display:block;font-family:Syne,sans-serif;font-size:1rem;margin-bottom:2px}
.epc-br__strip span{font-size:.8rem;color:var(--br-muted)}
.epc-br__sec{margin:0 0 36px;break-inside:avoid}
.epc-br__sec h2{font-family:Syne,sans-serif;font-size:1.55rem;letter-spacing:-.02em;margin:0 0 8px}
.epc-br__sec > p{color:var(--br-muted);margin:0 0 14px;max-width:48em}
.epc-br__points{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px 18px;margin:0;padding:0;list-style:none}
.epc-br__points li{position:relative;padding:8px 0 8px 22px;font-size:.95rem}
.epc-br__points li::before{content:'';position:absolute;left:0;top:14px;width:10px;height:10px;border-radius:2px;background:linear-gradient(135deg,var(--br-accent),var(--br-accent2))}
.epc-br__journey{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:8px 0 40px}
.epc-br__j{padding:0;background:var(--br-white);border-left:3px solid var(--br-accent);box-shadow:0 1px 0 rgba(15,23,42,.04);overflow:hidden;display:flex;flex-direction:column}
.epc-br__j-photo{display:block;width:100%;aspect-ratio:16/10;object-fit:cover;background:var(--br-ink)}
.epc-br__j-copy{padding:14px 14px 16px}
.epc-br__j em{font-style:normal;font-family:Syne,sans-serif;font-weight:800;color:var(--br-accent);font-size:1.1rem}
.epc-br__j strong{display:block;margin:6px 0 4px;font-size:1rem}
.epc-br__j span{font-size:.88rem;color:var(--br-muted)}
.epc-br__sec-photo{display:block;width:100%;max-height:220px;object-fit:cover;margin:0 0 14px;background:var(--br-ink)}
.epc-br__points-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;margin:14px 0 0;padding:0;list-style:none}
.epc-br__point-card{margin:0;background:var(--br-white);border:1px solid rgba(15,23,42,.08);overflow:hidden}
.epc-br__point-card img{display:block;width:100%;aspect-ratio:16/9;object-fit:cover;background:var(--br-ink)}
.epc-br__point-card span{display:block;padding:10px 12px;font-size:.9rem;line-height:1.4}
.epc-br__cp{display:grid;grid-template-columns:1.1fr .9fr;gap:20px;align-items:stretch;margin:0 0 40px}
.epc-br__cp-copy h2{margin-top:0}
.epc-br__panel{background:linear-gradient(160deg,var(--br-ink),var(--br-ink2));color:#fff;padding:22px;border-radius:14px;min-height:260px;
  background-image:radial-gradient(ellipse at 20% 0%,rgba(255,255,255,.08),transparent 50%),linear-gradient(160deg,var(--br-ink),var(--br-ink2))}
.epc-br__panel-top{display:flex;gap:6px;margin-bottom:16px}
.epc-br__dot{width:9px;height:9px;border-radius:50%;background:rgba(255,255,255,.25)}
.epc-br__dot:first-child{background:var(--br-accent)}
.epc-br__menu{display:grid;gap:6px}
.epc-br__menu span{display:block;padding:8px 10px;border-radius:6px;font-size:.82rem;background:rgba(255,255,255,.06)}
.epc-br__menu span.is-on{background:var(--br-accent);color:#fff;font-weight:700}
.epc-br__foot{margin-top:48px;padding:28px 0 8px;border-top:1px solid rgba(15,23,42,.1);display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap}
.epc-br__foot strong{font-family:Syne,sans-serif}
.epc-br__foot a{color:var(--br-accent);font-weight:600;text-decoration:none}
.epc-br__gallery{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin:0 0 40px}
.epc-br__gallery figure{margin:0;position:relative;overflow:hidden;min-height:140px;background:var(--br-ink)}
.epc-br__gallery img{width:100%;height:100%;object-fit:cover;display:block;min-height:140px;opacity:.9;transition:transform .45s ease}
.epc-br__gallery figure:hover img{transform:scale(1.05)}
.epc-br__gallery figcaption{position:absolute;left:0;right:0;bottom:0;padding:10px 12px;font-size:.78rem;font-weight:700;color:#fff;background:linear-gradient(transparent,rgba(0,0,0,.75))}
.epc-br__cta-band{margin:8px 0 40px;padding:28px 24px;background:linear-gradient(120deg,var(--br-ink),var(--br-ink2));color:#fff;display:flex;flex-wrap:wrap;gap:16px;align-items:center;justify-content:space-between}
.epc-br__cta-band h2{margin:0 0 6px;font-family:Syne,sans-serif;font-size:1.45rem}
.epc-br__cta-band p{margin:0;color:rgba(255,255,255,.82);max-width:36em;font-size:.95rem}
.epc-br__hero{width:100vw;margin-left:calc(50% - 50vw);margin-right:calc(50% - 50vw)}
.epc-br__hero-media{animation:epc-br-ken 20s ease-in-out infinite alternate}
@keyframes epc-br-ken{from{transform:scale(1)}to{transform:scale(1.05)}}
@media (max-width:860px){
  .epc-br__strip{grid-template-columns:repeat(2,1fr)}
  .epc-br__journey{grid-template-columns:1fr 1fr}
  .epc-br__cp{grid-template-columns:1fr}
  .epc-br__points,.epc-br__points-grid{grid-template-columns:1fr}
  .epc-br__gallery{grid-template-columns:1fr 1fr}
}
@media print{
  .epc-br__bar,.epc-br__actions{display:none!important}
  body{background:#fff}
  .epc-br{max-width:100%;padding:0}
  .epc-br__hero{min-height:320px;break-after:avoid}
  .epc-br__sec,.epc-br__j,.epc-br__cp{break-inside:avoid}
  a{color:inherit;text-decoration:none}
}
""";
}
