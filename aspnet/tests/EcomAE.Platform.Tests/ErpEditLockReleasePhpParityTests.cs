using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEditLockReleasePhpParityTests
{
    [Fact]
    public void SalesOrdersApp_PostsNativeReleaseForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("/erp/edit-lock/release", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"lock_token\"", text, StringComparison.Ordinal);
        Assert.Contains("Release edit lock", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersEditLockReleaseWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpEditLockReleaseWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksEditLockReleaseLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/edit-lock/release");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_edit_lock_release", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/edit-lock-release").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpEditLockReleaseDryRun().Evaluate(new ErpEditLockReleaseRequest("so:1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpEditLockReleaseDryRun().Evaluate(new ErpEditLockReleaseRequest("so:1", true)).ValidationCode);
        Assert.Equal(
            "invalid_request",
            new ErpEditLockReleaseDryRun().Evaluate(new ErpEditLockReleaseRequest()).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpEditLockRelease", text, StringComparison.Ordinal);
        Assert.Contains("HandleEditLockReleaseAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpEditLockReleaseWriteService.cs"));
        Assert.Contains("Edit lock released", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_edit_locks", service, StringComparison.Ordinal);
        Assert.Contains("lock_token", service, StringComparison.Ordinal);
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
