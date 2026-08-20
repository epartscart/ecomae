using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseInvoiceWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpPurchaseInvoiceWriteService Service() => new(
        new UnconfiguredConnections(),
        new ErpVoucherNumberService(),
        new ErpTaxAmountCalculator(),
        new ErpGlPostingService(new ErpVoucherNumberService()),
        new ErpAuditLogWriter());

    [Fact]
    public async Task MissingSupplierIsRejected()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().CreateAsync(
            new ErpPurchaseInvoiceInput { SupplierId = 0, AmountExVat = 100m },
            adminId: 1));
        Assert.Equal("No database", ex.Message);
    }

    [Fact]
    public async Task MissingPurchaseOrderIsRejected()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ConvertPurchaseOrderAsync(0, adminId: 1));
        Assert.Equal("No database", ex.Message);
    }

    [Theory]
    [InlineData("AE", "vat-decree-8-2017")]
    [InlineData("are", "vat-decree-8-2017")]
    [InlineData("SA", "")]
    [InlineData("", "")]
    public void LegislationReferenceFollowsTenantCountry(string country, string expected)
        => Assert.Equal(expected, ErpPurchaseInvoiceWriteService.LegislationRefFor(country));

    [Fact]
    public void PurchaseInvoiceVoucherNumbersRenderLikePhp()
    {
        Assert.Equal("PI", ErpVoucherNumberService.NormalizeType("pi"));
        Assert.Equal("PI-2026-00007", ErpVoucherNumberService.Render("PI-", 2026, 7, 5));
    }
}
