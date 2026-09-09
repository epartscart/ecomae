using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmOpportunityStageWriteTests
{
    [Fact]
    public void Route_exposes_opportunity_write()
    {
        Assert.Equal("/cp/crm/opportunities/write", EcomAeRoutes.CpCrmOpportunitiesWrite);
    }

    [Fact]
    public void Stage_allowlist_matches_php()
    {
        Assert.Contains("prospect", CpCrmOpportunityWriteService.Stages);
        Assert.Contains("won", CpCrmOpportunityWriteService.Stages);
        Assert.Contains("lost", CpCrmOpportunityWriteService.Stages);
        Assert.DoesNotContain("converted", CpCrmOpportunityWriteService.Stages);
    }

    [Fact]
    public void Page_posts_native_update_stage()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/opportunities/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"update_stage\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_opportunity\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"stage\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_update_stage_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/opportunities/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("update_stage", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_opportunity", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_update_stage()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmOpportunityWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmOpportunityWriteService", module, StringComparison.Ordinal);
        Assert.Contains("UpdateStageAsync", module, StringComparison.Ordinal);
        Assert.Contains("SaveAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmOpportunityWriteService.cs"));
        Assert.Contains("epc_crm_update_opportunity_stage", service, StringComparison.Ordinal);
        Assert.Contains("epc_crm_save_opportunity", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_crm_opportunities`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_opportunities`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
