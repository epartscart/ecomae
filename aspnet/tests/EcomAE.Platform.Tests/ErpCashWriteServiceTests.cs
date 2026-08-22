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
        new ErpAuditLogWriter(),
        new ErpSettlementAllocationService(),
        new ErpAdvanceVatService(new ErpGlPostingService(new ErpVoucherNumberService())));

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

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task AdvanceReceiptsPassValidationAndReachTheDatabase(bool? isAdvance)
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ReceiptVoucherAsync(
            new ErpReceiptVoucherInput { UserId = 9, AccountId = 4, Amount = 100m, IsAdvance = isAdvance },
            adminId: 1));

    [Theory]
    [InlineData(0, 4, 100)]
    [InlineData(4, 0, 100)]
    [InlineData(4, 4, 100)]
    [InlineData(4, 5, 0)]
    public async Task TransferVoucherRequiresTwoDistinctAccountsAndPositiveAmount(int fromId, int toId, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().TransferVoucherAsync(
            new ErpTransferVoucherInput { FromAccountId = fromId, ToAccountId = toId, Amount = amount },
            adminId: 1));
        Assert.Equal("Two distinct accounts and a positive amount required", ex.Message);
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

    [Theory]
    [InlineData(0, 100)]
    [InlineData(7, 0)]
    [InlineData(7, -5)]
    public async Task SupplierSettlementRequiresSupplierAndPositiveAmount(int supplierId, decimal amount)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SupplierSettlementAsync(
            new ErpSupplierSettlementInput { SupplierId = supplierId, Amount = amount },
            adminId: 1));
        Assert.Equal("Supplier and positive amount required", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("raise")]
    [InlineData("credit")]
    public async Task SupplierSettlementRejectsUnknownDirection(string direction)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SupplierSettlementAsync(
            new ErpSupplierSettlementInput { SupplierId = 7, Amount = 100m, Direction = direction },
            adminId: 1));
        Assert.Equal("Direction must be increase or decrease payable", ex.Message);
    }

    [Fact]
    public async Task SupplierWriteOffCannotIncreasePayable()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SupplierSettlementAsync(
            new ErpSupplierSettlementInput
            {
                SupplierId = 7,
                Amount = 100m,
                Direction = "increase",
                EntryKind = "write_off",
            },
            adminId: 1));
        Assert.Equal("Write-off must decrease payable", ex.Message);
    }

    [Theory]
    [InlineData("settlement", "settlement")]
    [InlineData("write_off", "write_off")]
    [InlineData("adjustment", "adjustment")]
    [InlineData("", "adjustment")]
    [InlineData("bogus", "adjustment")]
    [InlineData(null, "adjustment")]
    public void SettlementKindFollowsPhpKindList(string? requested, string expected)
        => Assert.Equal(expected, ErpCashWriteService.NormalizeSettlementKind(requested));

    [Theory]
    [InlineData("adjustment", "Adjustment / correction")]
    [InlineData("settlement", "Settlement (non-cash close-off)")]
    [InlineData("write_off", "Write-off")]
    public void SettlementLabelsMatchPhpKindLabels(string kind, string expected)
        => Assert.Equal(expected, ErpCashWriteService.SettlementKindLabel(kind));
}
