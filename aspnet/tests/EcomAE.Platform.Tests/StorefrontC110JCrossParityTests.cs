using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// JS ASAKASHI / C110J PHP twin: 700+ unique crosses, Genuine/Aftermarket warehouse rows,
/// Add to Cart when stock &gt; 0, Add to Quote when cross / not available.
/// </summary>
public sealed class StorefrontC110JCrossParityTests
{
    [Fact]
    public void CrossSearch_DefaultLimitMatchesPhpLocalMax()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("limit ?? LegacySurfaceDashboardSql.StorefrontCrossSearchMax", module, StringComparison.Ordinal);
        Assert.Equal(5000, LegacySurfaceDashboardSql.StorefrontCrossSearchMax);
    }

    [Fact]
    public void ChpuPage_ShowsAllCrossesAndCartQuoteActions()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("fetchCross(800, 20000, false)", text, StringComparison.Ordinal);
        Assert.Contains("fetchCross(5000, 60000, true)", text, StringComparison.Ordinal);
        Assert.Contains("opts.showAll ? 5000 : refs.length", text, StringComparison.Ordinal);
        Assert.Contains("Genuine (OE)", text, StringComparison.Ordinal);
        Assert.Contains("Aftermarket", text, StringComparison.Ordinal);
        Assert.Contains("Add to Cart", text, StringComparison.Ordinal);
        Assert.Contains("Add to Quote", text, StringComparison.Ordinal);
        Assert.Contains("fromCrossStock: true", text, StringComparison.Ordinal);
        Assert.Contains("epc-product-actions--quote-only", text, StringComparison.Ordinal);
        Assert.Contains("exist > 0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseParityJs_RequestsFullPhpCap()
    {
        var js = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("&limit=5000&include_crossbase=1", js, StringComparison.Ordinal);
        Assert.Contains("shown < 5000", js, StringComparison.Ordinal);
        Assert.Contains("epc-btn-cart", js, StringComparison.Ordinal);
        Assert.Contains("epc-btn-quote", js, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossbaseParse_DoesNotCapBelowC110JCatalog()
    {
        var html = new System.Text.StringBuilder("<html><body><p>существует 720 замен</p>");
        for (var i = 0; i < 720; i++)
        {
            html.Append("<a href=\"/cross/?q=C110X").Append(i).Append("\">BRAND").Append(i)
                .Append(" C110X").Append(i).Append("</a>");
        }

        html.Append("</body></html>");
        var rows = CrossbaseReferenceLoader.ParseHtml(html.ToString(), selfNorm: "C110J", maxRefs: 2500);
        Assert.Equal(720, rows.Count);
        Assert.All(rows, r => Assert.Equal("crossbase", r.Source));
    }

    [Fact]
    public void Reporter_TwoSidedLocalLoad_DoesNotOrScanInCrossSearch()
    {
        var reporter = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        var start = reporter.IndexOf("public async Task<StorefrontCrossSearchResult> BuildStorefrontCrossSearchAsync", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = reporter.IndexOf("private async Task AppendStorefrontCrossPairsAsync", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var body = reporter[start..end];
        Assert.Contains("StorefrontCrossArticleSideMatchSql", body, StringComparison.Ordinal);
        Assert.Contains("StorefrontCrossAnalogSideMatchSql", body, StringComparison.Ordinal);
        Assert.DoesNotContain("StorefrontCrossArticleMatchSql(", body, StringComparison.Ordinal);
        Assert.Contains("timeoutMs: 12000", body, StringComparison.Ordinal);
        Assert.Contains("StorefrontCrossStockMax", reporter, StringComparison.Ordinal);
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
