using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwVoucherWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task SaveRequiresVoucherType()
    {
        var result = await new ErpJwVoucherWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwVoucherSaveRequest(CustomerName: "Walk-in", NetAmount: 10));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Voucher type required", result.Message);
    }

    [Fact]
    public void NormalizeVocType_InfersPurchaseAndSaleAliases()
    {
        Assert.Equal("MMP", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_metal_purchase_save"));
        Assert.Equal("DMP", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_diamond_purchase_save"));
        Assert.Equal("RSI", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_retail_sale_save"));
        Assert.Equal("MSI", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_metal_sale_save"));
        Assert.Equal("SRN", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_sales_return_save"));
        Assert.Equal("PAD", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_pos_advance_save"));
        Assert.Equal("JVG", ErpJwVoucherWriteService.NormalizeVocType(null, "jw_journal_voucher_save"));
        Assert.Equal("RSI", ErpJwVoucherWriteService.NormalizeVocType("RSI", "jw_metal_purchase_save"));
    }

    [Fact]
    public void VoucherLineContractPreservesPhpValuationFields()
    {
        var line = new ErpJwVoucherLineSaveRequest(
            StockCode: "28K-001",
            Division: "G",
            Description: "Gold chain",
            Pcs: 1,
            GrossWeight: 10.25m,
            Purity: 0.916m,
            MetalRate: 250m,
            MakingRate: 15m,
            StoneAmount: 25m,
            DiscountAmount: 5m);

        Assert.Equal("28K-001", line.StockCode);
        Assert.Equal(10.25m, line.GrossWeight);
        Assert.Equal(0.916m, line.Purity);
        Assert.Equal(250m, line.MetalRate);
        Assert.Equal(15m, line.MakingRate);
        Assert.Equal(25m, line.StoneAmount);
        Assert.Equal(5m, line.DiscountAmount);
    }
}
