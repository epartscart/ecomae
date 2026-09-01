using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocLifecycleWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public UnusedConnections(bool configured) => IsConfigured = configured;

        public bool IsConfigured { get; }

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Guards must run before a connection is opened.");
    }

    private static ErpDocLifecycleWriteService Service(bool configured = true)
    {
        var connections = new UnusedConnections(configured);
        return new ErpDocLifecycleWriteService(
            connections,
            new ErpGlLedgerWriteService(
                connections,
                new ErpGlPostingService(new ErpVoucherNumberService()),
                new ErpAuditLogWriter()),
            new ErpTaxAmountCalculator(),
            new ErpAuditLogWriter());
    }

    [Fact]
    public async Task VoidRefusesWhenTheTenantHasNoDatabase()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(
            () => Service(configured: false).PurchaseVoidAsync(7, "duplicate", adminId: 1));
        Assert.Equal("No database", ex.Message);
    }

    [Fact]
    public async Task AmendRefusesWhenTheTenantHasNoDatabase()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(
            () => Service(configured: false).CashVoucherAmendAsync(7, "REF", "note", adminId: 1));
        Assert.Equal("No database", ex.Message);
    }

    [Fact]
    public async Task CashVoucherVoidReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().CashVoucherVoidAsync(12, "wrong account", adminId: 1));

    [Fact]
    public async Task PurchaseDeleteReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().PurchaseDeleteAsync(12, adminId: 1));

    [Fact]
    public async Task PurchaseAmendReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service().PurchaseAmendAsync(
            new ErpPurchaseAmendInput { PurchaseId = 12, Note = "typo fixed" },
            adminId: 1));

    [Fact]
    public async Task InvoiceCancelReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().InvoiceCancelAsync(12, "customer withdrew", adminId: 1));

    [Fact]
    public async Task InvoiceDeleteReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().InvoiceDeleteAsync(12, adminId: 1));

    [Fact]
    public async Task SalesOrderCancelReachesTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service().SalesOrderCancelAsync(12, string.Empty, adminId: 1));

    [Fact]
    public async Task PurchaseAmendRejectsANullPayload()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => Service().PurchaseAmendAsync(null!, adminId: 1));
}
