using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpInfoBlocksWriteTests
{
    [Fact]
    public void Route_exposes_info_blocks_write()
    {
        Assert.Equal("/cp/info-blocks/write", EcomAeRoutes.CpInfoBlocksWrite);
    }

    [Fact]
    public void Normalize_key_scope_placement_locale_match_php()
    {
        Assert.Equal("summer_promo", CpInfoBlocksWriteService.NormalizeBlockKey(" Summer_Promo! "));
        Assert.Equal("", CpInfoBlocksWriteService.NormalizeBlockKey(" !!! "));
        Assert.Equal("epartscart", CpInfoBlocksWriteService.NormalizeSiteKey(" ePartsCart! "));
        Assert.Equal("platform", CpInfoBlocksWriteService.NormalizeScope(""));
        Assert.Equal("tenant", CpInfoBlocksWriteService.NormalizeScope(" Tenant "));
        Assert.Equal("homepage", CpInfoBlocksWriteService.NormalizePlacement("unknown"));
        Assert.Equal("checkout", CpInfoBlocksWriteService.NormalizePlacement("checkout"));
        Assert.Equal("en", CpInfoBlocksWriteService.NormalizeLocale(""));
        Assert.Equal("en-US", CpInfoBlocksWriteService.NormalizeLocale("en-US"));
        Assert.Equal("en-US-ex", CpInfoBlocksWriteService.NormalizeLocale("en-US-extra"));
        Assert.Contains("cp_notice", CpInfoBlocksWriteService.Placements.Keys);
    }

    [Fact]
    public void Page_posts_native_save_info_block()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpInfoBlocksApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/info-blocks/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_info_block\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"block_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"content_html\"", razor, StringComparison.Ordinal);
        Assert.Contains("Leave blank to keep current HTML", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("_isAdmin && _isSuper", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_info_block_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/info-blocks/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_super_cp_info_blocks.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_info_block()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpInfoBlocksWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpInfoBlocksWriteService", module, StringComparison.Ordinal);
        Assert.Contains("save_info_block", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpInfoBlocksWriteService.cs"));
        Assert.Contains("epc_scp_info_block_save", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_platform_info_blocks`", service, StringComparison.Ordinal);
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
