using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwRepairLifecycleWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    [Fact]
    public async Task ReceiptRequiresCustomerName()
    {
        var result = await new ErpJwRepairReceiptWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwRepairReceiptSaveRequest(Mobile: "0500000000"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Customer name is required.", result.Message);
    }

    [Fact]
    public async Task TransferRequiresRepairNumber()
    {
        var result = await new ErpJwRepairTransferWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwRepairTransferSaveRequest(ToBranch: "WS1"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Repair number is required.", result.Message);
    }

    [Fact]
    public async Task WorkshopRequiresTransferRef()
    {
        var result = await new ErpJwWorkshopReceiveWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwWorkshopReceiveSaveRequest(FromWorkshop: "WS1"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Transfer reference is required.", result.Message);
    }

    [Fact]
    public async Task DeliveryRequiresRepairNumber()
    {
        var result = await new ErpJwRepairDeliveryWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwRepairDeliverySaveRequest(CustomerName: "Ada"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Repair number is required.", result.Message);
    }

    [Fact]
    public async Task StockVerifyRequiresLocation()
    {
        var result = await new ErpJwStockVerifyWriteService(new UnusedConnections())
            .SaveAsync(new ErpJwStockVerifySaveRequest(Supervisor: "Ali"));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Location is required.", result.Message);
    }
}
