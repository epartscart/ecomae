using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Empty ERP/CP modules must still render PHP page chrome (tabs, forms, columns).</summary>
public sealed class ErpCpEmptyModuleParityTests
{
    [Fact]
    public void QualityApp_UsesPhpViewTabsAndNativeForms()
    {
        var text = ReadApp("ErpQualityApp.razor");
        Assert.Contains("qv", text, StringComparison.Ordinal);
        Assert.Contains("Quality orders", text, StringComparison.Ordinal);
        Assert.Contains("Test plans", text, StringComparison.Ordinal);
        Assert.Contains("Non-conformance", text, StringComparison.Ordinal);
        Assert.Contains("New test plan", text, StringComparison.Ordinal);
        Assert.Contains("Raise non-conformance", text, StringComparison.Ordinal);
        Assert.Contains("New quality order", text, StringComparison.Ordinal);
        Assert.Contains("ErpQualityPlanSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpQualityOrderCreateForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpQualityNcrCreateForm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RfidApp_ShowsPhpScannerAndConfigChrome()
    {
        var text = ReadApp("ErpRfidApp.razor");
        Assert.Contains("RFID tag management", text, StringComparison.Ordinal);
        Assert.Contains("RFID scanner", text, StringComparison.Ordinal);
        Assert.Contains("RFID bulk scan mode", text, StringComparison.Ordinal);
        Assert.Contains("Recent scans", text, StringComparison.Ordinal);
        Assert.Contains("RFID configuration", text, StringComparison.Ordinal);
        Assert.Contains("Anti-theft gate", text, StringComparison.Ordinal);
        Assert.Contains("Date/time", text, StringComparison.Ordinal);
        Assert.Contains("Discrepancy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2,458", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffApp_ShowsPhpDepartmentMap()
    {
        var text = ReadApp("ErpStaffApp.razor");
        Assert.Contains("Department map", text, StringComparison.Ordinal);
        Assert.Contains("Staff directory", text, StringComparison.Ordinal);
        Assert.Contains("Dashboard centre", text, StringComparison.Ordinal);
        Assert.Contains("ErpStaffDepartmentCatalog", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductInfoApp_UsesPhpPmViewTabs()
    {
        var text = ReadApp("ErpProductInfoApp.razor");
        Assert.Contains("pm_view", text, StringComparison.Ordinal);
        Assert.Contains("Product dev kit", text, StringComparison.Ordinal);
        Assert.Contains("Release product", text, StringComparison.Ordinal);
        Assert.Contains("Dimensions &amp; variants", text, StringComparison.Ordinal);
        Assert.Contains("Field setup", text, StringComparison.Ordinal);
        Assert.Contains("ErpProductInfoCreateItemForm", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
    }

    [Fact]
    public void HrOverview_UsesPhpSalaryAndStatutoryColumns()
    {
        var text = ReadApp("CpHrOverviewApp.razor");
        Assert.Contains("Fixed basic", text, StringComparison.Ordinal);
        Assert.Contains("Allowances", text, StringComparison.Ordinal);
        Assert.Contains("Days worked", text, StringComparison.Ordinal);
        Assert.Contains("Est. pay", text, StringComparison.Ordinal);
        Assert.Contains("Gratuity", text, StringComparison.Ordinal);
        Assert.Contains("Annual leave", text, StringComparison.Ordinal);
        Assert.Contains("ListErpHrRecordsAsync", text, StringComparison.Ordinal);
        Assert.Contains("CpPhpModuleCopy.PurposeFor", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Manage store operations, catalogue, and partner integrations", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleApp_RendersPhpCatalogChrome()
    {
        var text = ReadApp("ErpModuleApp.razor");
        Assert.Contains("ErpPhpModuleChromeCatalog", text, StringComparison.Ordinal);
        Assert.Contains("ErpPhpCreateWell", text, StringComparison.Ordinal);
        Assert.Contains("EmptyCopy", text, StringComparison.Ordinal);
        Assert.Contains("Query[\"php\"]", text, StringComparison.Ordinal);
        Assert.Contains("Opt-in only", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DepartmentCatalog_MatchesPhpStaffConfig()
    {
        Assert.Contains(ErpStaffDepartmentCatalog.All, d => d.Code == "sales" && d.Name == "Sales");
        Assert.Contains(ErpStaffDepartmentCatalog.All, d => d.Code == "hr" && d.Name == "Human Resources");
        Assert.Contains(ErpStaffDepartmentCatalog.All, d => d.Code == "finance");
        Assert.Equal("Sales", ErpStaffDepartmentCatalog.NameFor("sales"));
        Assert.True(ErpStaffDepartmentCatalog.All.Count >= 8);
    }

    [Fact]
    public void HrStatutory_AeGratuityMatchesPhp()
    {
        var under = ErpHrStatutory.GratuityAe(10000m, 0.5);
        Assert.False(under.Eligible);
        var mid = ErpHrStatutory.GratuityAe(10000m, 3.0);
        Assert.True(mid.Eligible);
        Assert.Equal(21000m, mid.Amount); // 3 * 21 * (10000/30)
        Assert.Equal(9000m, ErpHrStatutory.EstPay(6000m, 3000m, 30));
        Assert.Equal(4500m, ErpHrStatutory.EstPay(6000m, 3000m, 15));
    }

    [Fact]
    public void CpModuleCopy_IsNotGenericForKnownModules()
    {
        Assert.False(CpPhpModuleCopy.IsGeneric(CpPhpModuleCopy.PurposeFor("/cp/hr-overview-app")));
        Assert.Contains("salary", CpPhpModuleCopy.PurposeFor("/cp/hr-overview-app"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("credit", CpPhpModuleCopy.PurposeFor("/cp/credit-limits-app"), StringComparison.OrdinalIgnoreCase);
        Assert.False(CpPhpModuleCopy.IsGeneric(CpPhpModuleCopy.PurposeFor("/cp/landed-cost-app")));
    }

    [Fact]
    public void QualityFormRoutes_AreDedicatedHtmlPosts()
    {
        Assert.Equal("/erp/quality/plan-save", EcomAeRoutes.ErpQualityPlanSaveForm);
        Assert.Equal("/erp/quality/order-create", EcomAeRoutes.ErpQualityOrderCreateForm);
        Assert.Equal("/erp/quality/ncr-create", EcomAeRoutes.ErpQualityNcrCreateForm);
        Assert.Equal("/erp/product-info/create-item", EcomAeRoutes.ErpProductInfoCreateItemForm);
    }

    [Fact]
    public void HrSql_OmitsNotesAndMasksBank()
    {
        Assert.Contains("epc_erp_hr_records", LegacySurfaceDashboardSql.SelectErpHrRecords, StringComparison.Ordinal);
        Assert.Contains("bank_account_preview", LegacySurfaceDashboardSql.SelectErpHrRecords, StringComparison.Ordinal);
        Assert.DoesNotContain("`notes`", LegacySurfaceDashboardSql.SelectErpHrRecords, StringComparison.Ordinal);
        Assert.Contains("RIGHT(", LegacySurfaceDashboardSql.SelectErpHrRecords, StringComparison.Ordinal);
    }

    [Fact]
    public void CpApps_NoLongerUseGenericHeroCopy()
    {
        var dir = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Components", "Pages");
        var offenders = Directory.GetFiles(dir, "Cp*App.razor")
            .Where(f => File.ReadAllText(f).Contains("Manage store operations, catalogue, and partner integrations", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(offenders.Count == 0, "Generic CP hero leftovers: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DryRunHtmlForm_RejectsExternalReturnUrl()
    {
        var evil = new DefaultHttpContext();
        evil.Request.QueryString = new QueryString("?returnUrl=https://evil.example/");
        Assert.Equal("/erp/quality-app", DryRunHtmlForm.SafeReturnUrl(evil.Request, "/erp/quality-app"));

        var ok = new DefaultHttpContext();
        ok.Request.QueryString = new QueryString("?returnUrl=/erp/quality-app?qv=plans");
        Assert.Equal("/erp/quality-app?qv=plans", DryRunHtmlForm.SafeReturnUrl(ok.Request, "/erp/fallback"));
    }

    [Fact]
    public void ChromeCatalog_QualityAndRfidHavePhpSections()
    {
        var q = ErpPhpModuleChromeCatalog.ForTab("quality");
        Assert.Contains(q.ViewTabs, t => t.Key == "ncr");
        Assert.Contains(q.KpiLabels, l => l.Contains("NCR", StringComparison.OrdinalIgnoreCase));
        var r = ErpPhpModuleChromeCatalog.ForTab("rfid");
        Assert.Contains(r.Sections, s => s.Title.Contains("scan", StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadApp(string fileName)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
                return dir.FullName;
            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
