using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosRmaRegisterWriteTests
{
    [Fact]
    public void Route_exposes_register()
    {
        Assert.Equal("/bos/rma/register", EcomAeRoutes.BosRmaRegister);
    }

    [Fact]
    public void Php_warranty_data_matches_php()
    {
        var empty = BosRmaWriteService.ParseWarrantyData(null);
        Assert.Equal("", empty.ProductSku);
        Assert.Equal(12, empty.WarrantyMonths);
        Assert.Equal("standard", empty.WarrantyType);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), empty.PurchaseDate);
        Assert.Equal(BosRmaWriteService.ExpiryDate(empty.PurchaseDate, 12), empty.ExpiryDate);

        var parsed = BosRmaWriteService.ParseWarrantyData(
            "{\"product_sku\":\"SKU-1\",\"product_name\":\"Pad\",\"serial_number\":\"SN1\",\"customer_id\":\"4x\",\"purchase_date\":\"2026-01-15\",\"warranty_months\":\"24\"}");
        Assert.Equal("SKU-1", parsed.ProductSku);
        Assert.Equal("Pad", parsed.ProductName);
        Assert.Equal("SN1", parsed.SerialNumber);
        Assert.Equal(4, parsed.CustomerId);
        Assert.Equal("2026-01-15", parsed.PurchaseDate);
        Assert.Equal(24, parsed.WarrantyMonths);
        Assert.Equal("2028-01-15", parsed.ExpiryDate);
        Assert.Equal("2026-07-11", BosRmaWriteService.ExpiryDate("2026-01-11", 6));
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosRmaWriteService(new UnconfiguredConnections())
            .RegisterAsync("!!!", null);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_register()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/rma/register\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"serial_number\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_register_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/rma/register");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_warranty_register", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_warranties", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_register_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosRmaRegister", module, StringComparison.Ordinal);
        Assert.Contains("RegisterAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosRmaWriteService.cs"));
        Assert.Contains("epc_warranty_register", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_warranties`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
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
