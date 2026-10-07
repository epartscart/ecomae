using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlPostingServiceTests
{
    [Fact]
    public void JournalNeedsLines()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate([]));
        Assert.Equal("Journal must have lines", ex.Message);
    }

    [Fact]
    public void SingleLineJournalIsRejected()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate(
            [new ErpGlLine(1, 100m, 0m, "Cash")]));
        Assert.Equal("Double-entry bookkeeping requires at least two lines", ex.Message);
    }

    [Fact]
    public void NegativePostingValuesAreRejected()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate(
        [
            new ErpGlLine(1, -100m, 0m, "Cash"),
            new ErpGlLine(2, 0m, -100m, "Revenue"),
        ]));
        Assert.StartsWith("Ledger posting values must be greater than or equal to zero", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnbalancedJournalIsRejected()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate(
        [
            new ErpGlLine(1, 100m, 0m, "Cash"),
            new ErpGlLine(2, 0m, 90m, "Revenue"),
        ]));
        Assert.StartsWith("Journal not balanced", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JournalThatOnlyBalancesBelowStoredPrecisionIsRejected()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate(
        [
            new ErpGlLine(1, 10.005m, 0m, "Cash"),
            new ErpGlLine(2, 0m, 5.0025m, "Revenue"),
            new ErpGlLine(3, 0m, 5.0025m, "Revenue"),
        ]));
        Assert.Equal("Journal not balanced: debit 10.01 vs credit 10.00", ex.Message);
    }

    [Fact]
    public void ZeroValueJournalIsRejected()
    {
        var ex = Assert.Throws<ErpWriteException>(() => ErpGlPostingService.Validate(
        [
            new ErpGlLine(1, 0m, 0m, "Cash"),
            new ErpGlLine(2, 0m, 0m, "Revenue"),
        ]));
        Assert.Equal("Journal amount must be greater than zero", ex.Message);
    }

    [Fact]
    public void BalancedJournalPasses()
        => ErpGlPostingService.Validate(
        [
            new ErpGlLine(1, 100m, 0m, "Cash"),
            new ErpGlLine(2, 0m, 95.24m, "Revenue"),
            new ErpGlLine(3, 0m, 4.76m, "Output tax"),
        ]);

    [Theory]
    [InlineData("soft_close", true)]
    [InlineData("locked", true)]
    [InlineData("LOCKED", true)]
    [InlineData("open", false)]
    [InlineData("", false)]
    public void SharedJournalPathBlocksSoftClosedAndLockedPeriods(string status, bool blocked)
        => Assert.Equal(blocked, ErpGlPostingService.PeriodBlocksPosting(status));

    [Fact]
    public void PeriodBlockMessageMatchesManualJournalWording()
        => Assert.Equal(
            "Journal posting is blocked because the accounting period is soft close",
            ErpGlPostingService.PeriodBlockedMessage("soft_close"));

    [Fact]
    public void PeriodKeyIsUtcYearMonth()
        => Assert.Equal("2026-10", ErpGlPostingService.PeriodKey(1_791_104_600));

    [Fact]
    public void PeriodGuardLivesInSharedPostJournalPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")) && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var gl = File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform/Erp/ErpGlPostingService.cs"));
        var postJournal = gl.IndexOf("public async Task<long> PostJournalAsync(", StringComparison.Ordinal);
        var guard = gl.IndexOf("await AssertPostingPeriodOpenAsync(", postJournal, StringComparison.Ordinal);
        var begin = gl.IndexOf("BeginTransactionAsync", postJournal, StringComparison.Ordinal);
        Assert.True(postJournal > 0 && guard > postJournal && guard < begin);
    }
}
