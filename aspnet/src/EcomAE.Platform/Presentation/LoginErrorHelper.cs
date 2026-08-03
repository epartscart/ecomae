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
            "bridge_not_configured" => "ASP.NET login bridge is not configured. Use the PHP login, or set EcomAE__SecretSuccession.",
            "no_backend_access" => "Account lacks backend permissions.",
            "missing_fields" => "Enter login and password.",
            _ => "Incorrect login or password."
        };
    }
}
