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
        "erp-cash-entries.js",
        "erp-inventory-stock.js",
        "erp-purchase-orders.js",
        "erp-master-picker.js",
    };

    public const string StylePrefix = "/platform-assets/css";

    private static readonly HashSet<string> StyleAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "epc-enterprise-design-system.css",
        "erp-document-workspace.css",
        "erp-finance-voucher-workspace.css",
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

        endpoints.MapGet(StylePrefix + "/{fileName}.css", (IWebHostEnvironment env, string fileName) =>
        {
            var file = fileName + ".css";
            if (!StyleAllow.Contains(file))
            {
                return Results.NotFound(new { ok = false, error = "unknown-erp-asset", file });
            }

            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;
            var root = Path.GetFullPath(Path.Combine(webRoot, "css"));
            var path = Path.GetFullPath(Path.Combine(root, file));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound(new { ok = false, error = "missing-erp-asset", file });
            }

            return Results.File(path, "text/css; charset=utf-8");
        });
    }
}
