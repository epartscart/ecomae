using EcomAE.Platform.Cp;
using EcomAE.Platform.Services;

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

    /// <summary>
    /// Tenant host view: drop Super-only, BOS, jewellery-pack, other-industry and ERP-window
    /// destinations so the live CP top menu only keeps links that belong on that shop.
    /// Super hosts keep Super-CP operator tools — not tenant shop desks and not ERP modules.
    /// </summary>
    public static IReadOnlyList<CpNavGroup> ForHost(IReadOnlyList<CpNavGroup> groups, string? host)
    {
        var super = PlatformHostPolicy.IsSuperCpHost(host);
        var industry = ErpHostContext.Resolve(host).IndustryCode;
        var jewellery = ErpIndustryNav.ShowJewelleryModules(industry, company: null);
        var construction = IsConstructionIndustry(industry);

        var result = new List<CpNavGroup>(groups.Count);
        foreach (var group in groups)
        {
            var items = group.Items.Where(item =>
            {
                var href = CpNavTree.AspNetHref(item.Url);
                if (!super && LegacyDesktopChromeCatalog.IsSuperOnlyCpLink(href, group.Caption))
                {
                    return false;
                }

                if (!super && IsTenantHiddenHref(href))
                {
                    return false;
                }

                if (!jewellery && LegacyDesktopChromeCatalog.IsJewelleryHrefOrLabel(href, item.Label))
                {
                    return false;
                }

                if (!construction && IsConstructionHref(href, item.Label))
                {
                    return false;
                }

                return true;
            }).ToList();

            if (items.Count > 0)
            {
                result.Add(group with { Items = items });
            }
        }

        return SurfaceChromePolicy.KeepCpSurface(result, super);
    }

    private static bool IsTenantHiddenHref(string href)
    {
        var h = href ?? string.Empty;
        return h.StartsWith("/bos", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/tenant-hub-app", StringComparison.OrdinalIgnoreCase)
            || h.Contains("/cp/lifeos-", StringComparison.OrdinalIgnoreCase)
            || h.Contains("lifeos", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConstructionIndustry(string? industry)
        => industry is "construction" or "fit_out" or "fitout" or "projects";

    private static bool IsConstructionHref(string? href, string? label)
    {
        var h = href ?? string.Empty;
        var l = label ?? string.Empty;
        return h.Contains("project-accounting", StringComparison.OrdinalIgnoreCase)
            || h.Contains("contracts-app", StringComparison.OrdinalIgnoreCase)
            || l.Contains("Project accounting", StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<(PhpSuperCpBocNav.BocGroup Group, IReadOnlyList<PhpSuperCpBocNav.BocArea> Areas)> DedupeBoc(
        IEnumerable<(PhpSuperCpBocNav.BocGroup Group, IReadOnlyList<PhpSuperCpBocNav.BocArea> Areas)> nav,
        bool pathOnly = false)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (group, areas) in nav)
        {
            var kept = areas.Where(a =>
            {
                var key = DestinationKey(a.Href);
                if (pathOnly)
                {
                    var q = key.IndexOf('?', StringComparison.Ordinal);
                    if (q >= 0)
                    {
                        key = key[..q];
                    }
                }

                return seen.Add(key);
            }).ToList();
            if (kept.Count > 0)
            {
                yield return (group, kept);
            }
        }
    }
}
