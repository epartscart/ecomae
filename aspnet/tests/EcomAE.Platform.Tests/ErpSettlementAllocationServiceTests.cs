using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Parity checks for the allocation maths ported from PHP
/// <c>epc_erp_settlement_parse_allocations</c> / <c>epc_erp_settlement_fifo</c>.
/// </summary>
public sealed class ErpSettlementAllocationServiceTests
{
    [Fact]
    public void ParseAllocationsSumsDuplicateInvoicesAndDropsJunk()
    {
        var parsed = ErpSettlementAllocationService.ParseAllocations(
            new long[] { 7, 7, 0, 9, 11 },
            new[] { 25.005m, 10m, 500m, 0m, -3m });

        Assert.Equal(new long[] { 7 }, parsed.Keys.Order().ToArray());
        Assert.Equal(35.01m, parsed[7]);
    }

    [Fact]
    public void ParseAllocationsIgnoresUnpairedAmounts()
    {
        Assert.Empty(ErpSettlementAllocationService.ParseAllocations(null, new[] { 10m }));
        Assert.Empty(ErpSettlementAllocationService.ParseAllocations(new long[] { 4 }, null));
    }

    [Fact]
    public void FifoFillsOldestDocumentsAndStopsAtTheCashAmount()
    {
        var open = new List<ErpOpenDocument>
        {
            new(1, "INV-1", 100, 400m, 400m),
            new(2, "INV-2", 200, 300m, 300m),
            new(3, "INV-3", 300, 500m, 500m),
        };

        var fifo = ErpSettlementAllocationService.Fifo(open, 550m);

        Assert.Equal(new long[] { 1, 2 }, fifo.Keys.Order().ToArray());
        Assert.Equal(400m, fifo[1]);
        Assert.Equal(150m, fifo[2]);
    }

    [Fact]
    public void FifoNeverExceedsDocumentOutstanding()
    {
        var fifo = ErpSettlementAllocationService.Fifo(
            new List<ErpOpenDocument> { new(5, "INV-5", 10, 120m, 40m) },
            1_000m);

        Assert.Equal(40m, Assert.Single(fifo).Value);
    }

    [Fact]
    public void FifoIgnoresNonPositiveCash()
    {
        Assert.Empty(ErpSettlementAllocationService.Fifo(
            new List<ErpOpenDocument> { new(5, "INV-5", 10, 120m, 40m) },
            0m));
    }
}
