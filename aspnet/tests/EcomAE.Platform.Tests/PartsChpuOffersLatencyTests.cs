using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the ~1s CHPU warehouse + cross first-paint budget for brand+article pages
/// (e.g. /en/parts/JS%20ASAKASHI/C110J, /en/parts/AISIN/DT068).
/// </summary>
public sealed class PartsChpuOffersLatencyTests
{
    [Fact]
    public void ChpuClient_FiresProtocol3BeforeSearchBunches()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        var immediate = text.IndexOf("Immediate protocol-3 poll", StringComparison.Ordinal);
        var bunches = text.IndexOf("Background nested-bunch enrichment", StringComparison.Ordinal);
        Assert.True(immediate >= 0, "missing immediate protocol-3 poll marker");
        Assert.True(bunches > immediate, "search-bunches enrichment must come after immediate poll");
        Assert.Contains("AbortSignal.timeout(3000)", text, StringComparison.Ordinal);
        Assert.Contains("data-enhance-nav=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("epcRunChpuPriceSearchBootstrap", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChpuClient_UsesAspNetCrossSearchBeforePhpCrossbase()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        var asp = text.IndexOf("/storefront/cross-search?", StringComparison.Ordinal);
        var php = text.IndexOf("ajax_epc_cross_search.php", StringComparison.Ordinal);
        Assert.True(asp >= 0, "missing ASP.NET /storefront/cross-search fast path");
        Assert.True(php > asp, "PHP crossbase enrich must come after ASP.NET local crosses");
        Assert.Contains("AbortSignal.timeout(1500)", text, StringComparison.Ordinal);
        Assert.Contains("Local crosses ready — expanding crossbase network", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ChpuSsr_DoesNotAwaitGenuineBrands()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("do NOT await warehouse/cross/genuine SSR", text, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource(TimeSpan.FromMilliseconds(250))", text, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ListStorefrontGenuineBrandsAsync()", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductsOfBunch_HardCapsProtocol3Budget()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("CancelAfter(TimeSpan.FromMilliseconds(2500))", text, StringComparison.Ordinal);
        Assert.Contains("timeoutSeconds: 2", text, StringComparison.Ordinal);
        Assert.Contains("command.CommandTimeout = 2", text, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.Contains("aspnet-cross-local", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_MapsCrossSearchRoute()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontCrossSearch = \"/storefront/cross-search\"", routes, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontCrossSearch", module, StringComparison.Ordinal);
        Assert.Contains("BuildStorefrontCrossSearchAsync", module, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
