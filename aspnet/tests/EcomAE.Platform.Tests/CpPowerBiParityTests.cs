using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPowerBiParityTests
{
    [Fact]
    public void Catalog_matches_php_power_bi_module()
    {
        Assert.Equal("1.0.0", PowerBiCatalog.Version);
        Assert.Equal("https://www.ecomae.com/epc-api/v1/powerbi", PowerBiCatalog.ApiRoot);
        Assert.Equal(
            new[] { "catalog", "kpis", "orders", "sales", "stock", "gl", "metrics" },
            PowerBiCatalog.Datasets.Select(dataset => dataset.Id).ToArray());
        Assert.All(
            PowerBiCatalog.Datasets,
            dataset => Assert.StartsWith(PowerBiCatalog.ApiRoot, dataset.Path, StringComparison.Ordinal));
        Assert.All(PowerBiCatalog.Datasets, dataset => Assert.Equal("read:bi", dataset.Scope));
        Assert.Contains("tenant_isolation", PowerBiCatalog.AvailableNow);
        Assert.Contains("url_embed_iframe", PowerBiCatalog.AvailableNow);
        Assert.Contains(PowerBiCatalog.NeedsCustomerCredentials, need => need.Key == "azure_ad_app");
        Assert.Contains("azure_embed_token_generation", PowerBiCatalog.NotInScopePhaseA);
        Assert.Equal(5, PowerBiCatalog.ConnectGuide.Count);
        Assert.Contains("X-API-Key", PowerBiCatalog.CurlExample, StringComparison.Ordinal);
    }

    [Fact]
    public void Embed_url_allowlist_matches_php_regex()
    {
        Assert.True(PowerBiCatalog.EmbedUrlAllowed("https://app.powerbi.com/view?r=abc"));
        Assert.True(PowerBiCatalog.EmbedUrlAllowed("https://powerbi.us/report"));
        Assert.False(PowerBiCatalog.EmbedUrlAllowed("http://app.powerbi.com/view"));
        Assert.False(PowerBiCatalog.EmbedUrlAllowed("https://evil.com/app.powerbi.com"));
        Assert.False(PowerBiCatalog.EmbedUrlAllowed(""));
    }

    [Fact]
    public void Embed_resolution_mirrors_php_phases()
    {
        Assert.Equal("config_missing", PowerBiCatalog.ResolveEmbed(false, "url", "https://app.powerbi.com/x").Phase);
        Assert.Equal("needs_azure", PowerBiCatalog.ResolveEmbed(true, "azure", "").Phase);
        Assert.Equal("url_missing", PowerBiCatalog.ResolveEmbed(true, "none", "").Phase);
        Assert.Equal("url_invalid", PowerBiCatalog.ResolveEmbed(true, "url", "https://example.com/r").Phase);

        var ready = PowerBiCatalog.ResolveEmbed(true, "url", "https://app.powerbi.com/view?r=abc");
        Assert.True(ready.Ok);
        Assert.Equal("ready", ready.Phase);
        Assert.Equal("https://app.powerbi.com/view?r=abc", ready.Url);

        var reportWins = PowerBiCatalog.ResolveEmbed(
            true, "url", "https://app.powerbi.com/view?r=config", "https://app.powerbi.com/view?r=report");
        Assert.Equal("https://app.powerbi.com/view?r=report", reportWins.Url);

        Assert.False(PowerBiCatalog.ResolveEmbed(true, "azure", "https://app.powerbi.com/view?r=abc").Ok);
    }

    [Fact]
    public void Site_key_resolution_matches_php_host_matching()
    {
        IReadOnlyList<CpPowerBiTenantOption> tenants =
        [
            new("alpha", "alpha.example.com", "Alpha"),
            new("beta", "www.beta.example.com", "Beta"),
        ];

        Assert.Equal("beta", CpPowerBiService.ResolveSiteKey("", "beta.example.com", tenants));
        Assert.Equal("beta", CpPowerBiService.ResolveSiteKey("", "www.beta.example.com:8443", tenants));
        Assert.Equal("alpha", CpPowerBiService.ResolveSiteKey("", "unknown.example.com", tenants));
        Assert.Equal("beta", CpPowerBiService.ResolveSiteKey("beta", "alpha.example.com", tenants));
        Assert.Equal("epartscart", CpPowerBiService.ResolveSiteKey("", "x.example.com", []));
    }

    [Fact]
    public void Page_renders_php_sections_over_typed_read_service()
    {
        var razor = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpPowerBiApp.razor"));
        Assert.Contains("ICpPowerBiService", razor, StringComparison.Ordinal);
        Assert.Contains("What is possible now", razor, StringComparison.Ordinal);
        Assert.Contains("Power BI datasets (Web connector)", razor, StringComparison.Ordinal);
        Assert.Contains("Registered reports", razor, StringComparison.Ordinal);
        Assert.Contains("Embed preview", razor, StringComparison.Ordinal);
        Assert.Contains("PowerBiCatalog.Datasets", razor, StringComparison.Ordinal);
        Assert.Contains("PowerBiCatalog.ConnectGuide", razor, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", razor, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_service_is_registered_and_tenant_scoped()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpPowerBiService, EcomAE.Platform.Cp.CpPowerBiService", program, StringComparison.Ordinal);

        var service = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpPowerBiService.cs"));
        Assert.Contains("epc_portal_tenants", service, StringComparison.Ordinal);
        Assert.Contains("epc_power_bi_config", service, StringComparison.Ordinal);
        Assert.Contains("epc_power_bi_reports", service, StringComparison.Ordinal);
        Assert.Contains("EnsureSchemaAsync", service, StringComparison.Ordinal);
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
