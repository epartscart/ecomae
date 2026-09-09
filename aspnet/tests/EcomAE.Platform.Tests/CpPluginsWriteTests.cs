using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpPluginsWriteTests
{
    [Fact]
    public void Routes_expose_activate()
    {
        Assert.Equal("/cp/plugins-manager-app", EcomAeRoutes.ControlPanelPluginsManagerApp);
        Assert.Equal("/cp/plugins-manager/activate", EcomAeRoutes.ControlPanelPluginsActivate);
    }

    [Fact]
    public void ParsePluginIds_accepts_single_and_json_list()
    {
        Assert.Equal(new long[] { 4 }, CpPluginsWriteService.ParsePluginIds(null, 4));
        Assert.Equal(new long[] { 2, 5 }, CpPluginsWriteService.ParsePluginIds("[2,5]", 0));
        Assert.Equal(new long[] { 2, 5 }, CpPluginsWriteService.ParsePluginIds("[2,5]", 2));
        Assert.Empty(CpPluginsWriteService.ParsePluginIds("not-json", 0));
        Assert.Equal(new long[] { 4 }, CpPluginsWriteService.ParsePluginIds("not-json", 4));
        Assert.Equal(1, CpPluginsWriteService.NormalizeFlag(9));
        Assert.Equal(0, CpPluginsWriteService.NormalizeFlag(0));
    }

    [Fact]
    public void Page_posts_native_activate_forms()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPluginsManagerApp.razor"));
        Assert.Contains("method=\"post\"", razor);
        Assert.Contains("action=\"/cp/plugins-manager/activate\"", razor);
        Assert.Contains("name=\"confirmWrites\"", razor);
        Assert.Contains("value=\"true\"", razor);
        Assert.Contains("name=\"plugin_id\"", razor);
        Assert.Contains("name=\"flag_value\"", razor);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor);
        Assert.DoesNotContain("ASP.NET", razor);
        Assert.DoesNotContain("/php-reference/", razor);
        Assert.DoesNotContain("plugins_action_type\" value=\"delete\"", razor);
    }

    [Fact]
    public void Module_maps_activate()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ControlPanelPluginsActivate", src);
        Assert.Contains("ICpPluginsWriteService", src);
        Assert.Contains("Backend 2FA plugin activate stays on the Classic twin", File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPluginsWriteService.cs")));
    }

    [Fact]
    public void Catalog_marks_activate_write_live_gated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/plugins-manager/activate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("2FA", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_registers_write_service()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPluginsWriteService", src);
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
