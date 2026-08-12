using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CrossbaseReferenceLoaderTests
{
    [Fact]
    public void ParseHtml_ExtractsCrossbaseLinks()
    {
        var html = """
            <html><body>
            <p>существует 12 замен</p>
            <a href="/cross/?q=PF1233">ACDELCO PF1233</a>
            <a href="/cross/?q=C110J">JS ASAKASHI C110J</a>
            <a href="/cross/?q=SP1008">ALCO SP1008</a>
            <a href="/cross/?q=PF1233">ACDELCO PF1233 duplicate</a>
            </body></html>
            """;

        var rows = CrossbaseReferenceLoader.ParseHtml(html, selfNorm: "C110J", maxRefs: 50);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Article.Contains("PF1233", StringComparison.OrdinalIgnoreCase) && r.Source == "crossbase");
        Assert.Contains(rows, r => r.Article.Contains("SP1008", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rows, r => PriceLookupLike(r.Article) == "C110J");
    }

    [Fact]
    public void ChpuClient_RequestsIncludeCrossbaseAndHidesStuckPoll()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("include_crossbase=1", text, StringComparison.Ordinal);
        Assert.Contains("Never leave \"Polling suppliers…\" visible", text, StringComparison.Ordinal);
        Assert.Contains("__epcChpuBootRunning", text, StringComparison.Ordinal);
        Assert.Contains("notranslate", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_ExposesCrossbaseCount()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("include_crossbase", module, StringComparison.Ordinal);
        Assert.Contains("crossbase_count = result.CrossbaseCount", module, StringComparison.Ordinal);
        Assert.Contains("includeCrossbase: wantCrossbase", module, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossSearch_ReservesSlotsForUniqueCrossbase()
    {
        var reporter = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("safeLimit * 0.65", reporter, StringComparison.Ordinal);
        Assert.Contains("uniqueCrossbase", reporter, StringComparison.Ordinal);
        Assert.Contains("Prefer showing distinct crossbase rows", reporter, StringComparison.Ordinal);
        // Must merge even when local CP already filled the limit (AISIN/DT068).
        Assert.DoesNotContain("includeCrossbase && rows.Count < safeLimit", reporter, StringComparison.Ordinal);
        // CP∩crossbase overlap must keep crossbase provenance for the green CROSSBASE badge UX.
        Assert.Contains("Source = \"cp+crossbase\"", reporter, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseParityJs_RequestsIncludeCrossbase()
    {
        var js = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("include_crossbase=1", js, StringComparison.Ordinal);
        Assert.Contains("data-source=", js, StringComparison.Ordinal);
        Assert.Contains("Do not wipe a larger CHPU bootstrap list", js, StringComparison.Ordinal);
        // PHP part_search_page twin: green button opens modal, not only scroll-to-list.
        Assert.Contains("function openCrossModal(", js, StringComparison.Ordinal);
        Assert.Contains("openCrossModalFromButton", js, StringComparison.Ordinal);
        Assert.Contains("__epcLastCrossPayload", js, StringComparison.Ordinal);
    }

    [Fact]
    public void ChpuClient_CachesCrossPayloadForModal()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("__epcLastCrossPayload", text, StringComparison.Ordinal);
        Assert.Contains("epc_warehouse_search_parity.js?v=20260812-cross-quote", text, StringComparison.Ordinal);
        Assert.Contains("indexOf('crossbase')", text, StringComparison.Ordinal);
        // PHP empty-warehouse path: merge cross stock into the main offer table.
        Assert.Contains("mergeCrossStockIntoOffers", text, StringComparison.Ordinal);
        Assert.Contains("__epcPendingCrossStock", text, StringComparison.Ordinal);
        Assert.Contains("Cross reference stock found", text, StringComparison.Ordinal);
        // Heavy articles (ASAKASHI/C110J ~3.6s+) must not abort at 1.5s/4s.
        Assert.Contains("fetchCross(200, 12000, false)", text, StringComparison.Ordinal);
        Assert.Contains("fetchCross(600, 20000, true)", text, StringComparison.Ordinal);
        Assert.Contains("fromCrossStock: true", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossSearch_LoadsBatchedStockForReferences()
    {
        var reporter = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("LoadStorefrontCrossStockAsync", reporter, StringComparison.Ordinal);
        Assert.Contains("StorefrontCrossStockDigest", reporter, StringComparison.Ordinal);
        // Must use PHP REPLACE-normalize IN (not only exact trim) so STD / OE variants hit prices_data.
        Assert.Contains("StorefrontPriceArticleReplaceInSql", reporter, StringComparison.Ordinal);
        // Local CP reader must dispose before stock batch reuses the same MySqlConnection.
        Assert.Contains("already in use", reporter, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("await using (var reader = await command.ExecuteReaderAsync", reporter, StringComparison.Ordinal);
        // Heavy analogs queries exceed the old 2s CommandTimeout after republish load.
        Assert.Contains("command.CommandTimeout = 10", reporter, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("stock_count = stock.Count", module, StringComparison.Ordinal);
        Assert.Contains("prices_visible = access.PricesVisible", module, StringComparison.Ordinal);
    }

    private static string PriceLookupLike(string article)
        => new string((article ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

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
