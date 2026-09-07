using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwBarcodeWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task GenerateRequiresStockCode()
    {
        var result = await new ErpJwBarcodeWriteService(new UnusedConnections())
            .GenerateAsync(new ErpJwBarcodeGenerateRequest(Division: "G", Karat: "22K"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Stock code is required.", result.Message);
    }
}
