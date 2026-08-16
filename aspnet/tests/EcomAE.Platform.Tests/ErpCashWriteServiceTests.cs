using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCashWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpCashWriteService Service() => new(
        new UnusedConnections(),
        new ErpVoucherNumberService(),
        new ErpGlPostingService(new ErpVoucherNumberService()),
        new ErpAuditLogWriter());

    [Theory]
    [InlineData(0, 100)]
    [InlineData(4, 0)]
    public async Task CashEntryRequiresAccountAndAmount(int accountId, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().CashEntryAsync(
            new ErpCashEntryInput { AccountId = accountId, Amount = amount },
            adminId: 1));
        Assert.Equal("Invalid entry", ex.Message);
    }

    [Theory]
    [InlineData(0, 4, 100)]
    [InlineData(9, 0, 100)]
    [InlineData(9, 4, 0)]
    public async Task ReceiptVoucherRequiresCustomerAccountAndAmount(int userId, int accountId, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ReceiptVoucherAsync(
            new ErpReceiptVoucherInput { UserId = userId, AccountId = accountId, Amount = amount },
            adminId: 1));
        Assert.Equal("Customer, bank account, and amount required", ex.Message);
    }

    [Fact]
    public async Task AdvanceReceiptsStayPhpOnly()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ReceiptVoucherAsync(
            new ErpReceiptVoucherInput { UserId = 9, AccountId = 4, Amount = 100m, IsAdvance = true },
            adminId: 1));
        Assert.StartsWith("Advance receipts", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 4, 100)]
    [InlineData(9, 0, 100)]
    [InlineData(9, 4, 0)]
    public async Task PaymentVoucherRequiresSupplierAccountAndAmount(int supplierId, int accountId, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().PaymentVoucherAsync(
            new ErpPaymentVoucherInput { SupplierId = supplierId, AccountId = accountId, Amount = amount },
            adminId: 1));
        Assert.Equal("Invalid payment data", ex.Message);
    }

    [Theory]
    [InlineData("", true, "receipt")]
    [InlineData("", false, "payment")]
    [InlineData("bogus", true, "receipt")]
    [InlineData("adjustment", false, "adjustment")]
    [InlineData("transfer_in", false, "transfer_in")]
    public void EntryTypeFollowsPhpResolution(string requested, bool direction, string expected)
        => Assert.Equal(expected, ErpCashWriteService.ResolveEntryType(requested, direction));
}
