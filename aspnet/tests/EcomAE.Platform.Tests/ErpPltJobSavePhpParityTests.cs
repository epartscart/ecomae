using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPltJobSavePhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeJobSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("action=\"/erp/platform/jobs/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"recurrence_min\"", text, StringComparison.Ordinal);
        Assert.Contains("Save batch job", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersJobSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPltJobSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksJobSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/platform/jobs/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_plt_batch_job_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/plt-job-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPltJobSaveDryRun().Evaluate(new ErpPltJobSaveRequest(Code: "NIGHTLY"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Job code is required", new ErpPltJobSaveDryRun().Evaluate(new ErpPltJobSaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpPltJobSaveDryRun().Evaluate(new ErpPltJobSaveRequest(ConfirmWrites: true, Code: "NIGHTLY")).ValidationCode);
    }

    [Fact]
    public void Validate_AndNextRun_MatchPhp()
    {
        Assert.Equal("Job code is required", ErpPltJobSaveWriteService.Validate(""));
        Assert.Null(ErpPltJobSaveWriteService.Validate("NIGHTLY"));
        Assert.Equal(0, ErpPltJobSaveWriteService.NextRun(1_700_000_000, 15, false));
        Assert.Equal(0, ErpPltJobSaveWriteService.NextRun(1_700_000_000, 0, true));
        Assert.Equal(1_700_000_000 + 15 * 60, ErpPltJobSaveWriteService.NextRun(1_700_000_000, 15, true));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPlatformJobsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePltJobSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPltJobSaveWriteService.cs"));
        Assert.Contains("Batch job saved", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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
