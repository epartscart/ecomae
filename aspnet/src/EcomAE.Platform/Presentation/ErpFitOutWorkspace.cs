namespace EcomAE.Platform.Presentation;

/// <summary>
/// The tab strip of the fit-out workspace (/erp/fitout/workspace).
/// Guide is the default first tab; every other tab is a work area filtered
/// by the top project + period bar.
/// </summary>
public static class ErpFitOutWorkspace
{
    public sealed record Tab(string Key, string Label, string Icon, string Href);

    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new(ErpFitOutGuide.TabKey, "Guide", "fa-map-signs", "/erp/fitout/workspace?tab=guide"),
        new("projects", "Projects", "fa-sitemap", "/erp/fitout/workspace?tab=projects"),
        new("costing", "Costing", "fa-calculator", "/erp/fitout/workspace?tab=costing"),
        new("materials", "Materials", "fa-cubes", "/erp/fitout/workspace?tab=materials"),
        new("procurement", "Procurement", "fa-shopping-cart", "/erp/fitout/workspace?tab=procurement"),
        new("claims", "Claims", "fa-certificate", "/erp/fitout/workspace?tab=claims"),
        new("month-end", "Month-end", "fa-calendar-check-o", "/erp/fitout/workspace?tab=month-end"),
        new("reports", "Reports", "fa-bar-chart", "/erp/fitout/workspace?tab=reports"),
    ];

    public static bool IsContentTab(string? key)
        => Tabs.Any(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? key)
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
           ?? "Guide";
}
