using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpStorefrontStorageToggleWriteTests
{
    [Fact]
    public void Route_exposes_storefront_toggle()
    {
        Assert.Equal("/cp/prices/storefront-storage/toggle", EcomAeRoutes.CpPricesStorefrontStorageToggle);
    }

    [Fact]
    public void Entity_and_enabled_match_php()
    {
        Assert.Equal("storage", CpPricesUploadWriteService.ParseEntityType(""));
        Assert.Equal("storage", CpPricesUploadWriteService.ParseEntityType("warehouse"));
        Assert.Equal("price_list", CpPricesUploadWriteService.ParseEntityType("price_list"));
        Assert.True(CpPricesUploadWriteService.ParseStorefrontEnabled("1"));
        Assert.True(CpPricesUploadWriteService.ParseStorefrontEnabled("yes"));
        Assert.False(CpPricesUploadWriteService.ParseStorefrontEnabled(""));
        Assert.False(CpPricesUploadWriteService.ParseStorefrontEnabled("0"));
    }

    [Fact]
    public void Page_posts_native_storefront_toggle()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPricesUploadApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/prices/storefront-storage/toggle\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"storefront_enabled\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_storefront_toggle_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/prices/storefront-storage/toggle");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_ssf_set_toggle", write.Notes, StringComparison.Ordinal);
        Assert.Contains("storefront_temp_disabled", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_storefront_storage_toggle.php", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_storefront_toggle_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpPricesStorefrontStorageToggle", module, StringComparison.Ordinal);
        Assert.Contains("SetStorefrontToggleAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPricesUploadWriteService.cs"));
        Assert.Contains("epc_ssf_set_toggle", service, StringComparison.Ordinal);
        Assert.Contains("storefront_temp_disabled", service, StringComparison.Ordinal);
        Assert.Contains("epc_storefront_storage_toggle_audit", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", service, StringComparison.Ordinal);
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
