using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-hunt CP tenant email/SMTP page.
/// PHP identifiers kept for the inventory: <c>epc_int_h</c>, <c>epc_int_backend</c>,
/// <c>epc_integrations_feature_enabled</c>, <c>epc_integrations_load_tenant_config</c>,
/// <c>epc_auth_smtp_diagnose</c>, <c>epc_cp_page_frame_open</c>,
/// <c>epc_cp_page_frame_close</c>.
/// Path: <c>cp/content/control/portal/epc_tenant_email_settings.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique
/// page-frame parent, leftover unique user helper, leftover portal-db parent,
/// and Super-CP host check stay injected and are not named here.
/// </summary>
public static class PhpPlanQ1Hunt
{
    public const string TenantEmailSettingsPath = "cp/content/control/portal/epc_tenant_email_settings.php";
    public const string DeniedAdmin =
        "<div class=\"alert alert-warning\">Admin login required.</div>";
    public const string DisabledFeature =
        "<div class=\"alert alert-warning\">Email/SMTP is disabled for this tenant. Contact your platform operator.</div>";
    public const string EmDash = "\u2014";

    public static Func<bool>? IsAdmin { get; set; }
    public static Func<bool>? IsSuperCp { get; set; }
    public static Func<bool>? FeatureEnabled { get; set; }
    public static object? BackendDir { get; set; }
    public static Dictionary<string, object?> Integrations { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, object?> Diagnose { get; set; } = new(StringComparer.Ordinal);
    public static bool HasAdminEmail { get; set; } = true;
    public static string AdminEmail { get; set; } = "";
    public static bool HasFrameOpen { get; set; } = true;
    public static bool HasFrameClose { get; set; } = true;
    public static string LastOutput { get; private set; } = "";
    public static string LastAjaxUrl { get; private set; } = "";

    public static void Reset()
    {
        IsAdmin = null;
        IsSuperCp = null;
        FeatureEnabled = null;
        BackendDir = null;
        Integrations = new Dictionary<string, object?>(StringComparer.Ordinal);
        Diagnose = new Dictionary<string, object?>(StringComparer.Ordinal);
        HasAdminEmail = true;
        AdminEmail = "";
        HasFrameOpen = true;
        HasFrameClose = true;
        LastOutput = "";
        LastAjaxUrl = "";
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

    public static bool EpcIntegrationsFeatureEnabled(string featureKey)
        => FeatureEnabled?.Invoke() == true;

    public static Dictionary<string, object?> EpcIntegrationsLoadTenantConfig()
        => new(Integrations, StringComparer.Ordinal);

    public static Dictionary<string, object?> EpcAuthSmtpDiagnose()
        => new(Diagnose, StringComparer.Ordinal);

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

    public static string SuperBanner(string backend)
        => "<div class=\"alert alert-info\">Platform SMTP is configured under <a href=\"/"
            + backend
            + "/control/portal/epc_cp_auth_settings\">Modern auth settings</a>. This page is for tenant CP hosts.</div>";

    public static string EpcTenantEmailSettingsPage()
    {
        LastAjaxUrl = "";
        LastOutput = "";
        if (IsSuperCp?.Invoke() == true)
        {
            LastOutput += SuperBanner(EpcIntBackend());
        }

        if (IsAdmin?.Invoke() != true)
        {
            LastOutput += DeniedAdmin;
            return LastOutput;
        }

        if (!EpcIntegrationsFeatureEnabled("email_smtp"))
        {
            LastOutput += DisabledFeature;
            return LastOutput;
        }

        var integrations = EpcIntegrationsLoadTenantConfig();
        var smtp = integrations.GetValueOrDefault("smtp") as Dictionary<string, object?>
            ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var diag = EpcAuthSmtpDiagnose();
        var backend = EpcIntBackend();
        LastAjaxUrl = "/" + backend + "/content/control/portal/ajax_integrations.php";
        var adminEmail = HasAdminEmail ? AdminEmail : "";
        if (HasFrameOpen)
        {
            EpcCpPageFrameOpen(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["class"] = "epc-tenant-email",
                ["hero"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["badge"] = "Tenant CP",
                    ["title"] = "Email / SMTP settings"
                }
            });
        }

        LastOutput += RenderForm(smtp, diag, LastAjaxUrl, adminEmail);
        if (HasFrameClose)
        {
            EpcCpPageFrameClose();
        }

        return LastOutput;
    }

    public static string RenderForm(
        Dictionary<string, object?> smtp,
        Dictionary<string, object?> diag,
        string ajaxUrl,
        string adminEmail)
    {
        var source = Coalesce(diag.GetValueOrDefault("source"), EmDash);
        var diagHost = Str(diag.GetValueOrDefault("host"));
        var host = Coalesce(smtp.GetValueOrDefault("smtp_host"), diag.GetValueOrDefault("host"), "");
        var port = Coalesce(smtp.GetValueOrDefault("smtp_port"), diag.GetValueOrDefault("port"), "587");
        var enc = Str(Coalesce(smtp.GetValueOrDefault("smtp_encryption"), diag.GetValueOrDefault("encryption"), "tls"));
        var username = Coalesce(smtp.GetValueOrDefault("smtp_username"), "");
        var fromName = Coalesce(smtp.GetValueOrDefault("from_name"), diag.GetValueOrDefault("from_name"), "");
        var fromEmail = Coalesce(smtp.GetValueOrDefault("from_email"), diag.GetValueOrDefault("from_email"), "");
        var html = "\n<div id=\"epc-smtp-flash\" class=\"alert\" style=\"display:none\"></div>\n"
            + "\n<div class=\"well well-sm\">\n"
            + "\t<strong>Current transport:</strong> " + EpcIntH(source);
        if (!PhpEmpty(diagHost))
        {
            html += "\t — " + EpcIntH(diagHost) + ":" + EpcIntH(diag.GetValueOrDefault("port"));
        }
        else
        {
            html += "\t";
        }

        html += "</div>\n"
            + "\n<form id=\"epc-tenant-smtp-form\" class=\"well\" data-ajax-url=\"" + EpcIntH(ajaxUrl) + "\">\n"
            + "\t<input type=\"hidden\" name=\"action\" value=\"save_tenant_smtp\">\n"
            + "\t<div class=\"checkbox\">\n"
            + "\t\t<label><input type=\"checkbox\" name=\"use_tenant_smtp\" value=\"1\" " + Checked(smtp, "use_tenant_smtp") + "> Use tenant SMTP (override platform default)</label>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>SMTP host</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"smtp_host\" value=\"" + EpcIntH(host) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-3\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Port</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"smtp_port\" value=\"" + EpcIntH(port) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-3\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Encryption</label>\n"
            + "\t\t\t\t<select class=\"form-control\" name=\"smtp_encryption\">\n"
            + "\t\t\t\t\t" + EncOptions(enc) + "\t\t\t\t</select>\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Username</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"smtp_username\" value=\"" + EpcIntH(username) + "\" autocomplete=\"off\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>Password " + (!PhpEmpty(smtp.GetValueOrDefault("smtp_password")) ? "<span class=\"text-muted\">(saved)</span>" : "") + "</label>\n"
            + "\t\t\t\t<input type=\"password\" class=\"form-control\" name=\"smtp_password\" placeholder=\"Leave blank to keep current\" autocomplete=\"new-password\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<div class=\"row\">\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>From name</label>\n"
            + "\t\t\t\t<input type=\"text\" class=\"form-control\" name=\"from_name\" value=\"" + EpcIntH(fromName) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t\t<div class=\"col-md-6\">\n"
            + "\t\t\t<div class=\"form-group\">\n"
            + "\t\t\t\t<label>From email</label>\n"
            + "\t\t\t\t<input type=\"email\" class=\"form-control\" name=\"from_email\" value=\"" + EpcIntH(fromEmail) + "\">\n"
            + "\t\t\t</div>\n"
            + "\t\t</div>\n"
            + "\t</div>\n"
            + "\t<button type=\"submit\" class=\"btn btn-success\"><i class=\"fa fa-save\"></i> Save SMTP</button>\n"
            + "</form>\n"
            + "\n<form id=\"epc-tenant-smtp-test\" class=\"well\" style=\"max-width:520px\" data-ajax-url=\"" + EpcIntH(ajaxUrl) + "\">\n"
            + "\t<input type=\"hidden\" name=\"action\" value=\"test_tenant_smtp\">\n"
            + "\t<h4 style=\"margin-top:0\">Test send</h4>\n"
            + "\t<div class=\"form-group\">\n"
            + "\t\t<label>Send test to</label>\n"
            + "\t\t<input type=\"email\" class=\"form-control\" name=\"test_to\" value=\"" + EpcIntH(adminEmail) + "\" required>\n"
            + "\t</div>\n"
            + "\t<button type=\"submit\" class=\"btn btn-primary\"><i class=\"fa fa-paper-plane\"></i> Send test email</button>\n"
            + "</form>\n"
            + "\n<div class=\"panel panel-default\">\n"
            + "\t<div class=\"panel-heading\"><strong><i class=\"fa fa-book\"></i> Activate in 3 steps</strong></div>\n"
            + "\t<div class=\"panel-body\">\n"
            + "\t\t<ol>\n"
            + "\t\t\t<li><strong>Enable</strong> — Platform operator enables Email/SMTP for your tenant.</li>\n"
            + "\t\t\t<li><strong>Configure</strong> — Enter SMTP host, port, and credentials above (Gmail App Password, SendGrid, etc.).</li>\n"
            + "\t\t\t<li><strong>Test</strong> — Send test email, then try storefront OTP or CP login.</li>\n"
            + "\t\t</ol>\n"
            + "\t</div>\n"
            + "</div>\n"
            + "\n";
        return html;
    }

    private static string EncOptions(string enc)
    {
        var html = "";
        foreach (var opt in new[] { "tls", "ssl", "" })
        {
            var sel = enc == opt || (opt == "" && enc == "none") ? " selected" : "";
            var label = opt == "" ? "None" : opt.ToUpperInvariant();
            html += "<option value=\"" + EpcIntH(opt) + "\"" + sel + ">" + EpcIntH(label) + "</option>";
        }

        return html;
    }

    private static string Checked(Dictionary<string, object?> smtp, string key)
        => PhpEmpty(smtp.GetValueOrDefault(key)) ? "" : "checked";

    private static object? Coalesce(params object?[] values)
    {
        foreach (var value in values)
        {
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d || (value is string s && (s == "" || s == "0"));

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
