using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpAutoPriceSourceToggleWriteTests
{
    [Fact]
    public void Route_exposes_auto_price_write()
    {
        Assert.Equal("/cp/auto-price/write", EcomAeRoutes.CpAutoPriceWrite);
    }

    [Fact]
    public void Site_key_and_toggle_match_php()
    {
        Assert.Equal("epartscart", CpAutoPriceWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal("", CpAutoPriceWriteService.NormalizeSiteKey(""));
        Assert.Equal(1, CpAutoPriceWriteService.NextEnabled(0, null));
        Assert.Equal(0, CpAutoPriceWriteService.NextEnabled(1, null));
        Assert.Equal(1, CpAutoPriceWriteService.NextEnabled(0, true));
        Assert.Equal(0, CpAutoPriceWriteService.NextEnabled(1, false));
    }

    [Fact]
    public void Page_posts_native_toggle_source()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpAutoPriceApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/auto-price/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"toggle_discovery_source\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_toggle_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/auto-price/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_auto_price.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("toggle_discovery_source", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_toggle_source()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpAutoPriceWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpAutoPriceWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpAutoPriceWriteService.cs"));
        Assert.Contains("epc_disc_source_toggle", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_discovery_sources`", service, StringComparison.Ordinal);
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
