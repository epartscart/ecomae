namespace EcomAE.Platform.Presentation;

/// <summary>
/// The tab strip shared by the property register and the year book.
/// Year book sits with Properties so the income and expense page is one of those tabs.
/// </summary>
public static class ErpRealEstateWorkspace
{
    public sealed record Tab(string Key, string Label, string Icon, string Href);

    public const string YearBookKey = "yearbook";

    public static readonly IReadOnlyList<Tab> Tabs =
    [
        new(ErpRealEstateGuide.TabKey, "Guide", "fa-map-signs", "/erp/real-estate-app?tab=guide"),
        new("dashboard", "Dashboard", "fa-dashboard", "/erp/real-estate-app?tab=dashboard"),
        new("opportunities", "Opportunities", "fa-lightbulb-o", "/erp/real-estate-app?tab=opportunities"),
        new("properties", "Properties", "fa-building", "/erp/real-estate-app?tab=properties"),
        new(YearBookKey, "Year book", "fa-calendar", "/erp/landlord-app?year=2025"),
        new("acquisitions", "Buy / sell", "fa-exchange", "/erp/real-estate-app?tab=acquisitions"),
        new("valuations", "Valuation", "fa-line-chart", "/erp/real-estate-app?tab=valuations"),
        new("leases", "Tenants & leases", "fa-key", "/erp/real-estate-app?tab=leases"),
        new("expiry", "Expiry email", "fa-envelope", "/erp/real-estate-app?tab=expiry"),
        new("collections", "Rent & cheques", "fa-money", "/erp/real-estate-app?tab=collections"),
        new("maintenance", "Maintenance", "fa-wrench", "/erp/real-estate-app?tab=maintenance"),
        new("projects", "Development", "fa-sitemap", "/erp/real-estate-app?tab=projects"),
        new("sales", "Unit sales", "fa-home", "/erp/real-estate-app?tab=sales"),
        new("construction", "Contracts", "fa-hard-hat", "/erp/real-estate-app?tab=construction"),
        new("calendar", "Calendars", "fa-calendar", "/erp/real-estate-app?tab=calendar"),
        new("documents", "Documents", "fa-folder-open", "/erp/real-estate-app?tab=documents"),
        new("import", "Import / export", "fa-file-excel-o", "/erp/real-estate-app?tab=import"),
        new("alerts", "Alerts", "fa-bell", "/erp/real-estate-app?tab=alerts"),
        new("reports", "Reports", "fa-bar-chart", "/erp/real-estate-app?tab=reports"),
    ];

    public static bool IsContentTab(string? key)
        => Tabs.Any(tab =>
            !string.Equals(tab.Key, YearBookKey, StringComparison.OrdinalIgnoreCase)
            && string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string Label(string? key)
        => Tabs.FirstOrDefault(tab => string.Equals(tab.Key, key, StringComparison.OrdinalIgnoreCase))?.Label
           ?? "Dashboard";
}
