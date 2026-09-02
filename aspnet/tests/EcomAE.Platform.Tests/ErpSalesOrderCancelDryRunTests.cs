using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSalesOrderCancelDryRunTests
{
    private static ErpSalesOrderDigest Order(long id, string status = "open") =>
        new(id, $"SO-{id}", 5, 100m, status, 1_700_000_000, []);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpSalesOrderCancelDryRun.EvaluateAgainstOrders(
            [Order(9)], new ErpSalesOrderCancelRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void InvoicedIsInvalid()
    {
        var r = ErpSalesOrderCancelDryRun.EvaluateAgainstOrders(
            [Order(9, "invoiced")], new ErpSalesOrderCancelRequest(9));
        Assert.Equal("so_already_invoiced", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }

    [Fact]
    public void CancelIsDryRunValidated()
    {
        var r = ErpSalesOrderCancelDryRun.EvaluateAgainstOrders(
            [Order(9, "confirmed")], new ErpSalesOrderCancelRequest(9, "customer withdrew"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
