using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEditLockHeartbeatPhpParityTests
{
    [Fact]
    public void SalesOrdersApp_PostsNativeHeartbeatForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("/erp/edit-lock/heartbeat", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"lock_token\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"ttl\"", text, StringComparison.Ordinal);
        Assert.Contains("Refresh edit lock", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersEditLockHeartbeatWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpEditLockHeartbeatWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksEditLockHeartbeatLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/edit-lock/heartbeat");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_edit_lock_heartbeat", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/edit-lock-heartbeat").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpEditLockHeartbeatDryRun().Evaluate(new ErpEditLockHeartbeatRequest("so:1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpEditLockHeartbeatDryRun().Evaluate(new ErpEditLockHeartbeatRequest("so:1", true)).ValidationCode);
        Assert.Equal(
            "invalid_request",
            new ErpEditLockHeartbeatDryRun().Evaluate(new ErpEditLockHeartbeatRequest()).ValidationCode);
    }

    [Fact]
    public void Ttl_ClampsLikePhp()
    {
        Assert.Equal(30, ErpEditLockHeartbeatWriteService.ClampTtl(-5));
        Assert.Equal(30, ErpEditLockHeartbeatWriteService.ClampTtl(0));
        Assert.Equal(90, ErpEditLockHeartbeatWriteService.ClampTtl(90));
        Assert.Equal(600, ErpEditLockHeartbeatWriteService.ClampTtl(601));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpEditLockHeartbeat", text, StringComparison.Ordinal);
        Assert.Contains("HandleEditLockHeartbeatAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpEditLockHeartbeatWriteService.cs"));
        Assert.Contains("Lock refreshed", service, StringComparison.Ordinal);
        Assert.Contains("Edit lock lost or expired", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_edit_locks", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_concurrency_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_edit_lock_acquire", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt)) return alt;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }
}
