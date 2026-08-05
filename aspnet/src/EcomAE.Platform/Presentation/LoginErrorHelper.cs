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
            "login_backend_error" => "Login backend error (DB/session write). Check journalctl -u ecomae-platform and TenantRegistry connection.",
            "no_backend_access" => "Account lacks backend permissions.",
            "missing_fields" => "Enter login and password.",
            _ => "Incorrect login or password."
        };
    }
}
