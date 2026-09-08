using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPltJobRunPhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeJobRunForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("action=\"/erp/platform/jobs/run\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"job_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"status\"", text, StringComparison.Ordinal);
        Assert.Contains("Run batch job", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPltJobRunWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPltJobRunWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPltJobRunLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/platform/jobs/run");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_plt_batch_run", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/plt-job-run").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPltJobRunDryRun().Evaluate(new ErpPltJobRunRequest(JobId: 1, Status: "ended"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("ended", new ErpPltJobRunDryRun().Evaluate(new ErpPltJobRunRequest(JobId: 1)).JobStatus);
        Assert.Equal("Invalid batch status", new ErpPltJobRunDryRun().Evaluate(new ErpPltJobRunRequest(Status: "bogus")).Detail);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPltJobRunDryRun().Evaluate(new ErpPltJobRunRequest(ConfirmWrites: true, Status: "ended")).ValidationCode);
    }

    [Fact]
    public void ValidateAndNextRun_MatchPhp()
    {
        Assert.Null(ErpPltJobRunWriteService.Validate("waiting"));
        Assert.Null(ErpPltJobRunWriteService.Validate("executing"));
        Assert.Null(ErpPltJobRunWriteService.Validate("ended"));
        Assert.Null(ErpPltJobRunWriteService.Validate("error"));
        Assert.Null(ErpPltJobRunWriteService.Validate("canceled"));
        Assert.Equal("Invalid batch status", ErpPltJobRunWriteService.Validate(""));
        Assert.Equal("Invalid batch status", ErpPltJobRunWriteService.Validate("done"));
        Assert.Equal(0, ErpPltJobRunWriteService.NextRun(1_700_000_000, 0));
        Assert.Equal(1_700_000_000 + 3600, ErpPltJobRunWriteService.NextRun(1_700_000_000, 60));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPlatformJobsRun", text, StringComparison.Ordinal);
        Assert.Contains("HandlePltJobRunAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPltJobRunWriteService.cs"));
        Assert.Contains("Batch job executed (", service, StringComparison.Ordinal);
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
