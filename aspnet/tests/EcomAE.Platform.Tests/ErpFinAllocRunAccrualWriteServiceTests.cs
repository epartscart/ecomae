using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinAllocRunAccrualWriteServiceTests
{
    [Fact]
    public void SplitMatchesPhpRoundingAndRemainderToHeaviest()
    {
        var weights = ErpFinAllocRunWriteService.DecodeBasis("{\"DEPT-A\":1,\"DEPT-B\":1,\"DEPT-C\":1}");
        var lines = ErpFinAllocRunWriteService.Split(100m, weights);
        Assert.Equal(3, lines.Count);
        Assert.Equal(33.34m, lines[0].Value);
        Assert.Equal(33.33m, lines[1].Value);
        Assert.Equal(33.33m, lines[2].Value);
        Assert.Equal(100m, lines.Sum(l => l.Value));

        var heavy = ErpFinAllocRunWriteService.Split(10m, ErpFinAllocRunWriteService.DecodeBasis("{\"X\":1,\"Y\":\"2\"}"));
        Assert.Equal(3.33m, heavy[0].Value);
        Assert.Equal(6.67m, heavy[1].Value);
        Assert.Empty(ErpFinAllocRunWriteService.Split(10m, ErpFinAllocRunWriteService.DecodeBasis("[]")));
        Assert.Empty(ErpFinAllocRunWriteService.Split(10m, ErpFinAllocRunWriteService.DecodeBasis("not json")));
        Assert.Equal("Allocated across 2 destination(s)", ErpFinAllocRunWriteService.Message(2));
        Assert.Equal("{\"X\":3.33,\"Y\":6.67}", ErpFinAllocRunWriteService.EncodeLines(heavy));
    }

    [Fact]
    public void AccrualScheduleMatchesPhp()
    {
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, ErpFinAccrualSaveWriteService.Schedule(100m, 3));
        Assert.Equal(new[] { 50m }, ErpFinAccrualSaveWriteService.Schedule(50m, 0));
        var rows = ErpFinAccrualSaveWriteService.BuildRows(1200m, 3, 2026, 11);
        Assert.Equal((1, 2026, 11, 400m), (rows[0].Seq, rows[0].Fy, rows[0].PeriodNo, rows[0].Amount));
        Assert.Equal((2, 2026, 12), (rows[1].Seq, rows[1].Fy, rows[1].PeriodNo));
        Assert.Equal((3, 2027, 1), (rows[2].Seq, rows[2].Fy, rows[2].PeriodNo));
        var json = ErpFinAccrualSaveWriteService.EncodeSchedule(rows);
        Assert.Contains("\"seq\":1", json, StringComparison.Ordinal);
        Assert.Contains("\"period_no\":11", json, StringComparison.Ordinal);
        Assert.Contains("\"reversed\":0", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnconfiguredDbFailsClosed()
    {
        var alloc = await new ErpFinAllocRunWriteService(new Unconfigured()).RunAsync(new ErpFinAllocRunWriteRequest(1, 10m));
        Assert.False(alloc.Result.Succeeded);
        Assert.Equal("db", alloc.Result.Code);
        Assert.Empty(alloc.Lines);
        var acc = await new ErpFinAccrualSaveWriteService(new Unconfigured()).SaveAsync(new ErpFinAccrualSaveWriteRequest(Code: "A"));
        Assert.Equal("db", acc.Code);
        Assert.Equal(0, acc.Writes);
    }

    [Fact]
    public void DryRunsStayZeroWrite()
    {
        Assert.Equal(0, new ErpFinAllocRunDryRun().Evaluate(new ErpFinAllocRunRequest(false)).Writes);
        Assert.Equal(0, new ErpFinAccrualSaveDryRun().Evaluate(new ErpFinAccrualSaveRequest(0, "A", false)).Writes);
    }

    private sealed class Unconfigured : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
