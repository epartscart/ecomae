using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// One top-menu entry per ASP.NET destination. PHP menus list modules by PHP URL; several of those URLs land on
/// the same ASP.NET page (the module is not split out yet, or the same module sits in two areas). Two entries that
/// open the same data are one entry: the first in menu order stays, later ones are dropped.
/// </summary>
public static class TopMenuLinks
{
    /// <summary>The ERP chrome's href for a PHP ERP tab (<c>PhpErpDesktopChrome.ModuleHref</c> without the company).</summary>
    public static string ErpHref(string? phpHref) =>
        ErpPhpTabRouteMap.PreferErpSurface(PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));

    /// <summary>Lower-case path without a trailing slash, plus the query without <c>company</c> (the chrome adds it).</summary>
    public static string DestinationKey(string? href)
    {
        var value = (href ?? string.Empty).Trim();
        var hash = value.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            value = value[..hash];
        }

        var q = value.IndexOf('?', StringComparison.Ordinal);
        var path = (q < 0 ? value : value[..q]).TrimEnd('/');
        if (path.Length == 0)
        {
            path = "/";
        }

        var query = q < 0
            ? string.Empty
            : string.Join('&', value[(q + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.Split('=', 2)[0].Equals("company", StringComparison.OrdinalIgnoreCase)));
        return (query.Length == 0 ? path : path + "?" + query).ToLowerInvariant();
    }

    public static IReadOnlyList<CpNavGroup> DedupeCp(IReadOnlyList<CpNavGroup> groups)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CpNavGroup>(groups.Count);
        foreach (var group in groups)
        {
            var items = group.Items.Where(i => seen.Add(DestinationKey(CpNavTree.AspNetHref(i.Url)))).ToList();
            if (items.Count > 0)
            {
                result.Add(group with { Items = items });
            }
        }

        return result;
    }

    public static IEnumerable<(PhpSuperCpBocNav.BocGroup Group, IReadOnlyList<PhpSuperCpBocNav.BocArea> Areas)> DedupeBoc(
        IEnumerable<(PhpSuperCpBocNav.BocGroup Group, IReadOnlyList<PhpSuperCpBocNav.BocArea> Areas)> nav)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (group, areas) in nav)
        {
            var kept = areas.Where(a => seen.Add(DestinationKey(a.Href))).ToList();
            if (kept.Count > 0)
            {
                yield return (group, kept);
            }
        }
    }
}
