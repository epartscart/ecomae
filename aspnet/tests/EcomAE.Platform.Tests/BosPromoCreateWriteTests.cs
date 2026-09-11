using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosPromoCreateWriteTests
{
    [Fact]
    public void Route_exposes_create()
    {
        Assert.Equal("/bos/promos/create", EcomAeRoutes.BosPromosCreate);
    }

    [Fact]
    public void Php_promo_data_matches_php()
    {
        var empty = BosPromoWriteService.ParsePromoData(null);
        Assert.Equal("", empty.Name);
        Assert.Equal("", empty.Code);
        Assert.Equal("percentage", empty.Type);
        Assert.Equal(0m, empty.Value);
        Assert.Equal("[]", empty.ConditionsJson);
        Assert.Equal(DateTime.Now.ToString("yyyy-MM-dd"), empty.StartDate);
        Assert.Equal(DateTime.Now.AddDays(30).ToString("yyyy-MM-dd"), empty.EndDate);

        var parsed = BosPromoWriteService.ParsePromoData(
            "{\"name\":\"Spring\",\"code\":\"spr-10\",\"type\":\"fixed\",\"value\":\"12.5\",\"min_order\":\"20x\",\"conditions\":{\"sku\":\"A\"}}");
        Assert.Equal("Spring", parsed.Name);
        Assert.Equal("SPR-10", parsed.Code);
        Assert.Equal("fixed", parsed.Type);
        Assert.Equal(12.5m, parsed.Value);
        Assert.Equal(20m, parsed.MinOrder);
        Assert.Contains("sku", parsed.ConditionsJson, StringComparison.Ordinal);
        Assert.Equal(12.5m, BosPromoWriteService.PhpFloat("12.5off"));
        Assert.Equal(20, BosPromoWriteService.PhpIntval("20x"));
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosPromoWriteService(new UnconfiguredConnections())
            .CreateAsync("!!!", null);
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_create()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/promos/create\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_create_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/promos/create");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_promo_create", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_promotions", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_create_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosPromosCreate", module, StringComparison.Ordinal);
        Assert.Contains("CreateAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosPromoWriteService.cs"));
        Assert.Contains("epc_promo_create", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_promotions`", service, StringComparison.Ordinal);
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
