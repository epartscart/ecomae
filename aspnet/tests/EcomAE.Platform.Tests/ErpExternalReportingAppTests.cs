using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpExternalReportingAppTests
{
    [Fact]
    public void CatalogMatchesPhpRegistryShape()
    {
        Assert.Equal(26, ErpExternalReportingCatalog.CategoryCount);
        Assert.True(ErpExternalReportingCatalog.ReportCount >= 220);
        Assert.NotNull(ErpExternalReportingCatalog.Find("tax__vat_return"));
        Assert.Equal("VAT Return", ErpExternalReportingCatalog.Find("tax__vat_return")!.Name);
        Assert.Equal("vat_return", ErpExternalReportingCatalog.Find("tax__vat_return")!.Builder);
        Assert.Equal("IFRS18", ErpExternalReportingCatalog.Find("fin__annual_financial_statements")!.Std);
        Assert.Equal("United Arab Emirates", ErpExternalReportingCatalog.CountryName("AE"));
        Assert.Equal("Federal Tax Authority (FTA)", ErpExternalReportingCatalog.ResolveAuthority("AE", "tax").Name);
        Assert.True(ErpExternalReportingCatalog.Ifrs18Applies(2026));
        Assert.Contains("BOX1A", ErpExternalReportingCatalog.ImportTemplateCsv("vat"), StringComparison.Ordinal);
        Assert.Equal("tax__vat_return", ErpExternalReportingCatalog.ReportKey("tax", "VAT Return"));
    }

    [Fact]
    public void ErpTabStaysOnErpNotCp()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("ext_reports", out var href));
        Assert.Equal("/erp/tax-external-reporting-app", href);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("external_reports", out var alias));
        Assert.Equal("/erp/tax-external-reporting-app", alias);
        Assert.Equal("/erp/tax-external-reporting-app", EcomAeRoutes.ErpTaxExternalReportingApp);
        Assert.Equal("/cp/tax-external-reporting-app", EcomAeRoutes.ControlPanelTaxExternalReportingApp);
        Assert.Equal("/erp/tax-external-reporting-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=tax&tab=ext_reports"));
        Assert.Equal("/erp/tax-external-reporting-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=tax&tab=ext_reports&company=2"));
        Assert.DoesNotContain("/cp/tax-external-reporting-app", href, StringComparison.Ordinal);
    }

    [Fact]
    public void AppMatchesPhpExternalReportingSections()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpExternalReportingApp.razor"));
        Assert.Contains("@page \"/erp/tax-external-reporting-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/tax-external-reporting-app\"", razor, StringComparison.Ordinal);
        Assert.Contains("External Reporting", razor, StringComparison.Ordinal);
        Assert.Contains("Registration country", razor, StringComparison.Ordinal);
        Assert.Contains("Preview jurisdiction", razor, StringComparison.Ordinal);
        Assert.Contains("Import from Excel", razor, StringComparison.Ordinal);
        Assert.Contains("Guided IFRS report builder", razor, StringComparison.Ordinal);
        Assert.Contains("Fetch &amp; build", razor, StringComparison.Ordinal);
        Assert.Contains("UAE statutory sub-layer", razor, StringComparison.Ordinal);
        Assert.Contains("company=", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Manage store operations", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void FetchDryRunBlocksWrites()
    {
        var dry = new ErpExternalReportingFetchDryRun();
        var ok = dry.Evaluate(new ErpExternalReportingFetchRequest("fetch", "tax__vat_return", false));
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.True(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var refused = dry.Evaluate(new ErpExternalReportingFetchRequest("import", "vat", true));
        Assert.Equal(0, refused.Writes);
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
        Assert.False(refused.WouldWrite);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "epc-demo-provision-public.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
