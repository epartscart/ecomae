using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpVirtualWarehouseWriteServiceTests
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
        var result = await new ErpVirtualWarehouseWriteService(new UnusedConnections())
            .CreateAsync(new ErpVirtualWarehouseCreateRequest(Type: "virtual"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Warehouse code or name is required.", result.Message);
    }

    [Fact]
    public async Task TransferRequiresWarehouseIds()
    {
        var result = await new ErpVirtualWarehouseWriteService(new UnusedConnections())
            .TransferAsync(new ErpVirtualWarehouseTransferRequest(Qty: 1, Sku: "SKU1"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("From and to warehouse ids are required.", result.Message);
    }

    [Fact]
    public async Task TransferRequiresDistinctWarehouses()
    {
        var result = await new ErpVirtualWarehouseWriteService(new UnusedConnections())
            .TransferAsync(new ErpVirtualWarehouseTransferRequest(FromWarehouseId: 1, ToWarehouseId: 1, Qty: 1, Sku: "SKU1"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("From and to warehouses must differ.", result.Message);
    }

    [Fact]
    public async Task TransferRequiresLineQty()
    {
        var result = await new ErpVirtualWarehouseWriteService(new UnusedConnections())
            .TransferAsync(new ErpVirtualWarehouseTransferRequest(FromWarehouseId: 1, ToWarehouseId: 2));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("At least one transfer line with qty is required.", result.Message);
    }
}
