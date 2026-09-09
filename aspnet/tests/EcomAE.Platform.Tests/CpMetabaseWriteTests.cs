using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMetabaseWriteTests
{
    [Fact]
    public void Route_exposes_metabase_write()
    {
        Assert.Equal("/cp/metabase/write", EcomAeRoutes.CpMetabaseWrite);
    }

    [Fact]
    public void Normalize_site_key_matches_power_bi_hyphen_allowlist()
    {
        Assert.Equal("eparts-cart", CpMetabaseWriteService.NormalizeSiteKey(" eParts-Cart! "));
        Assert.Equal("epartscart", CpMetabaseWriteService.NormalizeSiteKey("ePartsCart"));
        Assert.Equal("https://mb.example", CpMetabaseWriteService.Clip("https://mb.example/extra-path-that-is-far-too-long-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", 256)[..18]);
    }

    [Fact]
    public void Page_posts_native_save_and_add()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpMetabaseApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/metabase/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_config\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_dashboard\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"secret", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/metabase/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_metabase_embed.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_config()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpMetabaseWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpMetabaseWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_config", module, StringComparison.Ordinal);
        Assert.Contains("add_dashboard", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpMetabaseWriteService.cs"));
        Assert.Contains("epc_metabase_configure", service, StringComparison.Ordinal);
        Assert.Contains("epc_metabase_register_dashboard", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_metabase_config`", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_metabase_dashboards`", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE `metabase_url`", service, StringComparison.Ordinal);
        Assert.Contains("`active`=1", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_key`=VALUES", service, StringComparison.Ordinal);
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
