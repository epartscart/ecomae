using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwPettyCashWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task SaveRequiresPayee()
    {
        var result = await new ErpJwPettyCashWriteService(new ErpJwVoucherWriteService(new UnusedConnections()))
            .SaveAsync(new ErpJwPettyCashSaveRequest(Total: 12.5m, AccountCode: "PC-1"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Payee is required.", result.Message);
    }
}
