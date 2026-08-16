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
}
