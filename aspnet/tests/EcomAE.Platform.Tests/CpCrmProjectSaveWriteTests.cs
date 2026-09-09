using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCrmProjectSaveWriteTests
{
    [Fact]
    public void Route_exposes_project_write()
    {
        Assert.Equal("/cp/crm/projects/write", EcomAeRoutes.CpCrmProjectsWrite);
    }

    [Fact]
    public void Status_and_name_match_php()
    {
        Assert.Contains("planned", CpCrmProjectWriteService.Statuses);
        Assert.Contains("on_hold", CpCrmProjectWriteService.Statuses);
        Assert.DoesNotContain("draft", CpCrmProjectWriteService.Statuses);
        Assert.Equal("planned", CpCrmProjectWriteService.NormalizeStatus("nope"));
        Assert.Equal("Project", CpCrmProjectWriteService.NormalizeName(""));
        Assert.Contains("todo", CpCrmProjectWriteService.TaskStatuses);
        Assert.Contains("doing", CpCrmProjectWriteService.TaskStatuses);
        Assert.Equal("todo", CpCrmProjectWriteService.NormalizeTaskStatus("nope"));
        Assert.Equal("Task", CpCrmProjectWriteService.NormalizeTaskTitle(""));
        Assert.Equal(0m, CpCrmProjectWriteService.NormalizeHours(-3));
        Assert.Equal(0, CpCrmProjectWriteService.ParseDate(""));
        Assert.True(CpCrmProjectWriteService.ParseDate("2026-09-09") > 0);
    }

    [Fact]
    public void Page_posts_native_save_project()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/crm/projects/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_project\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_project_task\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"project_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"hours_est\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"progress_pct\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_project_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/crm/projects/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_crm.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_project", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_project_task", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_project()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCrmProjectWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCrmProjectWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCrmProjectWriteService.cs"));
        Assert.Contains("epc_crm_save_project", service, StringComparison.Ordinal);
        Assert.Contains("epc_crm_save_project_task", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_projects`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_crm_project_tasks`", service, StringComparison.Ordinal);
        Assert.Contains("save_project_task", module, StringComparison.Ordinal);
        Assert.Contains("crm_save_project_task", module, StringComparison.Ordinal);
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
