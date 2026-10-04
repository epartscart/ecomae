using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinFxRevalueWriteServiceTests
{
    [Fact]
    public void ParseBalancesMatchesPhpSkipRules()
    {
        var lines = ErpFinFxRevalueWriteService.ParseBalances(
            "1100|USD|1000|3650|3.6725\r\n\nno pipes here\n1200|EUR|100|420\n 1300 | GBP | 50 | 240.00 | 4.70 ");
        Assert.Equal(2, lines.Count);
        Assert.Equal("1100", lines[0].Account);
        Assert.Equal(3672.50m, lines[0].RevaluedLc);
        Assert.Equal(22.50m, lines[0].Delta);
        Assert.Equal("gain", lines[0].Effect);
        Assert.Equal("GBP", lines[1].Currency);
        Assert.Equal(235.00m, lines[1].RevaluedLc);
        Assert.Equal(-5.00m, lines[1].Delta);
        Assert.Equal("loss", lines[1].Effect);
        Assert.Equal(17.50m, ErpFinFxRevalueWriteService.TotalDelta(lines));
    }

    [Fact]
    public void DeltaRoundsLikePhp()
    {
        var (revalued, delta) = ErpFinFxRevalueWriteService.Delta(123.456m, 100m, 1.005m);
        Assert.Equal(124.07m, revalued);
        Assert.Equal(24.07m, delta);
        Assert.Equal((0m, 0m), ErpFinFxRevalueWriteService.Delta(0m, 0m, 0m));
    }

    [Fact]
    public void MessageMatchesPhpWording()
    {
        Assert.Equal("Revaluation run #7 — net delta 1,234.50", ErpFinFxRevalueWriteService.Message(7, 1234.5m));
        Assert.Equal("Revaluation run #8 — net delta -5.00", ErpFinFxRevalueWriteService.Message(8, -5m));
    }

    [Fact]
    public void EncodeLinesUsesPhpKeys()
    {
        var json = ErpFinFxRevalueWriteService.EncodeLines(ErpFinFxRevalueWriteService.ParseBalances("1100|USD|10|36|3.67"));
        Assert.Contains("\"account\":\"1100\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fc_amount\":10", json, StringComparison.Ordinal);
        Assert.Contains("\"book_lc\":36", json, StringComparison.Ordinal);
        Assert.Contains("\"revalued_lc\":36.70", json, StringComparison.Ordinal);
        Assert.Contains("\"delta\":0.70", json, StringComparison.Ordinal);
        Assert.Contains("\"effect\":\"gain\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyBalancesAndUnconfiguredDbFailClosed()
    {
        var service = new ErpFinFxRevalueWriteService(new UnconfiguredConnections());
        var result = await service.RevalueAsync(new ErpFinFxRevalueWriteRequest("1100|USD|1|1|1"));
        Assert.False(result.Succeeded);
        Assert.Equal("db", result.Code);
        Assert.Equal(0, result.Writes);
    }

    [Fact]
    public void DryRunStaysZeroWriteWithoutConfirm()
    {
        var result = new ErpFinFxRevalueDryRun().Evaluate(new ErpFinFxRevalueRequest(false));
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.Equal("ok", result.ValidationCode);
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
