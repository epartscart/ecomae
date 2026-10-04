using Xunit;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Tests;

/// <summary>Static parity pins for the qm_order_record / demo_seed_sales / demo_clear_sales live twins.</summary>
public sealed class ErpQmDemoPhpParityTests
{
    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    private static string ReadRepo(string relative) => File.ReadAllText(FindRepoFile(relative));

    [Fact]
    public void QmOrderRecordCatalogRowIsLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(r => r.AspNetRouteOrCapability == EcomAeRoutes.ErpAjaxQmOrderRecord);
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes);
    }

    [Fact]
    public void DemoRoutesCatalogRowsAreLiveGated()
    {
        foreach (var route in new[] { EcomAeRoutes.ErpAjaxDemoSeedSales, EcomAeRoutes.ErpAjaxDemoClearSales })
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(r => r.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.Contains("confirm_writes=true", row.Notes);
        }
    }

    [Fact]
    public void HandlersAreWiredToLiveWriteServicesAndCsrfKept()
    {
        var module = ReadRepo("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxQmOrderRecord, HandleQmOrderRecordAsync).DisableAntiforgery();", module);
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxDemoSeedSales, HandleDemoSeedSalesAsync).DisableAntiforgery();", module);
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxDemoClearSales, HandleDemoClearSalesAsync).DisableAntiforgery();", module);
        Assert.Contains("Results saved · verdict: ", module);
        Assert.Contains("IErpQmOrderRecordWriteService", module);
        Assert.Contains("IErpDemoSalesWriteService", module);
    }

    [Fact]
    public void DemoServiceKeepsPhpContract()
    {
        var service = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpDemoSalesWriteService.cs");
        Assert.Contains("DEMO-SALE", service);
        Assert.Contains("20260601", service); // PHP mt_srand seed constant
        Assert.Contains("for_finish` = 1", service);
        Assert.Contains("count_flag` != 0", service);
        Assert.Contains("No finished-order status configured in this store", service);
        Assert.Contains("No costed inventory items to base sample sales on", service);
        Assert.Contains("shop_orders_items_details", service);
        Assert.Contains("shop_orders_logs", service);
    }

    [Fact]
    public void ServicesAreRegistered()
    {
        var program = ReadRepo("aspnet/src/EcomAE.Platform/Program.cs");
        Assert.Contains("IErpDemoSalesWriteService", program);
        Assert.Contains("IErpQmOrderRecordWriteService", program);
    }
}
