using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP ERP tabs dumped onto one ASP.NET app (by <see cref="ErpPhpTabRouteMap"/>).
/// Used so thin CP/ERP pages honor <c>?tab=</c> and show PHP columns/forms.
/// </summary>
public static class PhpParityDumpCatalog
{
    public static IReadOnlyList<(string Key, string Label)> TabsForPath(string? appPath)
    {
        var path = NormalizePath(appPath);
        if (path.Length == 0)
        {
            return [("list", "Records")];
        }

        var tabs = ErpPhpTabRouteMap.All
            .Where(kv => NormalizePath(kv.Value) == path)
            .Select(kv => (kv.Key, LabelFor(kv.Key)))
            .GroupBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(t => t.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tabs.Count == 0 ? [("list", "Records")] : tabs;
    }

    public static string DefaultTab(string? appPath, string? requested)
    {
        var tabs = TabsForPath(appPath);
        var key = (requested ?? string.Empty).Trim();
        if (key.Length > 0 && tabs.Any(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return tabs.First(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)).Key;
        }

        return tabs[0].Key;
    }

    public static bool HasStaffAccess(LegacySessionContext session)
        => session.Kind == LegacySessionKind.Admin
           && (session.Capabilities.Contains("erp") || session.Capabilities.Contains("cp"));

    public static string LabelFor(string tab)
        => ErpPhpModuleChromeCatalog.ForTab(tab).Title;

    public static string NormalizePath(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return string.Empty;
        var path = href.Trim();
        var q = path.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0) path = path[..q];
        if (path.Length > 1) path = path.TrimEnd('/');
        return path;
    }
}
