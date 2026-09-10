using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPartsAgentConfigWriteTests
{
    [Fact]
    public void Route_exposes_save_config()
    {
        Assert.Equal("/cp/parts-agent/save-config", EcomAeRoutes.CpPartsAgentSaveConfig);
    }

    [Fact]
    public void Enabled_parse_matches_php_int_cast()
    {
        Assert.True(CpPartsAgentWriteService.ParseEnabled("1"));
        Assert.True(CpPartsAgentWriteService.ParseEnabled("2"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled(""));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("0"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("yes"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("on"));
        Assert.False(CpPartsAgentWriteService.ParseEnabled("true"));
    }

    [Fact]
    public void Page_posts_native_save_config()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPartsAgentChatsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/parts-agent/save-config\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"enabled\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"agent_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"subtitle\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"greeting\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"system_prompt\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"teaser_text\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"placeholder\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"logo_url\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"domain\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_config_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/parts-agent/save-config");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("save_config", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_parts_agent_config", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_parts_agent_cp.php", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_config_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpPartsAgentSaveConfig", module, StringComparison.Ordinal);
        Assert.Contains("SaveConfigAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPartsAgentWriteService.cs"));
        Assert.Contains("epc_agent_save_config", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_parts_agent_config`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
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
