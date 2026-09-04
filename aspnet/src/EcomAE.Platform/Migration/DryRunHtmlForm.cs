using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Migration;

/// <summary>HTML form POST → dry-run evaluate → redirect with <c>?ok=</c> / <c>?err=</c>.</summary>
public static class DryRunHtmlForm
{
    public static string SafeReturnUrl(HttpRequest request, string fallback)
    {
        var raw = Read(request, "returnUrl");
        if (string.IsNullOrWhiteSpace(raw)) raw = Read(request, "return_url");
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        raw = raw.Trim();
        if (!raw.StartsWith('/') || raw.StartsWith("//", StringComparison.Ordinal) || raw.Contains("://", StringComparison.Ordinal))
            return fallback;
        return raw;
    }

    public static string Read(HttpRequest request, string key)
    {
        if (request.HasFormContentType)
        {
            var form = request.Form[key].ToString();
            if (!string.IsNullOrWhiteSpace(form)) return form;
        }

        return request.Query[key].ToString();
    }

    public static IResult Redirect(string returnUrl, bool ok, string message)
    {
        var sep = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var key = ok ? "ok" : "err";
        return Results.Redirect(returnUrl + sep + key + "=" + Uri.EscapeDataString(message ?? string.Empty));
    }
}
