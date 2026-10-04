using Xunit;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Tests;

public sealed class ErpPeriodLockReopenPhpParityTests
{
    [Fact]
    public void MonthEndMatchesPhpMktimeLastSecond()
    {
        Assert.Equal(new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), ErpPeriodLockReopenWriteService.MonthEndUnix("2026-02"));
        Assert.Equal(new DateTimeOffset(2028, 2, 29, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), ErpPeriodLockReopenWriteService.MonthEndUnix("2028-02"));
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), ErpPeriodLockReopenWriteService.MonthStartUnix("2026-08"));
    }

    [Fact]
    public void OnlyBlockerSeverityWithCountRefusesLock()
    {
        ErpPeriodChecklistItem Item(string sev, long count) => new("x", "X", count, sev, "");
        Assert.True(ErpPeriodLockReopenWriteService.HasBlockers([Item("warning", 5), Item("blocker", 1)]));
        Assert.False(ErpPeriodLockReopenWriteService.HasBlockers([Item("warning", 5), Item("blocker", 0), Item("info", 9)]));
        Assert.False(ErpPeriodLockReopenWriteService.HasBlockers([]));
    }

    [Fact]
    public void MessagesMatchPhp()
    {
        Assert.Equal("Period is already locked.", ErpPeriodLockReopenWriteService.AlreadyLocked);
        Assert.Equal("Period is already open.", ErpPeriodLockReopenWriteService.AlreadyOpen);
        Assert.Equal("Cannot lock period — resolve blockers first.", ErpPeriodLockReopenWriteService.Blockers);
    }

    [Fact]
    public async Task InvalidYearMonthFailsBeforeOpeningConnection()
    {
        var svc = new ErpPeriodLockReopenWriteService(new UnconfiguredConnections(), new ErpAuditLogWriter());
        var lockResult = await svc.LockAsync(new ErpPeriodLockWriteRequest("2026"));
        Assert.False(lockResult.Result.Succeeded);
        Assert.Equal("invalid", lockResult.Result.Code);
        Assert.Empty(lockResult.Checklist);
        var reopen = await svc.ReopenAsync(new ErpPeriodLockWriteRequest("2026-13"));
        Assert.Equal("invalid", reopen.Code);
        var db = await svc.ReopenAsync(new ErpPeriodLockWriteRequest("2026-08"));
        Assert.Equal("db", db.Code);
    }

    [Fact]
    public void DryRunsStillRefuseConfirmWritesAndCatalogIsLiveGated()
    {
        Assert.Equal("confirm_writes_refused", new ErpPeriodLockDryRun().Evaluate(new ErpPeriodLockRequest("2026-08", null, true)).ValidationCode);
        Assert.Equal("confirm_writes_refused", new ErpPeriodReopenDryRun().Evaluate(new ErpPeriodReopenRequest("2026-08", null, true)).ValidationCode);
        foreach (var route in new[] { "/erp/periods/lock", "/erp/periods/reopen" })
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(f => f.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.Contains("epc_erp_period_close_log", row.Notes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LiveServiceIsRegisteredAndRoutesUseIt()
    {
        var root = RepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPeriodLockReopenWriteService, EcomAE.Platform.Erp.ErpPeriodLockReopenWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("MapPost(EcomAeRoutes.ErpPeriodLock, HandlePeriodLockAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpPeriodReopen, HandlePeriodReopenAsync)", module, StringComparison.Ordinal);
        Assert.Contains("writes.LockAsync(", module, StringComparison.Ordinal);
        Assert.Contains("writes.ReopenAsync(", module, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }
        return dir!.FullName;
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
