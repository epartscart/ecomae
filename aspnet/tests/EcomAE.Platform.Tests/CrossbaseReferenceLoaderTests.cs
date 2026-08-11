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
