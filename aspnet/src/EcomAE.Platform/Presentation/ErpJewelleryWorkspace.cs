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
        new("indus", "INDUS migration", "fa-upload", "/erp/jewellery/workspace?tab=indus"),
    ];

    /// <summary>Quick links to every jewellery report and stock/finance page, shown on top of all jewellery screens.</summary>
    public static readonly IReadOnlyList<Tab> ReportLinks =
    [
        new("dashboard", "ERP dashboard", "fa-dashboard", "/erp"),
        new("metal-stock", "Metal stock", "fa-cubes", "/erp/jewellery-stock-verification-app?tab=jw_metal_stock"),
        new("stock-balance", "Stock balance (weight)", "fa-balance-scale", "/erp/jewellery-stock-verification-app?tab=jw_stock_balance"),
        new("stock-ledger", "Metal stock ledger", "fa-list-alt", "/erp/jewellery-stock-verification-app?tab=jw_stock_ledger"),
        new("metal-tb", "Metal trial balance", "fa-balance-scale", "/erp/jewellery-stock-verification-app?tab=jw_metal_tb"),
        new("stock-verification", "Stock verification", "fa-check-square-o", "/erp/jewellery-stock-verification-app?tab=jw_stock_verification"),
        new("metal-purchase", "Metal purchase", "fa-shopping-cart", "/erp/jewellery-fixing-app?tab=jw_metal_purchase"),
        new("sales-fixing", "Sales fixing", "fa-lock", "/erp/jewellery-fixing-app?tab=jw_sales_fixing"),
        new("metal-sales", "Metal sales", "fa-tags", "/erp/jewellery-retail-app?tab=jw_metal_sales"),
        new("sales-return", "Sales return", "fa-undo", "/erp/jewellery-retail-app?tab=jw_sales_return"),
        new("sales-analysis", "Sales analysis", "fa-line-chart", "/erp/jewellery-retail-app?tab=jw_sales_analysis"),
        new("repairs", "Repairs", "fa-wrench", "/erp/jewellery-repairs-app"),
        new("masters", "Masters", "fa-database", "/erp/jewellery-masters-app"),
        new("trial-balance", "Trial balance", "fa-table", "/erp/trial-balance-app"),
        new("balance-sheet", "Balance sheet", "fa-university", "/erp/report-center-app?tab=balance_sheet"),
        new("profit-loss", "Profit and loss", "fa-bar-chart", "/erp/report-center-app?tab=pl"),
        new("stock-movements", "Stock movements", "fa-exchange", "/erp/stock-movements-app"),
        new("gl-journals", "GL journals", "fa-book", "/erp/gl-journals-app"),
        new("print-designer", "Print designer", "fa-paint-brush", "/erp/print-designer-app"),
    ];

    public static bool IsContentTab(string? key)
        => Tabs.Any(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? key)
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
           ?? "Guide";
}
