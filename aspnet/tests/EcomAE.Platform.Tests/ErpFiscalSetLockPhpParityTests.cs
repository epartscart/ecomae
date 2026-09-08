using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFiscalSetLockPhpParityTests
{
    [Fact]
    public void PeriodCloseApp_PostsNativeFiscalLockForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPeriodCloseApp.razor"));
        Assert.Contains("/erp/fiscal/set-lock", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"lock_date\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"note\"", text, StringComparison.Ordinal);
        Assert.Contains("Clear fiscal lock", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFiscalSetLockWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFiscalSetLockWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFiscalSetLockLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/fiscal/set-lock");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_fiscal_set_lock", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/fiscal-set-lock").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpFiscalSetLockDryRun().Evaluate(new ErpFiscalSetLockRequest(0, "clear"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.Clearing);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpFiscalSetLockDryRun().Evaluate(new ErpFiscalSetLockRequest(1, "x", true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpFiscalSetLock", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxFiscalSetLock", text, StringComparison.Ordinal);
        Assert.Contains("HandleFiscalSetLockAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFiscalSetLockWriteService.cs"));
        Assert.Contains("Fiscal lock table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Periods locked up to ", service, StringComparison.Ordinal);
        Assert.Contains("Fiscal lock cleared", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_fiscal_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_audit_log", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveLockDate_MatchesPhpEmptyAndYmd()
    {
        Assert.Equal(0, ErpFiscalSetLockWriteService.ResolveLockDateUnix("", 0));
        Assert.Equal(0, ErpFiscalSetLockWriteService.ResolveLockDateUnix("0", 99));
        Assert.Equal(99, ErpFiscalSetLockWriteService.ResolveLockDateUnix("", 99));
        Assert.Equal(0, ErpFiscalSetLockWriteService.ResolveLockDateUnix("not-a-date", 0));
        var unix = ErpFiscalSetLockWriteService.ResolveLockDateUnix("2026-09-08", 0);
        Assert.Equal(1788911999, unix);
        Assert.Equal("2026-09-08", ErpFiscalSetLockWriteService.FormatYmd(unix));
        Assert.Equal("Periods locked up to 2026-09-08", ErpFiscalSetLockWriteService.SuccessMessage(unix));
        Assert.Equal("Fiscal lock cleared", ErpFiscalSetLockWriteService.SuccessMessage(0));
        Assert.Equal("abc", ErpFiscalSetLockWriteService.ClipNote("  abc  "));
        Assert.Equal(255, ErpFiscalSetLockWriteService.ClipNote(new string('n', 300)).Length);
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
