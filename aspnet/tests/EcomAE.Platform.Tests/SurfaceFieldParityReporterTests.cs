using System.Text.Json;
using System.Text.Json.Serialization;
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
        Assert.False(report.ReadyForPhpRemoval);
        Assert.Contains("cutover-blocked", report.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SurfacePayloadContractCatalog.All.Count, report.ContractCount);
        Assert.True(report.ContractCount >= 40);
        Assert.Contains(report.Contracts, c => c.Surface == "cp" && c.AspNetRoute == "/cp/dashboard-summary" && c.RequiredSummaryOrItemFields.Contains("users"));
        Assert.Contains(report.Contracts, c => c.Surface == "erp" && c.AspNetRoute == "/erp/coa-accounts" && c.RequiredSummaryOrItemFields.Contains("code"));
        Assert.Contains(report.Contracts, c => c.Surface == "bos" && c.AspNetRoute == "/bos/audit-log" && c.RequiredSummaryOrItemFields.Contains("actor"));
        Assert.Contains(report.Contracts, c => c.Surface == "storefront" && c.AspNetRoute == "/storefront/orders");
        Assert.Contains(report.Contracts, c => c.Surface == "api" && c.AspNetRoute == "/api/v1/catalog/manufacturers");
        Assert.Contains(report.Contracts, c => c.Surface == "api" && c.AspNetRoute == "/api/v1/catalog/vin");
        Assert.Contains(report.Functions, f => f.Surface == "frontend" && f.Status == "php-authoritative");
        Assert.Contains(report.Guarantees, g => g.Contains("CutoverAllowed is false", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, g => g.Contains("dual samples", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteSurfaceFieldParityProbeSnapshotsWhenRequested()
    {
        // Regenerates committed public-probe / surface-parity snapshots from the live reporter.
        // ECOMAE_WRITE_SURFACE_FIELD_PARITY_PROBE=1 dotnet test --filter WriteSurfaceFieldParityProbeSnapshotsWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_SURFACE_FIELD_PARITY_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new SurfaceFieldParityReporter().BuildReport();
        Assert.Equal(SurfacePayloadContractCatalog.All.Count, report.ContractCount);
        Assert.False(report.CutoverAllowed);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = FindRepoRoot();
        var targets = new[]
        {
            Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-surface-field-parity.json"),
            Path.Combine(root, "docs", "migration", "evidence", "surface-parity", "www-surface-field-parity.json")
        };
        foreach (var path in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "run_zero_php_final_gate_checklist.sh")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void CatalogPresentationChromeMatchesLegacyAssets()
    {
        foreach (var surface in new[] { "cp", "erp", "bos", "storefront", "marketing" })
        {
            Assert.NotEmpty(LegacyPresentationAssets.StylesheetsFor(surface));
            Assert.False(string.IsNullOrWhiteSpace(LegacyPresentationAssets.LegacyChromeSourceFor(surface)));
        }

        Assert.Contains(LegacyPresentationAssets.ControlPanelStylesheets, href => href.Contains("epc_cp_professional_css.php", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.BosStylesheets, href => href.Contains("epc_bos_shell.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.StorefrontStylesheets, href => href.Contains("templates/modex/", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.StorefrontStylesheets, href => href.Contains("epc_automotive_spareparts.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("epc_ecomae_platform_marketing_css.php", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.LoginStylesheets, href => href.Contains("epc_ecomae_hub_logo_css.php", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.BosLoginScripts, src => src.Contains("epc_bos_shell.js", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("storefront"), m => m.Contains("epc-engine-animation", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("bos"), m => m.Contains("bosParticles", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("cp"), m => m.Contains("ech-hub", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("marketing"), m => m.Contains("epm-hub", StringComparison.Ordinal));
        Assert.Equal("epc-erp-standalone", LegacyPresentationAssets.LoginBodyClassFor("erp"));
        Assert.Contains(LegacyPresentationAssets.ErpLoginStylesheets, href => href.Contains("epc_erp_portal_inline_css_serve", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("erp"), m => m.Contains("erpPortalParticles", StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardSummaryRecordsExposeContractedCamelCaseNames()
    {
        var cp = new ControlPanelDashboardSummary(1, 2, 3, 2, "migration", "");
        var erp = new ErpDashboardSummary(
            1, 2, 3, -1, 4, 5, 6,
            10, 11, 12,
            13, 14, 15, 16, 17, "open", 18,
            1, 2, 3, 4, 5, 6,
            7, 8, 9,
            "migration", "");
        var bos = new BosFleetSummary(1, 1, 1, 1, 0, 1, 0, 0, "migration", "");
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
