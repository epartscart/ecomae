using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwTouristVatWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task SaveRequiresTouristName()
    {
        var result = await new ErpJwTouristVatWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwTouristVatSaveRequest(PassportNo: "P123", TotalVat: 5));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Tourist name is required.", result.Message);
    }
}
