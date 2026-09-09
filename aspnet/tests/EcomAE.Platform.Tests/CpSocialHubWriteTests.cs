using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpSocialHubWriteTests
{
    [Fact]
    public void Route_exposes_social_hub_write()
    {
        Assert.Equal("/cp/social-hub/write", EcomAeRoutes.CpSocialHubWrite);
    }

    [Fact]
    public void Site_key_title_and_platform_match_php()
    {
        Assert.Equal("platform", CpSocialHubWriteService.ResolveSiteKey("", true, "www.ecomae.com"));
        Assert.Equal("epartscart", CpSocialHubWriteService.ResolveSiteKey(" ePartsCart! ", false, "other.host"));
        Assert.Equal("epartscart-com", CpSocialHubWriteService.ResolveSiteKey("", false, "www.epartscart.com:443"));
        Assert.Equal("instagram", CpSocialHubWriteService.NormalizePlatform(" Instagram! "));
        Assert.Equal("Untitled draft", CpSocialHubWriteService.NormalizeTitle(""));
        Assert.Equal("Hello", CpSocialHubWriteService.NormalizeTitle(" Hello "));
        Assert.Contains("tiktok", CpSocialHubWriteService.Platforms);
    }

    [Fact]
    public void Page_posts_native_save_draft()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpSocialHubApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/social-hub/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_draft\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"platform\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_draft_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/social-hub/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_epc_social_media.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_draft()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpSocialHubWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpSocialHubWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpSocialHubWriteService.cs"));
        Assert.Contains("epc_social_save_draft", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_social_post_drafts`", service, StringComparison.Ordinal);
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
