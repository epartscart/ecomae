using System.Net.Mail;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hive public platform marketing pages.
/// PHP identifiers kept for the inventory: <c>epc_ecomae_platform_get_industry_groups</c>,
/// <c>epc_ecomae_platform_render_page</c>, <c>epc_ecomae_platform_render_inner</c>,
/// <c>epc_ecomae_platform_page_home</c>, <c>epc_ecomae_platform_ecosystem_model_section</c>,
/// <c>epc_ecomae_platform_page_platform</c>, <c>epc_ecomae_platform_page_industries</c>,
/// <c>scoreText</c>, <c>filterSubs</c>, <c>epc_ecomae_platform_page_industry</c>,
/// <c>epc_ecomae_platform_page_pricing</c>, <c>epc_ecomae_platform_page_demo</c>,
/// <c>epc_ecomae_platform_page_contact</c>, <c>epc_ecomae_platform_page_about</c>,
/// <c>epc_ecomae_platform_page_platform_guides</c>, <c>epc_ecomae_platform_page_capabilities</c>,
/// <c>epc_ecomae_platform_page_business_continuity</c>,
/// <c>epc_ecomae_platform_page_customer_results</c>,
/// <c>epc_ecomae_platform_page_auto_price_ai</c>, <c>epc_ecomae_platform_page_faq</c>,
/// <c>epc_ecomae_platform_page_api_documentation</c>,
/// <c>epc_ecomae_platform_page_api_services</c>.
/// Path: <c>content/general_pages/epc_ecomae_platform_pages.php</c>.
/// GET never mints a session cookie. Leftover unique portal / platform-data
/// parents stay unnamed. Public marketing links stay on ecomae.com; Super CP
/// href uses the injected backend directory only.
/// </summary>
public static class PhpPlanQ1Hive
{
    public const string PlatformPagesPath = "content/general_pages/epc_ecomae_platform_pages.php";
    public const string DefaultBaseUrl = "https://www.ecomae.com/";
    public const string DefaultHomeInner = "<section class=\"epm-hub-section\" aria-label=\"ECOM AE\"></section>";
    private const string WaText = "Hello ECOM AE — I would like to discuss platform rental or a demo.";

    public static string BackendDir { get; set; } = "cp";
    public static string RequestMethod { get; set; } = "GET";
    public static Dictionary<string, string> Post { get; set; } = new(StringComparer.Ordinal);
    public static Func<IReadOnlyList<Dictionary<string, object?>>>? IndustryMarketingGrouped { get; set; }
    public static Func<IReadOnlyDictionary<string, Dictionary<string, object?>>>? IndustryMarketing { get; set; }
    public static Func<string, Dictionary<string, object?>?>? IndustryFull { get; set; }
    public static Func<string>? HomeInner { get; set; }
    public static Func<string>? FaqPage { get; set; }
    public static Func<string>? BrochureHtml { get; set; }
    public static Func<string>? BrochureCpHtml { get; set; }
    public static Func<string, string>? LayoutOpen { get; set; }
    public static Func<string>? LayoutClose { get; set; }
    public static Func<string, string, string>? HeadHtml { get; set; }
    public static Func<string, string>? PageInner { get; set; }
    public static int DemoDays { get; set; } = 3;

    public static void Reset()
    {
        BackendDir = "cp";
        RequestMethod = "GET";
        Post = new Dictionary<string, string>(StringComparer.Ordinal);
        IndustryMarketingGrouped = null;
        IndustryMarketing = null;
        IndustryFull = null;
        HomeInner = null;
        FaqPage = null;
        BrochureHtml = null;
        BrochureCpHtml = null;
        LayoutOpen = null;
        LayoutClose = null;
        HeadHtml = null;
        PageInner = null;
        DemoDays = 3;
    }

    public static string PlatformBaseUrl() => DefaultBaseUrl;

    public static string PlatformSuperCpUrl()
        => "https://www.ecomae.com/" + (BackendDir == "" ? "cp" : BackendDir);

    public static IReadOnlyList<Dictionary<string, object?>> EpcEcomaePlatformGetIndustryGroups()
    {
        IReadOnlyList<Dictionary<string, object?>>? groups = null;
        if (IndustryMarketingGrouped is not null)
        {
            try
            {
                groups = IndustryMarketingGrouped();
            }
            catch
            {
                groups = null;
            }
        }

        if (groups is null || groups.Count == 0)
        {
            return FallbackGroups();
        }

        foreach (var grp in groups)
        {
            if (grp.TryGetValue("industries", out var inds) && inds is System.Collections.ICollection col && col.Count > 0)
            {
                return groups;
            }
        }

        return FallbackGroups();
    }

    public static string EpcEcomaePlatformRenderPage(string page, Dictionary<string, object?>? parameters = null, string mode = "full")
    {
        if (page == "brochure")
        {
            return BrochureHtml?.Invoke() ?? "";
        }

        if (page == "brochure_cp")
        {
            return BrochureCpHtml?.Invoke() ?? "";
        }

        var inner = EpcEcomaePlatformRenderInner(page, parameters);
        if (mode == "inner")
        {
            return inner;
        }

        var active = page == "industry" ? "industries" : page;
        active = active switch
        {
            "customer-results" => "customer_results",
            "platform-guides" => "platform_guides",
            "api-documentation" => "api_documentation",
            "auto-price-ai" => "auto_price_ai",
            "api-services" or "catalog-api" or "price-pro-api" => "api_services",
            _ => active
        };
        var head = HeadHtml?.Invoke(page, active) ?? "";
        var open = LayoutOpen?.Invoke(active) ?? "";
        var close = LayoutClose?.Invoke() ?? "";
        return head + open + inner + close;
    }

    public static string EpcEcomaePlatformRenderInner(string page, Dictionary<string, object?>? parameters = null)
    {
        page = Regex.Replace(page ?? "", "[^a-z0-9_]", "");
        if (page == "" || page == "home")
        {
            return EpcEcomaePlatformPageHome();
        }

        return page switch
        {
            "platform" => EpcEcomaePlatformPagePlatform(),
            "industries" => EpcEcomaePlatformPageIndustries(),
            "industry" => EpcEcomaePlatformPageIndustry(parameters ?? EmptyParams()),
            "pricing" => EpcEcomaePlatformPagePricing(),
            "demo" => EpcEcomaePlatformPageDemo(),
            "contact" => EpcEcomaePlatformPageContact(),
            "about" => EpcEcomaePlatformPageAbout(),
            "platform_guides" => EpcEcomaePlatformPagePlatformGuides(),
            "capabilities" => EpcEcomaePlatformPageCapabilities(),
            "business_continuity" => EpcEcomaePlatformPageBusinessContinuity(),
            "customer_results" => EpcEcomaePlatformPageCustomerResults(),
            "auto_price_ai" => EpcEcomaePlatformPageAutoPriceAi(),
            "faq" => EpcEcomaePlatformPageFaq(),
            "api_documentation" => EpcEcomaePlatformPageApiDocumentation(),
            "api_services" => EpcEcomaePlatformPageApiServices(parameters ?? EmptyParams()),
            _ => EpcEcomaePlatformPageHome()
        };
    }

    public static string EpcEcomaePlatformPageHome()
        => HomeInner?.Invoke() ?? DefaultHomeInner;

    public static string EpcEcomaePlatformEcosystemModelSection()
        => PhpPlanQ1HivePages.EcosystemModelSectionTpl;

    public static string EpcEcomaePlatformPagePlatform()
        => PageInner?.Invoke("platform") ?? "<div class=\"epm-wrap\"><h1>Everything in the ECOM AE Blockchain BOS — by area</h1></div>";

    public static string EpcEcomaePlatformPageIndustries()
        => PageInner?.Invoke("industries") ?? "<div class=\"epm-ind-page\"></div>";

    public static int scoreText(string kw, string q)
    {
        if (q == "")
        {
            return 1;
        }

        var words = Regex.Split(q, @"\s+");
        var score = 0;
        foreach (var word in words)
        {
            if (word != "" && kw.Contains(word, StringComparison.Ordinal))
            {
                score++;
            }
        }

        return score;
    }

    public static Dictionary<string, object?> filterSubs(IReadOnlyList<Dictionary<string, string>> items, string q)
    {
        var visible = new List<string>();
        foreach (var item in items)
        {
            var kw = item.TryGetValue("kw", out var k) ? k : "";
            var ok = q == "" || scoreText(kw, q) > 0;
            if (ok)
            {
                visible.Add(item.TryGetValue("label", out var label) ? label : "");
            }
        }

        var n = visible.Count;
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["visible"] = visible,
            ["count"] = n,
            ["label"] = n + " page" + (n == 1 ? "" : "s")
        };
    }

    public static string EpcEcomaePlatformPageIndustry(Dictionary<string, object?> parameters)
    {
        var code = Str(parameters.TryGetValue("code", out var c) ? c : "");
        var ind = IndustryFull?.Invoke(code);
        if (ind is null)
        {
            return PhpPlanQ1HivePages.Fill(PhpPlanQ1HivePages.IndustryMissingTpl, PlatformBaseUrl());
        }

        return PageInner?.Invoke("industry") ?? "<div class=\"epm-wrap epm-industry-hero\"></div>";
    }

    public static string EpcEcomaePlatformPagePricing()
        => PageInner?.Invoke("pricing") ?? "<div class=\"epm-wrap\"><h1>Platform rental — per client tenant</h1></div>";

    public static string EpcEcomaePlatformPageDemo()
        => PageInner?.Invoke("demo") ?? "<div class=\"epm-wrap epm-layla-hero\"></div>";

    public static string EpcEcomaePlatformPageContact()
    {
        Dictionary<string, object?>? flash = null;
        if (RequestMethod == "POST" && !PhpEmpty(Post.TryGetValue("epc_contact_request", out var flag) ? flag : null))
        {
            var name = (Post.TryGetValue("contact_name", out var n) ? n : "").Trim();
            var email = (Post.TryGetValue("contact_email", out var e) ? e : "").Trim();
            if (name != "" && IsValidEmail(email))
            {
                flash = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["message"] = "Thank you — we respond within one business day (GST)."
                };
            }
            else
            {
                flash = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = false,
                    ["message"] = "Please enter your name and a valid email."
                };
            }
        }

        var flashHtml = "";
        if (flash is not null)
        {
            var ok = flash.TryGetValue("ok", out var ov) && ov is true;
            flashHtml = "\t\t<div class=\"epm-alert " + (ok ? "ok" : "err") + "\">"
                + Ent(flash["message"]) + "</div>\n";
        }

        return PhpPlanQ1HivePages.ContactTpl
            .Replace("{SUPER}", PlatformSuperCpUrl(), StringComparison.Ordinal)
            .Replace("{BASE}", PlatformBaseUrl(), StringComparison.Ordinal)
            .Replace("{FLASH}", flashHtml, StringComparison.Ordinal);
    }

    public static string EpcEcomaePlatformPageAbout()
        => PhpPlanQ1HivePages.Fill(PhpPlanQ1HivePages.AboutTpl, PlatformBaseUrl());

    public static string EpcEcomaePlatformPagePlatformGuides()
        => PageInner?.Invoke("platform_guides") ?? "<div class=\"epm-wrap\"><h1>What you get in Super CP — by area</h1></div>";

    public static string EpcEcomaePlatformPageCapabilities()
        => PageInner?.Invoke("capabilities") ?? "<div class=\"epm-wrap\"><h1>Super CP — full capability catalog</h1></div>";

    public static string EpcEcomaePlatformPageBusinessContinuity()
        => PageInner?.Invoke("business_continuity") ?? "<div class=\"epm-wrap\"><h1>Cloud + backup continuity</h1></div>";

    public static string EpcEcomaePlatformPageCustomerResults()
        => PageInner?.Invoke("customer_results") ?? "<div class=\"epm-wrap\"><h1>Customer results across live tenants</h1></div>";

    public static string EpcEcomaePlatformPageAutoPriceAi()
        => PageInner?.Invoke("auto_price_ai") ?? "<div class=\"epm-wrap\"><h1>Auto Price AI — discover, price, and list across markets</h1></div>";

    public static string EpcEcomaePlatformPageFaq()
        => FaqPage?.Invoke() ?? PhpPlanQ1Faq.EpcEcomaeFaqRenderPage();

    public static string EpcEcomaePlatformPageApiDocumentation()
        => PhpPlanQ1HivePages.Fill(PhpPlanQ1HivePages.ApiDocumentationTpl, PlatformBaseUrl(), DemoDays.ToString());

    public static string EpcEcomaePlatformPageApiServices(Dictionary<string, object?>? parameters = null)
    {
        var focus = Str(parameters is not null && parameters.TryGetValue("focus", out var f) ? f : "overview");
        var html = PhpPlanQ1HivePages.Fill(PhpPlanQ1HivePages.ApiServicesTpl, PlatformBaseUrl());
        if (html.EndsWith('\t'))
        {
            html = html[..^1];
        }

        if (focus == "catalog")
        {
            return html + "<script>document.addEventListener('DOMContentLoaded',function(){var el=document.getElementById('catalog-api');if(el)el.scrollIntoView({behavior:'smooth'});});</script>\n\t\t";
        }

        if (focus == "price_pro")
        {
            return html + "<script>document.addEventListener('DOMContentLoaded',function(){var el=document.getElementById('price-pro-api');if(el)el.scrollIntoView({behavior:'smooth'});});</script>\n\t\t";
        }

        return html + "\t";
    }

    private static List<Dictionary<string, object?>> FallbackGroups()
    {
        var marketing = IndustryMarketing?.Invoke() ?? new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        return
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = "all",
                ["name"] = "Industry solutions",
                ["industries"] = marketing,
                ["placeholders"] = new List<object>()
            }
        ];
    }

    private static Dictionary<string, object?> EmptyParams()
        => new(StringComparer.Ordinal);

    private static string Str(object? value)
        => Convert.ToString(value) ?? "";

    private static string Ent(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    private static bool PhpEmpty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        var s = Convert.ToString(value) ?? "";
        return s == "" || s == "0" || s == "0.0";
    }

    private static bool IsValidEmail(string email)
    {
        if (email == "" || email.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(email);
            return email.Contains('@', StringComparison.Ordinal) && email.Contains('.', StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    // WhatsApp text stays encoded the PHP rawurlencode way in the contact template.
    internal static string WhatsAppQuery() => Uri.EscapeDataString(WaText);
}
