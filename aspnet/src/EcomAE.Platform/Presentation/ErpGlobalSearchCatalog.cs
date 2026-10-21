namespace EcomAE.Platform.Presentation;

/// <summary>D365 F&amp;O-style page search over ERP workspaces and industry modules.</summary>
public static class ErpGlobalSearchCatalog
{
    public sealed record Entry(string Label, string Href, string Group, string Icon, string Keywords = "");

    public static IReadOnlyList<Entry> Build(bool jewellery, bool fitOut, bool realEstate = false, bool autoParts = false)
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
                string.IsNullOrWhiteSpace(tab.Icon) ? "fa-th-large" : tab.Icon,
                tab.Description ?? string.Empty));
        }

        if (jewellery)
        {
            rows.InsertRange(0,
            [
                new("Jewellery retail", "/erp/jewellery-retail-app", "industry", "fa-diamond"),
                new("Repair jobs", "/erp/jewellery-repairs-app", "industry", "fa-wrench"),
                new("Gold rate board", "/erp/jewellery-masters-app?tab=gold_rate", "industry", "fa-line-chart"),
                new("Jewellery tags", "/erp/jewellery-masters-app?tab=jewellery_tag", "industry", "fa-tags"),
                new("Jewellery workspace & guide", "/erp/jewellery/workspace", "industry", "fa-map-signs"),
            ]);
        }

        if (autoParts)
        {
            rows.InsertRange(0,
            [
                new("Auto-parts workspace & guide", "/erp/autoparts/workspace", "industry", "fa-map-signs"),
                new("Parts catalogue", "/erp/autoparts/workspace?tab=catalogue", "industry", "fa-cogs"),
                new("Parts stock & movements", "/erp/autoparts/workspace?tab=stock", "industry", "fa-cubes"),
                new("Parts sales & invoices", "/erp/autoparts/workspace?tab=sales", "industry", "fa-car"),
            ]);
        }

        if (fitOut)
        {
            rows.InsertRange(0,
            [
                new("Fit-out workspace & guide", "/erp/fitout/workspace", "industry", "fa-map-signs"),
                new("Fit-out board pack", "/erp/fitout/workspace?tab=reports", "industry", "fa-bar-chart"),
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
                new("Landlord year book", "/erp/landlord-app?year=2025", "industry", "fa-calendar"),
                new("Investment opportunities", "/erp/real-estate-app?tab=opportunities", "industry", "fa-lightbulb-o"),
                new("Property register", "/erp/real-estate-app?tab=properties", "industry", "fa-home"),
                new("Leases and collections", "/erp/real-estate-app?tab=leases", "industry", "fa-key"),
                new("Development projects", "/erp/real-estate-app?tab=projects", "industry", "fa-sitemap"),
                new("Contractor certificates", "/erp/real-estate-app?tab=construction", "industry", "fa-hard-hat"),
                new("Unit sales and instalments", "/erp/real-estate-app?tab=sales", "industry", "fa-home"),
                new("Cheque and instalment calendars", "/erp/real-estate-app?tab=calendar", "industry", "fa-calendar"),
                new("PDC cheque register", "/erp/real-estate-app?tab=collections", "industry", "fa-money"),
                new("Real estate import and export", "/erp/real-estate-app?tab=import", "industry", "fa-file-excel-o"),
                new("Investment real estate report (PDF / Word)", "/erp/real-estate/report?format=html", "industry", "fa-file-pdf-o"),
            ]);
        }

        rows.Add(new("Tenant guide — organisation structure & how it works", "/erp/guide", "administration", "fa-map-signs"));
        rows.Add(new("Master lookup (customers, suppliers, items, projects)", "/erp/lookups?kind=customer", "masters", "fa-search"));
        rows.Add(new("Users & credentials (department users, login logs, audit)", "/erp/users-app", "administration", "fa-users"));

        return rows
            .GroupBy(e => e.Href, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToArray();
    }

    public static IReadOnlyList<Entry> Filter(IReadOnlyList<Entry> catalog, string? query, int take = 24)
    {
        var needle = (query ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return catalog.Take(take).ToArray();
        }

        var tokens = needle.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return catalog
            .Select(e => (Entry: e, Score: Score(e, needle, tokens)))
            .Where(row => row.Score >= 0)
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Entry.Label, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .Select(row => row.Entry)
            .ToArray();
    }

    private static int Score(Entry entry, string needle, string[] tokens)
    {
        var label = entry.Label ?? string.Empty;
        var group = entry.Group ?? string.Empty;
        var keywords = entry.Keywords ?? string.Empty;
        foreach (var token in tokens)
        {
            if (!Hits(label, token) && !Hits(group, token) && !Hits(keywords, token))
            {
                return -1;
            }
        }

        var score = Hits(label, needle) ? 100 : 0;
        foreach (var token in tokens)
        {
            if (Hits(label, token))
            {
                score += 10;
            }
            else if (Hits(keywords, token))
            {
                score += 4;
            }
            else
            {
                score += 1;
            }
        }

        return score;
    }

    private static bool Hits(string haystack, string token)
    {
        if (haystack.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return token.Equals("exempt", StringComparison.OrdinalIgnoreCase)
            && haystack.Contains("exemption", StringComparison.OrdinalIgnoreCase);
    }
}
