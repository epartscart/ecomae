using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseOrderWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpPurchaseOrderWriteService Service() => new(
        new UnconfiguredConnections(),
        new ErpVoucherNumberService(),
        new ErpTaxAmountCalculator(),
        new ErpAuditLogWriter());

    [Fact]
    public void LinesJsonWinsOverRepeatedFields()
    {
        var lines = ErpPurchaseOrderWriteService.ResolveLines(new ErpPurchaseOrderInput
        {
            LinesJson = """[{"item_code":"A1","description":"Brake pad","qty":2,"unit_cost_ex_vat":30.5}]""",
            Lines = [new ErpPurchaseOrderLineInput("Z9", "Ignored", 1m, 1m, 1m)],
        });

        var line = Assert.Single(lines);
        Assert.Equal("A1", line.ItemCode);
        Assert.Equal("Brake pad", line.Description);
        Assert.Equal(2m, line.Qty);
        Assert.Equal(61.00m, line.LineExVat);
    }

    [Fact]
    public void BlankOrZeroQtyLinesAreDropped()
    {
        var lines = ErpPurchaseOrderWriteService.ResolveLines(new ErpPurchaseOrderInput
        {
            Lines =
            [
                new ErpPurchaseOrderLineInput("", "   ", 1m, 10m, 10m),
                new ErpPurchaseOrderLineInput("", "Zero qty", 0m, 10m, 10m),
                new ErpPurchaseOrderLineInput("", "Filter", 3m, 2.5m, 0m),
            ],
        });

        var line = Assert.Single(lines);
        Assert.Equal("Filter", line.Description);
        Assert.Equal(7.50m, line.LineExVat);
    }

    [Theory]
    [InlineData(0, "PO title")]
    [InlineData(5, "")]
    [InlineData(5, "   ")]
    public async Task SupplierAndTitleAreRequiredBeforeAnyWrite(int supplierId, string title)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SaveAsync(
            new ErpPurchaseOrderInput { SupplierId = supplierId, Title = title, AmountExVat = 100m },
            adminId: 1));
        Assert.Equal("Supplier and title are required", ex.Message);
    }

    [Fact]
    public async Task UnknownStatusIsRejected()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SetStatusAsync(7, "posted", adminId: 1));
        Assert.Equal("Invalid PO status", ex.Message);
    }

    [Fact]
    public void StatusListMatchesPhpEnum()
        => Assert.Equal(["draft", "approved", "partial", "received", "cancelled"], ErpPurchaseOrderWriteService.AllowedStatuses);

    [Fact]
    public void PoVoucherNumbersRenderLikePhp()
    {
        Assert.Equal("PO", ErpVoucherNumberService.NormalizeType("po_1"));
        Assert.Equal("PO-2026-00011", ErpVoucherNumberService.Render("PO-", 2026, 11, 5));
    }
}
