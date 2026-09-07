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
}
