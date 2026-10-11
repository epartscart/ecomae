using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HillParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hill");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hill_MatchPhpGolden()
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
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                var exp = expected.GetRawText();
                var got = Json(actual);
                failures.Add(name + " exp=" + FirstDiff(exp, got) + " gotLen=" + got.Length + " expLen=" + exp.Length);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/general_pages/epc_ecomae_free_tools.php", PhpPlanQ1Hill.FreeToolsPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hill.Reset();
        var hub = PhpPlanQ1Hill.EpcEcomaePlatformPageFreeTools();
        var vat = PhpPlanQ1Hill.EpcEcomaePlatformPageFreeTools(new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool"] = "vat" });
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hill.FreeToolsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", hub, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", vat, StringComparison.Ordinal);
        Assert.Contains("VAT / GST Return", hub, StringComparison.Ordinal);
        Assert.Contains("Create free account", vat, StringComparison.Ordinal);
        Assert.Contains("data-eft-tool=\"vat\"", vat, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Hill.FreeToolsPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hill.Reset();
        return name switch
        {
            "empty" => Empty(),
            "gates" => Gates(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Empty()
    {
        var cat = PhpPlanQ1Hill.EpcFreeToolsCatalog();
        var names = cat.ToDictionary(kv => kv.Key, kv => kv.Value["name"], StringComparer.Ordinal);
        var catalog = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["keys"] = cat.Keys.ToList(),
            ["names"] = names,
            ["count"] = cat.Count
        };
        var countries = PhpPlanQ1Hill.EpcFreeToolsCountries();
        return new object[]
        {
            catalog,
            countries["AE"],
            countries["XX"],
            PhpPlanQ1Hill.EpcFreeToolsCitRate("AE"),
            PhpPlanQ1Hill.EpcFreeToolsCitRate("zz"),
            PhpPlanQ1Hill.EpcFreeToolsAuthority("AE"),
            PhpPlanQ1Hill.EpcFreeToolsH("Ada & Co <x>"),
            PhpPlanQ1Hill.EpcFreeToolsRound("1.235"),
            PhpPlanQ1Hill.EpcFreeToolsFinding("warn", "Ada & Co")
        };
    }

    private static object Gates()
    {
        PhpPlanQ1Hill.HttpHost = "";
        PhpPlanQ1Hill.Https = "";
        var baseNone = PhpPlanQ1Hill.EpcFreeToolsSiteBase("");
        var baseFb = PhpPlanQ1Hill.EpcFreeToolsSiteBase("https://acme.example/");
        PhpPlanQ1Hill.HttpHost = "beta.example.com";
        PhpPlanQ1Hill.Https = "off";
        var baseHost = PhpPlanQ1Hill.EpcFreeToolsSiteBase("");
        var seoHub = PhpPlanQ1Hill.EpcFreeToolsSeo("");
        var seoVat = PhpPlanQ1Hill.EpcFreeToolsSeo("vat");
        var faq = PhpPlanQ1Hill.EpcFreeToolsFaqHtml("vat");
        var guide = PhpPlanQ1Hill.EpcFreeToolsGuide("vat");
        var disabled = PhpPlanQ1Hill.EpcFreeToolsDisabledMap();
        var active = PhpPlanQ1Hill.EpcFreeToolsIsActive("vat");
        return new object[] { baseNone, baseFb, baseHost, seoHub["title"], seoVat["title"], faq, guide["what"], disabled, active };
    }

    private static object Live()
    {
        var hub = PhpPlanQ1Hill.EpcEcomaePlatformPageFreeTools();
        var vat = PhpPlanQ1Hill.EpcEcomaePlatformPageFreeTools(new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool"] = "vat" });
        var bad = PhpPlanQ1Hill.EpcEcomaePlatformPageFreeTools(new Dictionary<string, object?>(StringComparer.Ordinal) { ["tool"] = "nope!" });
        var json = PhpPlanQ1Hill.EpcFreeToolsJsonld("https://www.ecomae.com/", "vat");
        var styles = PhpPlanQ1Hill.EpcFreeToolsStyles();
        var scripts = PhpPlanQ1Hill.EpcFreeToolsScripts("https://www.ecomae.com/");
        var fields = PhpPlanQ1Hill.EpcFreeToolsFieldsHtml("vat");
        return new object[]
        {
            hub.Length >= 80 ? hub[..80] : hub,
            hub.Contains("VAT / GST Return", StringComparison.Ordinal) ? 1 : 0,
            vat.Contains("Create free account", StringComparison.Ordinal) ? 1 : 0,
            bad.Contains("Free business tools", StringComparison.Ordinal) ? 1 : 0,
            json.Contains("FAQPage", StringComparison.Ordinal) ? 1 : 0,
            styles.Length,
            scripts.Length,
            fields.Contains("data-eft-field=\"standard_sales\"", StringComparison.Ordinal) ? 1 : 0,
            hub,
            vat
        };
    }

    private static object Tenant()
    {
        var catalog = PhpPlanQ1Hill.EpcFreeToolsCatalog();
        var acme = PhpPlanQ1Hill.EpcFreeToolsRenderHub(catalog, "https://acme.example/");
        var beta = PhpPlanQ1Hill.EpcFreeToolsRenderHub(catalog, "https://beta.example/");
        var off = PhpPlanQ1Hill.EpcFreeToolsRenderTool("vat", catalog["vat"], "https://acme.example/");
        return new object[] { acme, beta, off };
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static string FirstDiff(string expected, string actual)
    {
        var n = Math.Min(expected.Length, actual.Length);
        for (var i = 0; i < n; i++)
        {
            if (expected[i] != actual[i])
            {
                var from = Math.Max(0, i - 40);
                return "at " + i + " exp=" + expected[from..Math.Min(expected.Length, i + 80)] + " got=" + actual[from..Math.Min(actual.Length, i + 80)];
            }
        }

        return expected.Length == actual.Length
            ? "equal-len-mismatch"
            : "len exp=" + expected.Length + " got=" + actual.Length + " tailExp=" + expected[Math.Max(0, expected.Length - 80)..] + " tailGot=" + actual[Math.Max(0, actual.Length - 80)..];
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
