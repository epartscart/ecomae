namespace EcomAE.Platform.Presentation;

/// <summary>
/// The tab strip of the jewellery workspace (/erp/jewellery/workspace).
/// Guide is the default first tab; every other tab is a work area filtered
/// by the top metal/karat + period bar.
/// </summary>
public static class ErpJewelleryWorkspace
{
    public sealed record Tab(string Key, string Label, string Icon, string Href);

    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new(ErpJewelleryGuide.TabKey, "Guide", "fa-map-signs", "/erp/jewellery/workspace?tab=guide"),
        new("stock", "Stock", "fa-diamond", "/erp/jewellery/workspace?tab=stock"),
        new("purchases", "Purchases", "fa-shopping-cart", "/erp/jewellery/workspace?tab=purchases"),
        new("sales", "Sales", "fa-tags", "/erp/jewellery/workspace?tab=sales"),
        new("repairs", "Repairs", "fa-wrench", "/erp/jewellery/workspace?tab=repairs"),
        new("invoices", "Invoices", "fa-file-text-o", "/erp/jewellery/workspace?tab=invoices"),
        new("month-end", "Month-end", "fa-calendar-check-o", "/erp/jewellery/workspace?tab=month-end"),
        new("reports", "Reports", "fa-bar-chart", "/erp/jewellery/workspace?tab=reports"),
    ];

    public static bool IsContentTab(string? key)
        => Tabs.Any(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? key)
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
           ?? "Guide";
}
