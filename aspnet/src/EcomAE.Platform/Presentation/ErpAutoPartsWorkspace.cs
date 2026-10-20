namespace EcomAE.Platform.Presentation;

/// <summary>
/// The tab strip of the auto-parts workspace (/erp/autoparts/workspace).
/// Guide is the default first tab; every other tab is a work area filtered
/// by the top category + period bar.
/// </summary>
public static class ErpAutoPartsWorkspace
{
    public sealed record Tab(string Key, string Label, string Icon, string Href);

    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new(ErpAutoPartsGuide.TabKey, "Guide", "fa-map-signs", "/erp/autoparts/workspace?tab=guide"),
        new("catalogue", "Catalogue", "fa-cogs", "/erp/autoparts/workspace?tab=catalogue"),
        new("stock", "Stock", "fa-cubes", "/erp/autoparts/workspace?tab=stock"),
        new("purchases", "Purchases", "fa-shopping-cart", "/erp/autoparts/workspace?tab=purchases"),
        new("sales", "Sales", "fa-car", "/erp/autoparts/workspace?tab=sales"),
        new("invoices", "Invoices", "fa-file-text-o", "/erp/autoparts/workspace?tab=invoices"),
        new("month-end", "Month-end", "fa-calendar-check-o", "/erp/autoparts/workspace?tab=month-end"),
        new("reports", "Reports", "fa-bar-chart", "/erp/autoparts/workspace?tab=reports"),
    ];

    public static bool IsContentTab(string? key)
        => Tabs.Any(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? key)
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
           ?? "Guide";
}
