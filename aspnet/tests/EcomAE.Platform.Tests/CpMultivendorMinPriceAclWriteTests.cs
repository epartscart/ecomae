using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMultivendorMinPriceAclWriteTests
{
    [Fact]
    public void Route_exposes_min_price_acl_save()
    {
        Assert.Equal("/cp/multivendor/min-price-acl/save", EcomAeRoutes.CpMultivendorMinPriceAclSave);
    }

    [Fact]
    public void Restrict_and_ids_match_php()
    {
        Assert.True(CpPricesUploadWriteService.ParseRestrict(""));
        Assert.True(CpPricesUploadWriteService.ParseRestrict("yes"));
        Assert.False(CpPricesUploadWriteService.ParseRestrict("false"));
        Assert.False(CpPricesUploadWriteService.ParseRestrict("NO"));
        Assert.Equal(new[] { 2L, 5L }, CpPricesUploadWriteService.ParseIdList("2, 5;2"));
        Assert.Equal(new[] { 3L }, CpPricesUploadWriteService.ParseIdList("[3,\"0\",-1]"));
        Assert.Empty(CpPricesUploadWriteService.ParseIdList(""));
    }

    [Fact]
    public void Page_posts_native_min_price_acl()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPricesUploadApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/multivendor/min-price-acl/save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"restrict\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"group_ids\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"user_ids\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_min_price_acl_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/multivendor/min-price-acl/save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("min_price_acl_save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_mv_min_price_acl", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_multivendor_ingest.php", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_min_price_acl_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpMultivendorMinPriceAclSave", module, StringComparison.Ordinal);
        Assert.Contains("SaveMinPriceAclAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPricesUploadWriteService.cs"));
        Assert.Contains("epc_mv_min_price_acl_save", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_mv_min_price_acl`", service, StringComparison.Ordinal);
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
