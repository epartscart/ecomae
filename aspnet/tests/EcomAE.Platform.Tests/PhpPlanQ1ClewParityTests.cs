using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ClewParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Clew");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PlanQ1Clew_MatchPhpGolden()
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
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/general_pages/epc_seo_shipping_export.php", PhpPlanQ1Clew.SeoShippingExportPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Clew.Reset();
        var html = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        Assert.DoesNotContain("PHPSESSID", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Clew.SeoShippingExportPath, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Clew.Reset();
        return name switch
        {
            "en" => En(),
            "ar" => Ar(),
            "ru" => Ru(),
            "edge" => Edge(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object En()
    {
        var html = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        return new object[] { Clip(html), html.Contains("Shipping &amp; export", StringComparison.Ordinal) ? 1 : 0, html.Contains("/en/parts", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Ar()
    {
        PhpPlanQ1Clew.CurrentLang = () => "ar";
        PhpPlanQ1Clew.LangHref = () => "/ar";
        PhpPlanQ1Clew.ShippingPhrase = _ => "الشحن من O'man";
        var html = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        return new object[] { Clip(html), html.Contains("الشحن والتصدير", StringComparison.Ordinal) ? 1 : 0, html.Contains("O&#039;man", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Ru()
    {
        PhpPlanQ1Clew.CurrentLang = () => "ru";
        PhpPlanQ1Clew.LangHref = () => "/ru";
        PhpPlanQ1Clew.TenantCountry = () => "OM";
        var html = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        return new object[] { Clip(html), html.Contains("Доставка и экспорт", StringComparison.Ordinal) ? 1 : 0, html.Contains("Tenant country profile: OM", StringComparison.Ordinal) ? 1 : 0 };
    }

    private static object Edge()
    {
        PhpPlanQ1Clew.CurrentLang = () => "xx";
        PhpPlanQ1Clew.LangHref = () => "/xx";
        PhpPlanQ1Clew.HasPdo = false;
        PhpPlanQ1Clew.ShippingPhrase = _ => "O'Brien & <GCC>";
        PhpPlanQ1Clew.DomainPath = "https://shop.example/a/";
        PhpPlanQ1Clew.PartsSlug = "parts";
        var html = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        PhpPlanQ1Clew.Reset();
        PhpPlanQ1Clew.HasPdo = false;
        var noPdo = PhpPlanQ1Clew.EpcSeoShippingExportRenderHtml();
        return new object[]
        {
            Clip(html),
            html.Contains("Shipping &amp; export", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("O&#039;Brien &amp; &lt;GCC&gt;", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("/xx/parts", StringComparison.Ordinal) ? 1 : 0,
            html.Contains("/xx/available-brands", StringComparison.Ordinal) ? 1 : 0,
            noPdo.Contains("Tenant country profile: AE", StringComparison.Ordinal) ? 1 : 0
        };
    }

    private static object[] Clip(string html)
        => [PhpStrlen(html), PhpSubstr(html, 0, 80), PhpSubstr(html, -80, 80)];

    private static int PhpStrlen(string value) => Encoding.UTF8.GetByteCount(value);

    private static string PhpSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (start < 0)
        {
            start = bytes.Length + start;
        }

        if (start < 0)
        {
            start = 0;
        }

        if (start >= bytes.Length)
        {
            return "";
        }

        var take = Math.Min(length, bytes.Length - start);
        return Encoding.UTF8.GetString(bytes, start, take);
    }

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

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
            _ => true
        };
    }
}
