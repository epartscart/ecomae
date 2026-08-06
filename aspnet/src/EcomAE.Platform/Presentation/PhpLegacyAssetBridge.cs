namespace EcomAE.Platform.Presentation;

/// <summary>
/// Serves PHP chrome CSS/static files from the monorepo so ASP.NET shells get the same
/// presentation assets when nginx/PHP-FPM is not in front (local + Kestrel loopback).
/// URL shapes match <see cref="LegacyPresentationAssets"/> so markup does not change.
/// </summary>
public static class PhpLegacyAssetBridge
{
    private static readonly Dictionary<string, string> CssHelperMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/content/general_pages/epc_cp_ui_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_ui.css",
        ["/content/general_pages/epc_cp_professional_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_professional.css",
        ["/content/general_pages/epc_cp_density_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_density.css",
        ["/content/general_pages/epc_cp_tenant_polish_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_tenant_polish.css",
        ["/content/general_pages/epc_cp_storefront_topbar_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_storefront_topbar.css",
        ["/content/general_pages/epc_cp_command_dashboard_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_command_dashboard.css",
        ["/content/general_pages/epc_cp_login_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_login.css",
        ["/content/general_pages/epc_cp_login_hero_css.php"] = "cp/templates/bootstrap_admin/css/epc_cp_login_hero.css",
        ["/content/general_pages/epc_ecomae_hub_logo_css.php"] = "content/general_pages/epc_ecomae_hub_logo.css",
        ["/content/general_pages/epc_ecomae_platform_marketing_css.php"] = "content/general_pages/epc_ecomae_platform_marketing.css",
        ["/content/shop/finance/epc_erp_portal_css.php"] = "content/shop/finance/epc_erp_portal.css",
        ["/content/shop/finance/epc_erp_ui_css.php"] = "content/shop/finance/epc_erp_ui.css",
        ["/content/shop/finance/epc_erp_professional_css.php"] = "content/shop/finance/epc_erp_professional.css",
    };

    private static readonly string[] StaticAllowPrefixes =
    [
        "cp/",
        "bos/",
        "templates/",
        "content/",
        "modules/",
        "api/"
    ];

    public static void Map(IEndpointRouteBuilder endpoints, IWebHostEnvironment env)
    {
        var repoRoot = FindRepoRoot(env);

        endpoints.MapGet("/epc-static.php", async (HttpContext context) =>
        {
            var f = context.Request.Query["f"].ToString();
            if (string.IsNullOrWhiteSpace(f))
            {
                return Results.BadRequest("missing f");
            }

            f = f.Replace('\\', '/').TrimStart('/');
            if (f.Contains("..", StringComparison.Ordinal) || !StaticAllowPrefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return Results.NotFound();
            }

            var path = Path.GetFullPath(Path.Combine(repoRoot, f));
            if (!path.StartsWith(repoRoot, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound();
            }

            return Results.File(path, ContentTypeFor(path));
        });

        foreach (var (url, relative) in CssHelperMap)
        {
            var localRelative = relative;
            endpoints.MapGet(url, () =>
            {
                var path = Path.GetFullPath(Path.Combine(repoRoot, localRelative));
                if (!path.StartsWith(repoRoot, StringComparison.Ordinal) || !File.Exists(path))
                {
                    return Results.NotFound();
                }

                return Results.File(path, "text/css; charset=utf-8");
            });
        }

        // Animated logo CSS used by PhpEpartsCartAnimatedLogo.
        // Public path is stack-neutral (/platform-assets); keep legacy alias for old HTML.
        endpoints.MapGet("/platform-assets/eparts-animated-logo.css", () =>
            Results.Text(PhpEpartsCartLogoAssets.Css, "text/css; charset=utf-8"));
        endpoints.MapGet("/aspnet-php-assets/eparts-animated-logo.css", () =>
            Results.Text(PhpEpartsCartLogoAssets.Css, "text/css; charset=utf-8"));

        // Front-page catalog widget CSS referenced with PHP-identical URLs
        // (content/product_family_catalog.php links these directly).
        foreach (var cssName in new[]
                 {
                     "epc_vc_catalog.css",
                     "epc_car_mod_theme.css",
                     "epc_automotive_spareparts.css"
                 })
        {
            var rel = "content/general_pages/" + cssName;
            endpoints.MapGet("/content/general_pages/" + cssName, () =>
            {
                var path = Path.GetFullPath(Path.Combine(repoRoot, rel));
                if (path.StartsWith(repoRoot, StringComparison.Ordinal) && File.Exists(path))
                {
                    return Results.File(path, "text/css; charset=utf-8");
                }

                return Results.NotFound();
            });
        }

        // Static extract of site_professional_shell.php (preferred live URL).
        endpoints.MapGet("/content/general_pages/epc_storefront_professional_shell.css", () =>
        {
            var path = Path.GetFullPath(Path.Combine(repoRoot, "content/general_pages/epc_storefront_professional_shell.css"));
            if (path.StartsWith(repoRoot, StringComparison.Ordinal) && File.Exists(path))
            {
                return Results.File(path, "text/css; charset=utf-8");
            }

            return Results.NotFound();
        });

        // PHP helper URL — same bytes as the .css extract when present.
        endpoints.MapGet("/content/general_pages/epc_storefront_professional_shell_css.php", () =>
        {
            var staticPath = Path.GetFullPath(Path.Combine(repoRoot, "content/general_pages/epc_storefront_professional_shell.css"));
            if (staticPath.StartsWith(repoRoot, StringComparison.Ordinal) && File.Exists(staticPath))
            {
                return Results.File(staticPath, "text/css; charset=utf-8");
            }

            var path = Path.GetFullPath(Path.Combine(repoRoot, "content/general_pages/site_professional_shell.php"));
            if (!path.StartsWith(repoRoot, StringComparison.Ordinal) || !File.Exists(path))
            {
                return Results.NotFound();
            }

            var html = File.ReadAllText(path);
            var css = ExtractStyleBlocks(html);
            if (string.IsNullOrWhiteSpace(css))
            {
                return Results.NotFound();
            }

            return Results.Text(css, "text/css; charset=utf-8");
        });

        // Brand SVG mark — prefer file when present.
        endpoints.MapGet("/content/general_pages/epc_ecomae_logo_svg.php", () =>
        {
            foreach (var candidate in new[]
                     {
                         "content/general_pages/epc_ecomae_logo.svg",
                         "aspnet/src/EcomAE.Platform/wwwroot/assets/media/logos/ecomae_mark.svg"
                     })
            {
                var path = Path.GetFullPath(Path.Combine(repoRoot, candidate));
                if (path.StartsWith(repoRoot, StringComparison.Ordinal) && File.Exists(path))
                {
                    return Results.File(path, "image/svg+xml");
                }
            }

            return Results.Text(PhpEpartsCartLogoAssets.EcomaeMarkSvg, "image/svg+xml");
        });
    }

    private static string ContentTypeFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".css" => "text/css; charset=utf-8",
            ".js" => "application/javascript; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".woff2" => "font/woff2",
            ".woff" => "font/woff",
            ".ttf" => "font/ttf",
            ".map" => "application/json",
            _ => "application/octet-stream"
        };
    }

    /// <summary>Extract concatenated CSS from PHP template &lt;style&gt; blocks (skips scripts).</summary>
    public static string ExtractStyleBlocks(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            html,
            @"<style[^>]*>(.*?)</style>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (matches.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(matches.Count);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            parts.Add(match.Groups[1].Value);
        }

        return string.Join("\n", parts);
    }

    private static string FindRepoRoot(IWebHostEnvironment env)
    {
        var candidates = new List<string>();
        var envRoot = Environment.GetEnvironmentVariable("ECOMAE_PHP_SOURCE_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
        {
            candidates.Add(envRoot);
        }

        foreach (var start in new[] { env.ContentRootPath, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                candidates.Add(dir.FullName);
                dir = dir.Parent;
            }
        }

        // Live publish trees (/var/www/ecomae-aspnet/releases/<ts>/platform) never
        // contain the monorepo — fall back to the CloudPanel checkout locations.
        candidates.Add("/opt/ecomae-aspnet-source");
        candidates.Add("/root/ecomae");

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "aspnet", "EcomAE.AspNetCore.sln"))
                || File.Exists(Path.Combine(candidate, "content", "general_pages", "epc_cp_ui_css.php")))
            {
                return candidate;
            }
        }

        return env.ContentRootPath;
    }
}
