using System.Globalization;
using System.Text;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Next self-contained non-ERP includes after <see cref="PhpNextSmallBatch"/>.
/// Functions kept for the inventory: <c>epc_retail_tax_seed_tree</c>,
/// <c>epc_portal_favicon_svg</c>, <c>epc_portal_favicon_link_tags</c>,
/// <c>epc_storefront_auth_lang_href</c>, <c>epc_storefront_auth_login_url</c>,
/// <c>epc_storefront_auth_signup_url</c>, <c>epc_storefront_auth_vendor_url</c>,
/// <c>epc_storefront_auth_vendor_register_url</c>, <c>epc_storefront_auth_garage_login_url</c>,
/// <c>epc_storefront_auth_garage_manager_url</c>, <c>epc_storefront_auth_links_html</c>,
/// <c>epc_storefront_auth_links_render</c>, <c>epc_storefront_auth_links_styles</c>,
/// <c>epc_cpi_hero_banner_href</c>, <c>epc_frn_hero_href</c>, <c>epc_jrk_hero_href</c>,
/// <c>epc_er_hero_href</c>.
/// </summary>
public static class PhpSmallMore
{
    public const string LevamPath = "content/shop/levam/levam.php";
    public const string SpecialSearchesPath = "content/shop/catalogue/printSpecialSearches.php";
    public const string NewsModulePath = "modules/news/module.php";
    public const string RetailTaxonomyPath = "content/shop/price_engine/epc_retail_taxonomy.php";
    public const string FaviconPath = "content/general_pages/epc_portal_favicon.php";
    public const string AuthLinksPath = "content/general_pages/epc_storefront_auth_links.php";
    public const string GarageLoginPath = "content/shop/workshop/garage_login.php";
    public const string FashionMegaPath = "content/general_pages/epc_fashion_retail_namshi_mega_menu.php";
    public const string ConsultingHeroPath = "content/general_pages/epc_consulting_primeinvest_hero_banner.php";
    public const string FashionHeroPath = "content/general_pages/epc_fashion_retail_namshi_hero_banner.php";
    public const string JewelleryHeroPath = "content/general_pages/epc_jewellery_retail_kiyasha_hero_banner.php";
    public const string ElectronicsHeroPath = "content/general_pages/epc_electronics_retail_virgin_hero_banner.php";

    public static string Levam(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return "";
        }

        return "\t\t<style>\r\n\t\t/*Стили для адаптации самого каталога*/\r\n"
            + "\t\t.oem_widget_std_block\r\n\t\t{\r\n\t\t\twidth:210px!important;\r\n\t\t}\r\n"
            + "\t\t.oem_widget_left_sidebar2 + .w-100 + .pa-0 .oem_widget_std_block,\r\n"
            + "\t\t.oem_widget_marks .oem_widget_std_block,\r\n\t\t.oem_widget_std_block\r\n\t\t{\r\n"
            + "\t\t\theight:60px!important;\r\n\t\t\tpadding-top:0!important;\r\n\t\t\twidth:210px!important;\r\n\t\t}\r\n"
            + "\t\t@media screen and (max-width: 600px)\r\n\t\t{\r\n"
            + "\t\t\t.oem_widget_left_sidebar2 + .w-100 + .pa-0 .oem_widget_std_block,\r\n"
            + "\t\t\t.oem_widget_marks .oem_widget_std_block,\r\n\t\t\t.oem_widget_std_block\r\n\t\t\t{\r\n"
            + "\t\t\t\theight:60px!important;\r\n\t\t\t\tmin-height:60px!important;\r\n"
            + "\t\t\t\tpadding-top:0!important;\r\n\t\t\t\twidth:190px!important;\r\n\t\t\t}\r\n\t\t}\r\n"
            + "\t\t.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-responsive__sizer,\r\n"
            + "\t\t.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-image__image,\r\n"
            + "\t\t.oem_widget_left_sidebar2 + .w-100 + .pa-0 .v-image,\r\n"
            + "\t\t.oem_widget_mark_block .v-image,\r\n\t\t.oem_widget_std_block .v-image,\r\n"
            + "\t\t.oem_widget_top_car_image_wrap,\r\n\t\t.oem_widget_top_car_image\r\n\t\t{\r\n"
            + "\t\t\tdisplay:none;\r\n\t\t\theight:0!important;\r\n\t\t\twidth:0!important;\r\n"
            + "\t\t\tmargin-top:0!important;\r\n\t\t\tpadding:0!important;\r\n\t\t}\r\n"
            + "\t\t.oem_widget_std_block_wrap_img\r\n\t\t{\r\n\t\t\theight:10px!important;\r\n\t\t}\r\n"
            + "\t\t</style>\r\n\t\t<br>\r\n\t\t<script id=\"levam_oem_catalog\" src=\"https://widgets.levam.net/oem-widget/loader.js\" lang=\"ru\" code=\""
            + code + "\" async></script>\r\n\t\t";
    }

    public static string PrintSpecialSearches(string langHref, IReadOnlyList<(string Alias, string Img, object Caption)> rows, Func<object, string>? translate = null)
    {
        string T(object key) => (translate ?? (k => "{" + Convert.ToString(k, CultureInfo.InvariantCulture) + "}"))(key);
        const string N = "\r\n";
        var sb = new StringBuilder(N + N);
        if (rows.Count > 0)
        {
            sb.Append("\t<div class=\"col-xs-12 col-sm-12 col-md-12 col-lg-12\">").Append(N)
                .Append("\t<h2 class=\"section-title\">").Append(T(4146)).Append("</h2>").Append(N).Append('\t');
        }

        sb.Append("<ul class=\"cat_blocks\">").Append(N);
        foreach (var row in rows)
        {
            sb.Append("\t<li>").Append(N)
                .Append("    \t<a href=\"").Append(langHref).Append('/').Append(row.Alias).Append("\">").Append(N)
                .Append("    \t\t<span class=\"block_image\">").Append(N)
                .Append("\t\t\t\t<img src=\"/content/files/images/catalogue_images/").Append(row.Img)
                .Append("\" onerror=\"this.src='/content/files/images/no_image.png'\" />").Append(N)
                .Append("\t\t\t</span>").Append(N)
                .Append("    \t\t<span class=\"block_caption\">").Append(T(row.Caption)).Append("</span>").Append(N)
                .Append("    \t</a>").Append(N)
                .Append("    </li>").Append(N);
        }

        sb.Append("</ul>").Append(N).Append(N).Append(N);
        if (rows.Count > 0)
        {
            sb.Append("\t</div>").Append(N).Append('\t');
        }

        return sb.ToString();
    }

    public static string NewsModule(string langHref, string rootUrl, IReadOnlyList<(object Title, long Created, object Description, string Url)> news, Func<object, string>? translate = null)
    {
        string T(object key) => (translate ?? (k => "{" + Convert.ToString(k, CultureInfo.InvariantCulture) + "}"))(key);
        var sb = new StringBuilder();
        foreach (var item in news)
        {
            var created = DateTimeOffset.FromUnixTimeSeconds(item.Created).UtcDateTime;
            sb.Append("\t<div class=\"media news_block\">\n\t\t<div class=\"media-body\">\n\t\t\t<h4 class=\"media-heading\"><a href=\"")
                .Append(langHref).Append('/').Append(item.Url).Append("\">")
                .Append(T(item.Title)).Append("<br>").Append(T(item.Description)).Append("</a></h4>\n\t\t\t<small>")
                .Append(created.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)).Append("</small>\n\t\t</div>\n\t</div>\n    ");
        }

        return sb.Append("<a href=\"").Append(langHref).Append('/').Append(rootUrl)
            .Append("\" class=\"all_news_link\">").Append(T(4763)).Append("</a>").ToString();
    }

    public static object EpcRetailTaxSeedTree()
        => new object[]
        {
            Branch("retail-home", "Home & kitchen", 10, new object[]
            {
                Branch("retail-home-appliances", "Small appliances", null, new object[]
                {
                    Leaf("retail-home-appliances-coffee", "Coffee makers"),
                    Leaf("retail-home-appliances-blenders", "Blenders & mixers"),
                    Leaf("retail-home-appliances-airfryer", "Air fryers & ovens")
                }),
                Branch("retail-home-decor", "Home décor", null, new object[]
                {
                    Leaf("retail-home-decor-lighting", "Lamps & lighting"),
                    Leaf("retail-home-decor-rugs", "Rugs & carpets"),
                    Leaf("retail-home-decor-wall", "Wall art & mirrors")
                }),
                Leaf("retail-home-storage", "Storage & organisation"),
                Branch("retail-home-bedding", "Bedding & bath", null, new object[]
                {
                    Leaf("retail-home-bedding-sheets", "Sheets & duvet covers"),
                    Leaf("retail-home-bedding-towels", "Towels & bath mats")
                })
            }),
            Branch("retail-home-garden", "Home & garden", 15, new object[]
            {
                Branch("retail-garden-tools", "Garden tools", null, new object[]
                {
                    Leaf("retail-garden-tools-hand", "Hand tools & trowels"),
                    Leaf("retail-garden-tools-power", "Lawn mowers & trimmers")
                }),
                Leaf("retail-garden-plants", "Plants & seeds"),
                Branch("retail-garden-outdoor", "Outdoor & patio", null, new object[]
                {
                    Leaf("retail-garden-outdoor-furniture", "Outdoor furniture"),
                    Leaf("retail-garden-outdoor-bbq", "BBQ & grills"),
                    Leaf("retail-garden-outdoor-umbrella", "Umbrellas & shade")
                }),
                Leaf("retail-garden-watering", "Watering & irrigation"),
                Leaf("retail-garden-pots", "Pots, planters & soil")
            }),
            Branch("retail-industrial", "Industrial & trade", 18, new object[]
            {
                Branch("retail-industrial-power-tools", "Power tools", null, new object[]
                {
                    Leaf("retail-industrial-drills", "Drills & drivers"),
                    Leaf("retail-industrial-saws", "Saws & grinders")
                }),
                Leaf("retail-industrial-hand-tools", "Hand tools & toolkits"),
                Branch("retail-industrial-safety", "Safety & PPE", null, new object[]
                {
                    Leaf("retail-industrial-safety-gloves", "Gloves & workwear"),
                    Leaf("retail-industrial-safety-helmets", "Helmets & goggles")
                }),
                Leaf("retail-industrial-plumbing", "Plumbing supplies"),
                Branch("retail-industrial-electrical", "Electrical supplies", null, new object[]
                {
                    Leaf("retail-industrial-cables", "Cables & wiring"),
                    Leaf("retail-industrial-switches", "Switches & sockets")
                }),
                Leaf("retail-industrial-fasteners", "Fasteners & hardware"),
                Leaf("retail-industrial-paint", "Paint & coatings")
            }),
            Branch("retail-office", "Office & stationery", 20, new object[]
            {
                Branch("retail-office-supplies", "Office supplies", null, new object[]
                {
                    Leaf("retail-office-supplies-paper", "Paper & notebooks"),
                    Leaf("retail-office-supplies-pens", "Pens & markers")
                }),
                Branch("retail-office-furniture", "Office furniture", null, new object[]
                {
                    Leaf("retail-office-furniture-desks", "Desks & chairs"),
                    Leaf("retail-office-furniture-storage", "Filing & storage")
                }),
                Leaf("retail-office-tech", "Office tech accessories")
            }),
            Branch("retail-health", "Health & wellness", 30, new object[]
            {
                Leaf("retail-health-vitamins", "Vitamins & supplements"),
                Branch("retail-health-personal", "Personal care", null, new object[]
                {
                    Leaf("retail-health-skincare", "Skincare"),
                    Leaf("retail-health-haircare", "Hair care")
                }),
                Leaf("retail-health-fitness", "Fitness & sports nutrition")
            }),
            Branch("retail-gifts", "Gifts & lifestyle", 40, new object[]
            {
                Leaf("retail-gifts-hampers", "Gift hampers"),
                Leaf("retail-gifts-seasonal", "Seasonal gifts"),
                Leaf("retail-gifts-toys", "Toys & games")
            })
        };

    public static string EpcPortalFaviconSvg(string industry)
        => industry switch
        {
            "electronics" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">"
                + "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#1e40af\"/>"
                + "<rect x=\"16\" y=\"10\" width=\"32\" height=\"22\" rx=\"3\" fill=\"none\" stroke=\"#fff\" stroke-width=\"3\"/>"
                + "<line x1=\"32\" y1=\"32\" x2=\"32\" y2=\"38\" stroke=\"#fff\" stroke-width=\"3\" stroke-linecap=\"round\"/>"
                + "<line x1=\"24\" y1=\"38\" x2=\"40\" y2=\"38\" stroke=\"#fff\" stroke-width=\"3\" stroke-linecap=\"round\"/>"
                + "<circle cx=\"32\" cy=\"21\" r=\"5\" fill=\"none\" stroke=\"#60a5fa\" stroke-width=\"2\"/>"
                + "<circle cx=\"32\" cy=\"21\" r=\"1.5\" fill=\"#60a5fa\"/>"
                + "<rect x=\"14\" y=\"44\" width=\"10\" height=\"10\" rx=\"2\" fill=\"#3b82f6\"/>"
                + "<rect x=\"27\" y=\"44\" width=\"10\" height=\"10\" rx=\"2\" fill=\"#3b82f6\"/>"
                + "<rect x=\"40\" y=\"44\" width=\"10\" height=\"10\" rx=\"2\" fill=\"#3b82f6\"/>"
                + "<text x=\"19\" y=\"52\" font-size=\"7\" fill=\"#fff\" font-family=\"sans-serif\" font-weight=\"bold\">E</text>"
                + "<text x=\"30\" y=\"52\" font-size=\"7\" fill=\"#fff\" font-family=\"sans-serif\" font-weight=\"bold\">A</text>"
                + "<text x=\"43\" y=\"52\" font-size=\"7\" fill=\"#fff\" font-family=\"sans-serif\" font-weight=\"bold\">E</text>"
                + "</svg>",
            "fashion" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">"
                + "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#7c3aed\"/>"
                + "<path d=\"M32 8 L22 20 L26 20 L24 56 L40 56 L38 20 L42 20 Z\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/>"
                + "<path d=\"M26 20 Q32 28 38 20\" fill=\"none\" stroke=\"#c4b5fd\" stroke-width=\"2\"/>"
                + "<circle cx=\"32\" cy=\"12\" r=\"2\" fill=\"#c4b5fd\"/>"
                + "<text x=\"18\" y=\"61\" font-size=\"7\" fill=\"#e9d5ff\" font-family=\"sans-serif\" letter-spacing=\"1\">SNL</text>"
                + "</svg>",
            "jewellery" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">"
                + "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#78350f\"/>"
                + "<polygon points=\"32,8 20,24 12,24 32,56 52,24 44,24\" fill=\"none\" stroke=\"#fbbf24\" stroke-width=\"2.5\" stroke-linejoin=\"round\"/>"
                + "<polygon points=\"32,8 26,24 38,24\" fill=\"#fbbf24\" opacity=\"0.3\"/>"
                + "<line x1=\"12\" y1=\"24\" x2=\"52\" y2=\"24\" stroke=\"#fbbf24\" stroke-width=\"2\"/>"
                + "<line x1=\"26\" y1=\"24\" x2=\"32\" y2=\"56\" stroke=\"#fbbf24\" stroke-width=\"1.5\" opacity=\"0.5\"/>"
                + "<line x1=\"38\" y1=\"24\" x2=\"32\" y2=\"56\" stroke=\"#fbbf24\" stroke-width=\"1.5\" opacity=\"0.5\"/>"
                + "<text x=\"15\" y=\"62\" font-size=\"5.5\" fill=\"#fde68a\" font-family=\"serif\" letter-spacing=\"0.5\">TJT</text>"
                + "</svg>",
            "tax_advisory" or "consultancy" => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">"
                + "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#0f766e\"/>"
                + "<rect x=\"14\" y=\"12\" width=\"36\" height=\"44\" rx=\"3\" fill=\"none\" stroke=\"#fff\" stroke-width=\"2.5\"/>"
                + "<line x1=\"22\" y1=\"22\" x2=\"42\" y2=\"22\" stroke=\"#5eead4\" stroke-width=\"2\" stroke-linecap=\"round\"/>"
                + "<line x1=\"22\" y1=\"29\" x2=\"42\" y2=\"29\" stroke=\"#5eead4\" stroke-width=\"2\" stroke-linecap=\"round\"/>"
                + "<line x1=\"22\" y1=\"36\" x2=\"36\" y2=\"36\" stroke=\"#5eead4\" stroke-width=\"2\" stroke-linecap=\"round\"/>"
                + "<path d=\"M34 42 L38 46 L46 36\" fill=\"none\" stroke=\"#2dd4bf\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>"
                + "<text x=\"14\" y=\"62\" font-size=\"5.5\" fill=\"#99f6e4\" font-family=\"sans-serif\" letter-spacing=\"0.5\">TCA</text>"
                + "</svg>",
            _ => "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 64 64\">"
                + "<rect width=\"64\" height=\"64\" rx=\"14\" fill=\"#fff\"/>"
                + "<path d=\"M18 16h33c2.8 0 4.7 2.6 3.9 5.3L50 39H25L18 16Z\" fill=\"none\" stroke=\"#dc2626\" stroke-linejoin=\"round\" stroke-width=\"5\"/>"
                + "<path d=\"M18 16h-7l-4-5\" fill=\"none\" stroke=\"#dc2626\" stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"5\"/>"
                + "<path d=\"M25 25h22l-3 9H28Z\" fill=\"#dc2626\"/>"
                + "<circle cx=\"27\" cy=\"48\" r=\"7\" fill=\"#dc2626\"/><circle cx=\"27\" cy=\"48\" r=\"4\" fill=\"#fff\"/>"
                + "<circle cx=\"50\" cy=\"48\" r=\"7\" fill=\"#dc2626\"/><circle cx=\"50\" cy=\"48\" r=\"4\" fill=\"#fff\"/>"
                + "</svg>"
        };

    public static string EpcPortalFaviconLinkTags(string industry)
    {
        var encoded = "data:image/svg+xml," + Uri.EscapeDataString(EpcPortalFaviconSvg(industry));
        return "<link rel=\"icon\" type=\"image/svg+xml\" href=\"" + encoded + "\"/>\n"
            + "    <link rel=\"alternate icon\" href=\"/favicon.ico?v=20260621\"/>";
    }

    public static string EpcStorefrontAuthLangHref(IReadOnlyDictionary<string, string>? multilang)
    {
        var href = multilang is not null && multilang.TryGetValue("lang_href", out var value) ? value : "/en/";
        return href.TrimEnd('/') + "/";
    }

    public static string EpcStorefrontAuthLoginUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "users/login";

    public static string EpcStorefrontAuthSignupUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "users/registration";

    public static string EpcStorefrontAuthVendorUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "vendor";

    public static string EpcStorefrontAuthVendorRegisterUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "vendor/register";

    public static string EpcStorefrontAuthGarageLoginUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "garage/login";

    public static string EpcStorefrontAuthGarageManagerUrl(IReadOnlyDictionary<string, string>? multilang)
        => EpcStorefrontAuthLangHref(multilang) + "garage/manager";

    public static string EpcStorefrontAuthLinksHtml(IReadOnlyDictionary<string, string>? multilang, bool loggedIn, string wrapperClass = "epc-auth-header-links")
    {
        if (loggedIn)
        {
            return "";
        }

        string H(string value) => StorefrontTinyPages.HtmlSpecialChars(value);
        return "<span class=\"" + H(wrapperClass) + "\">"
            + "<span class=\"epc-auth-header-links__group epc-auth-header-links__group--vendor\">"
            + "<i class=\"fa fa-briefcase epc-auth-header-links__icon\" aria-hidden=\"true\"></i> "
            + "<a class=\"epc-auth-header-links__vendor-login\" href=\"" + H(EpcStorefrontAuthVendorUrl(multilang)) + "\" title=\"Vendor portal login\">Vendor login</a>"
            + "<span class=\"epc-auth-header-links__slash\" aria-hidden=\"true\">/</span>"
            + "<a class=\"epc-auth-header-links__vendor-register\" href=\"" + H(EpcStorefrontAuthVendorRegisterUrl(multilang)) + "\" title=\"Vendor registration\">Register</a>"
            + "</span>"
            + "<span class=\"epc-auth-header-links__sep\" aria-hidden=\"true\">|</span>"
            + "<span class=\"epc-auth-header-links__group epc-auth-header-links__group--customer\">"
            + "<i class=\"fa fa-user epc-auth-header-links__icon\" aria-hidden=\"true\"></i> "
            + "<a class=\"epc-auth-header-links__login\" href=\"" + H(EpcStorefrontAuthLoginUrl(multilang)) + "\" title=\"Customer login\">Customer login</a>"
            + "<span class=\"epc-auth-header-links__slash\" aria-hidden=\"true\">/</span>"
            + "<a class=\"epc-auth-header-links__signup\" href=\"" + H(EpcStorefrontAuthSignupUrl(multilang)) + "\" title=\"Customer registration\">register</a>"
            + "</span>"
            + "</span>";
    }

    public static string EpcStorefrontAuthLinksRender(IReadOnlyDictionary<string, string>? multilang, bool loggedIn, string wrapperClass = "epc-auth-header-links")
        => EpcStorefrontAuthLinksHtml(multilang, loggedIn, wrapperClass);

    public static string EpcStorefrontAuthLinksStyles()
        => "<style>"
            + ".epc-auth-header-links{display:inline-flex;align-items:center;gap:8px;white-space:nowrap;font-weight:700}"
            + ".epc-auth-header-links__group{display:inline-flex;align-items:center;gap:5px}"
            + ".epc-auth-header-links__icon,.epc-auth-header-links__group > .fa{color:#ef4444;margin-right:2px;font-size:14px;line-height:1;width:1em;text-align:center}"
            + ".epc-auth-header-links__sep{color:rgba(255,255,255,.35);font-weight:400;padding:0 2px}"
            + ".epc-auth-header-links__slash{color:rgba(255,255,255,.45);padding:0 1px;font-weight:600}"
            + ".epc-auth-header-links a{text-decoration:none;font-weight:700;color:inherit}"
            + ".epc-auth-header-links a:hover{color:#fff;text-decoration:underline}"
            + ".epc-auth-header-links__vendor-register,.epc-auth-header-links__signup{color:#fda4af}"
            + ".epc-er-utility__actions .epc-auth-header-links{margin-left:6px}"
            + "@media(max-width:991px){.epc-auth-header-links{flex-wrap:wrap;white-space:normal;row-gap:4px}}"
            + "</style>";

    public static string GarageLogin(string langHref, string backendDir, bool staff, bool stay)
    {
        var lang = langHref.TrimEnd('/');
        if (string.IsNullOrEmpty(lang))
        {
            lang = "/en";
        }

        var managerUrl = lang + "/garage/manager";
        var backend = string.IsNullOrEmpty(backendDir) ? "cp" : backendDir;
        if (staff && !stay)
        {
            return "<script>location=" + System.Text.Json.JsonSerializer.Serialize(managerUrl) + ";</script>";
        }

        var cpWorkshop = "/" + backend + "/shop/workshop/workshop";
        var cpLogin = "/" + backend + "/";
        var bookUrl = lang + "/auto-workshop";
        var myGarage = lang + "/garazh";
        string H(string value) => StorefrontTinyPages.HtmlSpecialChars(value);
        var staffBlock = staff
            ? "<div class=\"epc-gl__ok\">You are signed in with workshop access.</div>\n\t\t\t\t"
                + "<a class=\"epc-gl__btn\" href=\"" + H(managerUrl) + "\"><i class=\"fa fa-th-large\"></i> Open Garage Manager</a>\n\t\t\t\t"
                + "<a class=\"epc-gl__btn epc-gl__btn--ghost\" href=\"" + H(cpWorkshop) + "\"><i class=\"fa fa-desktop\"></i> CP workshop desk</a>"
            : "<p>Sign in with your <strong>Control Panel / workshop</strong> staff account to run the garage end-to-end.</p>\n\t\t\t\t"
                + "<a class=\"epc-gl__btn\" href=\"" + H(cpLogin) + "\"><i class=\"fa fa-sign-in\"></i> Staff CP login</a>\n\t\t\t\t"
                + "<a class=\"epc-gl__btn epc-gl__btn--ghost\" href=\"" + H(cpWorkshop) + "\"><i class=\"fa fa-car\"></i> Workshop desk</a>\n\t\t\t\t"
                + "<p style=\"margin-top:12px;font-size:13px\">After CP login, return here or open <em>Garage Manager</em> from the header.</p>";

        return GarageCss
            + "<div class=\"epc-gl\">\n\t<header class=\"epc-gl__hero\">\n\t\t<h1>Garage Manager login</h1>\n"
            + "\t\t<p>Staff portal for the full garage workflow — appointments, check-in, job cards, bays, technicians, parts &amp; labour, QC and handover.</p>\n"
            + "\t</header>\n\n\t<div class=\"epc-gl__grid\">\n\t\t<div class=\"epc-gl__card\">\n"
            + "\t\t\t<h2><i class=\"fa fa-wrench\"></i> Workshop staff</h2>\n\t\t\t" + staffBlock
            + "\n\t\t</div>\n\t\t<div class=\"epc-gl__card\">\n\t\t\t<h2><i class=\"fa fa-users\"></i> Customers</h2>\n"
            + "\t\t\t<p>Vehicle garage, notepad, and service booking — no staff login required.</p>\n"
            + "\t\t\t<a class=\"epc-gl__btn epc-gl__btn--ghost\" href=\"" + H(myGarage) + "\"><i class=\"fa fa-home\"></i> My Garage</a>\n"
            + "\t\t\t<a class=\"epc-gl__btn epc-gl__btn--ghost\" href=\"" + H(bookUrl) + "\"><i class=\"fa fa-calendar\"></i> Book / track service</a>\n"
            + "\t\t\t<ul class=\"epc-gl__list\">\n\t\t\t\t<li>Save vehicles &amp; VIN</li>\n"
            + "\t\t\t\t<li>Parts notepad per car</li>\n\t\t\t\t<li>Book service &amp; track job status</li>\n"
            + "\t\t\t</ul>\n\t\t</div>\n\t</div>\n</div>\n";
    }

    public static string FashionMegaMenu(
        IReadOnlyList<(string Label, string Href, string Image, string Alt)> catalog,
        IReadOnlyList<(string Label, string Href)> chips,
        string langHref)
    {
        var items = new List<(string Label, string Href, string Image, string Alt)>(catalog);
        foreach (var chip in chips)
        {
            items.Add((chip.Label, langHref + chip.Href, "/chip.png", chip.Label));
        }

        var sb = new StringBuilder("<ul class=\"epc-frn-mega-menu__grid\">\n\t");
        foreach (var item in items)
        {
            sb.Append("\t<li>\n\t\t<a href=\"").Append(H(item.Href)).Append("\">\n\t\t\t<img src=\"")
                .Append(H(item.Image)).Append("\" alt=\"").Append(H(item.Alt)).Append("\" width=\"48\" height=\"48\" loading=\"lazy\" decoding=\"async\" />\n\t\t\t<span>")
                .Append(H(item.Label)).Append("</span>\n\t\t</a>\n\t</li>\n\t");
        }

        return sb.Append("</ul>\n").ToString();
    }

    public static string EpcCpiHeroBannerHref(string? lang, string? path) => HeroHref(lang, path);
    public static string EpcFrnHeroHref(string? lang, string? path) => HeroHref(lang, path);
    public static string EpcJrkHeroHref(string? lang, string? path) => HeroHref(lang, path);
    public static string EpcErHeroHref(string? lang, string? path) => HeroHref(lang, path);

    public static string HeroBanner(
        string sectionClass,
        string particleColor,
        string particleCount,
        string eyebrowIcon,
        string visualAria,
        string animClass,
        string animAria,
        string animInner,
        string videoTitle,
        string videoCopy,
        string lang,
        string eyebrow,
        string title,
        string copy,
        IReadOnlyList<(string Label, string Href, string Icon, bool Primary)> actions,
        IReadOnlyList<(string Value, string Label)> stats)
    {
        var sb = new StringBuilder("<section class=\"epc-home-pro ").Append(sectionClass)
            .Append("\" style=\"position:relative;overflow:hidden\">\n")
            .Append("\t<div class=\"epc-particles\" data-color=\"").Append(particleColor)
            .Append("\" data-count=\"").Append(particleCount).Append("\"></div>\n")
            .Append("\t<div class=\"container\">\n\t\t<div class=\"epc-home-pro__grid\">\n\t\t\t<div>\n")
            .Append("\t\t\t\t<div class=\"epc-home-pro__eyebrow\"><i class=\"fa ").Append(eyebrowIcon)
            .Append("\"></i>&nbsp; ").Append(H(eyebrow)).Append("</div>\n")
            .Append("\t\t\t\t<h2>").Append(H(title)).Append("</h2>\n")
            .Append("\t\t\t\t<p class=\"epc-home-pro__copy\">").Append(H(copy)).Append("</p>\n")
            .Append("\t\t\t\t<div class=\"epc-home-pro__actions\">\n");
        foreach (var act in actions)
        {
            var cls = "epc-home-pro__btn" + (act.Primary ? " epc-home-pro__btn--primary" : " epc-home-pro__btn--ghost");
            sb.Append("\t\t\t\t\t<a class=\"").Append(cls).Append("\" href=\"").Append(HeroHref(lang, act.Href)).Append("\">\n")
                .Append("\t\t\t\t\t\t<i class=\"fa ").Append(H(act.Icon)).Append("\"></i>\n\t\t\t\t\t\t")
                .Append(H(act.Label)).Append("\n\t\t\t\t\t</a>\n");
        }

        sb.Append("\t\t\t\t</div>\n\t\t\t\t<div class=\"epc-home-pro__stats\">\n");
        foreach (var stat in stats)
        {
            sb.Append("\t\t\t\t\t<div class=\"epc-home-pro__stat\">\n")
                .Append("\t\t\t\t\t\t<strong>").Append(H(stat.Value)).Append("</strong>\n")
                .Append("\t\t\t\t\t\t<span>").Append(H(stat.Label)).Append("</span>\n")
                .Append("\t\t\t\t\t</div>\n");
        }

        return sb.Append("\t\t\t\t</div>\n\t\t\t</div>\n")
            .Append("\t\t\t<div class=\"epc-home-pro__visual\" aria-label=\"").Append(visualAria).Append("\">\n")
            .Append("\t\t\t\t<div class=\"").Append(animClass).Append("\" role=\"img\" aria-label=\"").Append(animAria).Append("\">\n")
            .Append(animInner)
            .Append("\t\t\t\t</div>\n")
            .Append("\t\t\t\t<div class=\"epc-home-pro__video-card\">\n")
            .Append("\t\t\t\t\t<span class=\"epc-home-pro__play\"><i class=\"fa fa-play\"></i></span>\n")
            .Append("\t\t\t\t\t<strong>").Append(videoTitle).Append("</strong>\n")
            .Append("\t\t\t\t\t<p>").Append(videoCopy).Append("</p>\n")
            .Append("\t\t\t\t</div>\n\t\t\t</div>\n\t\t</div>\n\t</div>\n</section>\n").ToString();
    }

    private const string GarageCss = "<style>\n.epc-gl{--ink:#0b1220;--muted:#5b6578;--teal:#0e7490;font-family:\"DM Sans\",\"Source Sans 3\",\"Segoe UI\",sans-serif;color:var(--ink);max-width:920px;margin:0 auto 48px}\n.epc-gl__hero{margin:0 0 22px;padding:36px 28px;border-radius:18px;color:#f8fafc;\nbackground:radial-gradient(800px 240px at 90% -10%,rgba(125,211,252,.28),transparent 55%),linear-gradient(125deg,#0b1220 0%,#164e63 48%,#0e7490 100%);\nbox-shadow:0 18px 40px rgba(11,18,32,.22)}\n.epc-gl__hero h1{margin:0 0 8px;font-size:clamp(1.8rem,4vw,2.4rem);font-weight:800;letter-spacing:-.03em}\n.epc-gl__hero p{margin:0;opacity:.92;max-width:52ch;line-height:1.5}\n.epc-gl__grid{display:grid;grid-template-columns:1.1fr .9fr;gap:16px}\n@media(max-width:800px){.epc-gl__grid{grid-template-columns:1fr}}\n.epc-gl__card{background:#fff;border:1px solid #d9e2e0;border-radius:16px;padding:22px;box-shadow:0 10px 28px rgba(15,23,42,.06)}\n.epc-gl__card h2{margin:0 0 10px;font-size:1.15rem;font-weight:800}\n.epc-gl__card p{color:var(--muted);line-height:1.5;margin:0 0 14px}\n.epc-gl__btn{display:inline-flex;align-items:center;gap:8px;padding:11px 16px;border-radius:10px;background:var(--teal);color:#fff!important;font-weight:800;text-decoration:none!important;margin:0 8px 8px 0}\n.epc-gl__btn--ghost{background:#fff;color:var(--ink)!important;border:1px solid #c9d4d1}\n.epc-gl__list{margin:0;padding-left:18px;color:var(--muted);line-height:1.55}\n.epc-gl__ok{background:#ecfeff;border:1px solid #a5f3fc;border-radius:12px;padding:12px 14px;margin:0 0 14px;font-weight:700;color:#155e75}\n</style>\n\n";

    private static string HeroHref(string? lang, string? path)
    {
        path ??= "";
        if (path.Length > 0 && path[0] == '/')
        {
            return H((lang ?? "").TrimEnd('/') + path);
        }

        return H(path);
    }

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);

    private static Dictionary<string, object> Leaf(string slug, string name)
        => new() { ["slug"] = slug, ["name"] = name };

    private static Dictionary<string, object> Branch(string slug, string name, int? sort, object[] children)
    {
        var row = new Dictionary<string, object> { ["slug"] = slug, ["name"] = name, ["children"] = children };
        if (sort is int value)
        {
            row["sort"] = value;
        }

        return row;
    }
}
