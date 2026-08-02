using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceFieldParityReporterTests
{
    [Fact]
    public void BuildReportLocksContractsAndBlocksCutover()
    {
        var report = new SurfaceFieldParityReporter().BuildReport();

        Assert.False(report.CutoverAllowed);
        Assert.Contains("cutover-blocked", report.Status, StringComparison.OrdinalIgnoreCase);
        Assert.True(report.ContractCount >= 20);
        Assert.Contains(report.Contracts, c => c.Surface == "cp" && c.AspNetRoute == "/cp/dashboard-summary" && c.RequiredSummaryOrItemFields.Contains("users"));
        Assert.Contains(report.Contracts, c => c.Surface == "erp" && c.AspNetRoute == "/erp/coa-accounts" && c.RequiredSummaryOrItemFields.Contains("code"));
        Assert.Contains(report.Contracts, c => c.Surface == "bos" && c.AspNetRoute == "/bos/audit-log" && c.RequiredSummaryOrItemFields.Contains("actor"));
        Assert.Contains(report.Contracts, c => c.Surface == "storefront" && c.AspNetRoute == "/storefront/orders");
        Assert.Contains(report.Functions, f => f.Surface == "frontend" && f.Status == "php-authoritative");
        Assert.Contains(report.Guarantees, g => g.Contains("CutoverAllowed is false", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, g => g.Contains("dual samples", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CatalogPresentationChromeMatchesLegacyAssets()
    {
        foreach (var surface in new[] { "cp", "erp", "bos", "storefront" })
        {
            Assert.NotEmpty(LegacyPresentationAssets.StylesheetsFor(surface));
            Assert.False(string.IsNullOrWhiteSpace(LegacyPresentationAssets.LegacyChromeSourceFor(surface)));
        }

        Assert.Contains(LegacyPresentationAssets.ControlPanelStylesheets, href => href.Contains("epc_cp_professional_css.php", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.BosStylesheets, href => href.Contains("epc_bos_shell.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.StorefrontStylesheets, href => href.Contains("templates/modex/", StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardSummaryRecordsExposeContractedCamelCaseNames()
    {
        var cp = new ControlPanelDashboardSummary(1, 2, 3, 2, "migration", "");
        var erp = new ErpDashboardSummary(1, 2, 3, -1, 4, 5, 6, "migration", "");
        var bos = new BosFleetSummary(1, 1, 1, 1, 0, "migration", "");
        var sf = new StorefrontAccountSummary(9, 1, 1, 1, "migration", "");

        Assert.Equal(1, cp.Users);
        Assert.Equal(1m, erp.CashPosition);
        Assert.Equal(1, bos.PortalTenants);
        Assert.Equal(9, sf.UserId);

        foreach (var contract in SurfacePayloadContractCatalog.All.Where(c => c.AspNetRoute.EndsWith("dashboard-summary", StringComparison.Ordinal) || c.AspNetRoute.EndsWith("account-summary", StringComparison.Ordinal)))
        {
            Assert.NotEmpty(contract.RequiredSummaryOrItemFields);
            Assert.Contains("source", contract.RequiredSummaryOrItemFields);
            Assert.Contains("message", contract.RequiredSummaryOrItemFields);
        }
    }
}
