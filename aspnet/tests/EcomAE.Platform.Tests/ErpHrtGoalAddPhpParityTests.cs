using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHrtGoalAddPhpParityTests
{
    [Fact]
    public void PerformanceApp_PostsNativeGoalForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPerformanceApp.razor"));
        Assert.Contains("ErpPerformanceGoalAdd", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"review_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("Add goal", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersHrtGoalAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrtGoalAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksHrtGoalAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/performance/goals/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_hrt_goal_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/hrt-goal-add").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpHrtGoalAddDryRun().Evaluate(new ErpHrtGoalAddRequest(ReviewId: 1, Title: "Close Q1"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpHrtGoalAddDryRun().Evaluate(new ErpHrtGoalAddRequest(ConfirmWrites: true, ReviewId: 1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPerformanceGoalAdd", text, StringComparison.Ordinal);
        Assert.Contains("HandleHrtGoalAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpHrtGoalAddWriteService.cs"));
        Assert.Contains("Goal added", service, StringComparison.Ordinal);
        Assert.Contains("Goal title is required", service, StringComparison.Ordinal);
        Assert.Contains("Review not found", service, StringComparison.Ordinal);
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
