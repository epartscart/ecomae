namespace EcomAE.Platform.Presentation;

/// <summary>D365 F&amp;O-style page search over ERP workspaces and industry modules.</summary>
public static class ErpGlobalSearchCatalog
{
    public sealed record Entry(string Label, string Href, string Group, string Icon);

    public static IReadOnlyList<Entry> Build(bool jewellery, bool fitOut, bool realEstate = false)
    {
        var rows = new List<Entry>(180);
        foreach (var tab in PhpModuleCatalog.ErpTabs.Concat(PhpModuleCatalog.AspNetErpTabs))
        {
            var href = PhpSurfaceLinkMap.AspNetPrimaryHref(tab.Href);
            if (string.IsNullOrWhiteSpace(href) || href.StartsWith("/php-reference/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new Entry(
                tab.Label,
                href,
                string.IsNullOrWhiteSpace(tab.Group) ? "ERP" : tab.Group,
                string.IsNullOrWhiteSpace(tab.Icon) ? "fa-th-large" : tab.Icon));
        }

        if (jewellery)
        {
            rows.InsertRange(0,
            [
                new("Jewellery retail", "/erp/jewellery-retail-app", "industry", "fa-diamond"),
                new("Repair jobs", "/erp/jewellery-repairs-app", "industry", "fa-wrench"),
                new("Gold rate board", "/erp/jewellery-masters-app?tab=gold_rate", "industry", "fa-line-chart"),
                new("Jewellery tags", "/erp/jewellery-masters-app?tab=jewellery_tag", "industry", "fa-tags"),
            ]);
        }

        if (fitOut)
        {
            rows.InsertRange(0,
            [
                new("Projects & cost control", "/erp/project-accounting-app", "industry", "fa-sitemap"),
                new("Site delivery dashboard", "/erp/fitout/delivery-dashboard", "industry", "fa-truck"),
                new("Project P&L & forecast", "/erp/fitout/executive-dashboard", "industry", "fa-line-chart"),
                new("Approval queue", "/erp/fitout/approval-queue", "industry", "fa-check-circle"),
            ]);
        }

        if (realEstate)
        {
            rows.InsertRange(0,
            [
                new("Real estate workspace", "/erp/real-estate-app", "industry", "fa-building"),
                new("Investment opportunities", "/erp/real-estate-app?tab=opportunities", "industry", "fa-lightbulb-o"),
                new("Property register", "/erp/real-estate-app?tab=properties", "industry", "fa-home"),
                new("Leases and collections", "/erp/real-estate-app?tab=leases", "industry", "fa-key"),
                new("Development projects", "/erp/real-estate-app?tab=projects", "industry", "fa-sitemap"),
                new("Contractor certificates", "/erp/real-estate-app?tab=construction", "industry", "fa-hard-hat"),
            ]);
        }

        return rows
            .GroupBy(e => e.Href, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
    }

    public static IReadOnlyList<Entry> Filter(IReadOnlyList<Entry> catalog, string? query, int take = 12)
    {
        var needle = (query ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return catalog.Take(take).ToArray();
        }

        return catalog
            .Where(e => e.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || e.Group.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToArray();
    }
}
