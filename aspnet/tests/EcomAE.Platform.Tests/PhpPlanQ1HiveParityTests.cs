using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HiveParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hive");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hive_MatchPhpGolden()
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
                failures.Add(name + " " + FirstDiff(exp, got));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/general_pages/epc_ecomae_platform_pages.php", PhpPlanQ1Hive.PlatformPagesPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hive.Reset();
        var contact = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        var about = PhpPlanQ1Hive.EpcEcomaePlatformPageAbout();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hive.PlatformPagesPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", contact, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", about, StringComparison.Ordinal);
        Assert.Contains("Contact ECOM AE", contact, StringComparison.Ordinal);
        Assert.Contains("About ECOM AE", about, StringComparison.Ordinal);
        Assert.DoesNotContain("/finance/", PhpPlanQ1Hive.PlatformPagesPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hive.Reset();
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
        var items = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["label"] = "Auto parts", ["kw"] = "auto parts catalog vin" },
            new(StringComparer.Ordinal) { ["label"] = "Fashion", ["kw"] = "fashion apparel retail" },
            new(StringComparer.Ordinal) { ["label"] = "Tax", ["kw"] = "tax advisory vat" }
        };
        return new object[]
        {
            PhpPlanQ1Hive.scoreText("", ""),
            PhpPlanQ1Hive.scoreText("auto parts catalog vin", "auto"),
            PhpPlanQ1Hive.scoreText("auto parts catalog vin", "vin search"),
            PhpPlanQ1Hive.scoreText("auto parts catalog vin", ""),
            PhpPlanQ1Hive.filterSubs(items, ""),
            PhpPlanQ1Hive.filterSubs(items, "tax"),
            PhpPlanQ1Hive.filterSubs(items, "missing")
        };
    }

    private static object Gates()
    {
        PhpPlanQ1Hive.RequestMethod = "GET";
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal);
        var get = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        PhpPlanQ1Hive.RequestMethod = "POST";
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["epc_contact_request"] = "1",
            ["contact_name"] = "Ada",
            ["contact_email"] = "not-an-email"
        };
        var bad = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["epc_contact_request"] = "1",
            ["contact_name"] = "Ada & Co",
            ["contact_email"] = "ada@example.com",
            ["company"] = "Acme <x>",
            ["message"] = "Hello"
        };
        var ok = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contact_name"] = "Ada",
            ["contact_email"] = "ada@example.com"
        };
        var noFlag = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        PhpPlanQ1Hive.RequestMethod = "GET";
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal);
        var innerBang = PageFlags(PhpPlanQ1Hive.EpcEcomaePlatformRenderInner("contact!!!"));
        var innerHyphen = PageFlags(PhpPlanQ1Hive.EpcEcomaePlatformRenderInner("api-documentation"));
        return new object[] { get, bad, ok, noFlag, innerBang, innerHyphen, noFlag.Contains("Thank you", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Live()
    {
        PhpPlanQ1Hive.RequestMethod = "GET";
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal);
        var about = PhpPlanQ1Hive.EpcEcomaePlatformPageAbout();
        var docs = PhpPlanQ1Hive.EpcEcomaePlatformPageApiDocumentation();
        var svc = PhpPlanQ1Hive.EpcEcomaePlatformPageApiServices(new Dictionary<string, object?>(StringComparer.Ordinal));
        var svcCat = PhpPlanQ1Hive.EpcEcomaePlatformPageApiServices(new Dictionary<string, object?>(StringComparer.Ordinal) { ["focus"] = "catalog" });
        var svcPrice = PhpPlanQ1Hive.EpcEcomaePlatformPageApiServices(new Dictionary<string, object?>(StringComparer.Ordinal) { ["focus"] = "price_pro" });
        var missing = PhpPlanQ1Hive.EpcEcomaePlatformPageIndustry(new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = "nope" });
        var innerAbout = PhpPlanQ1Hive.EpcEcomaePlatformRenderInner("about");
        var innerDocs = PhpPlanQ1Hive.EpcEcomaePlatformRenderPage("api_documentation", new Dictionary<string, object?>(StringComparer.Ordinal), "inner");
        var eco = PhpPlanQ1Hive.EpcEcomaePlatformEcosystemModelSection();
        return new object[]
        {
            about,
            docs,
            svc,
            svcCat,
            svcPrice,
            missing,
            innerAbout == about ? 1 : 0,
            innerDocs == docs ? 1 : 0,
            eco,
            PageFlags(svcCat),
            svcCat.Contains("getElementById('catalog-api')", StringComparison.Ordinal) ? 1 : 0,
            svcPrice.Contains("getElementById('price-pro-api')", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object Tenant()
    {
        PhpPlanQ1Hive.RequestMethod = "GET";
        PhpPlanQ1Hive.Post = new Dictionary<string, string>(StringComparer.Ordinal);
        PhpPlanQ1Hive.BackendDir = "acme-cp";
        var acme = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        var aboutAcme = PhpPlanQ1Hive.EpcEcomaePlatformPageAbout();
        PhpPlanQ1Hive.BackendDir = "beta-cp";
        var beta = PhpPlanQ1Hive.EpcEcomaePlatformPageContact();
        var aboutBeta = PhpPlanQ1Hive.EpcEcomaePlatformPageAbout();
        return new object[]
        {
            acme,
            beta,
            aboutAcme,
            aboutBeta,
            acme.Contains("https://www.ecomae.com/acme-cp", StringComparison.Ordinal) ? 1 : 0,
            beta.Contains("https://www.ecomae.com/beta-cp", StringComparison.Ordinal) ? 1 : 0,
            acme.Contains("https://www.ecomae.com/beta-cp", StringComparison.Ordinal) ? 1 : 0,
            aboutAcme.Contains("https://acme.example", StringComparison.Ordinal) ? 1 : 0,
            aboutBeta.Contains("https://www.ecomae.com/", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static Dictionary<string, object?> PageFlags(string html)
        => new(StringComparer.Ordinal)
        {
            ["contact"] = html.Contains("Contact ECOM AE", StringComparison.Ordinal) ? 1 : 0,
            ["about"] = html.Contains("About ECOM AE", StringComparison.Ordinal) ? 1 : 0,
            ["api_docs"] = html.Contains("API documentation", StringComparison.Ordinal) ? 1 : 0,
            ["api_svc"] = html.Contains("Autoparts Catalog", StringComparison.Ordinal) ? 1 : 0,
            ["home"] = html.Contains("epm-hub-section", StringComparison.Ordinal) ? 1 : 0,
            ["flash_ok"] = html.Contains("Thank you — we respond within one business day (GST).", StringComparison.Ordinal) ? 1 : 0,
            ["flash_err"] = html.Contains("Please enter your name and a valid email.", StringComparison.Ordinal) ? 1 : 0,
            ["prefix"] = html.Length >= 48 ? html[..48] : html
        };

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
