using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPmChequeSavePhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativeChequeForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("/erp/pm/cheques/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"cheque_no\"", text, StringComparison.Ordinal);
        Assert.Contains("Record cheque", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPmChequeSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPmChequeSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPmChequeSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/pm/cheques/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_pm_cheque_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pm-cheque-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPmChequeSaveDryRun().Evaluate(new ErpPmChequeSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPmChequeSaveDryRun().Evaluate(new ErpPmChequeSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void ChequeDate_MatchesPhpEmptyAndUnix()
    {
        Assert.Equal(100, ErpPmChequeSaveWriteService.ResolveChequeDate(null, 100));
        Assert.Equal(100, ErpPmChequeSaveWriteService.ResolveChequeDate("", 100));
        Assert.Equal(100, ErpPmChequeSaveWriteService.ResolveChequeDate("0", 100));
        Assert.Equal(1757289600, ErpPmChequeSaveWriteService.ResolveChequeDate("1757289600", 100));
        Assert.Equal(100, ErpPmChequeSaveWriteService.ResolveChequeDate("not-a-date", 100));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPmChequeSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePmChequeSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPmChequeSaveWriteService.cs"));
        Assert.Contains("Cheque recorded", service, StringComparison.Ordinal);
        Assert.Contains("printed", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_listing_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_next_listing_seq", service, StringComparison.Ordinal);
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
