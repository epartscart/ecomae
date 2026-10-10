using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1MereParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Mere");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Mere_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(280, exp.Length)] + " got=" + got[..Math.Min(280, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("modules/shop/cart/cart.php", PhpPlanQ1Mere.CartModulePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Mere.Reset();
        SeedEn();
        PhpPlanQ1Mere.GetUserId = () => 0;
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Mere.CartModulePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Mere.EpcCartModule(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Mere.Reset();
        return name switch
        {
            "guest" => Guest(),
            "signed" => Signed(),
            "currency" => Currency(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Guest()
    {
        SeedEn();
        PhpPlanQ1Mere.GetUserId = () => 0;
        PhpPlanQ1Mere.ProductsInCartCookie = null;
        var none = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.ProductsInCartCookie = "[1,2]";
        PhpPlanQ1Mere.QueryGuestCart = ids => ids.SequenceEqual(new[] { 1, 2 })
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 2, ["sum"] = 25.5 }
            : null;
        var cookie = PhpPlanQ1Mere.EpcCartModule();
        return new object[] { none, cookie };
    }

    private static object Signed()
    {
        SeedEn();
        PhpPlanQ1Mere.GetUserId = () => 7;
        PhpPlanQ1Mere.ProductsInCartCookie = "[1,2]";
        PhpPlanQ1Mere.QuerySignedCart = _ => null;
        var empty = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.QuerySignedCart = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 2, ["sum"] = 25.5 };
        var net = PhpPlanQ1Mere.EpcCartModule();
        return new object[] { empty, net };
    }

    private static object Currency()
    {
        SeedEn("Позиции", "/ar");
        PhpPlanQ1Mere.GetUserId = () => 7;
        PhpPlanQ1Mere.QuerySignedCart = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 1, ["sum"] = 12.5 };
        PhpPlanQ1Mere.CurrencyShowMode = () => "sign_before";
        var before = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.CurrencyShowMode = () => "sign_after";
        var after = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.CurrencyShowMode = () => "no";
        var none = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.CurrencyShowMode = () => "short_name_after";
        var shortAfter = PhpPlanQ1Mere.EpcCartModule();
        return new object[] { before, after, none, shortAfter };
    }

    private static object Tenant()
    {
        SeedEn();
        PhpPlanQ1Mere.GetUserId = () => 7;
        PhpPlanQ1Mere.QuerySignedCart = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 1, ["sum"] = 80 };
        var acme = PhpPlanQ1Mere.EpcCartModule();
        PhpPlanQ1Mere.LangHref = () => "/ar";
        PhpPlanQ1Mere.QueryCurrency = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["sign"] = "$", ["caption_short"] = "USD" };
        PhpPlanQ1Mere.CurrencyShowMode = () => "sign_before";
        PhpPlanQ1Mere.GetUserId = () => 8;
        PhpPlanQ1Mere.QuerySignedCart = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = 1, ["sum"] = 5 };
        var beta = PhpPlanQ1Mere.EpcCartModule();
        return new object[] { acme, beta };
    }

    private static void SeedEn(string items = "Items", string lang = "/en")
    {
        PhpPlanQ1Mere.TranslateById = raw => raw == "4495" ? items : raw;
        PhpPlanQ1Mere.LangHref = () => lang;
        PhpPlanQ1Mere.QueryCurrency = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["sign"] = "د.إ", ["caption_short"] = "AED" };
        PhpPlanQ1Mere.CurrencyShowMode = () => "sign_after";
        PhpPlanQ1Mere.GetUserSession = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
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
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
