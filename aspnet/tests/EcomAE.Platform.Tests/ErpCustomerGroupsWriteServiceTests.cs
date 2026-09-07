using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCustomerGroupsWriteServiceTests
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
        var result = await new ErpCustomerGroupsWriteService(new UnusedConnections())
            .CreateAsync(new ErpCustomerGroupCreateRequest(DiscountPct: 5));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Group code or name is required.", result.Message);
    }

    [Fact]
    public async Task AssignRequiresGroupId()
    {
        var result = await new ErpCustomerGroupsWriteService(new UnusedConnections())
            .AssignAsync(new ErpCustomerGroupAssignRequest(CustomerId: 9));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Group id is required.", result.Message);
    }

    [Fact]
    public async Task AssignRequiresCustomerId()
    {
        var result = await new ErpCustomerGroupsWriteService(new UnusedConnections())
            .AssignAsync(new ErpCustomerGroupAssignRequest(GroupId: 1));
        Assert.False(result.Succeeded);
        Assert.Equal("invalid", result.Code);
        Assert.Equal("Customer id is required.", result.Message);
    }
}
