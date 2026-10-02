using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJewelleryBlueprintParityTests
{
    [Fact]
    public void Observed_blueprint_controls_are_present_in_the_replacement_workspaces()
    {
        var masters = ReadApp("CpJewelleryMastersApp.razor");
        var retail = ReadApp("CpJewelleryRetailApp.razor");
        var stock = ReadApp("CpJewelleryStockVerificationApp.razor");
        var fixing = ReadApp("CpJewelleryFixingApp.razor");
        var repairs = ReadApp("CpJewelleryRepairsApp.razor");

        Assert.Contains("name=\"gross_wt\"", masters, StringComparison.Ordinal);
        Assert.Contains("name=\"purity\"", masters, StringComparison.Ordinal);
        Assert.Contains("name=\"gross_wt\"", retail, StringComparison.Ordinal);
        Assert.Contains("name=\"line_purity\"", retail, StringComparison.Ordinal);
        Assert.Contains("name=\"branch\"", stock, StringComparison.Ordinal);
        Assert.Contains("tab=jw_stock_verification", stock, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", stock, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Showroom\"", stock, StringComparison.Ordinal);
        Assert.Contains("name=\"fixed_rate\"", fixing, StringComparison.Ordinal);
        Assert.Contains("name=\"gross_wt_in\"", repairs, StringComparison.Ordinal);
        Assert.Contains("name=\"stone_details\"", repairs, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/jewellery-retail-app\"", retail, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/jewellery-stock-verification-app\"", stock, StringComparison.Ordinal);
    }

    [Fact]
    public void Blueprint_dimensions_and_php_owned_tables_remain_explicit()
    {
        var voucher = ReadSource("ErpJwVoucherWriteService.cs");
        var barcode = ReadSource("ErpJwBarcodeWriteService.cs");
        var tag = ReadSource("ErpJwTagWriteService.cs");
        var design = ReadSource("ErpJwDesignDetailReadService.cs");

        Assert.Contains("company_id", voucher, StringComparison.Ordinal);
        Assert.Contains("branch", voucher, StringComparison.Ordinal);
        Assert.Contains("division", voucher, StringComparison.Ordinal);
        Assert.Contains("purity", voucher, StringComparison.Ordinal);
        Assert.Contains("pure_wt", voucher, StringComparison.Ordinal);
        Assert.Contains("epc_jewel_barcode", barcode, StringComparison.Ordinal);
        Assert.Contains("epc_jewel_design_metals", design, StringComparison.Ordinal);
        Assert.Contains("epc_jewel_design_stones", design, StringComparison.Ordinal);
        Assert.Contains("company_id", tag, StringComparison.Ordinal);
        Assert.Contains("status` = 'in_stock'", tag, StringComparison.Ordinal);
    }

    [Fact]
    public void Blueprint_visual_processes_have_named_php_parity_specs()
    {
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, tab => tab.Key == "jewellery_tag");
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, tab => tab.Key == "jw_diamond");
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, tab => tab.Key == "jw_design");
        Assert.Contains(ErpJewelleryModuleChrome.RetailTabs, tab => tab.Key == "jw_retail_sales");
        Assert.Contains(ErpJewelleryModuleChrome.StockTabs, tab => tab.Key == "jw_stock_verification");
        Assert.Contains(ErpJewelleryModuleChrome.FixingTabs, tab => tab.Key == "jw_purchase_fixing");
        Assert.Contains(ErpJewelleryModuleChrome.RepairTabs, tab => tab.Key == "jw_repair_receipt");
    }

    private static string ReadApp(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string ReadSource(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Erp", fileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Presentation", fileName);
        }
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
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

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
