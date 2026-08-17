namespace EcomAE.Platform.Erp;

/// <summary>
/// Serves the ERP app front-end scripts. Platform does not call <c>UseStaticFiles</c>,
/// so wwwroot paths must be allowlisted here.
/// </summary>
public static class ErpAppAssets
{
    public const string PathPrefix = "/erp";

    private static readonly HashSet<string> ScriptAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "erp-sales-orders.js",
    };

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(PathPrefix + "/{fileName}.js", (IWebHostEnvironment env, string fileName) =>
        {
            var file = fileName + ".js";
            if (!ScriptAllow.Contains(file))
            {
                return Results.NotFound(new { ok = false, error = "unknown-erp-asset", file });
            }

            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;
            var root = Path.GetFullPath(Path.Combine(webRoot, "erp"));
            var path = Path.GetFullPath(Path.Combine(root, file));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound(new { ok = false, error = "missing-erp-asset", file });
            }

            return Results.File(path, "application/javascript; charset=utf-8");
        });
    }
}
