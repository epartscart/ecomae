using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOrderFulfillmentPhpParityTests
{
    [Fact]
    public void Program_RegistersOrderFulfillmentWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOrderFulfillmentWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpOrderFulfillmentWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAllSevenFulfillmentRoutesLiveGated()
    {
        string[] routes =
        [
            "/erp/ajax/order-fulfillment-bootstrap",
            "/erp/ajax/order-fulfillment-status",
            "/erp/ajax/order-fulfillment-sync",
            "/erp/ajax/order-fulfillment-post-po",
            "/erp/ajax/order-fulfillment-post-sales",
            "/erp/ajax/order-fulfillment-auto-post",
            "/erp/ajax/order-fulfillment-swap-supplier",
        ];
        foreach (var route in routes)
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(f => f.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.DoesNotContain("confirm_writes refused", row.Notes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Service_ImplementsPhpFulfillmentFunctions()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOrderFulfillmentWriteService.cs"));
        foreach (var token in new[]
        {
            "BootstrapAsync", "StatusAsync", "SyncAsync", "PostPoInvoiceAsync",
            "PostSalesInvoiceAsync", "AutoPostAsync", "SwapLineSupplierAsync",
            "epc_erp_po_lines", "shop_order_item_id", "fulfillment_status",
            "Shop order not found", "t2_json_params",
        })
        {
            Assert.Contains(token, service, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Module_WiresLiveGatedFulfillmentRoutes()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        foreach (var constant in new[]
        {
            "ErpAjaxOrderFulfillmentBootstrap", "ErpAjaxOrderFulfillmentStatus",
            "ErpAjaxOrderFulfillmentSync", "ErpAjaxOrderFulfillmentPostPo",
            "ErpAjaxOrderFulfillmentPostSales", "ErpAjaxOrderFulfillmentAutoPost",
            "ErpAjaxOrderFulfillmentSwapSupplier",
        })
        {
            Assert.Contains("EcomAeRoutes." + constant, text, StringComparison.Ordinal);
        }
        Assert.Contains("HandleOrderFulfillmentAsync", text, StringComparison.Ordinal);
        Assert.Contains("IErpOrderFulfillmentWriteService", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("LegacySessionRecord", text, StringComparison.Ordinal);
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
