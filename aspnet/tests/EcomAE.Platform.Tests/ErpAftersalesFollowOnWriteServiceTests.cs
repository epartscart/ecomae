using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAftersalesFollowOnWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task WarrantyRequiresItemOrSerial()
    {
        var result = await new ErpAftersalesWarrantyWriteService(new UnusedConnections())
            .RegisterAsync(new ErpAftersalesWarrantyRegisterRequest(Months: 12));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Item id or serial is required.", result.Message);
    }

    [Fact]
    public async Task JobRequiresComplaintOrAsset()
    {
        var result = await new ErpAftersalesJobWriteService(new UnusedConnections())
            .CreateAsync(new ErpAftersalesJobCreateRequest(CustomerId: 5));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Complaint or asset reference is required.", result.Message);
    }

    [Fact]
    public async Task JobAddLineRequiresJobId()
    {
        var result = await new ErpAftersalesJobWriteService(new UnusedConnections())
            .AddLineAsync(new ErpAftersalesJobAddLineRequest(Description: "Pad"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Job id is required.", result.Message);
    }

    [Fact]
    public async Task JobAddLineRequiresDescriptionOrItem()
    {
        var result = await new ErpAftersalesJobWriteService(new UnusedConnections())
            .AddLineAsync(new ErpAftersalesJobAddLineRequest(JobId: 3, Qty: 1, UnitPrice: 10));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Description or item id is required.", result.Message);
    }

    [Fact]
    public async Task JobCloseRequiresJobId()
    {
        var result = await new ErpAftersalesJobWriteService(new UnusedConnections())
            .CloseAsync(new ErpAftersalesJobCloseRequest());
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Job id is required.", result.Message);
    }
}
