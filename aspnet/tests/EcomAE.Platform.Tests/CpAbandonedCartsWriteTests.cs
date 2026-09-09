using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpAbandonedCartsWriteTests
{
    [Fact]
    public void Route_exposes_abandoned_carts_write()
    {
        Assert.Equal("/cp/abandoned-carts/write", EcomAeRoutes.CpAbandonedCartsWrite);
    }

    [Fact]
    public void Page_posts_native_type2_delete()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpAbandonedCartsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/abandoned-carts/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete\"", razor, StringComparison.Ordinal);
        Assert.Contains("ProductType == 2", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_delete_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/abandoned-carts/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_delete_cart_record.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("stay Classic", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_delete_type2()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpAbandonedCartsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpAbandonedCartsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("delete_cart", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpAbandonedCartsWriteService.cs"));
        Assert.Contains("deleteRecordType2", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `shop_carts`", service, StringComparison.Ordinal);
        Assert.Contains("`product_type`=2", service, StringComparison.Ordinal);
        Assert.Contains("Catalogue reserve release stay Classic", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.DoesNotContain("shop_storages_data", service, StringComparison.Ordinal);
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
