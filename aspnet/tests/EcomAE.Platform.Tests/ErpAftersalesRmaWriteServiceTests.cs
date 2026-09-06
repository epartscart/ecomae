using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAftersalesRmaWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpAftersalesRmaWriteService Service() => new(new UnusedConnections());

    [Fact]
    public async Task CreateRequiresAtLeastOneLine()
    {
        var result = await Service().CreateAsync(new ErpAftersalesRmaCreateRequest(1));
        Assert.False(result.Succeeded);
        Assert.Equal("Add at least one return line (item_id,qty,...)", result.Message);
    }

    [Fact]
    public async Task CreateRequiresPositiveItemAndQty()
    {
        var result = await Service().CreateAsync(new ErpAftersalesRmaCreateRequest(
            1, Lines: [new ErpAftersalesRmaLine(0, 1)]));
        Assert.False(result.Succeeded);
        Assert.Equal("Each line requires itemId > 0 and qty > 0.", result.Message);
    }

    [Fact]
    public void ParseLinesCsvSkipsHeaderAndBlank()
    {
        var lines = ErpAftersalesRmaWriteService.ParseLinesCsv(
            "item_id,qty,unit_price,condition_note\n\n10,2,12.50,scratched\n");
        Assert.Single(lines);
        Assert.Equal(10, lines[0].ItemId);
        Assert.Equal(2m, lines[0].Qty);
        Assert.Equal(12.50m, lines[0].UnitPrice);
        Assert.Equal("scratched", lines[0].ConditionNote);
    }
}
