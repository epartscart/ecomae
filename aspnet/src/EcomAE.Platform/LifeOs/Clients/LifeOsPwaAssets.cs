namespace EcomAE.Platform.LifeOs.Clients;

/// <summary>
/// Serves LifeOS PWA shell files. Platform does not call <c>UseStaticFiles</c>,
/// so wwwroot paths must be allowlisted here.
/// </summary>
public static class LifeOsPwaAssets
{
    public const string ManifestPath = "/lifeos/manifest.webmanifest";
    public const string ServiceWorkerPath = "/lifeos/sw.js";
    public const string JoinScriptPath = "/lifeos/join.js";
    public const string CompanionScriptPath = "/lifeos/companion.js";
    public const string ResultsScriptPath = "/lifeos/results.js";
    public const string ProductCssPath = "/lifeos/lifeos-product.css";
    public const string IconPrefix = "/lifeos/icons";

    private static readonly HashSet<string> IconAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifeos-pwa-192.svg",
        "lifeos-pwa-512.svg",
    };

    public static void Map(IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        endpoints.MapGet(ManifestPath, (IWebHostEnvironment e) =>
            FileResult(e, "manifest.webmanifest", "application/manifest+json"));

        endpoints.MapGet(ServiceWorkerPath, (IWebHostEnvironment e) =>
            FileResult(e, "sw.js", "application/javascript; charset=utf-8"));

        endpoints.MapGet(JoinScriptPath, (IWebHostEnvironment e) =>
            FileResult(e, "join.js", "application/javascript; charset=utf-8"));

        endpoints.MapGet(CompanionScriptPath, (IWebHostEnvironment e) =>
            FileResult(e, "companion.js", "application/javascript; charset=utf-8"));

        endpoints.MapGet(ResultsScriptPath, (IWebHostEnvironment e) =>
            FileResult(e, "results.js", "application/javascript; charset=utf-8"));

        // Shared editorial chrome CSS (query string cache-bust ok — MapGet is prefix-exact enough).
        endpoints.MapGet(ProductCssPath, (IWebHostEnvironment e) =>
            FileResult(e, "lifeos-product.css", "text/css; charset=utf-8"));

        endpoints.MapGet(IconPrefix + "/{fileName}", (IWebHostEnvironment e, string fileName) =>
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || fileName.Contains('/')
                || fileName.Contains('\\')
                || fileName.Contains("..", StringComparison.Ordinal)
                || !IconAllow.Contains(fileName))
            {
                return Results.NotFound(new { ok = false, error = "unknown-lifeos-icon" });
            }

            return FileResult(e, Path.Combine("icons", fileName), ContentType(fileName));
        });
    }

    private static IResult FileResult(
        IWebHostEnvironment env,
        string relativeUnderLifeOs,
        string contentType)
    {
        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var path = Path.GetFullPath(Path.Combine(webRoot, "lifeos", relativeUnderLifeOs));
        var root = Path.GetFullPath(Path.Combine(webRoot, "lifeos"));
        if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
        {
            return Results.NotFound(new { ok = false, error = "missing-lifeos-pwa-asset", file = relativeUnderLifeOs });
        }

        return Results.File(path, contentType);
    }

    private static string ContentType(string fileName) =>
        fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? "image/svg+xml"
            : "application/octet-stream";
}
