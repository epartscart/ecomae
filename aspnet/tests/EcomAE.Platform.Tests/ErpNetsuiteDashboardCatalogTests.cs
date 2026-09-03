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
    public void DepartmentNameMatchesPhpUnassignedFallback()
    {
        Assert.Equal("Unassigned", ErpNetsuiteDashboardCatalog.DepartmentName(""));
        Assert.Equal("Purchasing", ErpNetsuiteDashboardCatalog.DepartmentName("purchase"));
        Assert.Equal("Sales", ErpNetsuiteDashboardCatalog.DepartmentName("sales"));
    }
}
