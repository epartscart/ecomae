using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFxRevaluationWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Must fail before a connection is opened.");
    }

    private static ErpFxRevaluationWriteService Service()
    {
        var connections = new UnconfiguredConnections();
        var gl = new ErpGlPostingService(new ErpVoucherNumberService());
        var audit = new ErpAuditLogWriter();
        return new(connections, gl, new ErpGlLedgerWriteService(connections, gl, audit), audit);
    }

    [Fact]
    public async Task PreviewAndPostFailClosedWithoutTenantDb()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().PreviewAsync(0));
        Assert.Equal("TenantRegistry DB is not configured.", ex.Message);
        ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().PostAsync(0, true, 1));
        Assert.Equal("TenantRegistry DB is not configured.", ex.Message);
    }

    [Theory]
    [InlineData(1.005, "AED", 1.01)]
    [InlineData(1.0005, "BHD", 1.001)]
    [InlineData(123.6, "JPY", 124)]
    [InlineData(-2.345, "EUR", -2.35)]
    public void CcyRoundUsesPhpMinorUnits(double amount, string code, double expected)
        => Assert.Equal((decimal)expected, ErpFxRevaluationWriteService.CcyRound((decimal)amount, code));

    [Fact]
    public void AsOfFromDateMatchesPhpEndOfDay()
    {
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), ErpFxRevaluationWriteService.AsOfFromDate("2026-09-30"));
        Assert.Equal(0, ErpFxRevaluationWriteService.AsOfFromDate(""));
        Assert.Equal(0, ErpFxRevaluationWriteService.AsOfFromDate("2026-13-01"));
    }

    [Fact]
    public void PostedMessageMatchesPhpGainLossWording()
    {
        Assert.Equal("FX revaluation posted: unrealised gain 1,250.50 AED (journal #7, auto-reversal #8)", ErpFxRevaluationWriteService.PostedMessage(1250.5m, "AED", 7, 8));
        Assert.Equal("FX revaluation posted: unrealised loss 3.00 AED (journal #9)", ErpFxRevaluationWriteService.PostedMessage(-3m, "AED", 9, 0));
    }

    [Fact]
    public void OpenArSqlFollowsPhpFilters()
    {
        var sql = ErpFxRevaluationWriteService.OpenForeignArSql;
        Assert.Contains("`doc_category` IN ('tax_invoice','commercial_invoice')", sql);
        Assert.Contains("`status` <> 'cancelled'", sql);
        Assert.Contains("ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005", sql);
        Assert.Contains("UPPER(`currency_code`) <> ?", sql);
        Assert.Contains("`issue_date` <= ?", sql);
        Assert.EndsWith("ORDER BY `currency_code` ASC, `id` ASC", sql);
    }

    [Fact]
    public void DryRunsStayZeroWriteWithoutConfirm()
    {
        var p = new ErpFxRevaluationPreviewDryRun().Evaluate(new ErpFxRevaluationPreviewRequest(false));
        var q = new ErpFxPostRevaluationDryRun().Evaluate(new ErpFxPostRevaluationRequest(false));
        Assert.Equal(0, p.Writes);
        Assert.Equal(0, q.Writes);
    }
}
