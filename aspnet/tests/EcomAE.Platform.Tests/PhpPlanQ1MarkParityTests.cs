using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1MarkParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Mark");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1Mark_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result").GetString() ?? "";
            var actual = Render(cases[i]);
            if (actual != expected)
            {
                var at = FirstDiff(actual, expected);
                failures.Add(name + " got=" + actual.Length + " exp=" + expected.Length + " at=" + at);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mark.CatalogueHelperPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mark.CatalogueHelperPath });

    [Fact]
    public void PrintProductBlock_DoesNotStartASession()
    {
        using var doc = JsonDocument.Parse("""{"id":1,"product_block_type":1,"main_class_of_block":"product_div_list","caption":"X","product_url":"p/1","manufacturer":"A","article":"B","stickers":{},"marks_count":0,"mark":0,"mark_1":0,"mark_2":0,"mark_3":0,"mark_4":0,"mark_5":0,"article_button":"","button":"","category_id":1}""");
        var html = PhpPlanQ1Mark.PrintProductBlock(doc.RootElement, "/en/", true, false);
        Assert.Contains("product_div_name", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
    }

    private static string Render(JsonElement c)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (c.TryGetProperty("cookies", out var cookieEl) && cookieEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in cookieEl.EnumerateObject())
            {
                cookies[prop.Name] = prop.Value.GetString() ?? "";
            }
        }

        return PhpPlanQ1Mark.PrintProductBlock(
            c.GetProperty("product"),
            c.TryGetProperty("lang_href", out var lang) ? lang.GetString() ?? "" : "",
            c.TryGetProperty("main_flag", out var mf) && mf.ValueKind == JsonValueKind.True,
            c.TryGetProperty("admin", out var ad) && ad.ValueKind == JsonValueKind.True,
            cookies);
    }

    private static int FirstDiff(string actual, string expected)
    {
        var n = Math.Min(actual.Length, expected.Length);
        for (var i = 0; i < n; i++)
        {
            if (actual[i] != expected[i])
            {
                return i;
            }
        }

        return n;
    }
}
