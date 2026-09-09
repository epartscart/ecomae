using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMarketplaceAppsWriteTests
{
    [Fact]
    public void Route_exposes_marketplace_apps_write()
    {
        Assert.Equal("/cp/marketplace-apps/write", EcomAeRoutes.CpMarketplaceAppsWrite);
    }

    [Fact]
    public void Normalize_site_key_allows_hyphen()
    {
        Assert.Equal("eparts-cart", CpMarketplaceAppsWriteService.NormalizeSiteKey(" eParts-Cart! "));
        Assert.Equal("epartscart", CpMarketplaceAppsWriteService.NormalizeSiteKey("ePartsCart"));
    }

    [Fact]
    public void Page_posts_native_install_and_uninstall()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketplaceAppsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/marketplace-apps/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"install\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"uninstall\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_install_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/marketplace-apps/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_marketplace.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_install_uninstall()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpMarketplaceAppsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpMarketplaceAppsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("\"install\"", module, StringComparison.Ordinal);
        Assert.Contains("\"uninstall\"", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpMarketplaceAppsWriteService.cs"));
        Assert.Contains("epc_marketplace_install", service, StringComparison.Ordinal);
        Assert.Contains("epc_marketplace_uninstall", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_marketplace_installs`", service, StringComparison.Ordinal);
        Assert.Contains("`status`='uninstalled'", service, StringComparison.Ordinal);
        Assert.Contains("`downloads`=`downloads`+1", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_marketplace_reviews", service, StringComparison.Ordinal);
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
