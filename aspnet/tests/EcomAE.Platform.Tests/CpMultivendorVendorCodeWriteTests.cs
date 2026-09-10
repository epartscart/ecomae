using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpMultivendorVendorCodeWriteTests
{
    [Fact]
    public void Route_exposes_vendor_code_save()
    {
        Assert.Equal("/cp/multivendor/vendor-code/save", EcomAeRoutes.CpMultivendorVendorCodeSave);
    }

    [Fact]
    public void Sanitize_matches_php()
    {
        Assert.Equal("ACME", CpPricesUploadWriteService.SanitizeShort("  ACME  "));
        Assert.Equal("ACMEParts", CpPricesUploadWriteService.SanitizeShort("ACME/#\"\\Parts"));
        Assert.Equal("", CpPricesUploadWriteService.SanitizeShort("///"));
        Assert.Equal("Acme Trading", CpPricesUploadWriteService.SanitizeFull("  Acme   Trading  "));
        Assert.Equal("ACME · Acme Trading", CpPricesUploadWriteService.ListBaseName("ACME", "Acme Trading"));
        Assert.Equal("ACME", CpPricesUploadWriteService.ListBaseName("ACME", "acme"));
    }

    [Fact]
    public void Page_posts_native_vendor_code_save()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPricesUploadApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/multivendor/vendor-code/save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"storage_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor_code\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"vendor_full\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_vendor_code_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/multivendor/vendor-code/save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("vendor_code_save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("shop_storages", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_multivendor_ingest.php", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_vendor_code_save_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpMultivendorVendorCodeSave", module, StringComparison.Ordinal);
        Assert.Contains("SaveVendorCodeAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPricesUploadWriteService.cs"));
        Assert.Contains("epc_multivendor_vendor_code_save", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `shop_storages`", service, StringComparison.Ordinal);
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
