using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwFixUnfixWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresSupplierName()
    {
        var result = await new ErpJwFixUnfixWriteService(new UnusedConnections())
            .CreateAsync(new ErpJwFixUnfixCreateRequest(WeightGrams: 10, FixRate: 250));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Supplier name is required.", result.Message);
    }

    [Fact]
    public async Task SettleRequiresPurchaseId()
    {
        var result = await new ErpJwFixUnfixWriteService(new UnusedConnections())
            .SettleAsync(new ErpJwFixUnfixSettleRequest(SettleRate: 260));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Purchase id is required.", result.Message);
    }
}
