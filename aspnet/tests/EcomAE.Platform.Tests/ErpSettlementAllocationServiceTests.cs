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

    [Fact]
    public void ReceiptKnockOffReChecksOutstandingAtomically()
    {
        var sql = ErpSettlementAllocationService.ReceiptKnockOffSql;
        Assert.Contains("WHERE `id` = ? AND ROUND(`total_incl_vat` - `paid_amount`, 2) >= ? - 0.005", sql, StringComparison.Ordinal);
        Assert.Equal(5, sql.Count(c => c == '?'));
        Assert.True(sql.IndexOf("`amount_due` =", StringComparison.Ordinal) < sql.IndexOf("`paid_amount` = ROUND", StringComparison.Ordinal));
    }
}

public sealed class ErpSubLedgerGuardTests
{
    private static string Source(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")) && !Directory.Exists(Path.Combine(dir.FullName, ".git")) && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }

    [Fact]
    public void SupplierBillRefusalMessagesNameBillAndAmounts()
    {
        Assert.Equal("Supplier bill 8 is already fully paid", ErpSettlementAllocationService.BillAlreadyPaidMessage(8));
        Assert.Equal(
            "Payment 400.00 exceeds open balance 300.00 of supplier bill 8",
            ErpSettlementAllocationService.BillOverpaymentMessage(8, 400m, 300m));
    }

    [Fact]
    public void PaymentAllocationRefusesOverpaymentAndPaidBillsInsteadOfCapping()
    {
        var src = Source("aspnet/src/EcomAE.Platform/Erp/ErpSettlementAllocationService.cs");
        var apply = src.IndexOf("public async Task<decimal> ApplyPaymentAllocationsAsync(", StringComparison.Ordinal);
        Assert.True(apply > 0);
        var body = src[apply..];
        Assert.Contains("throw new ErpWriteException(BillAlreadyPaidMessage(billId));", body, StringComparison.Ordinal);
        Assert.Contains("throw new ErpWriteException(BillOverpaymentMessage(billId, amount, outstanding));", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("public async Task<ErpCashEntryResult> ReceiptVoucherAsync(")]
    [InlineData("public async Task<ErpCashEntryResult> PaymentVoucherAsync(")]
    public void ReceiptAndPaymentCheckPeriodLockBeforeAnyWrite(string method)
    {
        var src = Source("aspnet/src/EcomAE.Platform/Erp/ErpCashWriteService.cs");
        var start = src.IndexOf(method, StringComparison.Ordinal);
        Assert.True(start > 0, method);
        var guard = src.IndexOf("ErpGlPostingService.AssertPostingPeriodOpenAsync(connection, null, time, cancellationToken)", start, StringComparison.Ordinal);
        var voucher = src.IndexOf("_vouchers.NextAsync(", start, StringComparison.Ordinal);
        var txn = src.IndexOf("BeginTransactionAsync", start, StringComparison.Ordinal);
        Assert.True(guard > start && guard < voucher && guard < txn, method);
    }

    [Fact]
    public void ReceiptNamingSalesInvoiceAllocatesToItAndIsNeverAnAdvance()
    {
        var src = Source("aspnet/src/EcomAE.Platform/Erp/ErpCashWriteService.cs");
        var start = src.IndexOf("public async Task<ErpCashEntryResult> ReceiptVoucherAsync(", StringComparison.Ordinal);
        var named = src.IndexOf("if (allocation.Count == 0 && input.SalesInvoiceId > 0)", start, StringComparison.Ordinal);
        var advance = src.IndexOf("var isAdvance = !hasAllocation && (input.IsAdvance ?? true);", start, StringComparison.Ordinal);
        Assert.True(named > start && named < advance);
        Assert.Contains("_allocations.CustomerInvoiceAsync(connection, input.SalesInvoiceId, input.UserId, cancellationToken)", src[named..advance], StringComparison.Ordinal);
    }
}
