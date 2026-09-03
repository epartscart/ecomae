using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpNetsuiteDashboardCatalogTests
{
    [Fact]
    public void FinanceProfileMatchesPhpNetsuiteHome()
    {
        var profile = ErpNetsuiteDashboardCatalog.Resolve(null, fullAdmin: false);
        Assert.Equal("finance", profile.Key);
        Assert.Equal("Finance centre", profile.Label);
        Assert.Contains("cash", profile.HeroKeys);
        Assert.Contains("sales", profile.HeroKeys);

        var tiles = ErpNetsuiteDashboardCatalog.ResolveTiles(profile);
        Assert.Equal(new[] { "Balance Sheet", "General Journal", "Reconcile Bank", "Income Statement" }, tiles.Select(t => t.Label).ToArray());

        var quick = ErpNetsuiteDashboardCatalog.ResolveQuick(profile);
        Assert.Contains(quick, q => q.Key == "ext_ifrs");
        Assert.Contains(quick, q => q.Key == "receivables");
        Assert.Contains(quick, q => q.Key == "payables");
        Assert.All(quick, q => Assert.StartsWith("qa-", q.Tone, StringComparison.Ordinal));
        Assert.Equal("qa-indigo", ErpNetsuiteDashboardCatalog.ShortcutTone("qa-indigo"));
        Assert.Equal("qa-blue", ErpNetsuiteDashboardCatalog.ShortcutTone("gold"));

        var nav = ErpNetsuiteDashboardCatalog.ResolveNav(profile);
        Assert.Contains(nav, g => g.Title == "Lists");
        Assert.Contains(nav, g => g.Title == "Transactions");
        Assert.Contains(nav, g => g.Title == "Reports");
        Assert.Contains(nav.SelectMany(g => g.Links), l => l.Label == "Customers");
        Assert.Contains(nav.SelectMany(g => g.Links), l => l.Label == "Financial report (IFRS)");
    }

    [Fact]
    public void AdminPreviewUsesCeoStyleCommandTiles()
    {
        var profile = ErpNetsuiteDashboardCatalog.Resolve("admin", fullAdmin: true);
        Assert.Equal("admin", profile.Key);
        Assert.True(ErpNetsuiteDashboardCatalog.Can(profile, "exec"));
        Assert.True(ErpNetsuiteDashboardCatalog.Can(profile, "hr_tasks"));
        var tiles = ErpNetsuiteDashboardCatalog.ResolveTiles(profile);
        Assert.Equal(new[] { "Income Statement", "Balance Sheet", "General Journal", "New Sales Order" }, tiles.Select(t => t.Label).ToArray());
    }

    [Fact]
    public void SalesProfileHidesProfitAndCash()
    {
        var profile = ErpNetsuiteDashboardCatalog.Resolve("sales", fullAdmin: true);
        Assert.False(ErpNetsuiteDashboardCatalog.Can(profile, "profit"));
        Assert.False(ErpNetsuiteDashboardCatalog.Can(profile, "cash"));
        Assert.False(ErpNetsuiteDashboardCatalog.Can(profile, "exec"));
        var tiles = ErpNetsuiteDashboardCatalog.ResolveTiles(profile);
        Assert.DoesNotContain(tiles, t => t.Label.Contains("Income", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tiles, t => t.Key == "sales_orders");
    }

    [Fact]
    public void GuestCannotPreviewRestrictedProfile()
    {
        var profile = ErpNetsuiteDashboardCatalog.Resolve("sales", fullAdmin: false);
        Assert.Equal("finance", profile.Key);
    }

    [Fact]
    public void ChangeBadgeMatchesPhpPriorPeriodRules()
    {
        var flat = ErpNetsuiteDashboardCatalog.Change(10, 0, true);
        Assert.Equal("ns-chg ns-flat", flat.Css);
        Assert.Equal("—", flat.Text);

        var upGood = ErpNetsuiteDashboardCatalog.Change(120, 100, true);
        Assert.Equal("ns-chg ns-up", upGood.Css);
        Assert.Contains("20.0%", upGood.Text);

        var upBad = ErpNetsuiteDashboardCatalog.Change(120, 100, false);
        Assert.Equal("ns-chg ns-down", upBad.Css);
    }

    [Fact]
    public void OperationalKpisAndFinancialsFillEmptyCompanyWithZeros()
    {
        var profile = ErpNetsuiteDashboardCatalog.Profiles["finance"];
        var zero = new ErpWorkspacePeriodKpis(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open");
        var kpis = ErpNetsuiteDashboardCatalog.OperationalKpis(profile, zero, 30);
        Assert.NotEmpty(kpis);
        Assert.Contains(kpis, k => k.Key == "revenue" && k.Value == "0.00");
        Assert.Contains(kpis, k => k.Key == "gross_margin");

        var fin = ErpNetsuiteDashboardCatalog.ResolveFinancials(profile, zero, "AED");
        Assert.Contains(fin, f => f.Label == "Gross profit %");
        Assert.Contains(fin, f => f.Label == "Cash & bank");
        Assert.Contains(fin, f => f.Value.Contains("AED", StringComparison.Ordinal));
    }

    [Fact]
    public void GlNetProfitIsNotGrossMargin()
    {
        var profile = ErpNetsuiteDashboardCatalog.Profiles["finance"];
        var cur = new ErpWorkspacePeriodKpis(
            0, 1000, 400, 600, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open", 180);
        var fin = ErpNetsuiteDashboardCatalog.ResolveFinancials(profile, cur, "AED");
        Assert.Contains(fin, f => f.Label == "Margin (ex VAT)" && f.Value.StartsWith("600.00", StringComparison.Ordinal));
        Assert.Contains(fin, f => f.Label == "GL net profit" && f.Value.StartsWith("180.00", StringComparison.Ordinal));
    }

    [Fact]
    public void PeriodDaysInclusiveCountsCurrentCalendarDay()
    {
        Assert.Equal(3, ErpNetsuiteDashboardCatalog.PeriodDaysInclusive("2026-09-01", "2026-09-03"));
        Assert.Equal(1, ErpNetsuiteDashboardCatalog.PeriodDaysInclusive("2026-09-03", "2026-09-03"));
        Assert.Equal(1, ErpNetsuiteDashboardCatalog.PeriodDaysInclusive(null, null));
    }

    [Fact]
    public void IndustryControlsStayGenericUnlessJewellery()
    {
        var core = ErpNetsuiteDashboardCatalog.IndustryControls("auto_parts", jewellery: false);
        Assert.Contains(core, c => c.Code == "monthly_close");
        Assert.Contains(core, c => c.Code == "vin_fitment");
        Assert.DoesNotContain(core, c => c.Code == "metal_weighbridge");

        var jw = ErpNetsuiteDashboardCatalog.IndustryControls("jewellery", jewellery: true);
        Assert.Contains(jw, c => c.Code == "metal_weighbridge");
    }

    [Fact]
    public void WorkspaceSqlMatchesPhpCogsTasksAndGlPl()
    {
        Assert.Contains("t2_price_purchase", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);
        Assert.Contains("shop_orders", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);
        Assert.Contains("for_finish", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);
        Assert.Contains("count_flag", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);
        Assert.Contains("shop_orders_items_statuses_ref", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_purchases", LegacySurfaceDashboardSql.SumErpWorkspacePurchaseExVat, StringComparison.Ordinal);

        Assert.Contains("for_finish", LegacySurfaceDashboardSql.SumErpWorkspaceRevenueExVat, StringComparison.Ordinal);
        Assert.Contains("for_finish", LegacySurfaceDashboardSql.SumErpWorkspaceSalesInclVat, StringComparison.Ordinal);
        Assert.Contains("for_finish", LegacySurfaceDashboardSql.SumErpWorkspaceReceivableDueOrders, StringComparison.Ordinal);
        Assert.Contains("for_finish", LegacySurfaceDashboardSql.CountErpWorkspaceCompletedOrders, StringComparison.Ordinal);
        Assert.Contains("shop_orders", LegacySurfaceDashboardSql.CountErpWorkspaceCompletedOrders, StringComparison.Ordinal);

        Assert.Contains("epc_erp_coa_accounts", LegacySurfaceDashboardSql.SumErpWorkspaceGlNetProfit, StringComparison.Ordinal);
        Assert.DoesNotContain("@companyId", LegacySurfaceDashboardSql.SumErpWorkspaceGlNetProfit, StringComparison.Ordinal);
        var scopedGl = LegacySurfaceDashboardSql.BuildSumErpWorkspaceGlNetProfit(7);
        Assert.Contains("j.`company_id` = @companyId", scopedGl, StringComparison.Ordinal);
        Assert.DoesNotContain("IFNULL(j.`company_id`", scopedGl, StringComparison.Ordinal);
        Assert.Contains("@dateFrom", scopedGl, StringComparison.Ordinal);
        var defaultGl = LegacySurfaceDashboardSql.BuildSumErpWorkspaceGlNetProfit(3, includeUnassignedZero: true);
        Assert.Contains("IFNULL(j.`company_id`, 0) = 0", defaultGl, StringComparison.Ordinal);
        Assert.DoesNotContain("j.`company_id`", LegacySurfaceDashboardSql.BuildSumErpWorkspaceGlNetProfit(0), StringComparison.Ordinal);
        Assert.Equal(7, ErpHostContext.ResolveErpGlCompanyId(7, [3, 7, 9]));
        Assert.Equal(3, ErpHostContext.ResolveErpGlCompanyId(99, [3, 7, 9]));
        Assert.Equal(3, ErpHostContext.ResolveErpGlCompanyId(null, [3, 7]));
        Assert.Equal(0, ErpHostContext.ResolveErpGlCompanyId(5, []));
        Assert.True(ErpHostContext.IncludeUnassignedGlJournals(3, [3, 7, 9]));
        Assert.False(ErpHostContext.IncludeUnassignedGlJournals(7, [3, 7, 9]));
        Assert.False(ErpHostContext.IncludeUnassignedGlJournals(0, []));
        Assert.Contains("completed_at", LegacySurfaceDashboardSql.SelectErpWorkspaceTopPerformers, StringComparison.Ordinal);
        Assert.Contains("department_code", LegacySurfaceDashboardSql.SelectErpWorkspaceTopPerformers, StringComparison.Ordinal);
        Assert.Contains("epc_erp_staff_profiles", LegacySurfaceDashboardSql.SelectErpWorkspaceTopPerformers, StringComparison.Ordinal);
        Assert.Contains("GROUP BY s.`acted_by`", LegacySurfaceDashboardSql.SelectErpWorkspaceTopPerformers, StringComparison.Ordinal);
        Assert.DoesNotContain("current_department", LegacySurfaceDashboardSql.SelectErpWorkspaceTopPerformers, StringComparison.Ordinal);
        Assert.Contains("@dateFrom", LegacySurfaceDashboardSql.CountErpWorkspaceProcessDoneInPeriod, StringComparison.Ordinal);
        Assert.Contains("epc_pf_case_steps", LegacySurfaceDashboardSql.CountErpWorkspaceProcessDoneInPeriod, StringComparison.Ordinal);
    }

    [Fact]
    public void DepartmentNameMatchesPhpUnassignedFallback()
    {
        Assert.Equal("Unassigned", ErpNetsuiteDashboardCatalog.DepartmentName(""));
        Assert.Equal("Purchasing", ErpNetsuiteDashboardCatalog.DepartmentName("purchase"));
        Assert.Equal("Sales", ErpNetsuiteDashboardCatalog.DepartmentName("sales"));
    }

    [Fact]
    public void InsightsSuiteAlwaysRendersPhpBandsAndZeroEmptyStates()
    {
        var zero = new ErpWorkspacePeriodKpis(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open");
        var suite = ErpInsightsSuiteCatalog.Build(
            zero, zero, ErpInsightsSuiteCatalog.EmptyCommerce, 0, [0, 0, 0, 0, 0],
            "AED", "2026-09-01", "2026-09-03", autoParts: true);
        Assert.Equal(3, suite.Bands.Count);
        Assert.Contains(suite.Bands, b => b.Key == "financial" && b.Items.Count == 6);
        Assert.Contains(suite.Bands, b => b.Key == "business" && b.Items.Count == 5);
        Assert.Contains(suite.Bands, b => b.Key == "cp" && b.Items.Any(c => c.Key == "vin"));
        Assert.Contains(suite.Bands.SelectMany(b => b.Items), c => c.Key == "revenue" && c.Narrative.Contains("No MTD sales", StringComparison.Ordinal));
        Assert.Contains(suite.Alerts, a => a.Title == "Pricing not ready");
        Assert.Equal("Ready", ErpInsightsSuiteCatalog.FormatValue(suite.Bands.SelectMany(b => b.Items).First(c => c.Key == "sku_media"), "AED"));
    }

    [Fact]
    public void InsightsVinCardStaysOffForJewelleryWhenNoOpenRequests()
    {
        var zero = new ErpWorkspacePeriodKpis(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open");
        var suite = ErpInsightsSuiteCatalog.Build(
            zero, zero, ErpInsightsSuiteCatalog.EmptyCommerce, 0, [0, 0, 0, 0, 0],
            "AED", "2026-09-01", "2026-09-03", autoParts: false);
        Assert.DoesNotContain(suite.Bands.SelectMany(b => b.Items), c => c.Key == "vin");
    }

    [Fact]
    public void PurchaseCentreIncludesInventoryTurnover()
    {
        var profile = ErpNetsuiteDashboardCatalog.Profiles["purchase"];
        var cur = new ErpWorkspacePeriodKpis(
            0, 0, 400, 0, 0, 0, 100, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "open");
        var kpis = ErpNetsuiteDashboardCatalog.OperationalKpis(profile, cur, 3);
        Assert.Contains(kpis, k => k.Key == "inv_turnover" && k.Value.StartsWith("4.00", StringComparison.Ordinal));
    }

    [Fact]
    public void StatutoryTilesDeepLinkLikePhpExtUrl()
    {
        Assert.Contains("tab=pl", ErpNetsuiteDashboardCatalog.Tiles["pl"].Href, StringComparison.Ordinal);
        Assert.Contains("tab=balance_sheet", ErpNetsuiteDashboardCatalog.Tiles["balance_sheet"].Href, StringComparison.Ordinal);
        Assert.Contains("cat=audit", ErpNetsuiteDashboardCatalog.Tiles["ext_ifrs"].Href, StringComparison.Ordinal);
        Assert.Contains("rep=tax__vat_return", ErpNetsuiteDashboardCatalog.Tiles["ext_vat"].Href, StringComparison.Ordinal);
        Assert.Contains("fetch=1", ErpNetsuiteDashboardCatalog.Tiles["ext_ct"].Href, StringComparison.Ordinal);
    }
}
