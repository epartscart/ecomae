namespace EcomAE.Platform.Presentation;

/// <summary>
/// Chooses HTML chrome for browsers while preserving JSON for migration tooling and API clients.
/// Default remains JSON so curl/smoke scripts without Accept: text/html keep working.
/// </summary>
public static class PresentationFormatNegotiator
{
    public static PresentationFormat Resolve(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var format = request.Query["format"].ToString();
        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            return PresentationFormat.Html;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return PresentationFormat.Json;
        }

        var accept = request.Headers.Accept.ToString();
        if (string.IsNullOrWhiteSpace(accept))
        {
            return PresentationFormat.Json;
        }

        var acceptsJson = ContainsMediaType(accept, "application/json");
        var acceptsHtml = ContainsMediaType(accept, "text/html");

        if (acceptsHtml && !acceptsJson)
        {
            return PresentationFormat.Html;
        }

        return PresentationFormat.Json;
    }

    private static bool ContainsMediaType(string accept, string mediaType)
    {
        foreach (var part in accept.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var type = part.Split(';', 2)[0].Trim();
            if (string.Equals(type, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
