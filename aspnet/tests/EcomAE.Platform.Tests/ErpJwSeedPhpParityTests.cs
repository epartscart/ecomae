using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwSeedPhpParityTests
{
    private static string RepoFile(params string[] parts)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
    }

    [Fact]
    public void JwSeedService_PortAnchorPhpContracts()
    {
        var service = RepoFile("src/EcomAE.Platform/Erp/ErpJwSeedWriteService.cs");
        Assert.Contains("epc_erp_jw_repairs", service);
        Assert.Contains("epc_erp_jw_weight_ledger", service);
        Assert.Contains("erp_industry_profile", service);
        Assert.Contains("jewellery", service);
        Assert.Contains("epc_erp_inv_warehouses", service);
        Assert.Contains("epc_erp_inv_items", service);
        Assert.Contains("epc_erp_inv_stock", service);
        Assert.Contains("epc_erp_contacts", service);
        Assert.Contains("epc_erp_purchase_orders", service);
        Assert.Contains("epc_erp_sales_orders", service);
        Assert.Contains("epc_erp_sales_order_lines", service);
        Assert.Contains("jw_metal_weight_gm", service);
        Assert.Contains("jw_weight_on_hand", service);
        Assert.Contains("WH-SHOWROOM", service);
        Assert.Contains("GR-22K-001", service);
        Assert.Contains("PostWeightLedgerAsync", service);
        Assert.Contains("NextAsync(connection, null, \"SO\"", service);
        Assert.Contains("x_repair_no", service);
    }

    [Fact]
    public void JwSeedHandler_BindsPhpContract()
    {
        var module = RepoFile("src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("HandleJwSeedSampleDataAsync", module);
        Assert.Contains("ErpAjaxJwSeedSampleData, HandleJwSeedSampleDataAsync", module);
        Assert.Contains("Sample data seeded", module);
        Assert.Contains("seeded = new", module);
        Assert.Contains("gl_entries", module);
    }

    [Fact]
    public void Catalog_PromotesJwSeedToLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(
            item => item.AspNetRouteOrCapability == "/erp/ajax/jw-seed-sample-data");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes);
    }
}
