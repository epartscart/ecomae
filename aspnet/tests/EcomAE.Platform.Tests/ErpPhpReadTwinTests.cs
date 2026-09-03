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

        var pages = Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages");
        Assert.True(File.Exists(Path.Combine(pages, "ErpWorkflowApp.razor")));
        Assert.Contains("BuildErpVatReturnDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpVatApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpWithholdingDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWithholdingApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListErpPettyCashAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpCashForecastDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpCashAccountsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpBankInstrumentsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpBankReconciliationApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSubscriptionsDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSalesOrdersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpSupplierPortalDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpSuppliersApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildErpVirtualWarehouseDigestAsync", File.ReadAllText(Path.Combine(pages, "ErpWarehousesApp.razor")), StringComparison.Ordinal);
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
