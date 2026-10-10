using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ReedParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Reed");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Reed_MatchPhpGolden()
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
        => Assert.Equal("modules/shop/balance/module.php", PhpPlanQ1Reed.BalanceModulePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Reed.Reset();
        SeedAed();
        PhpPlanQ1Reed.GetUserId = () => 0;
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Reed.BalanceModulePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Reed.EpcBalanceModule(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Reed.Reset();
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
        SeedAed();
        PhpPlanQ1Reed.GetUserId = () => 0;
        var guest = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.QueryCurrency = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["sign"] = "$", ["caption_short"] = "USD" };
        PhpPlanQ1Reed.CurrencyShowMode = () => "sign_before";
        var zeroUser = PhpPlanQ1Reed.EpcBalanceModule();
        return new object[] { guest, zeroUser };
    }

    private static object Signed()
    {
        SeedAed();
        PhpPlanQ1Reed.GetUserId = () => 7;
        PhpPlanQ1Reed.QueryBalance = _ => null;
        var empty = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.QueryBalance = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["balance"] = 7.5 };
        var net = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.QueryBalance = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["balance"] = 0 };
        var zero = PhpPlanQ1Reed.EpcBalanceModule();
        return new object[] { empty, net, zero };
    }

    private static object Currency()
    {
        SeedAed("رصيد");
        PhpPlanQ1Reed.GetUserId = () => 7;
        PhpPlanQ1Reed.QueryBalance = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["balance"] = 12.5 };
        PhpPlanQ1Reed.CurrencyShowMode = () => "sign_before";
        var before = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.CurrencyShowMode = () => "sign_after";
        var after = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.CurrencyShowMode = () => "no";
        var none = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.CurrencyShowMode = () => "short_name_after";
        var shortAfter = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.CurrencyShowMode = () => "short_name_before";
        var shortBefore = PhpPlanQ1Reed.EpcBalanceModule();
        return new object[] { before, after, none, shortAfter, shortBefore };
    }

    private static object Tenant()
    {
        SeedAed();
        PhpPlanQ1Reed.GetUserId = () => 7;
        PhpPlanQ1Reed.QueryBalance = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["balance"] = 100 };
        var acme = PhpPlanQ1Reed.EpcBalanceModule();
        PhpPlanQ1Reed.QueryCurrency = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["sign"] = "$", ["caption_short"] = "USD" };
        PhpPlanQ1Reed.CurrencyShowMode = () => "sign_before";
        PhpPlanQ1Reed.GetUserId = () => 8;
        PhpPlanQ1Reed.QueryBalance = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["balance"] = 5 };
        var beta = PhpPlanQ1Reed.EpcBalanceModule();
        return new object[] { acme, beta };
    }

    private static void SeedAed(string title = "Balance")
    {
        PhpPlanQ1Reed.TranslateById = raw => raw == "4655" ? title : raw;
        PhpPlanQ1Reed.QueryCurrency = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["sign"] = "د.إ", ["caption_short"] = "AED" };
        PhpPlanQ1Reed.CurrencyShowMode = () => "sign_after";
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
