using System.Text.RegularExpressions;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The CP, ERP and Super CP top menus: every entry opens its own ASP.NET page (no two entries open the same data),
/// every destination exists, and every CP/ERP page is either in a menu or on the documented list below.
/// CP menus are built from the production menu snapshots (tenant and Super CP views).
/// </summary>
public sealed class TopMenuIntegrityTests
{
    /// <summary>Endpoints that are not Razor pages.</summary>
    private static readonly HashSet<string> NonPageDestinations = new(StringComparer.Ordinal) { "/brochure/cp" };

    /// <summary>
    /// CP/ERP pages that no top menu opens. Sign-in pages, dashboards and hubs reached from their parent page, plus
    /// pages whose PHP module has no menu row on the production sites. Placing one in a menu removes it from here.
    /// </summary>
    private static readonly string[] NotInTopMenu =
    [
        // Sign-in pages and the generic module host are not menu destinations.
        "CpLoginApp.razor", "ErpLoginApp.razor", "ErpModuleApp.razor",
        // Linked from other pages, not from the top menu.
        "CpDashboardSummaryApp.razor", "ErpDashboardSummaryApp.razor", "ErpNavigationCoverageApp.razor",
        "CpPriceListsApp.razor", "CpPricesEditApp.razor", "CpPoApprovalsApp.razor", "CpCrmActivitiesApp.razor",
        "CpAdminSessionsApp.razor", "CpPrintDocsApp.razor", "CpBulkUploadApp.razor", "CpFinanceCloseApp.razor",
        // No row in the production CP menu snapshots; open menu placement items in the tracker.
        "CpIntegrationsGuideApp.razor", "CpLifeOsClientsApp.razor", "CpLifeOsGuideApp.razor", "CpOpsGuidesApp.razor",
        "CpVinFieldsApp.razor", "CpWorkshopApp.razor", "ErpMultiEntityApp.razor", "ErpUserControlApp.razor",
    ];

    public static IEnumerable<object[]> Menus()
    {
        foreach (var (name, links) in AllMenus())
        {
            yield return [name];
        }
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void Every_entry_opens_its_own_page(string menu)
    {
        var links = AllMenus().Single(m => m.Name == menu).Links;
        Assert.NotEmpty(links);
        var shared = links
            .GroupBy(l => TopMenuLinks.DestinationKey(l.Href))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + " <= " + string.Join(", ", g.Select(l => l.Label)))
            .ToList();
        Assert.True(shared.Count == 0, string.Join("\n", shared));
    }

    [Theory]
    [MemberData(nameof(Menus))]
    public void Every_destination_exists(string menu)
    {
        var pages = RazorPages().SelectMany(p => p.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = AllMenus().Single(m => m.Name == menu).Links
            .Select(l => l.Href.Split('?', 2)[0].TrimEnd('/'))
            .Where(path => !pages.Contains(path) && !NonPageDestinations.Contains(path))
            .Distinct()
            .ToList();
        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }

    [Fact]
    public void Every_cp_and_erp_page_is_in_a_menu_or_documented()
    {
        var destinations = AllMenus()
            .SelectMany(m => m.Links)
            .Select(l => l.Href.Split('?', 2)[0].TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var outside = RazorPages()
            .Where(p => p.Value.Any(r => (r.StartsWith("/cp/", StringComparison.OrdinalIgnoreCase) || r.StartsWith("/erp/", StringComparison.OrdinalIgnoreCase)) && !r.Contains('{')))
            .Where(p => !p.Value.Any(destinations.Contains))
            .Select(p => p.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(NotInTopMenu.Order(StringComparer.Ordinal).ToArray(), outside);
    }

    [Fact]
    public void Proper_modules_for_previously_merged_entries()
    {
        Assert.Equal("/cp/price-management-app", CpNavTree.AspNetHref("/cp/shop/price-management"));
        Assert.Equal("/cp/guides-app?g=payments", CpNavTree.AspNetHref("/cp/shop/payments/payments/guide"));
        Assert.Equal("/erp/payroll-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/finance/erp?epc_erp_shell=1&area=payroll"));
    }

    private static List<(string Name, List<(string Label, string Href)> Links)> AllMenus()
    {
        var menus = new List<(string, List<(string, string)>)>();
        foreach (var file in Directory.GetFiles(Path.Combine(FindRepoRoot(), "content/files/epc_cache"), "epc_cp_menu_rows_v1_*.json").Order(StringComparer.Ordinal))
        {
            var rows = CpNavMenuService.ParseSnapshot(File.ReadAllText(file), Path.GetFileName(file));
            Assert.NotNull(rows);
            foreach (var (view, policy) in new[] { ("tenant", CpNavPolicy.Tenant()), ("super", CpNavPolicy.SuperOperator()) })
            {
                var groups = TopMenuLinks.DedupeCp(CpNavTree.Build(rows.Groups, rows.Items, policy, static _ => null));
                Assert.All(groups, g => Assert.NotEmpty(g.Items));
                menus.Add(($"CP {Path.GetFileNameWithoutExtension(file)} {view}",
                    groups.SelectMany(g => g.Items).Select(i => (i.Label, CpNavTree.AspNetHref(i.Url))).ToList()));
            }
        }

        foreach (var industry in new string?[] { null, "auto_parts", "jewellery", "fit_out" })
        {
            foreach (var super in new[] { false, true })
            {
                var nav = ErpIndustryNav.FilterTopnav(LegacyDesktopChromeCatalog.ErpTopnav(), new ErpIndustryNav.ErpNavAudience("current", industry, null, super));
                Assert.All(nav, g => Assert.NotEmpty(g.Links));
                menus.Add(($"ERP {industry ?? "core"} {(super ? "super" : "tenant")}",
                    nav.SelectMany(g => g.Columns ?? []).SelectMany(c => c.Tabs).Select(t => (t.Label, TopMenuLinks.ErpHref(t.Href))).ToList()));
            }
        }

        menus.Add(("Super CP command centre", PhpSuperCpBocNav.Nav().SelectMany(n => n.Areas).Select(a => (a.Label, a.Href)).ToList()));
        return menus;
    }

    private static Dictionary<string, List<string>> RazorPages()
    {
        var pages = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components"), "*.razor", SearchOption.AllDirectories))
        {
            var routes = Regex.Matches(File.ReadAllText(file), "^@page \"([^\"]+)\"", RegexOptions.Multiline).Select(m => m.Groups[1].Value.TrimEnd('/')).ToList();
            if (routes.Count > 0)
            {
                pages[Path.GetFileName(file)] = routes;
            }
        }

        return pages;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
