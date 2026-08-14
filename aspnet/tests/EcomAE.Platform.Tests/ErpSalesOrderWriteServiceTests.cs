using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSalesOrderWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpSalesOrderWriteService Service() => new(
        new UnconfiguredConnections(),
        new ErpVoucherNumberService(),
        new ErpTaxAmountCalculator(),
        new ErpAuditLogWriter());

    [Fact]
    public void LinesJsonWinsOverRepeatedFields()
    {
        var lines = ErpSalesOrderWriteService.ResolveLines(new ErpSalesOrderInput
        {
            LinesJson = """[{"item_code":"A1","description":"Brake pad","qty":2,"unit_price_ex_vat":30.5}]""",
            Lines = [new ErpSalesOrderLineInput("Z9", "Ignored", 1m, 1m, 1m)],
        });

        var line = Assert.Single(lines);
        Assert.Equal("A1", line.ItemCode);
        Assert.Equal("Brake pad", line.Description);
        Assert.Equal(2m, line.Qty);
        Assert.Equal(61.00m, line.LineExVat);
    }

    [Fact]
    public void BlankDescriptionLinesAreDropped()
    {
        var lines = ErpSalesOrderWriteService.ResolveLines(new ErpSalesOrderInput
        {
            Lines =
            [
                new ErpSalesOrderLineInput("", "   ", 1m, 10m, 10m),
                new ErpSalesOrderLineInput("", "Filter", 3m, 2.5m, 0m),
            ],
        });

        var line = Assert.Single(lines);
        Assert.Equal("Filter", line.Description);
        Assert.Equal(7.50m, line.LineExVat);
    }

    [Fact]
    public void LineTotalsOverrideTheHeaderAmount()
    {
        var lines = ErpSalesOrderWriteService.ResolveLines(new ErpSalesOrderInput
        {
            Lines = [new ErpSalesOrderLineInput("", "Filter", 3m, 2.5m, 0m)],
        });

        Assert.Equal(7.50m, ErpSalesOrderWriteService.ResolveAmountExVat(999m, lines));
        Assert.Equal(12.35m, ErpSalesOrderWriteService.ResolveAmountExVat(12.345m, []));
    }

    [Theory]
    [InlineData(0, "Order", 100)]
    [InlineData(5, "", 100)]
    [InlineData(5, "Order", 0)]
    public async Task RequiredFieldsAreValidatedBeforeAnyWrite(int customerUserId, string title, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SaveAsync(
            new ErpSalesOrderInput { CustomerUserId = customerUserId, Title = title, AmountExVat = amount },
            adminId: 1));
        Assert.Equal("Customer, title, and amount (or lines) are required", ex.Message);
    }

    [Fact]
    public async Task UnknownStatusIsRejected()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SetStatusAsync(7, "posted", adminId: 1));
        Assert.Equal("Invalid sales order status", ex.Message);
    }

    [Fact]
    public void StatusListMatchesPhpEnum()
        => Assert.Equal(["draft", "confirmed", "invoiced", "cancelled"], ErpSalesOrderWriteService.AllowedStatuses);

    [Fact]
    public void VoucherNumbersRenderLikePhp()
    {
        Assert.Equal("SO", ErpVoucherNumberService.NormalizeType("so_2"));
        Assert.Equal("SO-2026-00007", ErpVoucherNumberService.Render("SO-", 2026, 7, 5));
        Assert.Equal("SI/2026-007", ErpVoucherNumberService.Render("SI/", 2026, 7, 3));
    }
}
