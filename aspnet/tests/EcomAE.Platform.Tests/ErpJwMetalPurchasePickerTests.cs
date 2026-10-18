using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwMetalPurchasePickerTests
{
    [Fact]
    public void PureWeight_MatchesFixingGoldScreenshot()
    {
        Assert.Equal(99.91m, ErpJwPurity.PureWeight(100.01m, 0.999m));
        Assert.Equal(10.3508m, ErpJwPurity.PureWeight(11.3m, 916m));
        Assert.Equal(48950.00m, ErpJwPurity.MetalAmount(100.01m, 0.999m, 489.94095m));
    }

    [Fact]
    public async Task MetalPurchaseRequiresPartyAndStockBeforeDb()
    {
        var unused = new UnusedConnections();
        var noParty = await new ErpJwVoucherWriteService(unused)
            .SaveAsync(new ErpJwVoucherSaveRequest(
                Action: "jw_metal_purchase_save",
                VocType: "RMP",
                Lines: [new ErpJwVoucherLineSaveRequest(StockCode: "24TTB")]));
        Assert.False(noParty.Succeeded);
        Assert.Equal("Party is required. Pick an approved supplier.", noParty.Message);

        var noStock = await new ErpJwVoucherWriteService(unused)
            .SaveAsync(new ErpJwVoucherSaveRequest(
                Action: "jw_metal_purchase_save",
                VocType: "RMP",
                PartyCode: "V-0001"));
        Assert.False(noStock.Succeeded);
        Assert.Equal("Stock code is required. Pick a metal stock item.", noStock.Message);
    }

    [Fact]
    public void MetalStockAndPurchaseFormsUseMasterPickers()
    {
        var stock = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryStockVerificationApp.razor"));
        Assert.Contains("Metal Stock Master", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-metal-stock\"", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-karat\"", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"supplier\"", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-type\"", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-brand\"", stock, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-category\"", stock, StringComparison.Ordinal);
        Assert.Contains("IErpJwMetalStockListReadService", stock, StringComparison.Ordinal);
        Assert.Contains("Save metal stock", stock, StringComparison.Ordinal);

        var purchase = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor"));
        Assert.Contains("Metal Purchase (FIXING GOLD)", purchase, StringComparison.Ordinal);
        Assert.Contains("Kind=\"jw-metal-stock\"", purchase, StringComparison.Ordinal);
        Assert.Contains("Kind=\"supplier\"", purchase, StringComparison.Ordinal);
        Assert.Contains("addin_code", purchase, StringComparison.Ordinal);
        Assert.Contains("line_pure_wt", purchase, StringComparison.Ordinal);
        Assert.Contains("data-epc-jw-purchase", purchase, StringComparison.Ordinal);
        Assert.Contains("RMP", purchase, StringComparison.Ordinal);
    }

    [Fact]
    public void LookupKindsAndAssetsAreWired()
    {
        Assert.Equal("jw-metal-stock", ErpMasterLookupReadService.ResolveKind("jw-item"));
        Assert.Equal("/erp/jewellery-stock-verification-app?tab=jw_metal_stock", ErpMasterLookupReadService.MasterHref("jw-metal-stock"));
        Assert.Equal("Karat", ErpMasterLookupReadService.KindLabel("jw-karat"));

        var assets = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpAppAssets.cs"));
        Assert.Contains("erp-jewellery-purchase.js", assets, StringComparison.Ordinal);
        Assert.True(File.Exists(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/erp/erp-jewellery-purchase.js")));

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpJwMetalStockListReadService", program, StringComparison.Ordinal);
    }

    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
