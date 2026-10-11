using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Industry chrome, SEO helpers, animated logos, marketing templates, channel schema,
/// CP page frame and two CP JS configs. Function names kept for the inventory:
/// <c>epc_frn_footer_href</c>, <c>epc_er_footer_href</c>, <c>epc_jrk_footer_href</c>, <c>epc_cpi_footer_href</c>,
/// <c>epc_fashion_retail_namshi_store_name</c>, <c>epc_fashion_retail_namshi_tagline</c>,
/// <c>epc_fashion_retail_namshi_public_url</c>, <c>epc_fashion_retail_namshi_apply_seo</c>,
/// <c>epc_fashion_retail_namshi_patch_template_html</c>, <c>epc_fashion_retail_namshi_scrub_legacy_strings</c>,
/// <c>epc_electronics_retail_store_name</c>, <c>epc_electronics_retail_tagline</c>,
/// <c>epc_electronics_retail_public_url</c>, <c>epc_electronics_retail_apply_seo</c>,
/// <c>epc_electronics_retail_patch_template_html</c>, <c>epc_electronics_retail_scrub_autoparts_strings</c>,
/// <c>epc_jewellery_retail_kiyasha_store_name</c>, <c>epc_jewellery_retail_kiyasha_tagline</c>,
/// <c>epc_jewellery_retail_kiyasha_public_url</c>, <c>epc_jewellery_retail_kiyasha_apply_seo</c>,
/// <c>epc_jewellery_retail_kiyasha_patch_template_html</c>, <c>epc_jewellery_retail_kiyasha_scrub_legacy_strings</c>,
/// <c>epc_cpi_store_name</c>, <c>epc_cpi_tagline</c>, <c>epc_cpi_apply_seo</c>, <c>epc_cpi_patch_template_html</c>,
/// <c>epc_storefront_animated_logo_enqueue</c>, <c>epc_storefront_animated_logo_markup</c>,
/// <c>epc_storefront_animated_logo_fashion</c>, <c>epc_storefront_animated_logo_electronics</c>,
/// <c>epc_storefront_animated_logo_consulting</c>, <c>epc_storefront_animated_logo_jewellery</c>,
/// <c>epc_mb_email_templates</c>, <c>epc_mb_whatsapp_templates</c>, <c>epc_mb_apply_template_vars</c>,
/// <c>epc_channel_ensure_schema</c>,
/// <c>epc_cp_register_page_assets</c>, <c>epc_cp_page_frame_open</c>, <c>epc_cp_page_frame_hero</c>,
/// <c>epc_cp_page_frame_close</c>.
/// </summary>
public static class PhpIndustryChrome
{
    public const string FashionFooterPath = "content/general_pages/epc_portal_fashion_retail_namshi_footer.php";
    public const string ElectronicsFooterPath = "content/general_pages/epc_portal_electronics_retail_footer.php";
    public const string JewelleryFooterPath = "content/general_pages/epc_portal_jewellery_retail_kiyasha_footer.php";
    public const string ConsultingFooterPath = "content/general_pages/epc_portal_consulting_primeinvest_footer.php";
    public const string FashionHelpersPath = "content/general_pages/epc_fashion_retail_namshi_helpers.php";
    public const string ElectronicsHelpersPath = "content/general_pages/epc_electronics_retail_helpers.php";
    public const string JewelleryHelpersPath = "content/general_pages/epc_jewellery_retail_kiyasha_helpers.php";
    public const string ConsultingHelpersPath = "content/general_pages/epc_consulting_primeinvest_helpers.php";
    public const string AnimatedLogosPath = "content/general_pages/epc_storefront_animated_logos.php";
    public const string MarketingTemplatesPath = "content/shop/marketing/epc_marketing_broadcast_templates.php";
    public const string ChannelSchemaPath = "content/shop/channels/epc_channel_schema.php";
    public const string PageFramePath = "content/general_pages/epc_cp_page_frame.php";
    public const string FilemanagerConfigPath = "cp/content/filemanager/epc_filemanager_config.php";
    public const string OrdersItemsConfigPath = "cp/content/shop/order_process/orders_items_config.php";

    public const string FilemanagerConfigUrl = "/cp/content/filemanager/epc_filemanager_config.php";
    public const string OrdersItemsConfigUrl = "/cp/content/shop/order_process/orders_items_config.php";

    public static readonly string[] EpcChannelEnsureSchemaTables =
    [
        "epc_marketplace_channels",
        "epc_marketplace_sku_map",
        "epc_marketplace_orders",
        "epc_carrier_accounts",
        "epc_carrier_shipments",
        "epc_channel_sync_log"
    ];

    public const string EpcChannelEnsureSchemaSql =
        "CREATE TABLE IF NOT EXISTS `epc_marketplace_channels` ("
        + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,`code` VARCHAR(64) NOT NULL,`name` VARCHAR(128) NOT NULL,"
        + "`marketplace_id` VARCHAR(64) DEFAULT NULL,`active` TINYINT(1) NOT NULL DEFAULT 1,`demo_mode` TINYINT(1) NOT NULL DEFAULT 1,"
        + "`config_json` TEXT,`last_sync_at` INT UNSIGNED NOT NULL DEFAULT 0,`time_created` INT UNSIGNED NOT NULL DEFAULT 0,"
        + "PRIMARY KEY (`id`),UNIQUE KEY `code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8";

    private static readonly HashSet<string> ScriptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        FilemanagerConfigUrl, OrdersItemsConfigUrl
    };

    public static bool IsScriptPath(string? path) => path is not null && ScriptPaths.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(FilemanagerConfigUrl, ["GET", "HEAD", "POST"], ServeFilemanagerAsync)
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(OrdersItemsConfigUrl, ["GET", "HEAD", "POST"], ServeOrdersItemsAsync)
            .DisableAntiforgery().AllowAnonymous();
    }

    public static string EpcFrnFooterHref(string? lang, string? path) => FooterHref(lang, path);
    public static string EpcErFooterHref(string? lang, string? path) => FooterHref(lang, path);
    public static string EpcJrkFooterHref(string? lang, string? path) => FooterHref(lang, path);
    public static string EpcCpiFooterHref(string? lang, string? path) => FooterHref(lang, path);

    public static string RetailFooter(
        string prefix,
        string lang,
        IReadOnlyList<(string Title, IReadOnlyList<(string Label, string Href)> Links)> columns,
        IReadOnlyList<(string Label, string Href, string Icon)> social,
        IReadOnlyList<string> payments,
        string tradeName,
        int year,
        string hostedHtml = "")
    {
        var p = prefix;
        var sb = new StringBuilder("<footer class=\"epc-").Append(p).Append("-footer\" id=\"epc_").Append(p).Append("_footer\">\n")
            .Append("\t<div class=\"epc-").Append(p).Append("-footer__main\">\n\t\t<div class=\"container\">\n")
            .Append("\t\t\t<div class=\"epc-").Append(p).Append("-footer__grid\">\n");
        foreach (var col in columns)
        {
            sb.Append("\t\t\t\t<div class=\"epc-").Append(p).Append("-footer__col\">\n")
                .Append("\t\t\t\t\t<h3 class=\"epc-").Append(p).Append("-footer__title\">").Append(H(col.Title)).Append("</h3>\n")
                .Append("\t\t\t\t\t<ul class=\"epc-").Append(p).Append("-footer__links\">\n");
            foreach (var link in col.Links)
            {
                sb.Append("\t\t\t\t\t\t<li>\n\t\t\t\t\t\t\t<a href=\"").Append(FooterHref(lang, link.Href)).Append("\">\n")
                    .Append("\t\t\t\t\t\t\t\t").Append(H(link.Label)).Append("\t\t\t\t\t\t\t</a>\n\t\t\t\t\t\t</li>\n");
            }

            sb.Append("\t\t\t\t\t</ul>\n\t\t\t\t</div>\n");
        }

        sb.Append("\t\t\t</div>\n\t\t\t<div class=\"epc-").Append(p).Append("-footer__social-row\">\n")
            .Append("\t\t\t\t<div class=\"epc-").Append(p).Append("-footer__social\">\n")
            .Append("\t\t\t\t\t<span class=\"epc-").Append(p).Append("-footer__social-label\">Follow us</span>\n");
        foreach (var s in social)
        {
            sb.Append("\t\t\t\t\t<a class=\"epc-").Append(p).Append("-footer__social-icon\" href=\"").Append(H(s.Href))
                .Append("\" target=\"_blank\" rel=\"noopener noreferrer\" title=\"").Append(H(s.Label)).Append("\">\n")
                .Append("\t\t\t\t\t\t<i class=\"fa ").Append(H(s.Icon)).Append("\" aria-hidden=\"true\"></i>\n\t\t\t\t\t</a>\n");
        }

        sb.Append("\t\t\t\t</div>\n\t\t\t\t<div class=\"epc-").Append(p).Append("-footer__payments\">\n")
            .Append("\t\t\t\t\t<span class=\"epc-").Append(p).Append("-footer__social-label\">We accept</span>\n")
            .Append("\t\t\t\t\t<div class=\"epc-").Append(p).Append("-footer__pay-icons\">\n");
        foreach (var pay in payments)
        {
            sb.Append("\t\t\t\t\t\t<span class=\"epc-").Append(p).Append("-footer__pay\" title=\"").Append(H(UcFirst(pay))).Append("\">\n")
                .Append("\t\t\t\t\t\t\t<img src=\"/content/files/images/icons/pay/").Append(H(pay))
                .Append(".jpg\" alt=\"\" width=\"48\" height=\"32\" loading=\"lazy\" />\n\t\t\t\t\t\t</span>\n");
        }

        return sb.Append("\t\t\t\t\t</div>\n\t\t\t\t</div>\n\t\t\t</div>\n\t\t</div>\n\t</div>\n")
            .Append("\t<div class=\"epc-").Append(p).Append("-footer__bar\">\n\t\t<div class=\"container\">\n")
            .Append("\t\t\t<p class=\"epc-").Append(p).Append("-footer__copy\">\n")
            .Append("\t\t\t\t&copy; ").Append(year.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(H(tradeName)).Append(".\n")
            .Append("\t\t\t\tAll rights reserved.\n\t\t\t\t").Append(hostedHtml).Append("\t\t\t</p>\n")
            .Append("\t\t</div>\n\t</div>\n</footer>\n").ToString();
    }

    public static string ConsultingFooter(
        string lang,
        IReadOnlyList<(string Title, IReadOnlyList<(string Label, string Href)> Links)> columns,
        string tradeName,
        int year,
        string email,
        string phone,
        string hostedHtml = "",
        string brandMarkup = "",
        IReadOnlyList<(string Url, string Label, string Icon)>? social = null)
    {
        var sb = new StringBuilder("<footer class=\"epc-cpi-footer\" id=\"epc_cpi_footer\">\n")
            .Append("\t<div class=\"epc-cpi-footer__top\">\n\t\t<div class=\"container\">\n\t\t\t<div class=\"epc-cpi-footer__grid\">\n")
            .Append("\t\t\t\t<div class=\"epc-cpi-footer__brand\">\n\t\t\t\t\t").Append(brandMarkup)
            .Append("\t\t\t\t\t<p>").Append(H(tradeName))
            .Append(" — tax, accounting and business advisory for UAE entities. Corporate tax, VAT and client ERP in one relationship.</p>\n")
            .Append("\t\t\t\t\t<div class=\"epc-cpi-footer__newsletter\">\n")
            .Append("\t\t\t\t\t\t<label for=\"epc_cpi_newsletter\">Stay informed on tax updates</label>\n")
            .Append("\t\t\t\t\t\t<div class=\"epc-cpi-footer__newsletter-row\">\n")
            .Append("\t\t\t\t\t\t\t<input type=\"email\" id=\"epc_cpi_newsletter\" placeholder=\"Your email\" autocomplete=\"email\" />\n")
            .Append("\t\t\t\t\t\t\t<button type=\"button\" onclick=\"window.location.href='")
            .Append(FooterHref(lang, "/kontakty")).Append("'\">Subscribe</button>\n")
            .Append("\t\t\t\t\t\t</div>\n\t\t\t\t\t</div>\n\t\t\t\t</div>\n");
        foreach (var col in columns)
        {
            sb.Append("\t\t\t\t<div class=\"epc-cpi-footer__col\">\n")
                .Append("\t\t\t\t\t<h3 class=\"epc-cpi-footer__title\">").Append(H(col.Title)).Append("</h3>\n")
                .Append("\t\t\t\t\t<ul class=\"epc-cpi-footer__links\">\n");
            foreach (var link in col.Links)
            {
                sb.Append("\t\t\t\t\t\t<li>\n\t\t\t\t\t\t\t<a href=\"").Append(FooterHref(lang, link.Href)).Append("\">\n")
                    .Append("\t\t\t\t\t\t\t\t").Append(H(link.Label)).Append("\t\t\t\t\t\t\t</a>\n\t\t\t\t\t\t</li>\n");
            }

            sb.Append("\t\t\t\t\t</ul>\n\t\t\t\t</div>\n");
        }

        sb.Append("\t\t\t</div>\n\t\t</div>\n\t</div>\n")
            .Append("\t<div class=\"epc-cpi-footer__bar\">\n")
            .Append("\t\t<div class=\"container\" style=\"display:flex;align-items:center;justify-content:space-between;flex-wrap:wrap;gap:12px;\">\n")
            .Append("\t\t\t<p class=\"epc-cpi-footer__copy\">\n")
            .Append("\t\t\t\t&copy; ").Append(year.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(H(tradeName)).Append(".\n")
            .Append("\t\t\t\t").Append(H(email)).Append(" &middot;\n\t\t\t\t").Append(H(phone));
        if (hostedHtml.Length > 0)
        {
            sb.Append("\t\t\t\t &middot; ").Append(hostedHtml);
        }

        sb.Append("\t\t\t</p>\n");
        if (social is { Count: > 0 })
        {
            sb.Append("\t\t\t<div class=\"epc-cpi-footer__social\" style=\"display:flex;gap:12px;\">");
            foreach (var s in social)
            {
                sb.Append("<a href=\"").Append(H(s.Url)).Append("\" target=\"_blank\" rel=\"noopener\" title=\"")
                    .Append(H(s.Label)).Append("\" style=\"color:#94a3b8;font-size:18px;\"><i class=\"fa ")
                    .Append(H(s.Icon)).Append("\"></i></a>");
            }

            sb.Append("</div>");
        }

        return sb.Append("\t\t</div>\n\t</div>\n</footer>\n").ToString();
    }

    public static string EpcFashionRetailNamshiStoreName(IndustryPortal portal) => StoreName(portal, "Stylenlook", tradeFirst: false);
    public static string EpcFashionRetailNamshiTagline(IndustryPortal portal) => Tagline(portal, "Fashion, beauty & lifestyle — UAE delivery, prices in AED");
    public static string EpcFashionRetailNamshiPublicUrl(IndustryPortal portal) => PublicUrl(portal);
    public static void EpcFashionRetailNamshiApplySeo(SeoContent? content, IndustryPortal portal)
        => ApplySeo(content, EpcFashionRetailNamshiStoreName(portal), EpcFashionRetailNamshiTagline(portal),
            " — Fashion & Beauty UAE",
            ": {tagline} Shop women, men, beauty & accessories with fast UAE delivery.",
            "fashion UAE, beauty Dubai, makeup, skincare, dresses, shoes, AED, {name}",
            "fashion, beauty, {name}, UAE, AED", "Shop");
    public static string EpcFashionRetailNamshiPatchTemplateHtml(string html, IndustryPortal portal)
        => PatchTemplate(html, EpcFashionRetailNamshiStoreName(portal) + " — Fashion & Beauty UAE",
            EpcFashionRetailNamshiStoreName(portal) + ": " + EpcFashionRetailNamshiTagline(portal),
            "fashion UAE, beauty Dubai, makeup, skincare, dresses, shoes, AED, " + EpcFashionRetailNamshiStoreName(portal));
    public static string EpcFashionRetailNamshiScrubLegacyStrings(string html, IndustryPortal portal)
        => Scrub(html, EpcFashionRetailNamshiStoreName(portal), "Fashion", "fashion", "products");

    public static string EpcElectronicsRetailStoreName(IndustryPortal portal) => StoreName(portal, "Electronicae", tradeFirst: false);
    public static string EpcElectronicsRetailTagline(IndustryPortal portal) => Tagline(portal, "Shop phones, gaming, audio & laptops — UAE delivery, prices in AED");
    public static string EpcElectronicsRetailPublicUrl(IndustryPortal portal) => PublicUrl(portal);
    public static void EpcElectronicsRetailApplySeo(SeoContent? content, IndustryPortal portal)
        => ApplySeo(content, EpcElectronicsRetailStoreName(portal), EpcElectronicsRetailTagline(portal),
            " — Tech, Gaming & Electronics UAE",
            ": {tagline} Free delivery across the UAE. Samsung, Apple, Sony, gaming & more.",
            "electronics UAE, smartphones, gaming UAE, laptops, headphones, smart home, AED, {name}",
            "electronics, {name}, UAE, AED", "Shop");
    public static string EpcElectronicsRetailPatchTemplateHtml(string html, IndustryPortal portal)
        => PatchTemplate(html, EpcElectronicsRetailStoreName(portal) + " — Tech, Gaming & Electronics UAE",
            EpcElectronicsRetailStoreName(portal) + ": " + EpcElectronicsRetailTagline(portal),
            "electronics UAE, smartphones, gaming, laptops, headphones, smart home, AED, " + EpcElectronicsRetailStoreName(portal));
    public static string EpcElectronicsRetailScrubAutopartsStrings(string html, IndustryPortal portal)
        => Scrub(html, EpcElectronicsRetailStoreName(portal), "Electronics", "electronics", "products");

    public static string EpcJewelleryRetailKiyashaStoreName(IndustryPortal portal) => StoreName(portal, "The Jewellery Trend", tradeFirst: false);
    public static string EpcJewelleryRetailKiyashaTagline(IndustryPortal portal) => Tagline(portal, "Fine gold, diamonds & bridal jewellery — UAE delivery, prices in AED");
    public static string EpcJewelleryRetailKiyashaPublicUrl(IndustryPortal portal) => PublicUrl(portal);
    public static void EpcJewelleryRetailKiyashaApplySeo(SeoContent? content, IndustryPortal portal)
        => ApplySeo(content, EpcJewelleryRetailKiyashaStoreName(portal), EpcJewelleryRetailKiyashaTagline(portal),
            " — Fine Jewellery UAE",
            ": {tagline} Shop rings, necklaces, earrings & bridal collections with insured UAE delivery.",
            "jewellery UAE, gold Dubai, diamond rings, necklaces, earrings, bridal, AED, {name}",
            "jewellery, gold, diamonds, {name}, UAE, AED", "Shop");
    public static string EpcJewelleryRetailKiyashaPatchTemplateHtml(string html, IndustryPortal portal)
        => PatchTemplate(html, EpcJewelleryRetailKiyashaStoreName(portal) + " — Fine Jewellery UAE",
            EpcJewelleryRetailKiyashaStoreName(portal) + ": " + EpcJewelleryRetailKiyashaTagline(portal),
            "jewellery UAE, gold Dubai, diamond rings, necklaces, earrings, bridal, AED, " + EpcJewelleryRetailKiyashaStoreName(portal));
    public static string EpcJewelleryRetailKiyashaScrubLegacyStrings(string html, IndustryPortal portal)
        => Scrub(html, EpcJewelleryRetailKiyashaStoreName(portal), "Jewellery", "jewellery", "pieces");

    public static string EpcCpiStoreName(IndustryPortal portal) => StoreName(portal, "Taxofinca", tradeFirst: true);
    public static string EpcCpiTagline(IndustryPortal portal) => Tagline(portal, "Tax & advisory services — UAE corporate tax, VAT and business compliance");
    public static void EpcCpiApplySeo(SeoContent? content, IndustryPortal portal)
        => ApplySeo(content, EpcCpiStoreName(portal), EpcCpiTagline(portal),
            " — Tax, Accounting & Advisory UAE",
            ": {tagline}",
            "tax advisory UAE, corporate tax, VAT, accounting, {name}, business compliance",
            "tax advisory, accounting, {name}, UAE", "Portal");
    public static string EpcCpiPatchTemplateHtml(string html, IndustryPortal portal)
    {
        var name = EpcCpiStoreName(portal);
        html = PatchTemplate(html, name + " — Tax, Accounting & Advisory UAE", name + ": " + EpcCpiTagline(portal),
            "tax advisory UAE, corporate tax, VAT, accounting, " + name);
        return ReplaceI(ReplaceI(ReplaceI(ReplaceI(html, "eParts Cart (Autoparts)", name), "eParts Cart", name), "(Autoparts)", ""), "Autoparts", "Tax advisory");
    }

    public static string EpcStorefrontAnimatedLogoEnqueue()
        => "<link rel=\"stylesheet\" href=\"/content/general_pages/epc_storefront_animated_logos.css?v=20260529h\" />\n";

    public static string EpcStorefrontAnimatedLogoFashion(string label) => LogoSpan("fashion", "0 0 36 40",
        "<path class=\"epc-sf-logo__hanger-hook\" d=\"M18 4c-2 0-3 1.5-3 3.5S16 11 18 11s3-1.5 3-3.5S20 4 18 4z\" fill=\"#f9a8d4\"/>\n"
        + "\t\t<path class=\"epc-sf-logo__dress\" d=\"M18 11 L6 28 L30 28 Z\" fill=\"#ec4899\" opacity=\".92\"/>", label);

    public static string EpcStorefrontAnimatedLogoElectronics(string label) => LogoSpan("electronics", "0 0 40 36",
        "<rect x=\"6\" y=\"8\" width=\"28\" height=\"20\" rx=\"4\" fill=\"#111\" stroke=\"#e10a0a\" stroke-width=\"2\"/>\n"
        + "\t\t<circle class=\"epc-sf-logo__pulse\" cx=\"20\" cy=\"18\" r=\"5\" fill=\"#e10a0a\"/>", label);

    public static string EpcStorefrontAnimatedLogoConsulting(string label) => LogoSpan("consulting", "0 0 48 36",
        "<rect class=\"epc-sf-logo__bar\" x=\"4\" y=\"18\" width=\"8\" height=\"14\" rx=\"2\" fill=\"#d4af37\"/>\n"
        + "\t\t<rect class=\"epc-sf-logo__bar\" x=\"18\" y=\"10\" width=\"8\" height=\"22\" rx=\"2\" fill=\"#1e40af\"/>\n"
        + "\t\t<rect class=\"epc-sf-logo__bar\" x=\"32\" y=\"14\" width=\"8\" height=\"18\" rx=\"2\" fill=\"#d4af37\"/>", label);

    public static string EpcStorefrontAnimatedLogoJewellery(string label) => LogoSpan("jewellery", "0 0 44 40",
        "<circle cx=\"22\" cy=\"22\" r=\"14\" fill=\"none\" stroke=\"#d4af37\" stroke-width=\"4\"/>\n"
        + "\t\t<path class=\"epc-sf-logo__spark\" d=\"M22 6 L24 12 L30 12 L25 16 L27 22 L22 18 L17 22 L19 16 L14 12 L20 12 Z\" fill=\"#fbbf24\"/>", label);

    public static (string Echo, string Markup) EpcStorefrontAnimatedLogoMarkup(string packageId, string tradeName, bool enqueueAlready)
    {
        packageId = Regex.Replace(packageId, "[^a-z0-9_]", "");
        if (packageId.Length == 0)
        {
            return ("", "");
        }

        var label = tradeName;
        if (label.Length == 0 || label.Contains("epart", StringComparison.OrdinalIgnoreCase))
        {
            label = packageId switch
            {
                "fashion_retail_namshi" => "Stylenlook",
                "electronics_retail_virgin" => "Electronicae",
                "consulting_primeinvest" => "TaxoFinca",
                "jewellery_retail_kiyasha" => "The Jewellery Trend",
                _ => label
            };
        }

        var echo = enqueueAlready ? "" : EpcStorefrontAnimatedLogoEnqueue();
        var markup = packageId switch
        {
            "fashion_retail_namshi" => EpcStorefrontAnimatedLogoFashion(label),
            "electronics_retail_virgin" => EpcStorefrontAnimatedLogoElectronics(label),
            "consulting_primeinvest" => EpcStorefrontAnimatedLogoConsulting(label),
            "jewellery_retail_kiyasha" => EpcStorefrontAnimatedLogoJewellery(label),
            _ => ""
        };
        return (echo, markup);
    }

    public static Dictionary<string, Dictionary<string, string>> EpcMbEmailTemplates() => new()
    {
        ["promo_sale"] = new()
        {
            ["label"] = "Seasonal sale brochure",
            ["subject"] = "{{shop_name}} — Limited-time offers inside",
            ["preview"] = "Exclusive deals on parts & accessories — open to see your savings."
        },
        ["new_arrivals"] = new()
        {
            ["label"] = "New arrivals brochure",
            ["subject"] = "{{shop_name}} — New stock just landed",
            ["preview"] = "Fresh inventory — see what's new in our warehouse."
        },
        ["service_reminder"] = new()
        {
            ["label"] = "Service reminder",
            ["subject"] = "{{shop_name}} — Time for your next service?",
            ["preview"] = "Keep your vehicle running smoothly — book parts or service today."
        },
        ["blank"] = new()
        {
            ["label"] = "Blank HTML brochure",
            ["subject"] = "{{shop_name}} — Message for you",
            ["preview"] = "A message from {{shop_name}}."
        }
    };

    public static Dictionary<string, Dictionary<string, string>> EpcMbWhatsappTemplates() => new()
    {
        ["promo_bilingual"] = new()
        {
            ["label"] = "Promo offer (EN + AR)",
            ["body"] = "Hello {{customer_name}}! 🎉\n\n{{shop_name}} has special offers this week.\nVisit: {{shop_url}}\n\n"
                + "مرحباً {{customer_name}}! عروض خاصة من {{shop_name}} هذا الأسبوع.\n{{shop_url}}"
        },
        ["brochure_share"] = new()
        {
            ["label"] = "Brochure / catalogue share",
            ["body"] = "Hi {{customer_name}},\n\nHere is our latest brochure from {{shop_name}}.\nBrowse: {{shop_url}}\n\n"
                + "مرحباً، إليك أحدث كتالوج من {{shop_name}}.\n{{shop_url}}"
        },
        ["follow_up"] = new()
        {
            ["label"] = "Order follow-up",
            ["body"] = "Hello {{customer_name}},\n\nThank you for shopping with {{shop_name}}. Need anything else? Reply here or visit {{shop_url}}.\n\n"
                + "شكراً لتسوقكم مع {{shop_name}}. للمساعدة ردّوا على هذه الرسالة."
        },
        ["event_invite"] = new()
        {
            ["label"] = "Event / open day invitation",
            ["body"] = "You're invited! {{shop_name}} open day — visit us or shop online: {{shop_url}}\n\n"
                + "دعوة من {{shop_name}} — زورونا أو تسوقوا أونلاين: {{shop_url}}"
        },
        ["blank"] = new()
        {
            ["label"] = "Blank WhatsApp message",
            ["body"] = "Hello {{customer_name}},\n\nMessage from {{shop_name}}.\n{{shop_url}}"
        }
    };

    public static string EpcMbApplyTemplateVars(string text, IReadOnlyDictionary<string, string> vars)
    {
        foreach (var pair in vars)
        {
            text = text.Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.Ordinal);
        }

        return text;
    }

    public static string[] EpcChannelEnsureSchema() => EpcChannelEnsureSchemaTables;

    public static Dictionary<string, Dictionary<string, bool>> EpcCpRegisterPageAssets(
        IReadOnlyList<string> css, IReadOnlyList<string> js,
        Dictionary<string, Dictionary<string, bool>>? existing = null)
    {
        var bag = existing ?? new Dictionary<string, Dictionary<string, bool>>
        {
            ["css"] = new(StringComparer.Ordinal),
            ["js"] = new(StringComparer.Ordinal)
        };
        foreach (var href in css)
        {
            var value = href.Trim();
            if (value.Length > 0)
            {
                bag["css"][value] = true;
            }
        }

        foreach (var src in js)
        {
            var value = src.Trim();
            if (value.Length > 0)
            {
                bag["js"][value] = true;
            }
        }

        return bag;
    }

    public static string EpcCpPageFrameOpen(string extraClass = "")
    {
        var classes = "col-lg-12 epc-cp-page-frame";
        if (extraClass.Length > 0)
        {
            classes += " " + extraClass;
        }

        return "<div class=\"" + H(classes) + "\">";
    }

    public static string EpcCpPageFrameHero(
        string badge, string title, string sub, bool htmlSub,
        IReadOnlyList<(string Label, string Url, string Icon, bool Primary)> actions)
    {
        var sb = new StringBuilder("<div class=\"epc-scp-dashboard__hero epc-cp-page-header\">\n\t<div>\n");
        if (badge.Length > 0)
        {
            sb.Append("\t\t<span class=\"epc-scp-dashboard__badge\">").Append(H(badge)).Append("</span>\n");
        }

        if (title.Length > 0)
        {
            sb.Append("\t\t<h2 class=\"epc-scp-dashboard__title\">").Append(H(title)).Append("</h2>\n");
        }

        if (sub.Length > 0)
        {
            sb.Append("\t\t<p class=\"epc-scp-dashboard__sub\">").Append(htmlSub ? sub : H(sub)).Append("</p>\n");
        }

        sb.Append("\t</div>\n");
        if (actions.Count > 0)
        {
            sb.Append("\t<div class=\"epc-scp-dashboard__hero-actions\">\n");
            foreach (var act in actions)
            {
                var btn = act.Primary ? "btn-primary" : "btn-default";
                sb.Append("\t\t<a class=\"btn btn-sm ").Append(btn).Append("\" href=\"").Append(H(act.Url.Length == 0 ? "#" : act.Url)).Append("\">\n");
                if (act.Icon.Length > 0)
                {
                    sb.Append("\t\t\t<i class=\"fa ").Append(H(act.Icon)).Append("\"></i> ");
                }

                sb.Append(H(act.Label)).Append("\t\t</a>\n");
            }

            sb.Append("\t</div>\n");
        }

        return sb.Append("</div>\n").ToString();
    }

    public static string EpcCpPageFrameClose() => "</div>";

    public static string FilemanagerConfig(string backend, string csrf, string langCookie)
    {
        var lang = FilemanagerLang(langCookie);
        var langUrl = lang == "en" ? "" : "/" + backend + "/lib/elfinder/js/i18n/elfinder." + lang + ".js";
        return "window.EPC_FILEMANAGER=" + JsObject(
            ("connectorUrl", PhpCpConfigScripts.Text("/" + backend + "/lib/elfinder/php/connector.php")),
            ("csrf", PhpCpConfigScripts.Text(csrf)),
            ("lang", PhpCpConfigScripts.Text(lang)),
            ("langUrl", PhpCpConfigScripts.Text(langUrl)),
            ("height", "560")) + ";";
    }

    public static string FilemanagerConfigFailed()
        => "window.EPC_FILEMANAGER=" + JsObject(
            ("connectorUrl", PhpCpConfigScripts.Text("/cp/lib/elfinder/php/connector.php")),
            ("csrf", PhpCpConfigScripts.Text("")),
            ("lang", PhpCpConfigScripts.Text("en")),
            ("langUrl", PhpCpConfigScripts.Text("")),
            ("height", "560"),
            ("error", PhpCpConfigScripts.Text("config_failed"))) + ";";

    public static string FilemanagerLang(string? cookie)
    {
        var lang = Regex.Replace((cookie ?? "").ToLowerInvariant(), "[^a-z_]", "");
        if (lang.Length == 0)
        {
            lang = "en";
        }

        return lang switch
        {
            "ru" => "ru",
            "ar" => "ar",
            "de" => "de",
            "es" => "es",
            "fr" => "fr",
            "zh" or "zh_cn" => "zh_CN",
            "pt" or "pt_br" => "pt_BR",
            _ => "en"
        };
    }

    public static string OrdersItemsEmpty() => PhpCpConfigScripts.Empty("EPC_OI");

    public static string OrdersItemsConfig(
        string backend,
        string lang,
        string csrf,
        long managerId,
        string sortField,
        string sortDir,
        string timeFrom,
        string timeTo,
        IReadOnlyList<string> inProcess,
        Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        return "window.EPC_OI=" + JsObject(
            ("backend", PhpCpConfigScripts.Text(backend)),
            ("lang", PhpCpConfigScripts.Text(lang)),
            ("csrf", PhpCpConfigScripts.Text(csrf)),
            ("managerId", managerId.ToString(CultureInfo.InvariantCulture)),
            ("sortField", PhpCpConfigScripts.Text(sortField)),
            ("sortDir", PhpCpConfigScripts.Text(sortDir)),
            ("timeFrom", PhpCpConfigScripts.Text(timeFrom)),
            ("timeTo", PhpCpConfigScripts.Text(timeTo)),
            ("inProcessStatuses", "[" + string.Join(",", inProcess.Select(PhpCpConfigScripts.Text)) + "]"),
            ("urls", JsObject(
                ("items", PhpCpConfigScripts.Text("/" + backend + "/shop/orders/items")),
                ("setViewed", PhpCpConfigScripts.Text("/" + backend + "/content/shop/order_process/ajax_set_orders_viewed.php")),
                ("userModal", PhpCpConfigScripts.Text("/" + backend + "/content/users/statistics/frontAjax/ajax_loadUserModal.php")),
                ("setItemStatus", PhpCpConfigScripts.Text("/content/shop/protocol/set_order_item_status.php")))),
            ("msg", JsObject(
                ("selectItemsViewed", PhpCpConfigScripts.Text(T(3619))),
                ("setViewedFail", PhpCpConfigScripts.Text(T(3599))),
                ("selectItemsStatus", PhpCpConfigScripts.Text(T(3559))),
                ("setStatusFail", PhpCpConfigScripts.Text(T(3560))),
                ("statusOk", PhpCpConfigScripts.Text(T(3755))),
                ("userModalFail", PhpCpConfigScripts.Text(T(3541))),
                ("selectPlaceholder", PhpCpConfigScripts.Text(T(2094)))))) + ";";
    }

    public static (string Field, string Dir) ParseOrdersItemsSort(string? cookie)
    {
        var field = "id";
        var dir = "desc";
        if (string.IsNullOrEmpty(cookie))
        {
            return (field, dir);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(cookie);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("field", out var f) && !PhpEmpty(f.GetString()))
                {
                    field = f.GetString() ?? field;
                }

                if (doc.RootElement.TryGetProperty("asc_desc", out var d))
                {
                    dir = string.Equals(d.GetString(), "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return (field, dir);
    }

    public static (string From, string To) ParseOrdersItemsFilter(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie))
        {
            return ("", "");
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(cookie);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return ("", "");
            }

            var from = doc.RootElement.TryGetProperty("time_from", out var a) ? a.GetString() ?? "" : "";
            var to = doc.RootElement.TryGetProperty("time_to", out var b) ? b.GetString() ?? "" : "";
            return (from, to);
        }
        catch (System.Text.Json.JsonException)
        {
            return ("", "");
        }
    }

    public sealed class SeoContent
    {
        public object? MainFlag { get; set; }
        public object? Value { get; set; }
        public string? TitleTag { get; set; }
        public string? DescriptionTag { get; set; }
        public string? KeywordsTag { get; set; }
    }

    public sealed record IndustryPortal(
        IReadOnlyDictionary<string, object?>? Settings = null,
        IReadOnlyDictionary<string, object?>? Site = null,
        string? Host = null,
        string? ConfigDomainPath = null);

    private static async Task ServeFilemanagerAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        try
        {
            var csrf = "";
            if (connections.IsConfigured)
            {
                try
                {
                    var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                    await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                    csrf = await PhpCpConfigScripts.AdminCsrfAsync(
                        connection, context.Request.Cookies["admin_session"], context.Request.Cookies["admin_u_id"], context.RequestAborted)
                        .ConfigureAwait(false) ?? "";
                }
                catch (System.Data.Common.DbException)
                {
                    csrf = "";
                }
            }

            await response.WriteAsync(FilemanagerConfig("cp", csrf, context.Request.Cookies["lang_cp"] ?? ""), context.RequestAborted)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            await response.WriteAsync(FilemanagerConfigFailed(), context.RequestAborted).ConfigureAwait(false);
        }
    }

    private static async Task ServeOrdersItemsAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        string? csrf = null;
        var statuses = new List<string>();
        if (connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                csrf = await PhpCpConfigScripts.AdminCsrfAsync(
                    connection, context.Request.Cookies["admin_session"], context.Request.Cookies["admin_u_id"], context.RequestAborted)
                    .ConfigureAwait(false);
                if (csrf is not null)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = ErpDb.Positional(
                        "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` != 0 AND `for_finish` != 1 AND `for_created` != 1");
                    await using var reader = await command.ExecuteReaderAsync(context.RequestAborted).ConfigureAwait(false);
                    while (await reader.ReadAsync(context.RequestAborted).ConfigureAwait(false))
                    {
                        statuses.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "");
                    }
                }
            }
            catch (System.Data.Common.DbException)
            {
                csrf = null;
            }
        }

        if (csrf is null)
        {
            await response.WriteAsync(OrdersItemsEmpty(), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var userId = StorefrontPhpInt.Cast(context.Request.Cookies["admin_u_id"] ?? "");
        var sort = ParseOrdersItemsSort(context.Request.Cookies["orders_items_sort"]);
        var filter = ParseOrdersItemsFilter(context.Request.Cookies["orders_items_filter"]);
        await response.WriteAsync(
            OrdersItemsConfig("cp", "en", csrf, userId, sort.Field, sort.Dir, filter.From, filter.To, statuses),
            context.RequestAborted).ConfigureAwait(false);
    }

    private static string FooterHref(string? lang, string? path)
    {
        path ??= "";
        if (path.Length > 0 && path[0] == '/')
        {
            return H((lang ?? "").TrimEnd('/') + path);
        }

        return H(path);
    }

    private static string StoreName(IndustryPortal portal, string fallback, bool tradeFirst)
    {
        if (tradeFirst && Nested(portal.Settings, "contact", "trade_name") is { } trade && PhpFilled(trade))
        {
            return trade.Trim();
        }

        if (Str(portal.Settings, "system_name") is { } system && PhpFilled(system))
        {
            return system.Trim();
        }

        if (Str(portal.Site, "system_name") is { } site && PhpFilled(site))
        {
            return site.Trim();
        }

        return fallback;
    }

    private static string Tagline(IndustryPortal portal, string fallback)
        => Str(portal.Settings, "tagline") is { } tag && PhpFilled(tag) ? tag.Trim() : fallback;

    private static string PublicUrl(IndustryPortal portal)
    {
        if (Str(portal.Settings, "domain_path") is { } domain && PhpFilled(domain))
        {
            return domain.TrimEnd('/') + "/";
        }

        if (PhpFilled(portal.ConfigDomainPath))
        {
            return portal.ConfigDomainPath!.TrimEnd('/') + "/";
        }

        return !string.IsNullOrEmpty(portal.Host) ? "https://" + portal.Host + "/" : "/";
    }

    private static void ApplySeo(
        SeoContent? content, string name, string tagline, string mainTitleSuffix, string mainDesc,
        string mainKeys, string pageKeys, string fallbackPage)
    {
        if (content is null)
        {
            return;
        }

        if (!PhpEmpty(content.MainFlag))
        {
            content.TitleTag = name + mainTitleSuffix;
            content.DescriptionTag = name + mainDesc.Replace("{tagline}", tagline, StringComparison.Ordinal);
            content.KeywordsTag = mainKeys.Replace("{name}", name, StringComparison.Ordinal);
            return;
        }

        var page = Regex.Replace(Convert.ToString(content.Value, CultureInfo.InvariantCulture) ?? "", "<[^>]*>", "").Trim();
        if (page.Length == 0)
        {
            page = fallbackPage;
        }

        foreach (var needle in new[] { "epartscart", "eParts Cart", "Autoparts", "auto parts", "autoparts", "spare parts", "Docpart" })
        {
            if (page.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                page = fallbackPage;
                break;
            }
        }

        content.TitleTag = page + " — " + name;
        content.DescriptionTag = page + " at " + name + ". " + tagline;
        content.KeywordsTag = pageKeys.Replace("{name}", name, StringComparison.Ordinal);
    }

    private static string PatchTemplate(string html, string title, string desc, string keys)
    {
        var titleEsc = H(title);
        var descEsc = H(desc);
        var keysEsc = H(keys);
        html = new Regex("<title>[^<]*</title>", RegexOptions.IgnoreCase).Replace(html, "<title>" + titleEsc + "</title>", 1);
        html = new Regex("<meta name=\"keywords\" content=\"[^\"]*\"[^>]*>", RegexOptions.IgnoreCase)
            .Replace(html, "<meta name=\"keywords\" content=\"" + keysEsc + "\">", 1);
        html = new Regex("<meta name=\"description\" content=\"[^\"]*\"[^>]*>", RegexOptions.IgnoreCase)
            .Replace(html, "<meta name=\"description\" content=\"" + descEsc + "\">", 1);
        return html;
    }

    private static string Scrub(string html, string store, string autoTitle, string autoLower, string spare)
    {
        html = ReplaceI(html, "eParts Cart (Autoparts)", store);
        html = ReplaceI(html, "eParts Cart", store);
        html = ReplaceI(html, "(Autoparts)", "");
        html = ReplaceI(html, "Autoparts", autoTitle);
        html = ReplaceI(html, "auto parts", autoLower);
        html = ReplaceI(html, "autoparts", autoLower);
        return ReplaceI(html, "spare parts", spare);
    }

    private static string LogoSpan(string kind, string viewBox, string inner, string label)
        => "<span class=\"epc-sf-logo epc-sf-logo--" + kind + "\" aria-label=\"" + H(label) + "\">\n"
            + "\t<svg class=\"epc-sf-logo__mark\" viewBox=\"" + viewBox + "\" xmlns=\"http://www.w3.org/2000/svg\" aria-hidden=\"true\">\n"
            + "\t\t" + inner + "\n\t</svg>\n"
            + "\t<span class=\"epc-sf-logo__text\">" + H(label) + "</span>\n</span>\n\t";

    private static string JsObject(params (string Key, string Json)[] pairs)
        => "{" + string.Join(",", pairs.Select(p => PhpCpConfigScripts.Text(p.Key) + ":" + p.Json)) + "}";

    private static string ReplaceI(string hay, string needle, string replacement)
    {
        var builder = new StringBuilder();
        var i = 0;
        while (true)
        {
            var at = hay.IndexOf(needle, i, StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                builder.Append(hay, i, hay.Length - i);
                return builder.ToString();
            }

            builder.Append(hay, i, at - i);
            builder.Append(replacement);
            i = at + needle.Length;
        }
    }

    private static bool PhpEmpty(object? value)
        => value is null
            || value is false
            || value is 0 or 0L or 0d
            || value is string s && (s.Length == 0 || s == "0");

    private static bool PhpFilled(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    private static string? Str(IReadOnlyDictionary<string, object?>? d, string key)
        => d is not null && d.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private static string? Nested(IReadOnlyDictionary<string, object?>? d, string a, string b)
    {
        if (d is null || !d.TryGetValue(a, out var inner))
        {
            return null;
        }

        return inner is IReadOnlyDictionary<string, object?> map ? Str(map, b)
            : inner is IDictionary<string, object?> map2 ? Str(new Dictionary<string, object?>(map2), b)
            : inner is IDictionary<string, string> map3 && map3.TryGetValue(b, out var s) ? s
            : null;
    }

    private static string UcFirst(string value)
        => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string H(string? value) => StorefrontTinyPages.HtmlSpecialChars(value);
}
