using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGoldRateBoardTests
{
    [Fact]
    public void DefaultKarats_ScaleFrom24KBase()
    {
        Assert.Equal("22K", ErpGoldRateBoard.NormalizeKarat("22"));
        Assert.Equal(1m, ErpGoldRateBoard.FactorFor("24K"));
        Assert.Equal(0.9167m, ErpGoldRateBoard.FactorFor("22K"));
        Assert.Equal(0.75m, ErpGoldRateBoard.FactorFor("18K"));
        Assert.Equal(229.18m, decimal.Round(250m * ErpGoldRateBoard.FactorFor("22K"), 2, MidpointRounding.AwayFromZero));
        Assert.Equal("22K", ErpGoldRateBoard.KaratFromPurity(0.9167m));
        Assert.Equal(147.8933m, ErpGoldRateBoard.ToGramRate(4600m, "ounce"));
    }

    [Fact]
    public void SchemaAndBoard_ProvisionDailyRatesAndApplyOnInvoice()
    {
        var schema = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJewelSchema.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_gold_rates`", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_gold_rate_config`", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_gold_schemes`", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_gold_scheme_enrollments`", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_gold_scheme_payments`", schema, StringComparison.Ordinal);
        Assert.Contains("UNIQUE KEY `ux_date_karat_cur`", schema, StringComparison.Ordinal);

        var writes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpGoldRateSetWriteService.cs"));
        Assert.Contains("PublishDailyAsync", writes, StringComparison.Ordinal);
        Assert.Contains("EnsureTodayAsync", writes, StringComparison.Ordinal);
        Assert.Contains("LoadBoardAsync", writes, StringComparison.Ordinal);

        var voucher = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJwVoucherWriteService.cs"));
        Assert.Contains("ErpGoldRateBoard.ResolveSellAsync", voucher, StringComparison.Ordinal);

        var purchase = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJwBarcodePurchaseWriteService.cs"));
        Assert.Contains("ErpGoldRateBoard.ResolveSellAsync", purchase, StringComparison.Ordinal);

        var scheme = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpJwGoldSchemeWriteService.cs"));
        Assert.Contains("ErpGoldRateBoard.ResolveSellAsync", scheme, StringComparison.Ordinal);
        Assert.Contains("LoadBoardAsync", scheme, StringComparison.Ordinal);

        var page = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryMastersApp.razor"));
        Assert.Contains("/erp/gold-rate/daily", page, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryGoldSchemeCreateForm", page, StringComparison.Ordinal);
        Assert.Contains("WriteAction", page, StringComparison.Ordinal);
        Assert.Contains("IsStaleProvisionFlash", page, StringComparison.Ordinal);
        Assert.Contains("Publish daily rates", page, StringComparison.Ordinal);
        Assert.Contains("applies on jewellery invoices", page, StringComparison.Ordinal);

        var routes = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpGoldRateDailyForm = \"/erp/gold-rate/daily\"", routes, StringComparison.Ordinal);
    }

    private static string Find(string relative)
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
