using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIndustryWorkspacesTests
{
    [Fact]
    public void AutoPartsGuideIsFirstTabAndCoversEveryOtherTabExactlyOnce()
    {
        Assert.Equal("guide", ErpAutoPartsWorkspace.Tabs[0].Key);
        Assert.Equal("guide", ErpAutoPartsGuide.Normalize(""));
        Assert.Equal("guide", ErpAutoPartsGuide.Normalize("nonexistent"));
        Assert.Equal("catalogue", ErpAutoPartsGuide.Normalize("catalogue"));

        var covered = ErpAutoPartsGuide.Steps.Select(step => step.Key).ToList();
        Assert.Equal(
            ErpAutoPartsWorkspace.Tabs.Where(tab => tab.Key != "guide").Select(tab => tab.Key).OrderBy(key => key, StringComparer.Ordinal),
            covered.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(Enumerable.Range(1, covered.Count), ErpAutoPartsGuide.Steps.Select(step => step.Number));
    }

    [Fact]
    public void JewelleryGuideIsFirstTabAndCoversEveryOtherTabExactlyOnce()
    {
        Assert.Equal("guide", ErpJewelleryWorkspace.Tabs[0].Key);
        Assert.Equal("guide", ErpJewelleryGuide.Normalize(""));
        Assert.Equal("guide", ErpJewelleryGuide.Normalize("nonexistent"));
        Assert.Equal("stock", ErpJewelleryGuide.Normalize("stock"));

        var covered = ErpJewelleryGuide.Steps.Select(step => step.Key).ToList();
        Assert.Equal(
            ErpJewelleryWorkspace.Tabs.Where(tab => tab.Key != "guide").Select(tab => tab.Key).OrderBy(key => key, StringComparer.Ordinal),
            covered.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(Enumerable.Range(1, covered.Count), ErpJewelleryGuide.Steps.Select(step => step.Number));
    }

    [Fact]
    public void WorkspacePagesTabsBoardAndFilterBarAreWired()
    {
        var ap = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpAutoPartsWorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/autoparts/workspace\"", ap, StringComparison.Ordinal);
        Assert.Contains("ErpAutoPartsWorkspaceTabs", ap, StringComparison.Ordinal);
        Assert.Contains("ErpAutoPartsGuideBoard", ap, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", ap, StringComparison.Ordinal);
        Assert.Contains("name=\"period\"", ap, StringComparison.Ordinal);
        Assert.Contains("Load sample data", ap, StringComparison.Ordinal);
        Assert.Contains("Remove sample data", ap, StringComparison.Ordinal);
        Assert.Contains("demo-", ap, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", ap, StringComparison.Ordinal);
        Assert.Contains("CpCsrfGuard.FieldName", ap, StringComparison.Ordinal);

        var jw = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpJewelleryWorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/jewellery/workspace\"", jw, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryWorkspaceTabs", jw, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryGuideBoard", jw, StringComparison.Ordinal);
        Assert.Contains("name=\"metal\"", jw, StringComparison.Ordinal);
        Assert.Contains("name=\"period\"", jw, StringComparison.Ordinal);
        Assert.Contains("Load sample data", jw, StringComparison.Ordinal);
        Assert.Contains("Remove sample data", jw, StringComparison.Ordinal);
        Assert.Contains("demo-", jw, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", jw, StringComparison.Ordinal);
        Assert.Contains("CpCsrfGuard.FieldName", jw, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesHandlersNavAndDiAreRegisteredForBothWorkspaces()
    {
        var routes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        foreach (var path in new[] { "demo-seed", "demo-clear", "gl-post" })
        {
            Assert.Contains("/erp/autoparts/" + path, routes, StringComparison.Ordinal);
            Assert.Contains("/erp/jewellery/" + path, routes, StringComparison.Ordinal);
        }

        Assert.Contains("/erp/autoparts/issue-invoice", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/jewellery/issue-invoice", routes, StringComparison.Ordinal);

        var module = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        foreach (var handler in new[]
                 {
                     "HandleAutoPartsDemoSeedAsync", "HandleAutoPartsDemoClearAsync",
                     "HandleAutoPartsGlPostAsync", "HandleAutoPartsIssueInvoiceAsync",
                     "HandleJewelleryDemoSeedAsync", "HandleJewelleryDemoClearAsync",
                     "HandleJewelleryGlPostAsync", "HandleJewelleryIssueInvoiceAsync",
                     "ErpAutoPartsDemoSeed", "ErpAutoPartsDemoClear", "ErpAutoPartsGlPost", "ErpAutoPartsIssueInvoice",
                     "ErpJewelleryDemoSeed", "ErpJewelleryDemoClear", "ErpJewelleryGlPost", "ErpJewelleryIssueInvoice",
                 })
        {
            Assert.Contains(handler, module, StringComparison.Ordinal);
        }

        var program = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        foreach (var service in new[]
                 {
                     "IErpAutoPartsWorkspaceReadService", "IErpAutoPartsDemoSeedWriteService",
                     "IErpAutoPartsGlPostWriteService", "IErpAutoPartsInvoiceWriteService",
                     "IErpJewelleryWorkspaceReadService", "IErpJewelleryDemoSeedWriteService",
                     "IErpJewelleryGlPostWriteService", "IErpJewelleryInvoiceWriteService",
                 })
        {
            Assert.Contains(service, program, StringComparison.Ordinal);
        }

        var dashboard = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("/erp/autoparts/workspace", dashboard, StringComparison.Ordinal);
        Assert.Contains("/erp/jewellery/workspace", dashboard, StringComparison.Ordinal);
        var search = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpGlobalSearchCatalog.cs"));
        Assert.Contains("/erp/autoparts/workspace", search, StringComparison.Ordinal);
        Assert.Contains("/erp/jewellery/workspace", search, StringComparison.Ordinal);
    }

    [Fact]
    public void FmcgWorkspaceIsWired()
    {
        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpFmcgWorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/fmcg/workspace\"", page, StringComparison.Ordinal);
        Assert.Contains("ErpGccLocalizationCatalog", page, StringComparison.Ordinal);
        Assert.Contains("name=\"country\"", page, StringComparison.Ordinal);
        Assert.Contains("Load sample data", page, StringComparison.Ordinal);
        Assert.Contains("Remove sample data", page, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", page, StringComparison.Ordinal);

        var routes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("/erp/fmcg/demo-seed", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/fmcg/demo-clear", routes, StringComparison.Ordinal);

        var module = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleFmcgDemoSeedAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleFmcgDemoClearAsync", module, StringComparison.Ordinal);

        var program = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFmcgWorkspaceReadService", program, StringComparison.Ordinal);
        Assert.Contains("IErpFmcgDemoSeedWriteService", program, StringComparison.Ordinal);

        var dashboard = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("/erp/fmcg/workspace", dashboard, StringComparison.Ordinal);
        var search = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpGlobalSearchCatalog.cs"));
        Assert.Contains("/erp/fmcg/workspace", search, StringComparison.Ordinal);

        var nav = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpIndustryNav.cs"));
        Assert.Contains("IsFmcgIndustry", nav, StringComparison.Ordinal);
        Assert.Contains("ShowFmcgModules", nav, StringComparison.Ordinal);
        Assert.Contains("IsFmcgCompany", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void GccCatalogResolvesQatarAndFallsBack()
    {
        var qa = EcomAE.Platform.Erp.ErpGccLocalizationCatalog.Resolve("QA");
        Assert.Equal("QAR", qa.CurrencyCode);
        Assert.True(qa.Enabled);
        Assert.Equal("QA", EcomAE.Platform.Erp.ErpGccLocalizationCatalog.Resolve(null).Code);
        Assert.Equal("QA", EcomAE.Platform.Erp.ErpGccLocalizationCatalog.Resolve("XX").Code);
        Assert.Equal("SAR", EcomAE.Platform.Erp.ErpGccLocalizationCatalog.Resolve("sa").CurrencyCode);
        Assert.Contains(EcomAE.Platform.Erp.ErpGccLocalizationCatalog.All, p => p.Code == "AE");
        Assert.Contains(EcomAE.Platform.Erp.ErpGccLocalizationCatalog.All, p => p.Code == "OM");
    }

    [Fact]
    public void IndustryGateCoversAutopartsAndJewellery()
    {
        var gate = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Middleware/IndustrySpecificRouteGateMiddleware.cs"));
        Assert.Contains("/erp/autoparts/", gate, StringComparison.Ordinal);
        Assert.Contains("/erp/jewellery/", gate, StringComparison.Ordinal);
        Assert.Contains("auto_parts", gate, StringComparison.Ordinal);
        var nav = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpIndustryNav.cs"));
        Assert.Contains("IsAutoPartsCompany", nav, StringComparison.Ordinal);
        Assert.Contains("spare_part", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoClearsDeleteOnlyRegisteredRowsAndNeverUsers()
    {
        var ap = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpAutoPartsDemoSeedWriteService.cs"));
        Assert.Contains("ecomae_autoparts_demo_registry", ap, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `users`", ap, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", ap, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", ap, StringComparison.Ordinal);

        var jw = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJewelleryDemoSeedWriteService.cs"));
        Assert.Contains("ecomae_jw_demo_registry", jw, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `users`", jw, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", jw, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", jw, StringComparison.Ordinal);
    }

    [Fact]
    public void GlBridgesPostBalancedJournals()
    {
        // Both workspaces post into the same shared GL — no parallel ledger.
        foreach (var lines in new[]
                 {
                     ErpAutoPartsIfrs.SaleInvoiced(100m, 5m),
                     ErpAutoPartsIfrs.CostOfSale(70m),
                     ErpAutoPartsIfrs.PurchaseReceived(100m),
                     ErpAutoPartsIfrs.PaymentReceived(105m),
                 })
        {
            Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        }

        var apService = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpAutoPartsGlPostWriteService.cs"));
        Assert.Contains("ErpAutoPartsGl.PostAsync", apService, StringComparison.Ordinal);
        Assert.Contains("PostedJournalIdAsync", apService, StringComparison.Ordinal);

        var jwService = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJewelleryGlPostWriteService.cs"));
        Assert.Contains("ErpAutoPartsGl.PostAsync", jwService, StringComparison.Ordinal);
        Assert.Contains("epc_jewel_voucher", jwService, StringComparison.Ordinal);
        Assert.Contains("PostedJournalIdAsync", jwService, StringComparison.Ordinal);
    }

    [Fact]
    public void Jewellery_reports_menu_is_on_every_jewellery_page_and_links_real_pages()
    {
        var links = ErpJewelleryWorkspace.ReportLinks;
        Assert.Equal(links.Count, links.Select(l => l.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(links, l => l.Href == "/erp/trial-balance-app");
        Assert.Contains(links, l => l.Href == "/erp/report-center-app?tab=pl");
        Assert.Contains(links, l => l.Href == "/erp/report-center-app?tab=balance_sheet");

        var pages = Directory.GetFiles(Path.GetDirectoryName(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpJewelleryWorkspaceApp.razor"))!, "*.razor")
            .Select(File.ReadAllText).ToList();
        foreach (var link in links.Where(l => l.Href != "/erp"))
        {
            var path = link.Href.Split('?')[0];
            Assert.True(pages.Any(p => p.Contains("@page \"" + path + "\"", StringComparison.Ordinal)), "no page for " + path);
        }

        foreach (var name in new[] { "CpJewelleryFixingApp", "CpJewelleryMastersApp", "CpJewelleryRepairsApp", "CpJewelleryRetailApp", "CpJewelleryStockVerificationApp" })
        {
            var razor = File.ReadAllText(Find($"aspnet/src/EcomAE.Platform/Components/Pages/{name}.razor"));
            Assert.Contains("<ErpJewelleryReportsMenu />", razor, StringComparison.Ordinal);
        }

        var tabs = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpJewelleryWorkspaceTabs.razor"));
        Assert.Contains("<ErpJewelleryReportsMenu />", tabs, StringComparison.Ordinal);
    }

    [Fact]
    public void Erp_report_pages_link_to_the_print_designer()
    {
        foreach (var name in new[] { "ErpTrialBalanceApp", "ErpReportCenterApp", "ErpGlJournalsApp", "ErpStockMovementsApp", "ErpAgingApp", "ErpInventoryReportApp", "ErpExpenseReportsApp", "ErpCustomShippingReportsApp" })
        {
            var razor = File.ReadAllText(Find($"aspnet/src/EcomAE.Platform/Components/Pages/{name}.razor"));
            Assert.Contains("<ErpPrintDesignerLink Kind=", razor, StringComparison.Ordinal);
        }

        var link = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpPrintDesignerLink.razor"));
        Assert.Contains("/erp/print-designer-app?kind=", link, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
