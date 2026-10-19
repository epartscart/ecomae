using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFitOutWorkspaceTests
{
    [Fact]
    public void GuideIsFirstTabAndCoversEveryOtherTabExactlyOnce()
    {
        Assert.Equal("guide", ErpFitOutWorkspace.Tabs[0].Key);
        Assert.Equal("guide", ErpFitOutGuide.Normalize(""));
        Assert.Equal("guide", ErpFitOutGuide.Normalize("nonexistent"));
        Assert.Equal("projects", ErpFitOutGuide.Normalize("projects"));

        var covered = ErpFitOutGuide.Steps.Select(step => step.Key).ToList();
        Assert.Equal(
            ErpFitOutWorkspace.Tabs.Where(tab => tab.Key != "guide").Select(tab => tab.Key).OrderBy(key => key, StringComparer.Ordinal),
            covered.OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(Enumerable.Range(1, covered.Count), ErpFitOutGuide.Steps.Select(step => step.Number));
        Assert.Equal(
            ["guide", "projects", "costing", "materials", "procurement", "claims", "month-end", "reports"],
            ErpFitOutWorkspace.Tabs.Select(tab => tab.Key).ToArray());
    }

    [Fact]
    public void WorkspacePageTabsBoardAndFilterBarAreWired()
    {
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpFitOutWorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/fitout/workspace\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutWorkspaceTabs", razor, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutGuideBoard", razor, StringComparison.Ordinal);
        Assert.Contains("fo-board-dock", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"project\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"period\"", razor, StringComparison.Ordinal);
        Assert.Contains("All projects", razor, StringComparison.Ordinal);
        Assert.Contains("Load sample data", razor, StringComparison.Ordinal);
        Assert.Contains("Remove sample data", razor, StringComparison.Ordinal);
        Assert.Contains("demo-", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/fitout/report?format=", razor, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", razor, StringComparison.Ordinal);
        Assert.Contains("CpCsrfGuard.FieldName", razor, StringComparison.Ordinal);

        var board = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpFitOutGuideBoard.razor"));
        Assert.Contains("fo-guide__track", board, StringComparison.Ordinal);
        var tabs = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/ErpFitOutWorkspaceTabs.razor"));
        Assert.Contains("QuerySuffix", tabs, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesHandlersNavAndDiAreRegistered()
    {
        var routes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("/erp/fitout/demo-seed", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/fitout/demo-clear", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/fitout/report", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/fitout/gl-post", routes, StringComparison.Ordinal);

        var module = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleFitOutDemoSeedAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutDemoClearAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutReportAsync", module, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutDemoSeed", module, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutDemoClear", module, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutReport", module, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutGlPostAsync", module, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutGlPost", module, StringComparison.Ordinal);

        var program = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFitOutDemoSeedWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutGlPostWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutWorkspaceReadService", program, StringComparison.Ordinal);

        var dashboard = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("/erp/fitout/workspace", dashboard, StringComparison.Ordinal);
        var search = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpGlobalSearchCatalog.cs"));
        Assert.Contains("/erp/fitout/workspace", search, StringComparison.Ordinal);
    }

    [Fact]
    public void IndustryGateAllowsFitOutConstructionAndBuildingMaterials()
    {
        var gate = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Middleware/IndustrySpecificRouteGateMiddleware.cs"));
        Assert.Contains("/erp/fitout/", gate, StringComparison.Ordinal);
        Assert.Contains("building_materials", gate, StringComparison.Ordinal);
        var nav = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/ErpIndustryNav.cs"));
        Assert.Contains("building_materials", nav, StringComparison.Ordinal);
    }

    [Fact]
    public void ClearDemoDeletesOnlyDemoTaggedRowsAndNeverUsers()
    {
        var src = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFitOutDemoSeedWriteService.cs"));
        Assert.Contains("SeedDemoAsync", src, StringComparison.Ordinal);
        Assert.Contains("ClearDemoAsync", src, StringComparison.Ordinal);
        Assert.Contains("DEMO-", src, StringComparison.Ordinal);
        Assert.Contains("DEMO | ", src, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_estimates", src, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_boq_lines", src, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_material_movements", src, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_delivery_records", src, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_procurement_links", src, StringComparison.Ordinal);
        Assert.Contains("epc_prja_budget", src, StringComparison.Ordinal);
        Assert.Contains("epc_prja_txn", src, StringComparison.Ordinal);
        Assert.Contains("epc_prja_recognition", src, StringComparison.Ordinal);
        Assert.Contains("epc_cc_allocations", src, StringComparison.Ordinal);
        Assert.Contains("epc_prj_projects", src, StringComparison.Ordinal);
        Assert.DoesNotContain("`users`", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `users`", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", src, StringComparison.Ordinal);
        Assert.DoesNotContain("TRUNCATE", src, StringComparison.Ordinal);
    }

    [Fact]
    public void GlBridgePostsBalancedIfrsJournals()
    {
        // Fit-out must reach the same GL that drives the income statement and
        // balance sheet — no parallel ledger.
        var service = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFitOutGlPostWriteService.cs"));
        var gl = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFitOutGl.cs"));
        Assert.Contains("PostJournalAsync", gl, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutGl.PostAsync", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_gl_journals", gl, StringComparison.Ordinal);
        Assert.Contains("epc_prja_txn", service, StringComparison.Ordinal);
        Assert.Contains("client_progress_claim", service, StringComparison.Ordinal);
        Assert.Contains("ecomae_fitout_contract_terms", service, StringComparison.Ordinal);
        Assert.Contains("PostedJournalIdAsync", service, StringComparison.Ordinal);

        foreach (var lines in new[]
                 {
                     ErpFitOutIfrs.CostIncurred(100m),
                     ErpFitOutIfrs.RevenueEarned(100m),
                     ErpFitOutIfrs.ClaimCertified(105m, 5m),
                     ErpFitOutIfrs.AdvanceReceived(100m),
                     ErpFitOutIfrs.RetentionWithheld(10m),
                 })
        {
            Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
        }

        var ifrs = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFitOutIfrs.cs"));
        Assert.Contains("balance_sheet", ifrs, StringComparison.Ordinal);
        Assert.Contains("income_statement", ifrs, StringComparison.Ordinal);
    }

    [Fact]
    public void BoardPackEmitsValidPdfWordAndHtml()
    {
        var snap = SampleSnap();
        var utc = new DateTime(2026, 10, 19, 4, 0, 0, DateTimeKind.Utc);
        var pdf = ErpFitOutReportPack.Pdf(snap, "Fit-out board pack", utc);
        Assert.True(pdf.Length > 1_000);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);

        var docx = ErpFitOutReportPack.Docx(snap, "Fit-out board pack", utc);
        Assert.Equal((byte)'P', docx[0]);
        Assert.Equal((byte)'K', docx[1]);
        Assert.Contains("word/document.xml", System.Text.Encoding.UTF8.GetString(docx), StringComparison.Ordinal);

        var html = ErpFitOutReportPack.Html(snap, "Fit-out board pack", "/erp/fitout/report?format=pdf", "/erp/fitout/report?format=docx", utc);
        Assert.Contains("Download PDF", html, StringComparison.Ordinal);
        Assert.Contains("Download DOCX", html, StringComparison.Ordinal);
        Assert.Contains("DEMO-PRJ-01", html, StringComparison.Ordinal);
        Assert.Contains("Project P", html, StringComparison.Ordinal);
        Assert.Equal("ecomae-fitout-board-pack-all-20261019", ErpFitOutReportPack.FileStem("all", utc));
        Assert.Equal("ecomae-fitout-board-pack-p7-20261019", ErpFitOutReportPack.FileStem("p7", utc));
    }

    private static ErpFitOutWorkspaceSnapshot SampleSnap()
    {
        var projects = new ErpFitOutProject[]
        {
            new(1, "DEMO-PRJ-01", "DEMO | Office fit-out", 250_000m, 190_000m, "open"),
        };
        var kpis = new ErpFitOutWorkspaceKpis(1, 250_000m, 3_000m, 5_000m, 40_000m, 2_000m, 75_000m, 1);
        ErpFitOutRow Row(long id, string kind) =>
            new(id, kind, "DEMO-" + kind.ToUpperInvariant(), "DEMO | " + kind, "approved", 1_000m, 2m, "demo");
        return new ErpFitOutWorkspaceSnapshot(
            projects, kpis,
            [Row(1, "cost_code")],
            [Row(2, "estimate")],
            [Row(3, "boq_line")],
            [Row(4, "quotation")],
            [Row(5, "contract_terms")],
            [Row(6, "budget")],
            [Row(7, "txn")],
            [Row(8, "material")],
            [Row(9, "procurement")],
            [Row(10, "three_way")],
            [Row(11, "client_progress_claim")],
            [Row(12, "subcontract_order")],
            [Row(13, "site_daily_report")],
            [Row(14, "recognition")],
            [Row(15, "allocation")],
            string.Empty);
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
