using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPfDemoSyncPhpParityTests
{
    [Fact]
    public void Program_RegistersPfDemoSyncWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfDemoSyncWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPfDemoSyncWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAllThreePfRoutesLiveGated()
    {
        string[] routes =
        [
            "/erp/ajax/pf-seed-demo",
            "/erp/ajax/pf-clear-demo",
            "/erp/ajax/pf-sync-orders",
        ];
        foreach (var route in routes)
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(f => f.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.DoesNotContain("confirm_writes refused", row.Notes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Service_ImplementsPhpProcessFlowFunctions()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfDemoSyncWriteService.cs"));
        foreach (var token in new[]
        {
            "SeedDemoAsync", "ClearDemoAsync", "SyncTasksAsync",
            "epc_pf_processes", "epc_pf_steps", "epc_pf_cases", "epc_pf_case_steps",
            "epc_pf_dept_heads", "epc_erp_staff_profiles",
            "@pf-demo.local", "[PF-DEMO]", "DEMO-PF-",
            "order_lifecycle", "po_lifecycle", "payment_lifecycle", "expense_lifecycle",
            "shop_order", "erp_po", "erp_payment", "erp_expense",
            "Auto-advanced from order status", "Dubai HQ", "ECOM AE FZE",
        })
        {
            Assert.Contains(token, service, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Module_WiresLiveGatedPfRoutes()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        foreach (var constant in new[]
        {
            "ErpAjaxPfSeedDemo", "ErpAjaxPfClearDemo", "ErpAjaxPfSyncOrders",
        })
        {
            Assert.Contains("EcomAeRoutes." + constant, text, StringComparison.Ordinal);
        }
        Assert.Contains("HandlePfSeedDemoAsync", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfClearDemoAsync", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfSyncOrdersAsync", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfDemoSyncWriteService", text, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }
        return Path.Combine(dir!.FullName, relative);
    }
}
