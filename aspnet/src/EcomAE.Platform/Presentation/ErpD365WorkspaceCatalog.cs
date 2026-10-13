namespace EcomAE.Platform.Presentation;

/// <summary>
/// Union of Dynamics-style workspace catalogs (part 1 finance/ops trees + part 3
/// leave/planning/equipment/organization/payroll/procurement).
/// </summary>
public static class ErpD365WorkspaceCatalog
{
    public const string SavePath = ErpD365Part3Catalog.SavePath;

    public static readonly IReadOnlyList<ErpD365Part3Catalog.Page> All =
        ErpD365Part1Catalog.All.Concat(ErpD365Part3Catalog.All).ToList();

    public static IReadOnlyList<ErpD365Part3Catalog.Page> ForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return All;
        }

        return All.Where(p => p.Area.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static bool TryGet(string? tab, out ErpD365Part3Catalog.Page page)
    {
        if (ErpD365Part1Catalog.TryGet(tab, out page))
        {
            return true;
        }

        return ErpD365Part3Catalog.TryGet(tab, out page);
    }

    public static bool TryMapTab(string? tab, out string href)
    {
        if (ErpD365Part1Catalog.TryMapTab(tab, out href))
        {
            return true;
        }

        return ErpD365Part3Catalog.TryMapTab(tab, out href);
    }

    public static string AppPathForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "leave_abs" or "master_planning_area" or "mhei" or "enterprise" or "payroll_area" or "purchasing"
                => ErpD365Part3Catalog.AppPathForArea(key),
            _ => ErpD365Part1Catalog.AppPathForArea(key),
        };
    }

    public static string DefaultTab(string? area)
        => ForArea(area).FirstOrDefault()?.Tab ?? "ap_invoice_journal";

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> AsModuleLinks()
        => ErpD365Part1Catalog.AsModuleLinks().Concat(ErpD365Part3Catalog.AsModuleLinks()).ToList();
}
