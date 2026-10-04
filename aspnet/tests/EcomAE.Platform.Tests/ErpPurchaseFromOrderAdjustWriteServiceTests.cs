using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseFromOrderAdjustWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpPurchaseFromOrderAdjustWriteService Service()
    {
        var connections = new UnconfiguredConnections();
        var vouchers = new ErpVoucherNumberService();
        var tax = new ErpTaxAmountCalculator();
        var gl = new ErpGlPostingService(vouchers);
        var audit = new ErpAuditLogWriter();
        return new(
            connections,
            new ErpPurchaseInvoiceWriteService(connections, vouchers, tax, gl, audit),
            tax,
            new ErpCashWriteService(connections, vouchers, gl, audit, new ErpSettlementAllocationService(), new ErpAdvanceVatService(gl)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 0)]
    public async Task FromOrderRejectsNonPositiveIdsBeforeOpeningConnection(long orderId, int supplierId)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().FromOrderAsync(orderId, supplierId, 1));
        Assert.Equal("orderId and supplierId must be positive", ex.Message);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(7, 0.004)]
    public async Task AdjustRejectsInvalidInputLikePhp(long purchaseId, double delta)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(
            () => Service().AdjustAsync(purchaseId, (decimal)delta, null, null, false, 1));
        Assert.Equal("Purchase ID and non-zero adjustment amount required", ex.Message);
    }

    [Fact]
    public void NoteSuffixMatchesPhpFormat()
    {
        var now = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);
        Assert.Equal("\n[2026-10-04 adjustment -12.5] freight", ErpPurchaseFromOrderAdjustWriteService.NoteSuffix(-12.5m, "freight", now));
        Assert.Equal("\n[2026-10-04 adjustment 100] ", ErpPurchaseFromOrderAdjustWriteService.NoteSuffix(100m, string.Empty, now));
    }

    [Fact]
    public void PurchaseSumSqlFollowsPhpOrderSumFilters()
    {
        var sql = ErpPurchaseFromOrderAdjustWriteService.PurchaseExVatSql;
        Assert.Contains("`t2_price_purchase`*`count_need`", sql, StringComparison.Ordinal);
        Assert.Contains("`for_finish` = 1", sql, StringComparison.Ordinal);
        Assert.Contains("`count_flag` = 0", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRunsNoLongerClaimLivePathUnimplemented()
    {
        var fo = ErpPurchaseFromOrderDryRun.EvaluateAgainstOrders([], new ErpPurchaseFromOrderRequest(1, 1, true));
        var ad = ErpPurchaseAdjustmentDryRun.EvaluateAgainstPurchases([], new ErpPurchaseAdjustmentRequest(1, 5m, null, true));
        Assert.Equal(0, fo.Writes);
        Assert.Equal(0, ad.Writes);
        Assert.DoesNotContain("not implemented", fo.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("not implemented", ad.Detail, StringComparison.Ordinal);
    }
}
