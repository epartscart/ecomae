namespace EcomAE.Platform.Presentation;

public static class LoginErrorHelper
{
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
            "bridge_not_configured" => "ASP.NET login bridge is not configured. On the server run: ECOMAE_CONFIRM_SYNC_SECRET_SUCCESSION=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES bash scripts/cloudpanel_sync_secret_succession_from_php.sh — then use the same PHP admin email/password.",
            "login_backend_error" => "Login DB access denied. On server run: ECOMAE_CONFIRM_USE_PHP_DP_CONFIG_AS_TENANT_REGISTRY=YES ECOMAE_CONFIRM_RESTART_PLATFORM=YES bash scripts/cloudpanel_use_php_dp_config_as_tenant_registry.sh",
            "tenant_db_unbound" => "Shop database is not bound for this host — CP cannot verify accounts. Operator: ECOMAE_CONFIRM_FIX_EPARTSCART_PORTAL_TENANT_DB=YES bash scripts/cloudpanel_fix_epartscart_portal_tenant_db.sh then LIVE_PUBLISH_NOW / restart ecomae-platform.",
            "email_unconfirmed" => "Email/phone is not confirmed on this account.",
            "account_locked" => "This account is locked.",
            "no_backend_access" => "Account lacks backend permissions for Control Panel.",
            "missing_fields" => "Enter login and password.",
            _ => "Incorrect login or password. Use the tenant host where the account was registered (e.g. taxofinca.com vs epartscart.com). If PHP login works on this same host, operator must sync SecretSuccession + shop db_name."
        };
    }
}
