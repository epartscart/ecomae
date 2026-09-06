using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwRepairWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpJwRepairWriteService Service() => new(new UnusedConnections());

    [Fact]
    public async Task CreateRequiresCustomerNameAndItem()
    {
        var missingName = await Service().CreateAsync(new ErpJwRepairSaveRequest(ItemDescription: "Ring"));
        Assert.False(missingName.Succeeded);
        Assert.Equal("invalid", missingName.Code);
        Assert.Equal("Customer name and item description are required.", missingName.Message);

        var missingItem = await Service().CreateAsync(new ErpJwRepairSaveRequest(CustomerName: "Aisha"));
        Assert.False(missingItem.Succeeded);
        Assert.Equal("invalid", missingItem.Code);
    }
}
