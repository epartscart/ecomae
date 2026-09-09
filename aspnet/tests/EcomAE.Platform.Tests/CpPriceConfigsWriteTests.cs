using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPriceConfigsWriteTests
{
    [Fact]
    public void Route_exposes_price_configs_write()
    {
        Assert.Equal("/cp/price-configs/write", EcomAeRoutes.CpPriceConfigsWrite);
        Assert.Equal("/cp/price-configs-app", EcomAeRoutes.ControlPanelPriceConfigsApp);
    }

    [Fact]
    public void Normalize_scope_client_type_site_key_currency_priority_match_php()
    {
        Assert.Equal("epartscart", CpPriceConfigsWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal("platform", CpPriceConfigsWriteService.NormalizeScope(""));
        Assert.Equal("tenant", CpPriceConfigsWriteService.NormalizeScope("tenant"));
        Assert.Equal("platform", CpPriceConfigsWriteService.NormalizeScope("office"));
        Assert.Equal("all", CpPriceConfigsWriteService.NormalizeClientType("unknown"));
        Assert.Equal("price_list", CpPriceConfigsWriteService.NormalizeClientType("price_list"));
        Assert.Equal("AED", CpPriceConfigsWriteService.NormalizeCurrency(null));
        Assert.Equal("", CpPriceConfigsWriteService.NormalizeCurrency(""));
        Assert.Equal("USD EXTR", CpPriceConfigsWriteService.NormalizeCurrency("usd extra"));
        Assert.Equal(1, CpPriceConfigsWriteService.NormalizePriority(0));
        Assert.Equal(100, CpPriceConfigsWriteService.NormalizePriority(100));
        Assert.Contains("catalog", CpPriceConfigsWriteService.ClientTypes.Keys);
        Assert.Contains("api", CpPriceConfigsWriteService.ClientTypes.Keys);
        Assert.Contains("channel", CpPriceConfigsWriteService.ClientTypes.Keys);
    }

    [Fact]
    public void Page_posts_native_save_and_delete()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPriceConfigsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/price-configs/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_price_config\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete_price_config\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"scope\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"client_type\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"markup_percent\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", razor, StringComparison.Ordinal);
        Assert.Contains("_allowed", razor, StringComparison.Ordinal);
        Assert.Contains("Not found", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/price-configs/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_super_cp_price_configs.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_price_config()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPriceConfigsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpPriceConfigsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_price_config", module, StringComparison.Ordinal);
        Assert.Contains("delete_price_config", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPriceConfigsWriteService.cs"));
        Assert.Contains("epc_scp_price_config_save", service, StringComparison.Ordinal);
        Assert.Contains("epc_scp_price_config_delete", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_platform_price_configs`", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `epc_platform_price_configs`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        var map = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Presentation/PhpSurfaceLinkMap.cs"));
        Assert.Contains("epc_super_cp_price_configs\", \"/cp/price-configs-app\"", map, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_super_cp_price_configs\", \"/cp/price-lists-app\"", map, StringComparison.Ordinal);
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
