using EcomAE.Platform.Services;

namespace EcomAE.Platform.Presentation;

public static class LoginErrorHelper
{
    public const string ClientErpShopBackendHint =
        "Client ERP shop database is empty or unreachable. Super CP: Reset password or Repair shop login for this company. Super /cp and /erp stay on the platform DB.";

    public const string SuperHostBackendHint =
        "Login DB access denied on the platform database. Super /cp and /erp stay on the platform TenantRegistry.";

    public const string TenantHostBackendHint =
        "Login DB access denied. On the server bind this host to its shop database, then restart the platform.";

    public static string? FromUri(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return null;
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query);
        if (!query.TryGetValue("error", out var code) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return code.ToString() switch
        {
            "bridge_not_configured" => "Login bridge is not configured. On the server sync the shop secret and restart the platform, then use the same admin email/password.",
            "login_backend_error" => BackendMessage(parsed),
            "client_erp_not_found" => "No Client ERP company is registered for this URL. Super CP must onboard the ERP-only tenant first.",
            "tenant_db_unbound" => "Shop database is not bound for this host — CP cannot verify accounts. Operator: ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES bash scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh then LIVE_PUBLISH_NOW / restart ecomae-platform.",
            "email_unconfirmed" => "Email/phone is not confirmed on this account.",
            "account_locked" => "This account is locked.",
            "no_backend_access" => "Account lacks backend permissions for Control Panel.",
            "missing_fields" => "Enter login and password.",
            "rate_limited" => "Too many failed attempts. Please wait "
                + (query.TryGetValue("wait", out var wait) && int.TryParse(wait, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minutes) && minutes > 0 ? minutes : 15)
                + " minutes before trying again.",
            _ => "Incorrect login or password. Use the tenant host where the account was registered (e.g. taxofinca.com vs epartscart.com). If login works on this same host elsewhere, sync the shop secret and database name."
        };
    }

    public static string BackendMessage(Uri parsed)
    {
        if (ClientErpPath.IsClientErpPath(parsed.AbsolutePath))
        {
            return ClientErpShopBackendHint;
        }

        return PlatformHostPolicy.IsSuperCpHost(parsed.Host)
            ? SuperHostBackendHint
            : TenantHostBackendHint;
    }
}
