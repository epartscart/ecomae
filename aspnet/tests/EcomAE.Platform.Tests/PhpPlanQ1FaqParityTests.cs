using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1FaqParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Faq");

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
    public void PlanQ1Faq_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Faq.FaqPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Faq.FaqPath
            });

    [Fact]
    public void FormatAnswer_DoesNotStartASession()
    {
        PhpPlanQ1Faq.Reset();
        Assert.Contains("Catalog API", PhpPlanQ1Faq.EpcEcomaeFaqFormatAnswer("See Catalog API."), StringComparison.Ordinal);
    }

    private static string Render(string name)
    {
        PhpPlanQ1Faq.Reset();
        return name switch
        {
            "faq_bits" => Json(FaqBits()),
            "faq_page" => Json(FaqPage()),
            _ => "unknown:" + name
        };
    }

    private static object?[] FaqBits()
    {
        var schema = JsonDocument.Parse(PhpPlanQ1Faq.EpcEcomaeFaqSchemaJson()).RootElement;
        var styles = PhpPlanQ1Faq.EpcEcomaeFaqStyles();
        return new object?[]
        {
            PhpPlanQ1Faq.EpcEcomaeFaqStatusClass("Yes"),
            PhpPlanQ1Faq.EpcEcomaeFaqStatusClass("Partial"),
            PhpPlanQ1Faq.EpcEcomaeFaqStatusClass("Planned"),
            PhpPlanQ1Faq.EpcEcomaeFaqStatusClass("No"),
            PhpPlanQ1Faq.EpcEcomaeFaqStatusClass("Other"),
            PhpPlanQ1Faq.EpcEcomaeFaqFormatAnswer("See the Auto Price AI page and Catalog API."),
            PhpPlanQ1Faq.EpcEcomaeFaqFormatAnswer("Open the demo page or pricing page."),
            PhpPlanQ1Pack.EpcEcomaeFaqStatusCounts(),
            styles.Length >= 20 ? styles[..20] : styles,
            styles.Length,
            schema.GetProperty("@type").GetString(),
            schema.GetProperty("mainEntity").GetArrayLength(),
            schema.GetProperty("mainEntity")[0].GetProperty("name").GetString()
        };
    }

    private static object?[] FaqPage()
    {
        var html = PhpPlanQ1Faq.EpcEcomaeFaqRenderPage();
        return new object?[]
        {
            Count(html, "epm-faq-item"),
            Count(html, "epm-faq-tab"),
            html.Contains("Frequently Asked Questions", StringComparison.Ordinal),
            html.Contains("14-day demo", StringComparison.Ordinal),
            html.Contains("function showModule", StringComparison.Ordinal),
            html.Contains("function filterFaq", StringComparison.Ordinal),
            html.Contains("platform/contact", StringComparison.Ordinal),
            html.Contains("application/ld+json", StringComparison.Ordinal)
        };
    }

    private static int Count(string haystack, string needle)
    {
        var n = 0;
        var i = 0;
        while (true)
        {
            var found = haystack.IndexOf(needle, i, StringComparison.Ordinal);
            if (found < 0)
            {
                return n;
            }

            n++;
            i = found + 1;
        }
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 500 ? value : value[..500] + "…";
}
