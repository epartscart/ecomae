using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP <c>cp/content/control/portal/epc_integrations_hub.php</c> +
/// <c>content/general_pages/epc_integrations_helpers.php</c> parity.
/// </summary>
public sealed class CpIntegrationsHubParityTests
{
    private static string PageSource()
        => File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsApp.razor"));

    private static string RepoPath(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "aspnet")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, relative);
    }

    [Fact]
    public void Categories_MatchPhpOrderAndLabels()
    {
        Assert.Equal(
            ["identity", "commerce", "growth", "catalog", "data", "platform"],
            CpIntegrationsHubCatalog.Categories.Select(c => c.Key));
        Assert.Equal("Identity & messaging", CpIntegrationsHubCatalog.CategoryLabel("identity"));
        Assert.Equal("Commerce & payments", CpIntegrationsHubCatalog.CategoryLabel("commerce"));
        Assert.Equal("Data & APIs", CpIntegrationsHubCatalog.CategoryLabel("data"));
        Assert.All(CpIntegrationsHubCatalog.Categories, c =>
        {
            Assert.StartsWith("fa-", c.Icon, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(c.Blurb));
        });
    }

    [Fact]
    public void EveryCatalogEntry_HasKnownCategory()
    {
        var keys = CpIntegrationsHubCatalog.Categories.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        Assert.All(CpIntegrationsHubCatalog.All, entry => Assert.Contains(entry.Category, keys));
    }

    [Fact]
    public void SuperRows_AreAllActive_AndKeepSuperUrls()
    {
        var rows = CpIntegrationsHubCatalog.BuildHubCards(isSuper: true);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.True(row.Active));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.ConfigureUrl)));
        Assert.Contains(rows, row => row.SuperOnly);
    }

    [Fact]
    public void TenantRows_SendSuperOnlyConfigBackToHub_AndHonourFeatureFlags()
    {
        var rows = CpIntegrationsHubCatalog.BuildHubCards(
            isSuper: false,
            featureFlags: new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["whatsapp"] = false,
                ["pos"] = true,
            });

        Assert.All(
            rows.Where(row => row.SuperOnly),
            row => Assert.Equal("/cp/control/portal/epc_integrations_hub", row.ConfigureUrl));
        Assert.False(rows.Single(row => row.Key == "whatsapp").Active);
        Assert.True(rows.Single(row => row.Key == "pos").Active);
    }

    [Fact]
    public void TenantRows_NeverPointGuideAtSuperApiDocumentation()
    {
        var rows = CpIntegrationsHubCatalog.BuildHubCards(isSuper: false);

        Assert.All(rows, row => Assert.DoesNotContain("epc_api_documentation_guide", row.Guide, StringComparison.Ordinal));
        Assert.All(rows, row => Assert.False(string.IsNullOrWhiteSpace(row.Guide)));
    }

    [Fact]
    public void ResolveGuide_MirrorsPhpAnchorFallback()
    {
        Assert.Equal("/cp/control/portal/epc_integrations_guide#pos", CpIntegrationsHubCatalog.ResolveGuide("", "pos"));
        Assert.Equal("/cp/control/portal/epc_web_tracker", CpIntegrationsHubCatalog.ResolveGuide("/cp/control/portal/epc_web_tracker", "web_tracker"));
        Assert.Equal("https://docs.example/x", CpIntegrationsHubCatalog.ResolveGuide("https://docs.example/x", "oauth"));
    }

    [Fact]
    public void MarketLabel_MirrorsPhpCountryMeta()
    {
        Assert.Equal("United Arab Emirates", CpIntegrationsHubService.MarketLabel(null));
        Assert.Equal("United Arab Emirates", CpIntegrationsHubService.MarketLabel("UAE"));
        Assert.Equal("Pakistan", CpIntegrationsHubService.MarketLabel("pk"));
        Assert.Equal("Saudi Arabia", CpIntegrationsHubService.MarketLabel("SA"));
        Assert.Equal("ZZ", CpIntegrationsHubService.MarketLabel("zz"));
    }

    [Fact]
    public void SearchHay_CoversLabelBlurbKeyAndCategory()
    {
        var card = new CpIntegrationsHubCard(
            "pos", "POS Terminal", "fa-cash-register", "#1d4ed8", "commerce",
            "Counter sales", true, "/cp/shop/pos/terminal", "/cp/control/portal/epc_integrations_guide#pos", false);

        var hay = card.SearchHay("Commerce & payments");

        Assert.Contains("pos terminal", hay, StringComparison.Ordinal);
        Assert.Contains("counter sales", hay, StringComparison.Ordinal);
        Assert.Contains("commerce & payments", hay, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_RendersPhpHeroStatsMarketToolbarAndPlaybook()
    {
        var source = PageSource();

        Assert.Contains("Ecomae · Super CP", source, StringComparison.Ordinal);
        Assert.Contains("EPartsCart · Tenant CP", source, StringComparison.Ordinal);
        Assert.Contains("Enable, configure, and test every connected module from one place.", source, StringComparison.Ordinal);
        Assert.Contains("Guides ready", source, StringComparison.Ordinal);
        Assert.Contains("In catalog", source, StringComparison.Ordinal);
        Assert.Contains("Tax, Auto Price discovery sources, and ERP defaults follow your registered country.", source, StringComparison.Ordinal);
        Assert.Contains("data-inthub-search", source, StringComparison.Ordinal);
        Assert.Contains("data-inthub-chip=\"all\"", source, StringComparison.Ordinal);
        Assert.Contains("data-inthub-card", source, StringComparison.Ordinal);
        Assert.Contains("data-inthub-section", source, StringComparison.Ordinal);
        Assert.Contains("No integrations match your filter. Clear search or choose another category.", source, StringComparison.Ordinal);
        Assert.Contains("Test &amp; go live", source, StringComparison.Ordinal);
        Assert.Contains("Configured on ecomae.com", source, StringComparison.Ordinal);
        Assert.Contains("Admin login required.", source, StringComparison.Ordinal);
        Assert.Contains("epc_integrations_hub_ui.js", source, StringComparison.Ordinal);
        Assert.DoesNotContain("table-condensed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_UsesHubServiceAndSuperHostGate()
    {
        var source = PageSource();

        Assert.Contains("@inject ICpIntegrationsHubService Hub", source, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed(ctx)", source, StringComparison.Ordinal);
        Assert.Contains("Hub.LoadAsync(isSuper", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetBridge_ServesPhpHubScript()
    {
        var bridge = File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));

        Assert.Contains("cp/content/control/portal/epc_integrations_hub_ui.js", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_IsRegistered()
    {
        var program = File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Program.cs"));

        Assert.Contains("ICpIntegrationsHubService, EcomAE.Platform.Cp.CpIntegrationsHubService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_SelectsNoSecrets()
    {
        var service = File.ReadAllText(RepoPath("aspnet/src/EcomAE.Platform/Cp/CpIntegrationsHubService.cs"));

        Assert.DoesNotContain("password", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", service, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpDb.Positional", service, StringComparison.Ordinal);
    }
}
