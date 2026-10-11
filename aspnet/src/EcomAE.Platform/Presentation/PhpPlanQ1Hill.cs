using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hill public Free Tools catalog and page.
/// PHP identifiers kept for the inventory: <c>epc_free_tools_h</c>,
/// <c>epc_free_tools_countries</c>, <c>epc_free_tools_cit_rate</c>,
/// <c>epc_free_tools_catalog</c>, <c>epc_free_tools_seo</c>,
/// <c>epc_free_tools_jsonld</c>, <c>epc_free_tools_site_base</c>,
/// <c>epc_free_tools_faq_html</c>, <c>epc_free_tools_round</c>,
/// <c>epc_free_tools_num</c>, <c>epc_free_tools_authority</c>,
/// <c>epc_free_tools_parse_csv</c>, <c>epc_free_tools_finding</c>,
/// <c>epc_free_tools_compute</c>, <c>epc_free_tools_vat_from_csv</c>,
/// <c>epc_free_tools_compute_vat</c>, <c>epc_free_tools_ct_from_csv</c>,
/// <c>epc_free_tools_compute_ct</c>, <c>epc_free_tools_compute_payroll</c>,
/// <c>epc_free_tools_ifrs_from_csv</c>, <c>epc_free_tools_compute_ifrs</c>,
/// <c>epc_free_tools_einvoice_scheme</c>, <c>epc_free_tools_compute_einvoice</c>,
/// <c>epc_free_tools_compute_extreport</c>, <c>epc_free_tools_compute_customs</c>,
/// <c>epc_free_tools_compute_insurance</c>, <c>epc_free_tools_compute_docexpiry</c>,
/// <c>epc_free_tools_compute_valuation</c>, <c>epc_free_tools_compute_finmodel</c>,
/// <c>epc_free_tools_compute_taxkit</c>, <c>epc_free_tools_compute_hrcompliance</c>,
/// <c>epc_free_tools_compute_workflow</c>, <c>epc_free_tools_db</c>,
/// <c>epc_free_tools_ensure_schema</c>, <c>epc_free_tools_send_mail</c>,
/// <c>epc_free_tools_mail_shell</c>, <c>epc_free_tools_register</c>,
/// <c>epc_free_tools_login</c>, <c>epc_free_tools_request_reset</c>,
/// <c>epc_free_tools_confirm_reset</c>, <c>epc_free_tools_account_by_token</c>,
/// <c>epc_free_tools_touch_account</c>, <c>epc_free_tools_save</c>,
/// <c>epc_free_tools_list_saves</c>, <c>epc_free_tools_disabled_map</c>,
/// <c>epc_free_tools_is_active</c>, <c>epc_free_tools_set_active</c>,
/// <c>epc_free_tools_request_delete</c>, <c>epc_free_tools_confirm_delete</c>,
/// <c>epc_free_tools_usage_stats</c>, <c>epc_ecomae_platform_page_free_tools</c>,
/// <c>epc_free_tools_render_hub</c>, <c>epc_free_tools_guide</c>,
/// <c>epc_free_tools_preview</c>, <c>epc_free_tools_preview_html</c>,
/// <c>epc_free_tools_guide_html</c>, <c>epc_free_tools_render_tool</c>,
/// <c>epc_free_tools_fields_html</c>, <c>epc_free_tools_einvoice_line_html</c>,
/// <c>epc_free_tools_styles</c>, <c>epc_free_tools_scripts</c>.
/// Path: <c>content/general_pages/epc_ecomae_free_tools.php</c>.
/// GET never mints a session cookie. Leftover unique portal / console parents
/// stay unnamed. Compute already lives in <c>FreeToolsCompute</c>; this slice
/// ports the public catalog and page.
/// </summary>
public static class PhpPlanQ1Hill
{
    public const string FreeToolsPath = "content/general_pages/epc_ecomae_free_tools.php";
    public const string DefaultPageBase = "https://www.ecomae.com/";
    public const string DefaultSite = "https://www.ecomae.com";
    private static readonly string[] CountryCodes = ["AE", "SA", "QA", "OM", "BH", "KW", "PK", "IN", "GB", "US", "EG", "BD", "NG", "ZA", "TR"];
    private static readonly string[] GalleryKeys = ["vat", "ct", "extreport", "payroll", "valuation", "taxkit"];
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Func<string>? PlatformBaseUrl { get; set; }
    public static string HttpHost { get; set; } = "";
    public static string Https { get; set; } = "";
    public static bool HasDb { get; set; }
    public static Dictionary<string, bool> DisabledTools { get; set; } = new(StringComparer.Ordinal);

    public static void Reset()
    {
        PlatformBaseUrl = null;
        HttpHost = "";
        Https = "";
        HasDb = false;
        DisabledTools = new Dictionary<string, bool>(StringComparer.Ordinal);
    }

    public static string EpcFreeToolsH(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static Dictionary<string, string> EpcFreeToolsCountries()
    {
        var outMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var code in CountryCodes)
        {
            outMap[code] = Str(FreeToolsEngines.CountryProfile(code)["name"]);
        }

        outMap["XX"] = "Other / generic";
        return outMap;
    }

    public static Dictionary<string, object?> EpcFreeToolsCitRate(string country)
    {
        var c = country.Trim().ToUpperInvariant();
        var map = new Dictionary<string, (double Rate, double Threshold, string Note)>(StringComparer.Ordinal)
        {
            ["AE"] = (9.0, 375000.0, "0% on first AED 375,000; 9% above (UAE CT)."),
            ["SA"] = (20.0, 0.0, "20% corporate income tax (Zakat may apply to GCC-owned)."),
            ["QA"] = (10.0, 0.0, "10% standard corporate tax."),
            ["OM"] = (15.0, 0.0, "15% standard corporate tax."),
            ["BH"] = (0.0, 0.0, "No general corporate income tax."),
            ["KW"] = (15.0, 0.0, "15% on foreign corporate bodies."),
            ["PK"] = (29.0, 0.0, "29% corporate tax (standard)."),
            ["IN"] = (25.0, 0.0, "~25% (domestic, turnover-linked); 22% under new regime."),
            ["GB"] = (25.0, 50000.0, "19% small-profits up to GBP 50k, 25% main rate."),
            ["US"] = (21.0, 0.0, "21% federal corporate tax (states add their own)."),
            ["EG"] = (22.5, 0.0, "22.5% standard corporate tax."),
            ["BD"] = (27.5, 0.0, "27.5% non-listed company rate."),
            ["NG"] = (30.0, 25000000.0, "0% small co (<NGN 25m), 30% large."),
            ["ZA"] = (27.0, 0.0, "27% corporate income tax."),
            ["TR"] = (25.0, 0.0, "25% corporate tax."),
        };
        if (!map.TryGetValue(c, out var row))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["rate"] = 0,
                ["threshold"] = 0,
                ["note"] = "No corporate tax reference for this country \u2014 set 0%. Confirm with a local advisor."
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rate"] = PhpNumber(row.Rate),
            ["threshold"] = PhpNumber(row.Threshold),
            ["note"] = row.Note
        };
    }

    public static Dictionary<string, Dictionary<string, string>> EpcFreeToolsCatalog()
        => PhpPlanQ1HillData.Catalog();

    public static Dictionary<string, object?> EpcFreeToolsSeo(string tool = "")
        => PhpPlanQ1HillData.Seo(tool);

    public static string EpcFreeToolsSiteBase(string fallback = "")
    {
        if (PlatformBaseUrl is not null)
        {
            var u = PlatformBaseUrl();
            if (u != "")
            {
                return u;
            }
        }

        if (HttpHost != "")
        {
            _ = Https;
            return "https://" + HttpHost;
        }

        return fallback != "" ? fallback : DefaultSite;
    }

    public static string EpcFreeToolsJsonld(string baseUrl, string tool = "")
    {
        var site = EpcFreeToolsSiteBase(baseUrl).TrimEnd('/');
        var seo = EpcFreeToolsSeo(tool);
        var graph = new List<object?>();
        var crumbs = new List<object?>
        {
            Item(1, "Home", site + "/"),
            Item(2, "Free Tools", site + "/platform/free-tools")
        };
        var catalog = EpcFreeToolsCatalog();
        if (tool == "")
        {
            var items = new List<object?>();
            var i = 0;
            foreach (var (k, meta) in catalog)
            {
                i++;
                items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["@type"] = "ListItem",
                    ["position"] = i,
                    ["name"] = meta["name"],
                    ["description"] = meta["blurb"],
                    ["url"] = site + "/platform/free-tools/" + k
                });
            }

            graph.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "WebApplication",
                ["name"] = "ECOM AE Free Business Tools",
                ["url"] = site + "/platform/free-tools",
                ["applicationCategory"] = "BusinessApplication",
                ["operatingSystem"] = "Web",
                ["description"] = seo["description"],
                ["offers"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "USD"
                }
            });
            graph.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "ItemList",
                ["name"] = "Free business tools",
                ["itemListElement"] = items
            });
        }
        else
        {
            var name = catalog.TryGetValue(tool, out var meta) ? meta["name"] : Str(seo["h1"]);
            graph.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "SoftwareApplication",
                ["name"] = name + " \u2014 Free Tool",
                ["url"] = site + "/platform/free-tools/" + tool,
                ["applicationCategory"] = "BusinessApplication",
                ["operatingSystem"] = "Web",
                ["description"] = seo["description"],
                ["offers"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["@type"] = "Offer",
                    ["price"] = "0",
                    ["priceCurrency"] = "USD"
                }
            });
            crumbs.Add(Item(3, name, site + "/platform/free-tools/" + tool));
        }

        graph.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = crumbs
        });
        if (seo["faq"] is List<object?> faq && faq.Count > 0)
        {
            var faqEls = new List<object?>();
            foreach (var raw in faq)
            {
                var f = (Dictionary<string, object?>)raw!;
                faqEls.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["@type"] = "Question",
                    ["name"] = f["q"],
                    ["acceptedAnswer"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["@type"] = "Answer",
                        ["text"] = f["a"]
                    }
                });
            }

            graph.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["@type"] = "FAQPage",
                ["mainEntity"] = faqEls
            });
        }

        var doc = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = graph
        };
        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(doc, JsonOpts) + "</script>";
    }

    public static string EpcFreeToolsFaqHtml(string tool = "")
    {
        var seo = EpcFreeToolsSeo(tool);
        if (seo["faq"] is not List<object?> faq || faq.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append("<section class=\"eft-faq\" aria-label=\"Frequently asked questions\"><h2>Frequently asked questions</h2>");
        foreach (var raw in faq)
        {
            var f = (Dictionary<string, object?>)raw!;
            sb.Append("<details class=\"eft-faq__item\"><summary>").Append(EpcFreeToolsH(f["q"])).Append("</summary><div class=\"eft-faq__a\">").Append(EpcFreeToolsH(f["a"])).Append("</div></details>");
        }

        sb.Append("</section>");
        return sb.ToString();
    }

    public static double EpcFreeToolsRound(object? value)
        => Math.Round(ToFloat(value), 2, MidpointRounding.AwayFromZero);

    public static double EpcFreeToolsNum(object? value)
        => FreeToolsCompute.Num(value);

    public static Dictionary<string, object?> EpcFreeToolsAuthority(string country)
    {
        var c = country.Trim().ToUpperInvariant();
        var map = new Dictionary<string, (string A, string V, string T)>(StringComparer.Ordinal)
        {
            ["AE"] = ("Federal Tax Authority (FTA)", "VAT 201", "CT return"),
            ["SA"] = ("Zakat, Tax and Customs Authority (ZATCA)", "VAT return", "Corporate income tax / Zakat"),
            ["QA"] = ("General Tax Authority (GTA)", "VAT return", "Corporate income tax"),
            ["OM"] = ("Oman Tax Authority", "VAT return", "Corporate income tax"),
            ["BH"] = ("National Bureau for Revenue (NBR)", "VAT return", "No general corporate tax"),
            ["KW"] = ("Ministry of Finance", "No VAT", "Corporate income tax (foreign)"),
            ["PK"] = ("Federal Board of Revenue (FBR)", "Sales tax return", "Income tax return"),
            ["IN"] = ("GSTN / Income Tax Dept", "GSTR-3B", "ITR"),
            ["GB"] = ("HM Revenue & Customs (HMRC)", "VAT return (MTD)", "CT600"),
            ["US"] = ("IRS / State authorities", "State sales tax", "Form 1120"),
            ["EG"] = ("Egyptian Tax Authority (ETA)", "VAT return", "Corporate income tax"),
            ["BD"] = ("National Board of Revenue (NBR)", "VAT return (Mushak)", "Income tax return"),
            ["NG"] = ("Federal Inland Revenue Service (FIRS)", "VAT return", "Company income tax"),
            ["ZA"] = ("South African Revenue Service (SARS)", "VAT201", "ITR14"),
            ["TR"] = ("Revenue Administration (GIB)", "KDV beyannamesi", "Kurumlar vergisi"),
        };
        if (!map.TryGetValue(c, out var row))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["authority"] = "Your national tax authority",
                ["vat_return"] = "VAT/GST return",
                ["ct_return"] = "Corporate tax return"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["authority"] = row.A,
            ["vat_return"] = row.V,
            ["ct_return"] = row.T
        };
    }

    public static List<Dictionary<string, string>> EpcFreeToolsParseCsv(string text)
        => FreeToolsCompute.ParseCsvRows(text)
            .Select(row =>
            {
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (k, v) in row)
                {
                    d[Str(k)] = Str(v);
                }

                return d;
            })
            .ToList();

    public static Dictionary<string, object?> EpcFreeToolsFinding(string level, string message)
        => new(StringComparer.Ordinal) { ["level"] = level, ["message"] = message };

    public static PhpArray EpcFreeToolsCompute(string tool, string country, PhpArray input, long now = 0)
        => FreeToolsCompute.Compute(tool, country, input, now);

    public static PhpArray EpcFreeToolsVatFromCsv(string country, string csv)
        => EpcFreeToolsCompute("vat", country, new PhpArray { { "csv", csv } }, 0);

    public static PhpArray EpcFreeToolsComputeVat(string country, PhpArray input)
        => EpcFreeToolsCompute("vat", country, input, 0);

    public static PhpArray EpcFreeToolsCtFromCsv(string country, string csv)
        => EpcFreeToolsCompute("ct", country, new PhpArray { { "csv", csv } }, 0);

    public static PhpArray EpcFreeToolsComputeCt(string country, PhpArray input)
        => EpcFreeToolsCompute("ct", country, input, 0);

    public static PhpArray EpcFreeToolsComputePayroll(string country, PhpArray input)
        => EpcFreeToolsCompute("payroll", country, input, 0);

    public static PhpArray EpcFreeToolsIfrsFromCsv(string country, string csv)
        => EpcFreeToolsCompute("ifrs", country, new PhpArray { { "csv", csv } }, 0);

    public static PhpArray EpcFreeToolsComputeIfrs(string country, PhpArray input)
        => EpcFreeToolsCompute("ifrs", country, input, 0);

    public static (string Scheme, string Network, string Eas, string EndpointLabel) EpcFreeToolsEinvoiceScheme(PhpArray prof)
        => FreeToolsEngines.EinvoiceScheme(prof);

    public static PhpArray EpcFreeToolsComputeEinvoice(string country, PhpArray input, long now = 0)
        => EpcFreeToolsCompute("einvoice", country, input, now);

    public static PhpArray EpcFreeToolsComputeExtreport(string country, PhpArray input)
        => EpcFreeToolsCompute("extreport", country, input, 0);

    public static PhpArray EpcFreeToolsComputeCustoms(string country, PhpArray input)
        => EpcFreeToolsCompute("customs", country, input, 0);

    public static PhpArray EpcFreeToolsComputeInsurance(string country, PhpArray input, long now = 0)
        => EpcFreeToolsCompute("insurance", country, input, now);

    public static PhpArray EpcFreeToolsComputeDocexpiry(string country, PhpArray input, long now = 0)
        => EpcFreeToolsCompute("docexpiry", country, input, now);

    public static PhpArray EpcFreeToolsComputeValuation(string country, PhpArray input)
        => EpcFreeToolsCompute("valuation", country, input, 0);

    public static PhpArray EpcFreeToolsComputeFinmodel(string country, PhpArray input, long now = 0)
        => EpcFreeToolsCompute("finmodel", country, input, now);

    public static PhpArray EpcFreeToolsComputeTaxkit(string country)
        => EpcFreeToolsCompute("taxkit", country, new PhpArray(), 0);

    public static PhpArray EpcFreeToolsComputeHrcompliance(string country, PhpArray input, long now = 0)
        => EpcFreeToolsCompute("hrcompliance", country, input, now);

    public static PhpArray EpcFreeToolsComputeWorkflow(string country, PhpArray input)
        => EpcFreeToolsCompute("workflow", country, input, 0);

    public static object? EpcFreeToolsDb()
        => HasDb ? new object() : null;

    public static void EpcFreeToolsEnsureSchema(object? db)
    {
        _ = db;
    }

    public static bool EpcFreeToolsSendMail(string to, string subject, string html)
    {
        _ = (to, subject, html);
        return false;
    }

    public static string EpcFreeToolsMailShell(string title, string body)
        => "<div>" + EpcFreeToolsH(title) + "</div>" + body;

    public static Dictionary<string, object?> EpcFreeToolsRegister(string email, string company, string country, string password = "")
    {
        _ = (email, company, country, password);
        return Unavailable();
    }

    public static Dictionary<string, object?> EpcFreeToolsLogin(string email, string password)
    {
        _ = (email, password);
        return Unavailable();
    }

    public static Dictionary<string, object?> EpcFreeToolsRequestReset(string email)
    {
        _ = email;
        return Unavailable();
    }

    public static Dictionary<string, object?> EpcFreeToolsConfirmReset(string email, string code, string password)
    {
        _ = (email, code, password);
        return Unavailable();
    }

    public static Dictionary<string, object?>? EpcFreeToolsAccountByToken(string token)
    {
        _ = token;
        return null;
    }

    public static void EpcFreeToolsTouchAccount(int accountId)
        => _ = accountId;

    public static Dictionary<string, object?> EpcFreeToolsSave(int accountId, string tool, string country, string title, Dictionary<string, object?> payload)
    {
        _ = (accountId, tool, country, title, payload);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Service unavailable." };
    }

    public static List<Dictionary<string, object?>> EpcFreeToolsListSaves(int accountId, int limit = 50)
    {
        _ = (accountId, limit);
        return [];
    }

    public static Dictionary<string, bool> EpcFreeToolsDisabledMap()
        => HasDb ? new Dictionary<string, bool>(DisabledTools, StringComparer.Ordinal) : [];

    public static bool EpcFreeToolsIsActive(string tool)
        => !EpcFreeToolsDisabledMap().ContainsKey(tool);

    public static Dictionary<string, bool> EpcFreeToolsSetActive(string tool, bool active)
    {
        if (!HasDb)
        {
            return [];
        }

        if (active)
        {
            DisabledTools.Remove(tool);
        }
        else
        {
            DisabledTools[tool] = true;
        }

        return new Dictionary<string, bool>(DisabledTools, StringComparer.Ordinal);
    }

    public static Dictionary<string, object?> EpcFreeToolsRequestDelete(string token)
    {
        _ = token;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Please sign in first." };
    }

    public static Dictionary<string, object?> EpcFreeToolsConfirmDelete(string token, string code)
    {
        _ = (token, code);
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Please sign in first." };
    }

    public static Dictionary<string, object?> EpcFreeToolsUsageStats()
        => new(StringComparer.Ordinal) { ["ok"] = false };

    public static string EpcEcomaePlatformPageFreeTools(Dictionary<string, object?>? parameters = null)
    {
        var baseUrl = PlatformBaseUrl is not null ? PlatformBaseUrl() : DefaultPageBase;
        var catalog = EpcFreeToolsCatalog();
        var tool = "";
        if (parameters is not null && parameters.TryGetValue("tool", out var raw) && raw is not null)
        {
            tool = Regex.Replace(Str(raw), "[^a-z]", "");
        }

        var body = tool != "" && catalog.ContainsKey(tool)
            ? EpcFreeToolsRenderTool(tool, catalog[tool], baseUrl)
            : EpcFreeToolsRenderHub(catalog, baseUrl);
        return EpcFreeToolsStyles() + body + EpcFreeToolsScripts(baseUrl);
    }

    public static string EpcFreeToolsRenderHub(Dictionary<string, Dictionary<string, string>> catalog, string baseUrl)
    {
        var disabled = EpcFreeToolsDisabledMap();
        var sb = new StringBuilder();
        sb.Append("<div class=\"epm-wrap eft-wrap\">\n");
        sb.Append("\t<div class=\"eft-hero\">\n");
        sb.Append("\t\t<div class=\"epm-badge\"><i class=\"fa fa-globe\"></i> Worldwide &middot; free &middot; no payment</div>\n");
        sb.Append("\t\t<h1>Free business tools for your company</h1>\n");
        sb.Append("\t\t<p class=\"lead\"><strong>Worldwide tools that work to your registered country's rules.</strong> Run VAT/GST &amp; corporate tax returns, IFRS financials, payroll &amp; gratuity, electronic invoicing (UAE FTA, Saudi ZATCA, EU Peppol&hellip;), customs &amp; landed cost, insurance, document expiry, business valuation, financial models and approval workflows — all free. Every tool auto-localises to your currency, tax rate, e-invoice scheme and labour law from the country you register with.</p>\n");
        sb.Append("\t\t<p class=\"eft-rules\"><i class=\"fa fa-check-circle\"></i> <strong>Register once</strong> with your email &mdash; that single free account unlocks <strong>every tool</strong>. One email = one account (you can't register the same email twice; we just sign you back in). Many tools accept a <strong>CSV upload</strong> for instant compliance checks and reports. <i class=\"fa fa-book\"></i> Open any tool to read its <strong>guide</strong> (what it does, what you get and the CSV format) before you register.</p>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t<div class=\"eft-grid\">\n");
        var first = true;
        foreach (var (key, t) in catalog)
        {
            var off = disabled.ContainsKey(key);
            if (first)
            {
                sb.Append("\t\t");
                first = false;
            }

            sb.Append("\t\t<a class=\"eft-card");
            if (off)
            {
                sb.Append(" eft-card--off");
            }

            sb.Append("\" href=\"").Append(EpcFreeToolsH(baseUrl)).Append("platform/free-tools/").Append(EpcFreeToolsH(key)).Append("\">\n");
            sb.Append("\t\t\t<span class=\"eft-card__tag\">").Append(EpcFreeToolsH(t["tag"])).Append("</span>\n");
            sb.Append("\t\t\t<i class=\"fa ").Append(EpcFreeToolsH(t["icon"])).Append("\" aria-hidden=\"true\"></i>\n");
            sb.Append("\t\t\t<h3>").Append(EpcFreeToolsH(t["name"])).Append("</h3>\n");
            sb.Append("\t\t\t<p>").Append(EpcFreeToolsH(t["blurb"])).Append("</p>\n");
            sb.Append("\t\t\t");
            if (off)
            {
                sb.Append("<span class=\"eft-card__cta eft-card__cta--off\"><i class=\"fa fa-clock-o\"></i> Temporarily unavailable</span>");
            }
            else
            {
                sb.Append("<span class=\"eft-card__cta\">Open tool <i class=\"fa fa-arrow-right\"></i></span>");
            }

            sb.Append("\t\t</a>\n\t\t");
        }

        sb.Append("\t</div>\n");
        sb.Append("\t<div class=\"eft-gallery\">\n");
        sb.Append("\t\t<h2><i class=\"fa fa-eye\"></i> See what you get — before you register</h2>\n");
        sb.Append("\t\t<p class=\"eft-gallery__lead\">Real sample results from our most popular tools. Register free to run any of them for <strong>your own company</strong>, localised to your country.</p>\n");
        sb.Append("\t\t<div class=\"eft-gallery__grid\">\n");
        var gFirst = true;
        foreach (var gk in GalleryKeys)
        {
            if (!catalog.TryGetValue(gk, out var meta))
            {
                continue;
            }

            if (gFirst)
            {
                sb.Append("\t\t\t");
                gFirst = false;
            }

            sb.Append("\t\t\t<a class=\"eft-gallery__item\" href=\"").Append(EpcFreeToolsH(baseUrl)).Append("platform/free-tools/").Append(EpcFreeToolsH(gk)).Append("\">");
            sb.Append(EpcFreeToolsPreviewHtml(gk, meta));
            sb.Append("</a>\n\t\t\t");
        }

        sb.Append("\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t").Append(EpcFreeToolsFaqHtml("")).Append("\n");
        sb.Append("\t<div class=\"eft-upsell\">\n");
        sb.Append("\t\t<h3>Ready for the full picture?</h3>\n");
        sb.Append("\t\t<p>These tools are a taste of the ECOM AE Business Operating System — full ERP, commerce, compliance and CRM, with your data carried across when you upgrade.</p>\n");
        sb.Append("\t\t<div class=\"epm-cta\">\n");
        sb.Append("\t\t\t<a class=\"epm-btn epm-btn--primary\" href=\"").Append(EpcFreeToolsH(baseUrl)).Append("platform/demo\"><i class=\"fa fa-play-circle\"></i> Start a demo</a>\n");
        sb.Append("\t\t\t<a class=\"epm-btn epm-btn--ghost\" href=\"").Append(EpcFreeToolsH(baseUrl)).Append("platform/pricing\"><i class=\"fa fa-tags\"></i> See pricing</a>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("</div>\n");
        sb.Append(EpcFreeToolsJsonld(baseUrl, ""));
        sb.Append("\t\t");
        return sb.ToString();
    }

    public static Dictionary<string, object?> EpcFreeToolsGuide(string key)
        => PhpPlanQ1HillData.Guide(key);

    public static Dictionary<string, object?> EpcFreeToolsPreview(string key)
        => PhpPlanQ1HillData.Preview(key);

    public static string EpcFreeToolsPreviewHtml(string key, Dictionary<string, string> meta)
    {
        var pv = EpcFreeToolsPreview(key);
        var head = (Dictionary<string, object?>)pv["head"]!;
        if (Str(head["value"]) == "")
        {
            return "";
        }

        var h = "<div class=\"eft-preview\" id=\"eft_preview\">";
        h += "<div class=\"eft-preview__bar\"><span class=\"eft-preview__dot\"></span><span class=\"eft-preview__dot\"></span><span class=\"eft-preview__dot\"></span><span class=\"eft-preview__title\">" + EpcFreeToolsH(meta["name"]) + " — sample result</span><span class=\"eft-preview__badge\">SAMPLE · UAE example</span></div>";
        h += "<div class=\"eft-preview__body\">";
        h += "<div class=\"eft-preview__head\"><span class=\"eft-preview__hlabel\">" + EpcFreeToolsH(head["label"]) + "</span><span class=\"eft-preview__hvalue\">" + EpcFreeToolsH(head["value"]) + "</span></div>";
        h += "<table class=\"eft-preview__tbl\"><tbody>";
        foreach (var raw in (List<object?>)pv["rows"]!)
        {
            var row = (List<object?>)raw!;
            h += "<tr><td>" + EpcFreeToolsH(row[0]) + "</td><td>" + EpcFreeToolsH(row[1]) + "</td></tr>";
        }

        h += "</tbody></table>";
        if (!PhpEmpty(pv["chip"]))
        {
            h += "<div class=\"eft-preview__chip\"><i class=\"fa fa-check-circle\"></i> " + EpcFreeToolsH(pv["chip"]) + "</div>";
        }

        h += "</div>";
        h += "<div class=\"eft-preview__foot\"><i class=\"fa fa-lock\"></i> Register free to run this for <strong>your own company</strong> &amp; your country&rsquo;s rules.</div>";
        h += "</div>";
        return h;
    }

    public static string EpcFreeToolsGuideHtml(string key, Dictionary<string, string> meta)
    {
        var g = EpcFreeToolsGuide(key);
        if (Str(g["what"]) == "")
        {
            return "";
        }

        var h = "<div class=\"eft-guide\" id=\"eft_guide\">";
        h += "<h3><i class=\"fa fa-book\"></i> Guide: " + EpcFreeToolsH(meta["name"]) + "</h3>";
        h += "<div class=\"eft-guide__grid\">";
        h += "<div class=\"eft-guide__col\"><h4>What it does</h4><p>" + EpcFreeToolsH(g["what"]) + "</p>";
        if (!PhpEmpty(g["who"]))
        {
            h += "<h4>Who it's for</h4><p>" + EpcFreeToolsH(g["who"]) + "</p>";
        }

        h += "</div>";
        h += "<div class=\"eft-guide__col\"><h4>What you get</h4><ul class=\"eft-guide__list\">";
        foreach (var item in (List<object?>)g["get"]!)
        {
            h += "<li><i class=\"fa fa-check\"></i> " + EpcFreeToolsH(item) + "</li>";
        }

        h += "</ul>";
        h += "<h4>How it works</h4><p class=\"eft-guide__how\"><i class=\"fa fa-globe\"></i> " + EpcFreeToolsH(g["how"]) + "</p>";
        if (g.TryGetValue("csv", out var csv) && !PhpEmpty(csv))
        {
            h += "<h4>CSV format</h4><p class=\"eft-guide__csv\"><code>" + EpcFreeToolsH(csv) + "</code></p>";
        }

        h += "</div></div></div>";
        return h;
    }

    public static string EpcFreeToolsRenderTool(string key, Dictionary<string, string> meta, string baseUrl)
    {
        var countries = EpcFreeToolsCountries();
        var active = EpcFreeToolsIsActive(key);
        var seo = EpcFreeToolsSeo(key);
        var sb = new StringBuilder();
        sb.Append("<div class=\"epm-wrap eft-wrap\" data-eft-tool=\"").Append(EpcFreeToolsH(key)).Append('"');
        if (!active)
        {
            sb.Append(" data-eft-inactive=\"1\"");
        }

        sb.Append(">\n");
        sb.Append("\t<a class=\"eft-back\" href=\"").Append(EpcFreeToolsH(baseUrl)).Append("platform/free-tools\"><i class=\"fa fa-arrow-left\"></i> All free tools</a>\n");
        sb.Append("\t\t<div class=\"eft-tool-head\">\n");
        sb.Append("\t\t<i class=\"fa ").Append(EpcFreeToolsH(meta["icon"])).Append("\" aria-hidden=\"true\"></i>\n");
        sb.Append("\t\t<div>\n");
        sb.Append("\t\t\t<h1>").Append(EpcFreeToolsH(meta["name"])).Append(" &mdash; free, worldwide</h1>\n");
        sb.Append("\t\t\t<p>").Append(EpcFreeToolsH(seo["intro"])).Append("</p>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t").Append(EpcFreeToolsPreviewHtml(key, meta)).Append("\n");
        if (!active)
        {
            sb.Append("\t<div class=\"eft-unavailable\">\n");
            sb.Append("\t\t<i class=\"fa fa-clock-o\"></i>\n");
            sb.Append("\t\t<div>\n");
            sb.Append("\t\t\t<strong>This tool is temporarily unavailable.</strong>\n");
            sb.Append("\t\t\t<span>We're improving it right now. Please check back soon — the other free tools are still available.</span>\n");
            sb.Append("\t\t</div>\n");
            sb.Append("\t</div>\n");
            sb.Append("\t").Append(EpcFreeToolsGuideHtml(key, meta)).Append("\n");
            sb.Append("\t").Append(EpcFreeToolsFaqHtml(key)).Append("\n");
            sb.Append("</div>\n");
            sb.Append(EpcFreeToolsJsonld(baseUrl, key));
            return sb.ToString();
        }

        sb.Append("\n");
        sb.Append("\t<div class=\"eft-gate\" id=\"eft_gate\">\n");
        sb.Append("\t\t<div class=\"eft-gate__tabs\">\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"eft-tab is-active\" id=\"eft_tab_register\"><i class=\"fa fa-user-plus\"></i> Register free</button>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"eft-tab\" id=\"eft_tab_login\"><i class=\"fa fa-sign-in\"></i> Log in</button>\n");
        sb.Append("\t\t</div>\n\n");
        sb.Append("\t\t<div class=\"eft-pane\" id=\"eft_pane_register\">\n");
        sb.Append("\t\t\t<h3><i class=\"fa fa-unlock-alt\"></i> Register free to use this tool</h3>\n");
        sb.Append("\t\t\t<p>Create your free account once — no card. One login unlocks every tool, saves your work and localises to your country.</p>\n");
        sb.Append("\t\t\t<div class=\"eft-form-row\">\n");
        sb.Append("\t\t\t\t<label>Work email<input type=\"email\" id=\"eft_email\" placeholder=\"you@company.com\" autocomplete=\"email\" /></label>\n");
        sb.Append("\t\t\t\t<label>Company<input type=\"text\" id=\"eft_company\" placeholder=\"Your company name\" autocomplete=\"organization\" /></label>\n");
        sb.Append("\t\t\t\t<label>Country\n");
        sb.Append("\t\t\t\t\t<select id=\"eft_country\">\n");
        var cFirst = true;
        foreach (var (cc, cn) in countries)
        {
            if (cFirst)
            {
                sb.Append("\t\t\t\t\t\t");
                cFirst = false;
            }

            sb.Append("\t\t\t\t\t\t<option value=\"").Append(EpcFreeToolsH(cc)).Append('"');
            if (cc == "AE")
            {
                sb.Append(" selected");
            }

            sb.Append('>').Append(EpcFreeToolsH(cn)).Append("</option>\n\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t\t</select>\n");
        sb.Append("\t\t\t\t</label>\n");
        sb.Append("\t\t\t\t<label>Password (min 6 chars)\n");
        sb.Append("\t\t\t\t\t<span class=\"eft-pass\"><input type=\"password\" id=\"eft_password\" placeholder=\"Create a password\" autocomplete=\"new-password\" /><button type=\"button\" class=\"eft-eye\" data-eye=\"eft_password\"><i class=\"fa fa-eye\"></i></button></span>\n");
        sb.Append("\t\t\t\t</label>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--primary\" id=\"eft_register\"><i class=\"fa fa-check\"></i> Create free account</button>\n");
        sb.Append("\t\t\t<p class=\"eft-msg\" id=\"eft_gate_msg\"></p>\n");
        sb.Append("\t\t</div>\n\n");
        sb.Append("\t\t<div class=\"eft-pane\" id=\"eft_pane_login\" hidden>\n");
        sb.Append("\t\t\t<h3><i class=\"fa fa-sign-in\"></i> Log in to your free account</h3>\n");
        sb.Append("\t\t\t<p>Welcome back — sign in to pick up your saved work across all tools.</p>\n");
        sb.Append("\t\t\t<div class=\"eft-form-row\">\n");
        sb.Append("\t\t\t\t<label>Work email<input type=\"email\" id=\"eft_login_email\" placeholder=\"you@company.com\" autocomplete=\"email\" /></label>\n");
        sb.Append("\t\t\t\t<label>Password\n");
        sb.Append("\t\t\t\t\t<span class=\"eft-pass\"><input type=\"password\" id=\"eft_login_password\" placeholder=\"Your password\" autocomplete=\"current-password\" /><button type=\"button\" class=\"eft-eye\" data-eye=\"eft_login_password\"><i class=\"fa fa-eye\"></i></button></span>\n");
        sb.Append("\t\t\t\t</label>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--primary\" id=\"eft_login\"><i class=\"fa fa-sign-in\"></i> Log in</button>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"eft-link\" id=\"eft_forgot\">Forgot password?</button>\n");
        sb.Append("\t\t\t<p class=\"eft-msg\" id=\"eft_login_msg\"></p>\n\n");
        sb.Append("\t\t\t<div class=\"eft-reset\" id=\"eft_reset\" hidden>\n");
        sb.Append("\t\t\t\t<h4>Reset password</h4>\n");
        sb.Append("\t\t\t\t<p>Enter the 6-digit code we emailed you, plus a new password.</p>\n");
        sb.Append("\t\t\t\t<div class=\"eft-form-row\">\n");
        sb.Append("\t\t\t\t\t<label>Reset code<input type=\"text\" id=\"eft_reset_code\" inputmode=\"numeric\" placeholder=\"6-digit code\" /></label>\n");
        sb.Append("\t\t\t\t\t<label>New password\n");
        sb.Append("\t\t\t\t\t\t<span class=\"eft-pass\"><input type=\"password\" id=\"eft_reset_password\" placeholder=\"New password\" autocomplete=\"new-password\" /><button type=\"button\" class=\"eft-eye\" data-eye=\"eft_reset_password\"><i class=\"fa fa-eye\"></i></button></span>\n");
        sb.Append("\t\t\t\t\t</label>\n");
        sb.Append("\t\t\t\t</div>\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--primary\" id=\"eft_reset_submit\"><i class=\"fa fa-key\"></i> Set new password &amp; sign in</button>\n");
        sb.Append("\t\t\t\t<p class=\"eft-msg\" id=\"eft_reset_msg\"></p>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n\n");
        sb.Append("\t").Append(EpcFreeToolsGuideHtml(key, meta)).Append("\n\n");
        sb.Append("\t<div class=\"eft-app\" id=\"eft_app\" hidden>\n");
        sb.Append("\t\t<div class=\"eft-app__bar\">\n");
        sb.Append("\t\t\t<span id=\"eft_who\"></span>\n");
        sb.Append("\t\t\t<span class=\"eft-app__menu\">\n");
        sb.Append("\t\t\t\t<span class=\"eft-country-pill\">Country: <strong id=\"eft_country_label\"></strong> · <button type=\"button\" class=\"eft-link\" id=\"eft_change_country\">change</button></span>\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"eft-link\" id=\"eft_my_saves\"><i class=\"fa fa-folder-open\"></i> My saved results</button>\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"eft-link\" id=\"eft_logout\"><i class=\"fa fa-sign-out\"></i> Log out</button>\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"eft-link eft-link--danger\" id=\"eft_delete\"><i class=\"fa fa-trash\"></i> Delete my data</button>\n");
        sb.Append("\t\t\t</span>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t\t<div class=\"eft-saves\" id=\"eft_saves\" hidden></div>\n");
        sb.Append("\t\t<div class=\"eft-danger\" id=\"eft_delete_box\" hidden>\n");
        sb.Append("\t\t\t<h4><i class=\"fa fa-shield\"></i> Delete your account &amp; all data</h4>\n");
        sb.Append("\t\t\t<p>For your security, deletion needs email confirmation. We'll email a one-time <strong>cross-code</strong> to your address; nothing is deleted until you enter it below.</p>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--ghost\" id=\"eft_delete_send\"><i class=\"fa fa-envelope\"></i> Email me a confirmation code</button>\n");
        sb.Append("\t\t\t<div class=\"eft-form-row\" id=\"eft_delete_confirm\" hidden>\n");
        sb.Append("\t\t\t\t<label>Confirmation cross-code<input type=\"text\" id=\"eft_delete_code\" inputmode=\"numeric\" placeholder=\"6-digit code\" /></label>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--danger\" id=\"eft_delete_confirm_btn\" hidden><i class=\"fa fa-trash\"></i> Permanently delete my data</button>\n");
        sb.Append("\t\t\t<button type=\"button\" class=\"eft-link\" id=\"eft_delete_cancel\">Cancel</button>\n");
        sb.Append("\t\t\t<p class=\"eft-msg\" id=\"eft_delete_msg\"></p>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t\t<div class=\"eft-split\">\n");
        sb.Append("\t\t\t<form class=\"eft-inputs\" id=\"eft_form\" onsubmit=\"return false;\">\n");
        sb.Append("\t\t\t\t").Append(EpcFreeToolsFieldsHtml(key)).Append("\n");
        sb.Append("\t\t\t\t<button type=\"button\" class=\"epm-btn epm-btn--primary\" id=\"eft_compute\"><i class=\"fa fa-calculator\"></i> Calculate</button>\n");
        sb.Append("\t\t\t</form>\n");
        sb.Append("\t\t\t<div class=\"eft-result\" id=\"eft_result\">\n");
        sb.Append("\t\t\t\t<div class=\"eft-result__empty\">Fill in the form and press <strong>Calculate</strong>.</div>\n");
        sb.Append("\t\t\t</div>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t</div>\n");
        sb.Append("\t").Append(EpcFreeToolsFaqHtml(key)).Append("\n");
        sb.Append("</div>\n");
        sb.Append(EpcFreeToolsJsonld(baseUrl, key));
        sb.Append("\t\t");
        return sb.ToString();
    }

    public static string EpcFreeToolsFieldsHtml(string key)
    {
        string Num(string id, string label, string val = "")
            => "<label>" + EpcFreeToolsH(label) + "<input type=\"number\" step=\"0.01\" data-eft-field=\"" + EpcFreeToolsH(id) + "\" value=\"" + EpcFreeToolsH(val) + "\" /></label>";
        string Txt(string id, string label, string val = "")
            => "<label>" + EpcFreeToolsH(label) + "<input type=\"text\" data-eft-field=\"" + EpcFreeToolsH(id) + "\" value=\"" + EpcFreeToolsH(val) + "\" /></label>";
        string Csv(string hint)
            => "<div class=\"eft-fieldset\">Or upload a CSV</div>"
               + "<label class=\"eft-csv\">Upload CSV file"
               + "<input type=\"file\" accept=\".csv,text/csv\" data-eft-csv=\"1\" /></label>"
               + "<label>CSV data (auto-fills when you choose a file; or paste here)"
               + "<textarea rows=\"5\" data-eft-field=\"csv\" placeholder=\"" + EpcFreeToolsH(hint) + "\"></textarea></label>"
               + "<p class=\"eft-hint\"><i class=\"fa fa-info-circle\"></i> " + EpcFreeToolsH(hint) + "</p>";

        return key switch
        {
            "vat" => Num("standard_sales", "Standard-rated sales (ex-tax)")
                     + Num("zero_sales", "Zero-rated sales")
                     + Num("exempt_sales", "Exempt sales")
                     + Num("standard_purchases", "Standard-rated purchases (ex-tax)")
                     + Num("import_vat", "Import tax paid")
                     + Csv("Header: type,category,amount,trn — e.g. sale,standard,1000,100123456700003"),
            "ct" => Num("revenue", "Revenue / turnover")
                    + Num("expenses", "Deductible expenses")
                    + Num("adjustments", "Tax adjustments (+/-)")
                    + Csv("Header: type,amount — e.g. revenue,500000 / expense,300000 / adjustment,20000"),
            "payroll" => Num("basic", "Basic salary (monthly)")
                         + Num("allowances", "Allowances (monthly)")
                         + Num("deductions", "Deductions (monthly)")
                         + Num("years", "Years of service (for gratuity)"),
            "ifrs" => "<div class=\"eft-fieldset\">Income statement</div>"
                      + Num("revenue", "Revenue")
                      + Num("cogs", "Cost of sales")
                      + Num("opex", "Operating expenses")
                      + Num("other_income", "Other income")
                      + "<div class=\"eft-fieldset\">Balance sheet</div>"
                      + Num("non_current_assets", "Non-current assets")
                      + Num("current_assets", "Current assets")
                      + Num("equity", "Equity")
                      + Num("non_current_liabilities", "Non-current liabilities")
                      + Num("current_liabilities", "Current liabilities")
                      + Csv("Header: account,debit,credit,classification — e.g. Sales,0,500000,revenue"),
            "einvoice" => Txt("number", "Invoice number (blank = auto)")
                          + Txt("seller", "Seller / your company")
                          + Txt("seller_trn", "Seller tax reg. no.")
                          + Txt("buyer", "Buyer / customer")
                          + Txt("buyer_trn", "Buyer tax reg. no.")
                          + "<div class=\"eft-fieldset\">Lines</div>"
                          + "<div id=\"eft_lines\">"
                          + EpcFreeToolsEinvoiceLineHtml()
                          + EpcFreeToolsEinvoiceLineHtml()
                          + "</div>"
                          + "<button type=\"button\" class=\"eft-link\" id=\"eft_add_line\"><i class=\"fa fa-plus\"></i> Add line</button>",
            "extreport" => "<div class=\"eft-fieldset\">VAT / GST return</div>"
                           + Num("standard_sales", "Standard-rated sales (ex-tax)")
                           + Num("zero_sales", "Zero-rated sales")
                           + Num("exempt_sales", "Exempt sales")
                           + Num("standard_purchases", "Standard-rated purchases (ex-tax)")
                           + Num("import_vat", "Import tax paid")
                           + "<div class=\"eft-fieldset\">Corporate tax return</div>"
                           + Num("revenue", "Revenue / turnover")
                           + Num("expenses", "Deductible expenses")
                           + Num("adjustments", "Tax adjustments (+/-)")
                           + Csv("VAT rows (type,category,amount,trn) or CT rows (type,amount) — both are read."),
            "customs" => Txt("hs_code", "HS code (single line)")
                         + Num("qty", "Quantity", "1")
                         + Num("unit_value", "Unit value (FOB)")
                         + Num("freight", "Freight")
                         + Num("insurance", "Insurance")
                         + Num("other", "Other landed costs (clearing, handling)")
                         + Num("fx_rate", "FX rate to " + EpcFreeToolsH("local currency"), "1")
                         + Csv("Header: hs_code,qty,unit_value — e.g. 8516,10,250 (freight/insurance from the fields above)."),
            "insurance" => Num("sum_insured", "Sum insured")
                           + Num("rate", "Premium rate (%) — optional")
                           + Num("premium", "Annual premium — optional (overrides rate)")
                           + Txt("expiry", "Policy expiry date (YYYY-MM-DD)"),
            "docexpiry" => Txt("title", "Document title (single)")
                           + Txt("expiry", "Expiry date (YYYY-MM-DD)")
                           + Txt("reminder_days", "Reminder lead days", "90,60,30,7")
                           + Csv("Header: title,expiry,reminder_days — e.g. Trade Licence,2026-03-31,90,60,30"),
            "valuation" => Num("revenue", "Annual revenue")
                           + Num("ebitda", "EBITDA")
                           + Num("net_debt", "Net debt (cash negative)")
                           + Num("growth", "Long-term growth %", "5")
                           + Num("discount", "Discount rate / WACC %", "15")
                           + Num("ebitda_multiple", "EBITDA multiple (x)", "6")
                           + Num("revenue_multiple", "Revenue multiple (x)", "1.5")
                           + Num("tax_rate", "Tax rate % (for DCF)", "0")
                           + Csv("Optional P&L CSV: type,amount (revenue / expense) to derive revenue & EBITDA."),
            "finmodel" => Num("revenue", "Base-year revenue")
                          + Num("growth", "Annual growth %", "10")
                          + Num("gross_margin", "Gross margin %", "40")
                          + Num("opex_pct", "Opex as % of revenue", "25")
                          + Num("tax_rate", "Tax rate %", "0")
                          + Num("years", "Years to project (1-10)", "5")
                          + Csv("Optional P&L CSV: type,amount (revenue) to set base-year revenue."),
            "taxkit" => "<p class=\"eft-hint\"><i class=\"fa fa-info-circle\"></i> This kit reads your registered country automatically. Use “change” above to preview another country.</p>",
            "hrcompliance" => "<p class=\"eft-hint\"><i class=\"fa fa-info-circle\"></i> The labour-law card uses your registered country. Add an employee below for an optional compliance check + end-of-service liability.</p>"
                              + "<div class=\"eft-fieldset\">Employee check (optional)</div>"
                              + Num("basic_salary", "Basic salary (monthly)")
                              + Txt("hire_date", "Hire date (YYYY-MM-DD)")
                              + Num("leave_balance", "Leave balance (days)"),
            "workflow" => Txt("approver0", "Tier 1 approver", "Line manager")
                          + Txt("approver1", "Tier 2 approver", "Department head")
                          + Txt("approver2", "Tier 3 approver", "Finance director")
                          + Num("tier1", "Tier 1 limit", "5000")
                          + Num("tier2", "Tier 2 limit", "50000"),
            _ => ""
        };
    }

    public static string EpcFreeToolsEinvoiceLineHtml()
        => "<div class=\"eft-line\">"
           + "<input type=\"text\" class=\"eft-ln-desc\" placeholder=\"Description\" />"
           + "<input type=\"number\" step=\"0.01\" class=\"eft-ln-qty\" placeholder=\"Qty\" />"
           + "<input type=\"number\" step=\"0.01\" class=\"eft-ln-price\" placeholder=\"Unit price\" />"
           + "</div>";

    public static string EpcFreeToolsStyles()
        => PhpPlanQ1HillData.Styles;

    public static string EpcFreeToolsScripts(string baseUrl)
    {
        var endpoint = baseUrl + "content/general_pages/ajax_epc_free_tools.php";
        var js = PhpPlanQ1HillData.ScriptsTemplate.Replace("__ENDPOINT__", JsonSerializer.Serialize(endpoint, JsonOpts), StringComparison.Ordinal);
        return "<script>" + js + "</script>";
    }

    private static Dictionary<string, object?> Unavailable()
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Service unavailable, please try again." };

    private static Dictionary<string, object?> Item(int position, string name, string item)
        => new(StringComparer.Ordinal)
        {
            ["@type"] = "ListItem",
            ["position"] = position,
            ["name"] = name,
            ["item"] = item
        };

    private static object PhpNumber(double value)
        => Math.Abs(value - Math.Round(value)) < 1e-9 ? (object)(int)Math.Round(value) : value;

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            string s => s.Length == 0,
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            bool b => b ? "1" : "",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "",
            _ => value.ToString() ?? ""
        };

    private static double ToFloat(object? value)
        => value switch
        {
            null => 0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0,
            IFormattable f => double.TryParse(f.ToString(null, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0,
            _ => 0
        };
}
