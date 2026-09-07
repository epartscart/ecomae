using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwBarcodePurchaseWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresBarcodeOrDescription()
    {
        var result = await new ErpJwBarcodePurchaseWriteService(new UnusedConnections())
            .CreateAsync(new ErpJwBarcodePurchaseCreateRequest(NetWeight: 8, GoldRateAtPurchase: 250));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Barcode or description is required.", result.Message);
    }

    [Fact]
    public async Task SellRequiresPurchaseId()
    {
        var result = await new ErpJwBarcodePurchaseWriteService(new UnusedConnections())
            .SellAsync(new ErpJwBarcodePurchaseSellRequest(CustomerId: 9, InvoiceId: 501));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Purchase id is required.", result.Message);
    }
}
