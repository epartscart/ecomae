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
        "erp-jewellery-purchase.js",
    };

    public const string StylePrefix = "/platform-assets/css";

    public const string LoginBrandPrefix = "/platform-assets/login-brand";

    public const string PlatformLogoUrl = LoginBrandPrefix + "/ecomae-fze.png";

    // ECOM GLOBAL FZE LLC owns the ecomae.com brand; the licence line travels
    // with every "design and build by" credit across BOS / CP / ERP surfaces.
    public const string Attribution =
        "Design & Build by ecomae.com (Licensed under: ECOM GLOBAL FZE LLC — Registered) · www.ecomae.com";

    private static readonly HashSet<string> LoginBrandAllow = new(StringComparer.OrdinalIgnoreCase)
    {
        "alshamali.png",
        "ecomae-fze.png",
    };

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

        endpoints.MapGet(LoginBrandPrefix + "/{fileName}", (IWebHostEnvironment env, string fileName) =>
        {
            if (!LoginBrandAllow.Contains(fileName))
            {
                return Results.NotFound(new { ok = false, error = "unknown-login-brand-asset", file = fileName });
            }

            var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;
            var root = Path.GetFullPath(Path.Combine(webRoot, "login-brand"));
            var path = Path.GetFullPath(Path.Combine(root, fileName));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound(new { ok = false, error = "missing-login-brand-asset", file = fileName });
            }

            return Results.File(path, "image/png");
        });
    }
}
