using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpTemplatesWriteTests
{
    [Fact]
    public void Routes_expose_set_current()
    {
        Assert.Equal("/cp/templates-manager-app", EcomAeRoutes.ControlPanelTemplatesManagerApp);
        Assert.Equal("/cp/templates-manager/set-current", EcomAeRoutes.ControlPanelTemplatesSetCurrent);
    }

    [Fact]
    public void Page_posts_native_set_current_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpTemplatesManagerApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/templates-manager/set-current\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"template_id\"", razor);
        Assert.Contains("Set current", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("templates_action_type\" value=\"delete\"", razor);
    }

    [Fact]
    public void Module_maps_set_current()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelTemplatesSetCurrent", src);
        Assert.Contains("ICpTemplatesWriteService", src);
    }

    [Fact]
    public void Catalog_marks_set_current_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/templates-manager/set-current");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("generate_style", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTemplatesWriteService", src);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root from " + AppContext.BaseDirectory);
    }
}
