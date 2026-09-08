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
        Assert.NotNull(ErpExternalReportingCatalog.Find("tax__electronic_invoicing_pint_ae"));
        Assert.Equal("einvoice", ErpExternalReportingCatalog.Find("tax__electronic_invoicing_pint_ae")!.Builder);
        Assert.Equal("PINTAE", ErpExternalReportingCatalog.Find("tax__electronic_invoicing_pint_ae")!.Std);
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
        Assert.Equal("/erp/external-reporting/import", EcomAeRoutes.ErpExternalReportingImport);
        Assert.Equal("/erp/external-reporting/intake", EcomAeRoutes.ErpExternalReportingIntake);
        Assert.Equal("/erp/external-reporting/template", EcomAeRoutes.ErpExternalReportingTemplate);
        Assert.Equal("/erp/external-reporting/xlsx", EcomAeRoutes.ErpExternalReportingXlsx);
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
        Assert.Contains("ErpExternalReportingImport", razor, StringComparison.Ordinal);
        Assert.Contains("ErpExternalReportingTemplate", razor, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", razor, StringComparison.Ordinal);
        Assert.Contains("RenderPack", razor, StringComparison.Ordinal);
        Assert.Contains("Download PDF", razor, StringComparison.Ordinal);
        Assert.Contains("Download Word", razor, StringComparison.Ordinal);
        Assert.Contains("Download Excel (linked model)", razor, StringComparison.Ordinal);
        Assert.Contains("Download Excel (linked audit pack)", razor, StringComparison.Ordinal);
        Assert.Contains("ErpExternalReportingXlsx", razor, StringComparison.Ordinal);
        Assert.Contains("epcExtPrint()", razor, StringComparison.Ordinal);
        Assert.Contains("epcExtWord(", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"epc_ext_doc\"", razor, StringComparison.Ordinal);
        Assert.Contains("UAE statutory sub-layer", razor, StringComparison.Ordinal);
        Assert.Contains("FY@(DateTime.UtcNow.Year)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("FY@DateTime.UtcNow.Year", razor, StringComparison.Ordinal);
        Assert.Contains("company=", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Manage store operations", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("stay on the PHP", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildersMatchPhpPresentation()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 12, 31);
        var vat = ErpExternalReportingBuild.Build(Input("tax__vat_return", from, to, 1_435_000m, 980_000m));
        Assert.Contains("FTA VAT 201", vat.Title, StringComparison.Ordinal);
        Assert.Contains("Box 1a", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Box 14", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("epc-ext-bars", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("PINT-AE", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Field guide", vat.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", vat.BodyHtml, StringComparison.Ordinal);

        var ct = ErpExternalReportingBuild.Build(Input("tax__corporate_income_tax_return", from, to, 8_400_000m, 6_200_000m));
        Assert.Contains("0%", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("9%", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Federal Decree-Law 47/2022", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("AED 375,000", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("epc-ext-bars", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("0% / 9%", ct.Summary.First(s => s.Label == "Rate").Value);

        var afs = ErpExternalReportingBuild.Build(Input("fin__annual_financial_statements", from, to, 8_400_000m, 5_800_000m));
        Assert.Contains("IFRS 18", afs.Title, StringComparison.Ordinal);
        Assert.Contains("Operating profit or loss", afs.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Profit or loss before financing and income taxes", afs.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Statement of Cash Flows", afs.BodyHtml, StringComparison.Ordinal);

        var audit = ErpExternalReportingBuild.Build(Input("audit__external_audit_report", from, to, 8_400_000m, 5_800_000m));
        Assert.Equal("red", audit.Theme);
        Assert.Contains("Independent Auditor", audit.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("ISA 700", audit.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("ext-cover", audit.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Table of contents", audit.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("#b3122a", audit.BodyHtml, StringComparison.Ordinal);

        var einv = ErpExternalReportingBuild.Build(Input("tax__electronic_invoicing_pint_ae", from, to, 240_000m, 80_000m));
        Assert.Contains("PINT-AE", einv.Title, StringComparison.Ordinal);
        Assert.Contains("Peppol", einv.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("UBL", einv.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("epc-ext-bars", einv.BodyHtml, StringComparison.Ordinal);

        Assert.Contains("function epcExtPrint", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
        Assert.Contains("function epcExtWord", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
        Assert.Contains("application/msword", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
        Assert.Contains("epc_ext_doc", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", ErpExternalReportingHtml.PrintFnJs, StringComparison.Ordinal);
    }

    private static ErpExternalReportingBuildInput Input(string key, DateTime from, DateTime to, decimal sales, decimal purch)
    {
        var def = ErpExternalReportingCatalog.Find(key)!;
        return new(
            def,
            "AE",
            "United Arab Emirates",
            "AED",
            "ECOM AE General Trading LLC",
            "100399998800003",
            from,
            to,
            "FY" + to.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            sales,
            purch,
            decimal.Round(sales * 0.05m, 2),
            decimal.Round(purch * 0.05m, 2),
            5m,
            true);
    }

    [Fact]
    public void ImportTemplatesBuildStatutoryPacks()
    {
        var vatMap = ErpExternalReportingImport.ParseCsvText(ErpExternalReportingCatalog.ImportTemplateCsv("vat"));
        Assert.Equal("vat", ErpExternalReportingImport.DetectKind(vatMap, "vat"));
        Assert.Null(ErpExternalReportingImport.ValidateKind(vatMap, "vat"));
        var vat = ErpExternalReportingImport.Build("vat", vatMap, "AED", "AE");
        Assert.Contains("Box 14", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("VAT Return", vat.Title, StringComparison.Ordinal);
        Assert.Contains("epc-ext-bars", vat.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("vat", ErpExternalReportingImport.DetectKind(vatMap, "fin"));

        var ctMap = ErpExternalReportingImport.ParseCsvText(ErpExternalReportingCatalog.ImportTemplateCsv("ct"));
        Assert.Equal("ct", ErpExternalReportingImport.DetectKind(ctMap, "ct"));
        var ct = ErpExternalReportingImport.Build("ct", ctMap, "AED", "AE");
        Assert.Contains("0%", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("9%", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("AED 375,000", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Art. 33", ct.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("0% / 9%", ct.Summary.First(s => s.Label == "Rate").Value);

        var finMap = ErpExternalReportingImport.ParseCsvText(ErpExternalReportingCatalog.ImportTemplateCsv("fin"));
        Assert.Equal("fin", ErpExternalReportingImport.DetectKind(finMap, "fin"));
        var fin = ErpExternalReportingImport.Build("fin", finMap, "AED", "AE");
        Assert.Contains("IFRS 18", fin.Title, StringComparison.Ordinal);
        Assert.Contains("Operating profit or loss", fin.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("red", fin.Theme);

        var intake = ErpExternalReportingImport.BuildIntake("Acme Trading LLC", 2026, "Trading, Contracting", "AED", "AE");
        Assert.Contains("guided intake", intake.Title, StringComparison.Ordinal);
        Assert.Contains("IFRS 18", intake.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Trading, Contracting", intake.BodyHtml, StringComparison.Ordinal);
        Assert.Equal("red", intake.Theme);

        var cache = new ErpExternalReportingPackCache();
        var id = cache.Store(vat, "vat", "Detected VAT lines in the file — switched the builder.");
        Assert.True(cache.TryGet(id, out var pack));
        Assert.NotNull(pack);
        Assert.Equal("vat", pack!.Kind);
        Assert.Contains("Box 14", pack.Built.BodyHtml, StringComparison.Ordinal);
        Assert.False(cache.TryGet("missing", out _));
    }

    [Fact]
    public void LinkedExcelPacksHaveLiveFormulas()
    {
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 12, 31);
        var data = ErpExternalReportingFin.Dataset(from, to, 8_400_000m, 5_800_000m);
        Assert.Equal(2026, data.CurYear);
        Assert.True(data.Cur.Rev > 0);
        Assert.True(data.Cur.TotalAssets > 0);
        var proj = ErpExternalReportingFin.Project(data);
        Assert.Equal(5, proj.Years.Count);

        var model = ErpExternalReportingXlsx.FinModel(data);
        var modelXml = XlsxInnerXml(model);
        Assert.Contains("Assumptions", modelXml, StringComparison.Ordinal);
        Assert.Contains("Calculations", modelXml, StringComparison.Ordinal);
        Assert.Contains("Results", modelXml, StringComparison.Ordinal);
        Assert.Contains("Assumptions!$B$3", modelXml, StringComparison.Ordinal);
        Assert.Contains("Calculations!B26", modelXml, StringComparison.Ordinal);
        Assert.Contains("PK", System.Text.Encoding.ASCII.GetString(model, 0, 2), StringComparison.Ordinal);

        var audit = ErpExternalReportingXlsx.Audit(data, "AED", "Acme Trading LLC", "AE");
        var auditXml = XlsxInnerXml(audit);
        Assert.Contains("Trial Balance", auditXml, StringComparison.Ordinal);
        Assert.Contains("Financial Position", auditXml, StringComparison.Ordinal);
        Assert.Contains("Profit &amp; Loss OCI", auditXml, StringComparison.Ordinal);
        Assert.True(
            auditXml.Contains("'Trial Balance'!C3", StringComparison.Ordinal)
            || auditXml.Contains("&#39;Trial Balance&#39;!C3", StringComparison.Ordinal),
            "Audit pack must link SOFP figures to the Trial Balance.");
        Assert.Contains("IFRS 18", auditXml, StringComparison.Ordinal);
        Assert.Contains("ISA 700", auditXml, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", auditXml, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", auditXml, StringComparison.Ordinal);
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

    private static string XlsxInnerXml(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var sb = new System.Text.StringBuilder();
        foreach (var entry in zip.Entries)
        {
            using var s = entry.Open();
            using var r = new StreamReader(s);
            sb.Append(r.ReadToEnd());
        }

        return sb.ToString();
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
