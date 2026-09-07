using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwTagWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresTagNoOrDescription()
    {
        var result = await new ErpJwTagWriteService(new UnusedConnections())
            .CreateAsync(new ErpJwTagCreateRequest(Karat: "22K"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Tag number or description is required.", result.Message);
    }

    [Fact]
    public async Task SellRequiresTagId()
    {
        var result = await new ErpJwTagWriteService(new UnusedConnections())
            .SellAsync(new ErpJwTagSellRequest(InvoiceId: 9));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Tag id is required.", result.Message);
    }
}
