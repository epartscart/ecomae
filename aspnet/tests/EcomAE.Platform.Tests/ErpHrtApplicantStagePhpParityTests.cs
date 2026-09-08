using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHrtApplicantStagePhpParityTests
{
    [Fact]
    public void RecruitmentApp_PostsNativeApplicantStageForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpRecruitmentApp.razor"));
        Assert.Contains("/erp/recruitment/applicants/stage", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"stage\"", text, StringComparison.Ordinal);
        Assert.Contains("Move applicant", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersHrtApplicantStageWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrtApplicantStageWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksHrtApplicantStageLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/recruitment/applicants/stage");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_hrt_applicant_set_stage", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/hrt-applicant-stage").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpHrtApplicantStageDryRun().Evaluate(new ErpHrtApplicantStageRequest(1, "hired"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpHrtApplicantStageDryRun().Evaluate(new ErpHrtApplicantStageRequest(1, "hired", true)).ValidationCode);
        Assert.Equal(
            "invalid_request",
            new ErpHrtApplicantStageDryRun().Evaluate(new ErpHrtApplicantStageRequest(-1, "hired")).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpRecruitmentApplicantStage", text, StringComparison.Ordinal);
        Assert.Contains("HandleHrtApplicantStageAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpHrtApplicantStageWriteService.cs"));
        Assert.Contains("Applicant moved to ", service, StringComparison.Ordinal);
        Assert.Contains("Invalid applicant stage", service, StringComparison.Ordinal);
        Assert.Contains("Applicant not found", service, StringComparison.Ordinal);
        Assert.Contains("`hired`", service, StringComparison.Ordinal);
        Assert.Contains("\"filled\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_hrt_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveStage_MatchesPhpDefaultsAndEnums()
    {
        Assert.True(ErpHrtApplicantStageWriteService.TryResolveStage(false, null, out var omitted, out _));
        Assert.Equal("applied", omitted);
        Assert.Equal("Applicant moved to ", ErpHrtApplicantStageWriteService.SuccessMessage(false, null));

        Assert.False(ErpHrtApplicantStageWriteService.TryResolveStage(true, "", out _, out var emptyErr));
        Assert.Equal(ErpHrtApplicantStageWriteService.InvalidStage, emptyErr);

        Assert.False(ErpHrtApplicantStageWriteService.TryResolveStage(true, "unknown", out _, out var bad));
        Assert.Equal(ErpHrtApplicantStageWriteService.InvalidStage, bad);

        Assert.True(ErpHrtApplicantStageWriteService.TryResolveStage(true, "hired", out var hired, out _));
        Assert.Equal("hired", hired);
        Assert.Equal("Applicant moved to hired", ErpHrtApplicantStageWriteService.SuccessMessage(true, "hired"));

        foreach (var stage in new[] { "applied", "screening", "interview", "offer", "hired", "rejected" })
        {
            Assert.True(ErpHrtApplicantStageWriteService.TryResolveStage(true, stage, out var resolved, out _));
            Assert.Equal(stage, resolved);
        }
    }

    [Fact]
    public void NextHire_FillsWhenHeadcountReached()
    {
        Assert.Equal((1, "open"), ErpHrtApplicantStageWriteService.NextHire(0, 2, "open"));
        Assert.Equal((2, "filled"), ErpHrtApplicantStageWriteService.NextHire(1, 2, "open"));
        Assert.Equal((1, "filled"), ErpHrtApplicantStageWriteService.NextHire(0, 0, "on_hold"));
        Assert.Equal((3, "on_hold"), ErpHrtApplicantStageWriteService.NextHire(2, 5, "on_hold"));
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
