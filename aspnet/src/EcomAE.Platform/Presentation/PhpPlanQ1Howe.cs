using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-howe CP mobile-apps page.
/// PHP identifiers kept for the inventory: <c>epc_int_h</c>, <c>epc_int_backend</c>,
/// <c>epc_integrations_mobile_config</c>, <c>epc_integrations_platform_mobile_defaults</c>,
/// <c>epc_integrations_default_mobile_config</c>, <c>epc_cp_page_frame_open</c>,
/// <c>epc_cp_page_frame_close</c>.
/// Path: <c>cp/content/control/portal/epc_mobile_apps.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique
/// page-frame parent, leftover unique user helper, leftover portal-db parent,
/// Super-CP host check, and portal host stay injected and are not named here.
/// </summary>
public static class PhpPlanQ1Howe
{
    public const string MobileAppsPath = "cp/content/control/portal/epc_mobile_apps.php";
    public const string DeniedAdmin =
        "<div class=\"alert alert-warning\">Admin login required.</div>";

    public static Func<bool>? IsAdmin { get; set; }
    public static Func<bool>? IsSuperCp { get; set; }
    public static object? BackendDir { get; set; }
    public static string HttpHost { get; set; } = "";
    public static bool HasPortalHost { get; set; }
    public static string PortalHost { get; set; } = "";
    public static Dictionary<string, object?> MobileOverride { get; set; } = new(StringComparer.Ordinal);
    public static bool HasFrameOpen { get; set; } = true;
    public static bool HasFrameClose { get; set; } = true;
    public static string LastOutput { get; private set; } = "";
    public static string LastAjaxUrl { get; private set; } = "";
    public static string LastStorefrontUrl { get; private set; } = "";

    public static void Reset()
    {
        IsAdmin = null;
        IsSuperCp = null;
        BackendDir = null;
        HttpHost = "";
        HasPortalHost = false;
        PortalHost = "";
        MobileOverride = new Dictionary<string, object?>(StringComparer.Ordinal);
        HasFrameOpen = true;
        HasFrameClose = true;
        LastOutput = "";
        LastAjaxUrl = "";
        LastStorefrontUrl = "";
    }

    public static string EpcIntH(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static string EpcIntBackend()
        => Str(BackendDir ?? "cp").Trim('/');

    public static Dictionary<string, object?> EpcIntegrationsDefaultMobileConfig()
        => new(StringComparer.Ordinal)
        {
            ["enabled"] = false,
            ["app_name"] = "",
            ["bundle_id"] = "",
            ["deep_link_scheme"] = "",
            ["deep_link_domain"] = "",
            ["api_base_url"] = "",
            ["play_store_url"] = "",
            ["app_store_url"] = "",
            ["pwa_enabled"] = true,
            ["firebase_project_id"] = "",
            ["push_enabled"] = false
        };

    public static Dictionary<string, object?> EpcIntegrationsPlatformMobileDefaults()
        => new(StringComparer.Ordinal)
        {
            ["api_base_url"] = "https://www.ecomae.com",
            ["firebase_template"] = "",
            ["capacitor_version"] = "6",
            ["allow_push"] = true,
            ["default_deep_link_scheme"] = "epartscart://"
        };

    public static Dictionary<string, object?> EpcIntegrationsMobileConfig()
    {
        var cfg = EpcIntegrationsDefaultMobileConfig();
        foreach (var pair in MobileOverride)
        {
            cfg[pair.Key] = pair.Value;
        }

        return cfg;
    }

    public static string EpcStorefrontUrl()
    {
        var host = HasPortalHost ? PortalHost : HttpHost;
        host = Str(host).ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = "www." + host[4..];
        }

        return "https://" + host + "/en/";
    }

    public static void EpcCpPageFrameOpen(Dictionary<string, object?> opts)
    {
        var hero = opts.GetValueOrDefault("hero") as Dictionary<string, object?>;
        LastOutput += "FRAME_OPEN:"
            + Str(opts.GetValueOrDefault("class")) + "|"
            + Str(hero?.GetValueOrDefault("badge")) + "|"
            + Str(hero?.GetValueOrDefault("title"));
    }

    public static void EpcCpPageFrameClose()
        => LastOutput += "FRAME_CLOSE";

    public static string EpcMobileAppsPage()
    {
        if (IsAdmin?.Invoke() != true)
        {
            LastAjaxUrl = "";
            LastStorefrontUrl = "";
            LastOutput = DeniedAdmin;
            return LastOutput;
        }

        var isSuper = IsSuperCp?.Invoke() == true;
        var mobile = EpcIntegrationsMobileConfig();
        var defaults = EpcIntegrationsPlatformMobileDefaults();
        var backend = EpcIntBackend();
        LastAjaxUrl = "/" + backend + "/content/control/portal/ajax_integrations.php";
        LastStorefrontUrl = EpcStorefrontUrl();
        LastOutput = "";
        if (HasFrameOpen)
        {
            EpcCpPageFrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["class"] = "epc-mobile-apps",
                ["hero"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["badge"] = isSuper ? "Super CP" : "Tenant CP",
                    ["title"] = "Mobile apps (Android / iOS / PWA)"
                }
            });
        }

        LastOutput += RenderForm(isSuper, mobile, defaults, LastAjaxUrl, LastStorefrontUrl);
        if (HasFrameClose)
        {
            EpcCpPageFrameClose();
        }

        return LastOutput;
    }

    public static string RenderForm(
        bool isSuper,
        Dictionary<string, object?> mobile,
        Dictionary<string, object?> defaults,
        string ajaxUrl,
        string storefrontUrl)
    {
        var apiBase = PhpOr(mobile, "api_base_url", storefrontUrl);
        var scheme = PhpOr(mobile, "deep_link_scheme", Str(defaults.GetValueOrDefault("default_deep_link_scheme")));
        var html = "\n<div id=\"epc-mobile-flash\" class=\"alert\" style=\"display:none\"></div>\n"
            + "\n<form id=\"epc-mobile-form\" class=\"well\" data-ajax-url=\"" + EpcIntH(ajaxUrl) + "\">\n"
            + "\t<input type=\"hidden\" name=\"action\" value=\"save_mobile\">\n"
            + "\t<div class=\"checkbox\">\n"
            + "\t\t<label><input type=\"checkbox\" name=\"enabled\" value=\"1\" " + Checked(mobile, "enabled") + "> Mobile app enabled for this tenant</label>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>App display name</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"app_name\" value=\"" + EpcIntH(mobile.GetValueOrDefault("app_name")) + "\" placeholder=\"eParts Cart\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Bundle / package ID</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"bundle_id\" value=\"" + EpcIntH(mobile.GetValueOrDefault("bundle_id")) + "\" placeholder=\"com.epartscart.app\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-4\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Storefront / API base URL</label>\n"
            + "\t\t\t\t<input type=\"url\" class=\"form-control\" name=\"api_base_url\" value=\"" + EpcIntH(apiBase) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-4\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Deep link scheme</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"deep_link_scheme\" value=\"" + EpcIntH(scheme) + "\" placeholder=\"epartscart://\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-4\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Universal link domain</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"deep_link_domain\" value=\"" + EpcIntH(mobile.GetValueOrDefault("deep_link_domain")) + "\" placeholder=\"www.epartscart.com\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Google Play store URL</label>\n"
            + "\t\t\t\t<input type=\"url\" class=\"form-control\" name=\"play_store_url\" value=\"" + EpcIntH(mobile.GetValueOrDefault("play_store_url")) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Apple App Store URL</label>\n"
            + "\t\t\t\t<input type=\"url\" class=\"form-control\" name=\"app_store_url\" value=\"" + EpcIntH(mobile.GetValueOrDefault("app_store_url")) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n";
        if (isSuper)
        {
            html += "\t\t<div class=\"row\">\n"
                + "\t\t<div class=\"col-md-6\">\n"
                + "\t\t\t<div class=\"form-group\">\n"
                + "\t\t\t\t<label>Firebase project ID (push template)</label>\n"
                + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"firebase_project_id\" value=\"" + EpcIntH(mobile.GetValueOrDefault("firebase_project_id")) + "\">\n"
                + "\t\t\t</div>\n"
                + "\t\t</div>\n"
                + "\t\t<div class=\"col-md-6\">\n"
                + "\t\t\t<div class=\"checkbox\" style=\"margin-top:28px\">\n"
                + "\t\t\t\t<label><input type=\"checkbox\" name=\"push_enabled\" value=\"1\" " + Checked(mobile, "push_enabled") + "> Push notifications (FCM/APNs) enabled</label>\n"
                + "\t\t\t</div>\n"
                + "\t\t</div>\n"
                + "\t</div>\n";
        }

        html += "\t\t<div class=\"checkbox\">\n"
            + "\t\t<label><input type=\"checkbox\" name=\"pwa_enabled\" value=\"1\" " + Checked(mobile, "pwa_enabled") + "> PWA manifest + service worker on storefront</label>\n"
            + "\t</div>\n"
            + "\t<button type=\"submit\" class=\"btn btn-success\"><i class=\"fa fa-save\"></i> Save mobile settings</button>\n"
            + "</form>\n"
            + "\n<div class=\"panel panel-default\">\n"
            + "\t<div class=\"panel-heading\"><strong><i class=\"fa fa-book\"></i> Publish guide</strong></div>\n"
            + "\t<div class=\"panel-body\">\n"
            + "\t\t<p><strong>Android:</strong> <code>cd mobile/epartscart-app && npm install && npx cap sync && npx cap open android</code> — Build APK in Android Studio.</p>\n"
            + "\t\t<p><strong>iOS:</strong> Requires macOS + Xcode. Run <code>npx cap add ios</code> once, then <code>npx cap sync ios && npx cap open ios</code>.</p>\n"
            + "\t\t<p><strong>Connect to tenant:</strong> Set <em>API base URL</em> above to this storefront (e.g. " + EpcIntH(storefrontUrl) + "). Capacitor loads the live site in WebView.</p>\n"
            + "\t\t<p class=\"text-muted small\">See <code>docs/guides/MOBILE_APPS.md</code> and <code>docs/EPARTSCART-MOBILE-PHASE1.md</code>.</p>\n"
            + "\t</div>\n"
            + "</div>\n"
            + "\n";
        return html;
    }

    private static string Checked(Dictionary<string, object?> mobile, string key)
        => PhpEmpty(mobile.GetValueOrDefault(key)) ? "" : "checked";

    private static string PhpOr(Dictionary<string, object?> mobile, string key, string fallback)
        => PhpEmpty(mobile.GetValueOrDefault(key)) ? fallback : Str(mobile.GetValueOrDefault(key));

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d || (value is string s && (s == "" || s == "0"));

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
