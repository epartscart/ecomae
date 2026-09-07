using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwGoldSchemeWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task CreateRequiresCodeOrName()
    {
        var result = await new ErpJwGoldSchemeWriteService(new UnusedConnections())
            .CreateAsync(new ErpJwGoldSchemeCreateRequest(MaturityMonths: 11));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Scheme code or name is required.", result.Message);
    }

    [Fact]
    public async Task EnrollRequiresSchemeId()
    {
        var result = await new ErpJwGoldSchemeWriteService(new UnusedConnections())
            .EnrollAsync(new ErpJwGoldSchemeEnrollRequest(CustomerName: "Aisha"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Scheme id is required.", result.Message);
    }

    [Fact]
    public async Task PayRequiresEnrollmentId()
    {
        var result = await new ErpJwGoldSchemeWriteService(new UnusedConnections())
            .PayAsync(new ErpJwGoldSchemePayRequest(Amount: 500));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Enrollment id is required.", result.Message);
    }
}
