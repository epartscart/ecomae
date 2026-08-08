namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative map of PHP <c>cp/content/*</c> top-level areas (non-shop) → ASP.NET CP apps.
/// Complements <see cref="CpShopModuleRouteMap"/> for full ePartsCart CP area coverage.
/// </summary>
public static class CpTopLevelAreaRouteMap
{
    private static readonly Dictionary<string, string> Areas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["content"] = "/cp/pages-app",
        ["control"] = "/cp/control",
        ["filemanager"] = "/cp/file-manager-app",
        ["lang"] = "/cp/languages-app",
        ["menu"] = "/cp/menus-app",
        ["modules_control"] = "/cp/modules-app",
        ["packs_control"] = "/cp/industry-packs-app",
        ["plugins_control"] = "/cp/plugins-manager-app",
        ["requests"] = "/cp/system-requests-app",
        ["templates_control"] = "/cp/templates-manager-app",
        ["users"] = "/cp/users-app",
        ["shop"] = "/cp", // hub; individual modules via CpShopModuleRouteMap
    };

    public static IReadOnlyDictionary<string, string> All => Areas;

    public static bool TryMap(string area, out string href)
        => Areas.TryGetValue(area.Trim(), out href!);

    public static object BuildCoverageReport()
    {
        var rows = Areas
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new
            {
                area = kv.Key,
                phpPath = "/CP/" + kv.Key,
                aspnetApp = kv.Value,
                liveMap = PhpSurfaceLinkMap.MapCpPhpPath("/CP/" + kv.Key),
            })
            .ToList();

        var resolved = rows.Count(r =>
            !string.Equals(r.liveMap, "/cp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.area, "shop", StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.area, "control", StringComparison.OrdinalIgnoreCase));

        return new
        {
            ok = true,
            surface = "cp",
            role = "cp-toplevel-area-coverage",
            host = "epartscart.com",
            areaCount = rows.Count,
            mappedCount = resolved,
            coveragePct = rows.Count == 0 ? 0 : (int)Math.Round(100.0 * resolved / rows.Count),
            cutoverAllowed = false,
            readyForPhpRemoval = false,
            phpAuthoritative = true,
            areas = rows,
            note = "Top-level cp/content areas resolve to ASP.NET-primary surfaces. Interactive writes remain PHP.",
        };
    }
}
