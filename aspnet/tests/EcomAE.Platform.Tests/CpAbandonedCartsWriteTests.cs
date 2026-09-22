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
    public void Page_is_live_carts_php_twin_with_native_bulk_delete()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpAbandonedCartsApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/abandoned-carts/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"records_to_del[]\"", razor, StringComparison.Ordinal);
        Assert.Contains("check_uncheck_all", razor, StringComparison.Ordinal);
        Assert.Contains("ICpAbandonedCartsEditorService", razor, StringComparison.Ordinal);
        Assert.Contains("Carts.ListAsync(", razor, StringComparison.Ordinal);
        foreach (var field in new[] { "time_from", "time_to", "customer", "storage_id" })
        {
            Assert.Contains("name=\"" + field + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var sorter in new[] { "id", "manufacturer", "article", "product_name", "price", "count_need", "price_sum", "price_purchase_sum", "profit", "time", "user_id" })
        {
            Assert.Contains("SortLink(\"" + sorter + "\"", razor, StringComparison.Ordinal);
        }

        Assert.Contains("epc-details-row", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpAbandonedCartsDigestAsync", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
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
        Assert.Contains("deleteRecordType1", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_delete_type1_and_type2()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpAbandonedCartsWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpAbandonedCartsWriteService", module, StringComparison.Ordinal);
        Assert.Contains("delete_cart", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpAbandonedCartsWriteService.cs"));
        Assert.Contains("deleteRecordType1", service, StringComparison.Ordinal);
        Assert.Contains("deleteRecordType2", service, StringComparison.Ordinal);
        Assert.Contains("DeleteManyAsync", service, StringComparison.Ordinal);
        Assert.Contains("BeginTransactionAsync", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `shop_carts`", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `shop_carts_details`", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `shop_storages_data` SET `exist` = `exist`+?, `reserved` = `reserved`-?", service, StringComparison.Ordinal);
        Assert.Contains("records_to_del[]", module, StringComparison.Ordinal);
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
