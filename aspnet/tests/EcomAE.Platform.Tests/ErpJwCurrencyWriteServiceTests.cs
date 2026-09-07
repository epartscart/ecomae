using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwCurrencyWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task SaveRequiresCurrencyCode()
    {
        var result = await new ErpJwCurrencyWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwCurrencySaveRequest(Description: "US Dollar"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Currency code is required.", result.Message);
    }
}
