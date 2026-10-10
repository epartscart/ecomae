using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-spray CP professional shell. PHP identifiers kept for the inventory:
/// <c>epc_cp_shell_css_version</c>, <c>epc_cp_shell_use_asset_proxies</c>,
/// <c>epc_cp_shell_asset_href</c>, <c>epc_cp_sidebar_first_paint_script</c>,
/// <c>epc_cp_nuclear_critical_css</c>, <c>epc_cp_force_visible_body_style</c>,
/// <c>epc_cp_force_visible_script</c>, <c>epc_cp_shell_body_classes</c>,
/// <c>epc_cp_page_header_context</c>, <c>epc_cp_page_header_actions_html</c>,
/// <c>epc_cp_menu_sections_early_style</c>, <c>epc_cp_sidebar_early_init_script</c>,
/// <c>epc_erp_sidebar_early_init_script</c>, <c>epc_erp_sidebar_accordion_script</c>,
/// <c>epc_cp_hide_menu_vanilla_script</c>, <c>epc_cp_menu_sections_script</c>,
/// <c>epc_cp_sidebar_collapse_script</c>, <c>epc_cp_modern_reveal_script</c>,
/// <c>epc_cp_login_context</c>, <c>epc_cp_shell_context</c>,
/// <c>epc_cp_shell_enqueue_assets</c>, <c>epc_cp_login_hero_markup</c>,
/// <c>epc_cp_shell_inline_style_block</c>.
/// Path: <c>content/general_pages/epc_cp_professional_shell.php</c>.
/// GET never mints a session cookie. Branding / hub / animated-logo / leftover ERP routers stay injected.
/// Do not write leftover finance CRM helpers or leftover demand intelligence.
/// </summary>
public static class PhpPlanQ1Spray
{
    public const string ProfessionalShellPath = "content/general_pages/epc_cp_professional_shell.php";
    public const string CssVersion = "20260721aoCfg1";

    public static Func<Dictionary<string, object?>>? BrandCpContext { get; set; }
    public static Func<bool>? IsPlatformErpActive { get; set; }
    public static Func<bool>? IsDemoCpContext { get; set; }
    public static Func<bool>? IsDemoErpOnly { get; set; }
    public static Func<bool>? IsAutopartsParity { get; set; }
    public static Func<string>? DemoCpSiteKey { get; set; }
    public static Func<Dictionary<string, object?>?>? DemoTenantRow { get; set; }
    public static Func<bool>? IsClientErpActive { get; set; }
    public static Func<Dictionary<string, object?>?>? ClientErpTenantRow { get; set; }
    public static Func<string>? ClientErpSiteKey { get; set; }
    public static Func<bool>? IsSuperCpHost { get; set; }
    public static Func<bool>? IsPlatformOperator { get; set; }
    public static Func<bool>? IsPlatformHostname { get; set; }
    public static Func<string>? ActiveIndustry { get; set; }
    public static Func<string?, Dictionary<string, object?>>? IndustryFor { get; set; }
    public static Func<int, string>? TranslateById { get; set; }
    public static Func<bool>? AnimatedApplies { get; set; }
    public static Action? AnimatedEnqueue { get; set; }
    public static Func<string, string>? AnimatedMarkup { get; set; }
    public static Func<string, Dictionary<string, object?>, string>? HubLogo { get; set; }
    public static Func<string, Dictionary<string, object?>, string>? StaticLogo { get; set; }
    public static Action? HubLogoEnqueue { get; set; }
    public static Action? LoginHeroEnqueue { get; set; }
    public static Action? LoginEnqueue { get; set; }
    public static Action<string>? Echo { get; set; }
    public static string BackendDir { get; set; } = "cp";
    public static string DocumentRoot { get; set; } = "";
    public static string RequestUri { get; set; } = "";
    public static string? InlineCssQuery { get; set; }
    public static string? LoginStaticCookie { get; set; }
    public static Func<string, bool>? FileExists { get; set; }
    public static Func<string, string?>? ReadFile { get; set; }

    private static bool EnqueueDone;

    public static void Reset()
    {
        BrandCpContext = null;
        IsPlatformErpActive = null;
        IsDemoCpContext = null;
        IsDemoErpOnly = null;
        IsAutopartsParity = null;
        DemoCpSiteKey = null;
        DemoTenantRow = null;
        IsClientErpActive = null;
        ClientErpTenantRow = null;
        ClientErpSiteKey = null;
        IsSuperCpHost = null;
        IsPlatformOperator = null;
        IsPlatformHostname = null;
        ActiveIndustry = null;
        IndustryFor = null;
        TranslateById = null;
        AnimatedApplies = null;
        AnimatedEnqueue = null;
        AnimatedMarkup = null;
        HubLogo = null;
        StaticLogo = null;
        HubLogoEnqueue = null;
        LoginHeroEnqueue = null;
        LoginEnqueue = null;
        Echo = null;
        BackendDir = "cp";
        DocumentRoot = "";
        RequestUri = "";
        InlineCssQuery = null;
        LoginStaticCookie = null;
        FileExists = null;
        ReadFile = null;
        EnqueueDone = false;
    }

    public static string EpcCpShellCssVersion() => CssVersion;

    public static bool EpcCpShellUseAssetProxies() => true;

    public static string EpcCpShellAssetHref(string staticPath, string phpProxyPath)
    {
        var path = EpcCpShellUseAssetProxies() ? phpProxyPath : staticPath;
        return path + "?v=" + Uri.EscapeDataString(EpcCpShellCssVersion());
    }

    public static string EpcCpSidebarFirstPaintScript() => PhpPlanQ1SprayData.FirstPaint + "\n";

    public static string EpcCpNuclearCriticalCss() => PhpPlanQ1SprayData.Nuclear;

    public static string EpcCpForceVisibleBodyStyle()
    {
        var ver = EpcCpShellCssVersion();
        return "<!-- epc-cp-build:" + H(ver) + " -->\n"
            + "<style id=\"epc-cp-body-force-visible\">"
            + "body.epc-cp-shell .content{display:block!important;width:100%!important;min-height:1px!important;opacity:1!important;visibility:visible!important}"
            + "#wrapper .content,#wrapper .content.epc-cp-main-pane,#wrapper .content .epc-cp-content-inner,#wrapper .content .epc-cp-content-inner>.row,#wrapper .content>.row,#wrapper .content .epc-portal-settings,#wrapper .epc-cp-page-header,#wrapper .epc-cp-page-header__card{display:block!important;opacity:1!important;visibility:visible!important;min-height:1px!important;height:auto!important;max-height:none!important;overflow:visible!important;float:none!important;width:100%!important;flex:none!important;animation:none!important;transform:none!important;position:relative!important;left:auto!important;top:auto!important;clip:auto!important;clip-path:none!important}"
            + "#wrapper .content .col-lg-12,#wrapper .content .col-lg-9,#wrapper .content [class*=\"col-\"]{display:block!important;opacity:1!important;visibility:visible!important;min-height:0!important;height:auto!important;max-height:none!important;overflow:visible!important;float:none!important;flex:none!important;animation:none!important;transform:none!important}"
            + "body.epc-cp-shell .splash{display:none!important}"
            + "</style>\n";
    }

    public static string EpcCpForceVisibleScript() => PhpPlanQ1SprayData.ForceJs;

    public static string EpcCpShellBodyClasses() => "epc-cp--blue-theme epc-cp-modern";

    public static Dictionary<string, object?> EpcCpPageHeaderContext()
    {
        var shell = EpcCpShellContext();
        var industryCode = ActiveIndustry?.Invoke() ?? "";
        if (industryCode == "" && Flag(IsSuperCpHost) && !Flag(IsDemoCpContext))
        {
            industryCode = "platform_host";
        }

        var industry = IndustryFor?.Invoke(industryCode)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "", ["icon"] = "fa-cog" };
        var isDemoCp = Flag(IsDemoCpContext);
        var eyebrow = Str(industry.GetValueOrDefault("name")).Trim();
        if (isDemoCp)
        {
            var demoTrade = Str(DemoTenantRow?.Invoke()?.GetValueOrDefault("trade_name")).Trim();
            if (demoTrade != "")
            {
                eyebrow = demoTrade;
            }
            else if (industryCode == "auto_parts")
            {
                eyebrow = "eParts Cart Demo";
            }
        }
        else if (Str(shell.GetValueOrDefault("type")) == "super" && Str(shell.GetValueOrDefault("company")).Trim() != "")
        {
            eyebrow = Str(shell.GetValueOrDefault("company")).Trim();
        }
        else if (eyebrow == "" && Str(shell.GetValueOrDefault("company")).Trim() != "")
        {
            eyebrow = Str(shell.GetValueOrDefault("company")).Trim();
        }

        var backend = BackendDir.Trim('/');
        if (backend == "")
        {
            backend = "cp";
        }

        var actions = new List<Dictionary<string, object?>>();
        var isSuper = Flag(IsSuperCpHost);
        var isPlatformOp = !isDemoCp && Flag(IsPlatformOperator);
        var here = RequestUri;
        if (isPlatformOp)
        {
            var erpUrl = Flag(IsPlatformHostname) ? "/erp/" : "/" + backend + "/platform-erp/";
            actions.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = erpUrl,
                ["label"] = "Platform ERP",
                ["icon"] = "fa-chart-line",
                ["primary"] = true
            });
        }

        if (isSuper && !isDemoCp && !here.Contains("/tenant_hub/", StringComparison.Ordinal))
        {
            actions.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = "/" + backend + "/shop/tenant_hub/tenant_hub",
                ["label"] = "Tenant hub",
                ["icon"] = "fa-cloud"
            });
        }

        if (isSuper && !isDemoCp)
        {
            actions.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["url"] = "https://www.ecomae.com/",
                ["label"] = "Marketing site",
                ["icon"] = "fa-globe",
                ["target"] = "_blank"
            });
        }
        else if (Flag(IsDemoErpOnly))
        {
            var key = Regex.Replace((DemoCpSiteKey?.Invoke() ?? "").ToLowerInvariant(), "[^a-z0-9_]", "");
            if (key != "")
            {
                actions.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["url"] = "https://www.ecomae.com/demo/" + key + "/",
                    ["label"] = "No storefront",
                    ["icon"] = "fa-ban",
                    ["target"] = "_blank"
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["eyebrow"] = eyebrow,
            ["icon"] = Str(industry.GetValueOrDefault("icon"), "fa-cog"),
            ["role_label"] = Str(shell.GetValueOrDefault("label")),
            ["role_type"] = Str(shell.GetValueOrDefault("type")),
            ["actions"] = actions
        };
    }

    public static string EpcCpPageHeaderActionsHtml(IEnumerable<Dictionary<string, object?>> actions)
    {
        var list = actions.ToList();
        if (list.Count == 0)
        {
            return "";
        }

        var html = "<div class=\"epc-cp-page-header__actions\">";
        foreach (var action in list)
        {
            if (PhpEmpty(action.GetValueOrDefault("url")) || PhpEmpty(action.GetValueOrDefault("label")))
            {
                continue;
            }

            var cls = "epc-cp-page-header__pill";
            if (PhpTruthy(action.GetValueOrDefault("primary")))
            {
                cls += " epc-cp-page-header__pill--primary";
            }

            var target = "";
            if (!PhpEmpty(action.GetValueOrDefault("target")))
            {
                target = " target=\"" + H(Str(action.GetValueOrDefault("target"))) + "\" rel=\"noopener noreferrer\"";
            }

            var icon = !PhpEmpty(action.GetValueOrDefault("icon")) ? Str(action.GetValueOrDefault("icon")) : "fa-link";
            html += "<a class=\"" + cls + "\" href=\"" + H(Str(action.GetValueOrDefault("url"))) + "\"" + target + ">";
            html += "<i class=\"fa " + H(icon) + "\"></i>";
            html += "<span>" + H(Str(action.GetValueOrDefault("label"))) + "</span>";
            html += "</a>";
        }

        html += "</div>";
        return html;
    }

    public static string EpcCpMenuSectionsEarlyStyle()
        => "<style id=\"epc-cp-menu-sections-early\">"
            + ".epc-cp #side-menu>li:not(.active)>ul.nav-second-level,"
            + ".epc-cp-shell #side-menu>li:not(.active)>ul.nav-second-level,"
            + ".epc-cp-shell #side-menu>li:not(.active)>ul.epc-cp-nav-section__children{display:none!important;visibility:hidden!important;height:0!important;overflow:hidden!important}"
            + ".epc-cp #side-menu>li.active>ul.nav-second-level,"
            + ".epc-cp-shell #side-menu>li.active>ul.nav-second-level,"
            + ".epc-cp-shell #side-menu>li.active>ul.epc-cp-nav-section__children{display:block!important;visibility:visible!important;height:auto!important;overflow:visible!important}"
            + "</style>\n";

    public static string EpcCpSidebarEarlyInitScript() => PhpPlanQ1SprayData.SidebarEarly;

    public static string EpcErpSidebarEarlyInitScript() => PhpPlanQ1SprayData.ErpEarly;

    public static string EpcErpSidebarAccordionScript() => PhpPlanQ1SprayData.ErpAcc;

    public static string EpcCpHideMenuVanillaScript() => PhpPlanQ1SprayData.HideMenu;

    public static string EpcCpMenuSectionsScript() => PhpPlanQ1SprayData.MenuSections;

    public static string EpcCpSidebarCollapseScript() => PhpPlanQ1SprayData.Collapse;

    public static string EpcCpModernRevealScript() => PhpPlanQ1SprayData.Reveal;

    public static Dictionary<string, object?> EpcCpLoginContext()
    {
        if (Flag(IsPlatformErpActive))
        {
            return Login(
                "platform_erp", "Platform ERP", "ECOM AE Platform ERP",
                "ECOM AE Operations — finance & platform ledger",
                "Operator ERP on ecomae registry · Super CP staff only",
                "epc-cp-login--platform_erp",
                ("fa-chart-line", "Platform finance, GL & VAT"),
                ("fa-building", "ECOM AE company operations"),
                ("fa-shield", "Isolated from client tenant databases"));
        }

        if (Flag(IsDemoCpContext))
        {
            var row = DemoTenantRow?.Invoke();
            if (Flag(IsDemoErpOnly))
            {
                var trade = DemoTrade(row, DemoCpSiteKey?.Invoke() ?? "", "Demo");
                return Login(
                    "demo_erp_only", "ERP-only demo", trade + " — ERP Sandbox",
                    "Finance, CRM & operations — no storefront",
                    "ECOM AE sandbox · expires automatically",
                    "epc-cp-login--demo_erp_only",
                    ("fa-chart-line", "Finance, GL & VAT reporting"),
                    ("fa-users", "CRM & company operations"),
                    ("fa-cubes", "Inventory & procurement"));
            }

            if (Flag(IsAutopartsParity))
            {
                return TenantLogin(Brand());
            }

            var demoTrade = DemoTrade(row, DemoCpSiteKey?.Invoke() ?? "", "Demo");
            return Login(
                "demo_cp", "Demo CP", demoTrade + " — Sandbox",
                "Shop, catalog & prices — isolated demo tenant",
                "ECOM AE sandbox · expires automatically",
                "epc-cp-login--demo_cp",
                ("fa-shopping-cart", "Orders & catalogue"),
                ("fa-tags", "Prices & promotions"),
                ("fa-cubes", "Inventory sample data"));
        }

        if (Flag(IsClientErpActive))
        {
            var trade = DemoTrade(ClientErpTenantRow?.Invoke(), ClientErpSiteKey?.Invoke() ?? "", "Client");
            return Login(
                "client_erp", "Client ERP", trade + " ERP",
                "Company ERP — finance & operations",
                "Secure tenant workspace · ECOM AE cloud",
                "epc-cp-login--client_erp",
                ("fa-chart-line", "Finance, GL & VAT reporting"),
                ("fa-cubes", "Inventory & procurement"),
                ("fa-users", "CRM & company operations"));
        }

        if (Flag(IsSuperCpHost))
        {
            return Login(
                "super", "BOS · Control", "BOS — Business Operation System",
                "One login · operators control the whole fleet, tenants see only their own area",
                "Unified control over every tenant, ERP-only client and demo",
                "epc-cp-login--super",
                ("fa-globe", "Fleet command — commerce, ERP-only & demo"),
                ("fa-rocket", "Tenant onboarding, templates & module packs"),
                ("fa-shield", "Platform health, governance & failover"));
        }

        return TenantLogin(Brand());
    }

    public static Dictionary<string, object?> EpcCpShellContext()
    {
        var login = EpcCpLoginContext();
        var type = Str(login.GetValueOrDefault("type"));
        if (type == "super")
        {
            return Shell("super", "BOS · Operator", "ECOM AE");
        }

        if (type == "platform_erp")
        {
            return Shell("platform_erp", "Platform ERP", "ECOM AE Operations");
        }

        if (type == "demo_erp_only")
        {
            var heading = Regex.Replace(Str(login.GetValueOrDefault("heading")), @"\s+—\s+ERP Sandbox$", "");
            return Shell("demo_erp_only", "ERP-only demo", heading);
        }

        if (type == "demo_cp")
        {
            var heading = Regex.Replace(Str(login.GetValueOrDefault("heading")), @"\s+—\s+Sandbox$", "");
            return Shell("demo_cp", "Demo CP", heading);
        }

        if (type == "client_erp")
        {
            var heading = Regex.Replace(Str(login.GetValueOrDefault("heading")), @"\s+ERP\s*$", "");
            return Shell("client_erp", Str(login.GetValueOrDefault("heading")), heading);
        }

        var ctx = Brand();
        return Shell("tenant", Translate(3992, "Control Panel"), Str(ctx.GetValueOrDefault("company_name"), Str(ctx.GetValueOrDefault("product_name"))).Trim());
    }

    public static string EpcCpShellEnqueueAssets(bool includeLoginHero = false)
    {
        if (EnqueueDone)
        {
            return "";
        }

        EnqueueDone = true;
        var backend = BackendDir.Trim('/');
        if (backend == "")
        {
            backend = "cp";
        }

        var baseCss = "/" + backend + "/templates/bootstrap_admin/css/";
        var html = "<link rel=\"stylesheet\" href=\"" + H(EpcCpShellAssetHref(baseCss + "epc_cp_ui.css", "/content/general_pages/epc_cp_ui_css.php")) + "\" />\n"
            + "<link rel=\"stylesheet\" href=\"" + H(EpcCpShellAssetHref(baseCss + "epc_cp_professional.css", "/content/general_pages/epc_cp_professional_css.php")) + "\" />\n"
            + "<link rel=\"stylesheet\" href=\"" + H(EpcCpShellAssetHref(baseCss + "epc_cp_density.css", "/content/general_pages/epc_cp_density_css.php")) + "\" />\n"
            + "<link rel=\"stylesheet\" href=\"" + H(EpcCpShellAssetHref(baseCss + "epc_cp_tenant_polish.css", "/content/general_pages/epc_cp_tenant_polish_css.php")) + "\" />\n"
            + "<link rel=\"stylesheet\" href=\"" + H(EpcCpShellAssetHref(baseCss + "epc_cp_storefront_topbar.css", "/content/general_pages/epc_cp_storefront_topbar_css.php")) + "\" />\n";
        Echo?.Invoke(html);
        HubLogoEnqueue?.Invoke();
        if (includeLoginHero)
        {
            LoginHeroEnqueue?.Invoke();
            LoginEnqueue?.Invoke();
        }

        return html;
    }

    public static string EpcCpLoginHeroMarkup()
    {
        if (Flag(AnimatedApplies))
        {
            AnimatedEnqueue?.Invoke();
            return AnimatedMarkup?.Invoke("login") ?? "";
        }

        var useAnimated = true;
        if (!PhpEmpty(LoginStaticCookie) && LoginStaticCookie == "1")
        {
            useAnimated = false;
        }

        var opts = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (useAnimated)
        {
            opts["show_title"] = true;
            opts["show_tagline"] = false;
            opts["aria_label"] = "ECOM AE unified ERP and commerce cloud";
            return HubLogo?.Invoke("login-panel", opts) ?? "";
        }

        opts["show_title"] = true;
        opts["show_tagline"] = true;
        opts["aria_label"] = "ECOM AE";
        return StaticLogo?.Invoke("login", opts) ?? "";
    }

    public static string EpcCpShellInlineStyleBlock()
    {
        if (PhpEmpty(InlineCssQuery))
        {
            return "";
        }

        var root = DocumentRoot.TrimEnd('/', '\\');
        if (root == "")
        {
            return "";
        }

        var backend = BackendDir.Trim('/');
        if (backend == "")
        {
            backend = "cp";
        }

        var files = new[]
        {
            "/" + backend + "/templates/bootstrap_admin/css/epc_cp_ui.css",
            "/" + backend + "/templates/bootstrap_admin/css/epc_cp_professional.css",
            "/" + backend + "/templates/bootstrap_admin/css/epc_cp_density.css",
            "/content/general_pages/epc_ecomae_hub_logo.css"
        };
        var css = "";
        foreach (var rel in files)
        {
            var path = root + rel;
            if (!(FileExists?.Invoke(path) ?? File.Exists(path)))
            {
                continue;
            }

            var chunk = ReadFile?.Invoke(path) ?? (File.Exists(path) ? File.ReadAllText(path) : null);
            if (!string.IsNullOrEmpty(chunk))
            {
                css += chunk + "\n";
            }
        }

        return css == "" ? "" : "<style id=\"epc-cp-inline-css\">" + css + "</style>\n";
    }

    private static Dictionary<string, object?> TenantLogin(Dictionary<string, object?> ctx)
    {
        var company = Str(ctx.GetValueOrDefault("company_name")).Trim();
        var product = Str(ctx.GetValueOrDefault("product_name"), "Control Panel").Trim();
        return Login(
            "tenant",
            Translate(3992, "Control Panel"),
            company != "" ? company : product,
            "Commerce, orders & operations",
            Str(ctx.GetValueOrDefault("hub_tagline"), "Finance & operations").Trim(),
            "epc-cp-login--tenant",
            ("fa-shopping-cart", "Orders & fulfilment"),
            ("fa-chart-line", "ERP, finance & reports"),
            ("fa-credit-card", "Payment gateways & channels"));
    }

    private static Dictionary<string, object?> Login(
        string type, string badge, string heading, string sub, string tagline, string bodyClass,
        params (string Icon, string Label)[] features)
    {
        var feat = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (icon, label) in features)
        {
            feat[icon] = label;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["badge"] = badge,
            ["heading"] = heading,
            ["sub"] = sub,
            ["tagline"] = tagline,
            ["body_class"] = bodyClass,
            ["features"] = feat
        };
    }

    private static Dictionary<string, object?> Shell(string type, string label, string company)
        => new(StringComparer.Ordinal) { ["type"] = type, ["label"] = label, ["company"] = company };

    private static Dictionary<string, object?> Brand()
        => BrandCpContext?.Invoke()
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["company_name"] = "",
                ["product_name"] = "Control Panel",
                ["hub_tagline"] = "Finance & operations"
            };

    private static string DemoTrade(Dictionary<string, object?>? row, string key, string fallback)
    {
        var trade = row != null ? Str(row.GetValueOrDefault("trade_name")).Trim() : "";
        if (trade == "" && key != "")
        {
            trade = key.ToUpperInvariant();
        }

        return trade == "" ? fallback : trade;
    }

    private static string Translate(int id, string fallback) => TranslateById?.Invoke(id) ?? fallback;

    private static bool Flag(Func<bool>? fn) => fn?.Invoke() == true;

    private static string H(string value)
        => WebUtility.HtmlEncode(value).Replace("&#39;", "&#039;", StringComparison.Ordinal).Replace("'", "&#039;", StringComparison.Ordinal);

    private static string Str(object? value, string fallback = "")
    {
        if (value is null)
        {
            return fallback;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
    }

    private static bool PhpTruthy(object? value)
        => value is true or 1 or 1L or 1.0 or "1" || (value is string s && s != "" && s != "0");

    private static bool PhpEmpty(object? value)
        => value is null or false or 0 or 0L or 0d || value is string s && (s == "" || s == "0");
}
