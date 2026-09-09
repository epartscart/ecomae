using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPowerBiWriteTests
{
    [Fact]
    public void Route_exposes_power_bi_write()
    {
        Assert.Equal("/cp/power-bi/write", EcomAeRoutes.CpPowerBiWrite);
    }

    [Fact]
    public void Normalize_site_key_and_embed_mode_match_php()
    {
        Assert.Equal("eparts-cart", CpPowerBiWriteService.NormalizeSiteKey(" eParts-Cart! "));
        Assert.Equal("epartscart", CpPowerBiWriteService.NormalizeSiteKey("ePartsCart"));
        Assert.Equal("none", CpPowerBiWriteService.NormalizeEmbedMode(""));
        Assert.Equal("url", CpPowerBiWriteService.NormalizeEmbedMode("url"));
        Assert.Equal("azure", CpPowerBiWriteService.NormalizeEmbedMode("azure"));
        Assert.Equal("ws", CpPowerBiWriteService.Clip("ws-too-long-value-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 64)[..2]);
    }

    [Fact]
    public void Page_posts_native_save_and_add()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPowerBiApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/power-bi/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_config\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_report\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/power-bi/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_power_bi.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_config()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPowerBiWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpPowerBiWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_config", module, StringComparison.Ordinal);
        Assert.Contains("add_report", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPowerBiWriteService.cs"));
        Assert.Contains("epc_power_bi_configure", service, StringComparison.Ordinal);
        Assert.Contains("epc_power_bi_register_report", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_power_bi_config`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_power_bi_reports`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
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
