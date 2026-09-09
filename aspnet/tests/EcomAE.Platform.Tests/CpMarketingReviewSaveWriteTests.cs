using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMarketingReviewSaveWriteTests
{
    [Fact]
    public void Route_exposes_marketing_growth_write()
    {
        Assert.Equal("/cp/marketing-growth/write", EcomAeRoutes.CpMarketingGrowthWrite);
    }

    [Fact]
    public void Strategy_and_review_type_allowlists()
    {
        Assert.Equal("seo", CpMarketingGrowthWriteService.Clip("  seo  ", 64));
        Assert.Contains("measurement", CpMarketingGrowthWriteService.StrategyKeys);
        Assert.Contains("quick_wins", CpMarketingGrowthWriteService.StrategyKeys);
        Assert.DoesNotContain("invented", CpMarketingGrowthWriteService.StrategyKeys);
        Assert.Contains("weekly", CpMarketingGrowthWriteService.ReviewTypes);
        Assert.Contains("monthly", CpMarketingGrowthWriteService.ReviewTypes);
    }

    [Fact]
    public void Page_posts_native_save_review()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingGrowthApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/marketing-growth/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"action\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_review\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"toggle_task\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_kpi\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"strategy_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"review_type\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"task_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"kpi_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_review_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/marketing-growth/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_marketing.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_review", write.Notes, StringComparison.Ordinal);
        Assert.Contains("toggle_task", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_kpi", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_review()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpMarketingGrowthWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpMarketingGrowthWriteService", module, StringComparison.Ordinal);
        Assert.Contains("SaveReviewAsync", module, StringComparison.Ordinal);
        Assert.Contains("ToggleTaskAsync", module, StringComparison.Ordinal);
        Assert.Contains("SaveKpiAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpMarketingGrowthWriteService.cs"));
        Assert.Contains("epc_marketing_save_review", service, StringComparison.Ordinal);
        Assert.Contains("epc_marketing_toggle_task", service, StringComparison.Ordinal);
        Assert.Contains("epc_marketing_save_kpi", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_marketing_reviews`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_marketing_task_progress`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_marketing_kpi_log`", service, StringComparison.Ordinal);
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
