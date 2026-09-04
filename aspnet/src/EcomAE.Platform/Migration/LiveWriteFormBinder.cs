using System.Globalization;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Binds native SSR forms (or JSON) onto live write endpoints and redirects HTML posters
/// back to the originating app. JSON clients keep the existing envelope.
/// </summary>
public static class LiveWriteFormBinder
{
    public static bool WantsHtml(HttpContext context)
    {
        if (context.Request.HasFormContentType)
        {
            return true;
        }

        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)
               && !accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    public static string ReturnUrl(HttpContext context, string fallback)
    {
        var raw = context.Request.HasFormContentType
            ? context.Request.Form["returnUrl"].ToString()
            : context.Request.Query["returnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = fallback;
        }

        return raw.StartsWith('/') && !raw.StartsWith("//", StringComparison.Ordinal) ? raw : fallback;
    }

    public static bool Flag(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = form[name].ToString().Trim();
            if (raw is "1" or "true" or "True" or "on" or "yes")
            {
                return true;
            }
        }

        return false;
    }

    public static long Long(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (long.TryParse(form[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    public static int Int(IFormCollection form, params string[] names)
        => (int)Math.Clamp(Long(form, names), int.MinValue, int.MaxValue);

    public static decimal Dec(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            if (decimal.TryParse(form[name].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    public static string Text(IFormCollection form, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = form[name].ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
        }

        return string.Empty;
    }

    public static IResult Complete(
        HttpContext context,
        string fallbackReturnUrl,
        bool ok,
        string message,
        object json,
        int failStatus = StatusCodes.Status400BadRequest)
    {
        if (WantsHtml(context))
        {
            var dest = ReturnUrl(context, fallbackReturnUrl);
            var sep = dest.Contains('?', StringComparison.Ordinal) ? '&' : '?';
            var key = ok ? "ok" : "err";
            return Results.Redirect(dest + sep + key + "=" + Uri.EscapeDataString(message ?? string.Empty));
        }

        return Results.Json(json, statusCode: ok ? StatusCodes.Status200OK : failStatus);
    }

    public static IResult LoginRedirect(HttpContext context, string loginPath, string jsonMessage)
    {
        if (WantsHtml(context))
        {
            return Results.Redirect(loginPath);
        }

        return Results.Json(
            new { ok = false, error = new { code = "unauthorized", message = jsonMessage } },
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
