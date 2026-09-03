using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPhpReadTwinTests
{
    [Fact]
    public void WorkflowTabMapsToDedicatedApp_NotProcessFlow()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("workflow", out var href));
        Assert.Equal("/erp/workflow-app", href);
        Assert.Equal("/erp/workflow-app", EcomAeRoutes.ErpWorkflowApp);
        Assert.Equal("/erp/process-flow-tasks-app", EcomAeRoutes.ErpProcessFlowTasksApp);
        Assert.NotEqual(EcomAeRoutes.ErpWorkflowApp, EcomAeRoutes.ErpProcessFlowTasksApp);
    }

    [Fact]
    public void MissingPhpTabsHaveSqlAndApps()
    {
        var root = FindRepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectErpWorkflowTasks", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpVatReturnSales", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpWithholdingCodes", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPettyCash", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpCashForecasts", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpBankInstruments", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpSubscriptions", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpSupplierPortalSuppliers", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpVirtualWarehouses", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpStaffProfiles", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpContracts", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpOpeningBatches", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpMarketingCampaigns", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPayrollRuns", sql, StringComparison.Ordinal);
        Assert.Contains("SelectErpPrintTemplates", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("body_text", LegacySurfaceDashboardSql.SelectErpContracts, StringComparison.Ordinal);
        Assert.DoesNotContain("ocr_text", LegacySurfaceDashboardSql.SelectErpContracts, StringComparison.Ordinal);
        Assert.DoesNotContain("header_html", LegacySurfaceDashboardSql.SelectErpPrintTemplates, StringComparison.Ordinal);
        Assert.DoesNotContain("custom_css", LegacySurfaceDashboardSql.SelectErpPrintTemplates, StringComparison.Ordinal);

        var pages = Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages");
        Assert.True(File.Exists(Path.Combine(pages, "ErpWorkflowApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpStaffApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpContractsApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpOpeningApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpMarketingApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpPayrollApp.razor")));
        Assert.True(File.Exists(Path.Combine(pages, "ErpPrintDesignerApp.razor")));
        Assert.Contains("BuildErpVatReturnDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpVatApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpWithholdingDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWithholdingApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPettyCashAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpCashForecastDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpBankInstrumentsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpBankReconciliationApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSubscriptionsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSalesOrdersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSupplierPortalDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSuppliersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpVirtualWarehouseDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWarehousesApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpStaffAsync", File.ReadAllText(Path.Combine(pages, "ErpStaffApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpContractsAsync", File.ReadAllText(Path.Combine(pages, "ErpContractsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpOpeningBatchesAsync", File.ReadAllText(Path.Combine(pages, "ErpOpeningApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpMarketingCampaignsAsync", File.ReadAllText(Path.Combine(pages, "ErpMarketingApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPayrollRunsAsync", File.ReadAllText(Path.Combine(pages, "ErpPayrollApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPrintTemplatesAsync", File.ReadAllText(Path.Combine(pages, "ErpPrintDesignerApp.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void DedicatedPeopleAndSetupTabsMapOffPhpAndGenericShells()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("staff", out var staff));
        Assert.Equal("/erp/staff-app", staff);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("contracts", out var contracts));
        Assert.Equal("/erp/contracts-app", contracts);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("opening", out var opening));
        Assert.Equal("/erp/opening-app", opening);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("opening_balances", out var openingBalances));
        Assert.Equal("/erp/opening-app", openingBalances);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("marketing", out var marketing));
        Assert.Equal("/erp/marketing-app", marketing);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("payroll", out var payroll));
        Assert.Equal("/erp/payroll-app", payroll);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("print_designer", out var print));
        Assert.Equal("/erp/print-designer-app", print);
        Assert.Equal("/erp/staff-app", EcomAeRoutes.ErpStaffApp);
        Assert.Equal("/erp/print-designer-app", EcomAeRoutes.ErpPrintDesignerApp);
        Assert.DoesNotContain("/cp/hr-overview-app", staff, StringComparison.Ordinal);
        Assert.DoesNotContain("/erp/module-app", print, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpAppsDoNotDiscloseAspNetInMarkup()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages");
        var offenders = Directory.GetFiles(dir, "Erp*App.razor")
            .Where(f => File.ReadAllText(f).Contains("ASP.NET Core digest", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(offenders.Count == 0, "ASP.NET disclosure leftovers: " + string.Join(", ", offenders));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
