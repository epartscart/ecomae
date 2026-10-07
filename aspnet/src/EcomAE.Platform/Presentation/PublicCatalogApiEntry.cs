using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>/api/v1/catalog</c> and <c>/api/v1/catalog.php?action=</c>.
/// Known actions are rewritten onto the existing <c>/api/v1/catalog/{action}</c> routes.
/// A missing action is PHP's 400 <c>missing_action</c>, not a 404.
/// </summary>
public static class PublicCatalogApiEntry
{
    public const string MissingActionCode = "missing_action";
    public const string MissingActionMessage = "Query param action is required (e.g. manufacturers, vin, status).";
    public const string UnknownActionMessage = "Unknown catalog action.";

    private static readonly Dictionary<string, string> ActionRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["status"] = "status",
        ["manufacturers"] = "manufacturers",
        ["models"] = "models",
        ["modifications"] = "modifications",
        ["categories"] = "categories",
        ["products"] = "products",
        ["articles"] = "articles",
        ["article"] = "article",
        ["analogs"] = "analogs",
        ["brands"] = "brands",
        ["vin"] = "vin",
        ["engines"] = "engines",
        ["engine"] = "engine",
        ["engine_search"] = "engine-search",
        ["article_links"] = "article-links",
        ["suppliers"] = "suppliers",
        ["brand_parts"] = "brand-parts",
    };

    public static bool IsEntryPath(string? path)
    {
        var value = PublicAnonymousPages.NormalizePath(path);
        return value.Equals("/api/v1/catalog", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/v1/catalog.php", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryMapAction(string? action, out string routeSlug)
    {
        routeSlug = string.Empty;
        var key = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0 || !ActionRoutes.TryGetValue(key, out var slug))
        {
            return false;
        }

        routeSlug = slug;
        return true;
    }

    public static bool TryRewriteToExistingRoute(HttpContext context)
    {
        if (!IsEntryPath(context.Request.Path.Value))
        {
            return false;
        }

        var action = context.Request.Query["action"].ToString();
        if (!TryMapAction(action, out var slug))
        {
            return false;
        }

        context.Request.Path = "/api/v1/catalog/" + slug;
        return true;
    }

    public static string ErrorJson(string code, string message)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = false,
            ["error"] = new Dictionary<string, string>
            {
                ["code"] = code,
                ["message"] = message,
            },
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
}

/// <summary>PHP <c>epc_api_clients_json_error</c> adds <c>X-ECOM-API-Client: 1</c>.</summary>
public sealed class CatalogClientJsonResult(int status, string body) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/json; charset=utf-8";
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers["X-ECOM-API-Client"] = "1";
        await httpContext.Response.WriteAsync(body).ConfigureAwait(false);
    }
}
