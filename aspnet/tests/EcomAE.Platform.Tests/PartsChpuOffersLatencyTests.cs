using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the 1–3s CHPU warehouse first-paint budget for brand+article pages
/// (e.g. /en/parts/AISIN/DT068).
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
    }

    [Fact]
    public void ChpuSsr_DoesNotAwaitGenuineBrands()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("do NOT await warehouse/cross/genuine SSR", text, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource(TimeSpan.FromMilliseconds(250))", text, StringComparison.Ordinal);
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
