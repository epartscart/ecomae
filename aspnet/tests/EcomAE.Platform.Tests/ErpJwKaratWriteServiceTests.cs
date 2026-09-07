using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwKaratWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task SaveRequiresKaratCode()
    {
        var result = await new ErpJwKaratWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwKaratSaveRequest(Description: "22 Karat"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Karat code is required.", result.Message);
    }
}
