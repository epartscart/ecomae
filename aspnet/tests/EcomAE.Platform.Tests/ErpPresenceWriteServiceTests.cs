using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPresenceWriteServiceTests
{
    [Fact]
    public void SessionTokenMatchesPhpSha256Prefix()
    {
        var t = ErpPresenceWriteService.SessionToken("abc", "", "", 0);
        Assert.Equal(40, t.Length);
        Assert.StartsWith("ba7816bf8f01cfea414140de5dae2223b00361a3", t);
        var fallback = ErpPresenceWriteService.SessionToken("", "UA", "1.2.3.4", 7);
        Assert.Equal(ErpPresenceWriteService.SessionToken("UA|1.2.3.4|7", "", "", 0), fallback);
    }

    [Fact]
    public void CtypeDigitParity()
    {
        Assert.True(ErpPresenceWriteService.IsDigits("123"));
        Assert.False(ErpPresenceWriteService.IsDigits(""));
        Assert.False(ErpPresenceWriteService.IsDigits("12a"));
        Assert.False(ErpPresenceWriteService.IsDigits("-1"));
    }

    [Fact]
    public void DryRunUsesPhpRequestContractAndBlocksWrites()
    {
        var r = new ErpConcurrencyStatusDryRun().Evaluate(new ErpConcurrencyStatusRequest("invoice", "5"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.Equal("invoice", r.EntityType);
        var refused = new ErpConcurrencyStatusDryRun().Evaluate(new ErpConcurrencyStatusRequest(ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
        var p = new ErpPresenceHeartbeatDryRun().Evaluate(new ErpPresenceHeartbeatRequest(":"));
        Assert.Equal("dry-run-validated", p.Status);
    }

    [Fact]
    public void ServiceNeverCreatesSchemaAndRedactsLockToken()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform/Erp/ErpPresenceWriteService.cs"));
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("`lock_token`", src, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", src, StringComparison.Ordinal);
    }
}
