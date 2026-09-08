using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinAllocSavePhpParityTests
{
    [Fact]
    public void FinAdvancedApp_PostsNativeAllocForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("/erp/fin/alloc/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"basis\"", text, StringComparison.Ordinal);
        Assert.Contains("New allocation rule", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFinAllocSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFinAllocSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFinAllocSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/fin/alloc/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_fin_alloc_rule_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/fin-alloc-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpFinAllocSaveDryRun().Evaluate(new ErpFinAllocSaveRequest(Code: "SPLIT"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpFinAllocSaveDryRun().Evaluate(new ErpFinAllocSaveRequest(ConfirmWrites: true, Code: "SPLIT")).ValidationCode);
    }

    [Fact]
    public void BasisParser_MatchesPhpAjaxLines()
    {
        var parsed = ErpFinAllocSaveWriteService.ParseBasis("DEPT-A|2\n\nskip\nDEPT-B|1\n|9");
        Assert.Equal(2, parsed.Count);
        Assert.Equal(2m, parsed["DEPT-A"]);
        Assert.Equal(1m, parsed["DEPT-B"]);
        Assert.Equal("[]", ErpFinAllocSaveWriteService.EncodeBasis(new Dictionary<string, decimal>()));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpFinAllocSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleFinAllocSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFinAllocSaveWriteService.cs"));
        Assert.Contains("Allocation rule saved", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_fin_alloc_run(", service, StringComparison.Ordinal);
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
