using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlLedgerWriteServiceTests
{
    private sealed class UnusedConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpGlLedgerWriteService Service() => new(
        new UnusedConnections(),
        new ErpGlPostingService(new ErpVoucherNumberService()),
        new ErpAuditLogWriter());

    [Fact]
    public async Task ManualJournalRequiresLines()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ManualJournalAsync(
            new ErpManualJournalInput(),
            adminId: 1));
        Assert.Equal("Add at least two GL lines", ex.Message);
    }

    [Fact]
    public async Task ManualJournalRejectsUnbalancedLinesBeforeTouchingTheDatabase()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ManualJournalAsync(
            new ErpManualJournalInput
            {
                Lines =
                [
                    new ErpGlLine(1, 100m, 0m, "debit"),
                    new ErpGlLine(2, 0m, 90m, "credit"),
                ],
            },
            adminId: 1));
        Assert.Contains("balance", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReverseJournalRequiresAnExistingJournal()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().ReverseJournalAsync(
            journalId: 0,
            reverseDate: 0,
            note: string.Empty,
            adminId: 1));
        Assert.Equal("Journal not found or already reversed", ex.Message);
    }

    [Fact]
    public async Task CoaCreateRequiresACode()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().CreateCoaAccountAsync(
            new ErpCoaAccountInput { Name = "Marketing" },
            adminId: 1));
        Assert.Equal("Account code required", ex.Message);
    }

    [Theory]
    [InlineData("bank")]
    [InlineData("Expense")]
    [InlineData("contra")]
    public async Task CoaCreateRejectsUnknownAccountTypes(string accountType)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().CreateCoaAccountAsync(
            new ErpCoaAccountInput { Code = "6200", Name = "Marketing", AccountType = accountType },
            adminId: 1));
        Assert.Equal("Invalid account type", ex.Message);
    }

    [Fact]
    public async Task CashAccountCreateRequiresAName()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().CreateCashAccountAsync(
            new ErpCashAccountInput { AccountType = "bank" },
            adminId: 1));
        Assert.Equal("Account name required", ex.Message);
    }

    [Fact]
    public async Task ValidCoaPayloadsReachTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service().CreateCoaAccountAsync(
            new ErpCoaAccountInput { Code = "6200", Name = "Marketing", AccountType = "expense" },
            adminId: 1));

    [Fact]
    public async Task ValidCashAccountPayloadsReachTheDatabase()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => Service().CreateCashAccountAsync(
            new ErpCashAccountInput { Name = "Main current account", AccountType = "bank" },
            adminId: 1));
}
