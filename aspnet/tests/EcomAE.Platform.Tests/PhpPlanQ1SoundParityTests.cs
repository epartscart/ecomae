using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SoundParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Sound");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Sound_MatchPhpGolden()
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
                failures.Add(name + " exp=" + exp[..Math.Min(350, exp.Length)] + " got=" + got[..Math.Min(350, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/control/cp_guideline.php", PhpPlanQ1Sound.CpGuidelinePath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Sound.Reset();
        PhpPlanQ1Sound.UserSession = new Dictionary<string, object?>(StringComparer.Ordinal) { ["user_id"] = 21 };
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Sound.CpGuidelinePath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Sound.Capture(PhpPlanQ1Sound.EpcCpgRenderPage), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Sound.Reset();
        return name switch
        {
            "gates" => Gates(),
            "helpers" => Helpers(),
            "menu" => Menu(),
            "page" => Page(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Gates()
    {
        PhpPlanQ1Sound.Backend = () => "cp";
        PhpPlanQ1Sound.GetAdminSession = () => [];
        var cp = PhpPlanQ1Sound.Capture(() => PhpPlanQ1Sound.EpcCpgRequireSession());
        PhpPlanQ1Sound.UserSession = null;
        PhpPlanQ1Sound.Backend = () => "O'Reilly";
        var quoted = PhpPlanQ1Sound.Capture(() => PhpPlanQ1Sound.EpcCpgRequireSession());
        PhpPlanQ1Sound.UserSession = new Dictionary<string, object?>(StringComparer.Ordinal) { ["user_id"] = 21 };
        var ok = PhpPlanQ1Sound.Capture(() => PhpPlanQ1Sound.EpcCpgRequireSession());
        return new object[] { cp, quoted, ok };
    }

    private static object Helpers()
    {
        PhpPlanQ1Sound.TranslateByKey = key => key == "price_mgmt" ? "O'Reilly prices" : key;
        PhpPlanQ1Sound.TranslateById = id => id == 2070 ? "Orders desk" : id.ToString();
        var hints = PhpPlanQ1Sound.EpcCpgPageHints();
        return new object[]
        {
            hints.Count,
            hints.GetValueOrDefault("/shop/price-management") ?? "",
            PhpPlanQ1Sound.EpcCpgHintForUrl("/cp/shop/price-management?x=1", hints),
            PhpPlanQ1Sound.EpcCpgHintForUrl("/cp/unknown", hints),
            PhpPlanQ1Sound.EpcCpgItemLabel("price_mgmt"),
            PhpPlanQ1Sound.EpcCpgItemLabel(" 2070"),
            PhpPlanQ1Sound.EpcCpgItemLabel("O'Reilly"),
            PhpPlanQ1Sound.EpcCpgH("O'Reilly <x>")
        };
    }

    private static object Menu()
    {
        PhpPlanQ1Sound.TranslateByKey = key => key switch
        {
            "price_mgmt" => "O'Reilly prices",
            "SHOP" => "Shop desk",
            _ => key
        };
        PhpPlanQ1Sound.IsAnable = item =>
        {
            var id = Convert.ToInt32(item.GetValueOrDefault("id"), System.Globalization.CultureInfo.InvariantCulture);
            return id is not 12 and not 13;
        };
        PhpPlanQ1Sound.QueryGroups = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 1, ["caption"] = "SHOP", ["order"] = 10 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 2, ["caption"] = "USERS", ["order"] = 20 }
        ];
        PhpPlanQ1Sound.QueryItems = () =>
        [
            Item(11, 1, "price_mgmt", "/<backend>/shop/price-management", "fa-tags", 0),
            Item(12, 1, "hidden_row", "/<backend>/shop/hidden", "fa-ban", 0),
            Item(13, 1, "forced_row", "/<backend>/shop/orders/orders", "fa-list", 1),
            Item(14, 9, "O'Reilly extra", "/<backend>/users/customer_mgmt", "fa-users", 0)
        ];
        return PhpPlanQ1Sound.DumpTabs(PhpPlanQ1Sound.EpcCpgLoadMenuTabs("cp"));
    }

    private static object Page()
    {
        PhpPlanQ1Sound.UserSession = new Dictionary<string, object?>(StringComparer.Ordinal) { ["user_id"] = 21 };
        PhpPlanQ1Sound.Backend = () => "cp";
        PhpPlanQ1Sound.IsSuperCp = () => true;
        PhpPlanQ1Sound.Today = () => "2026-10-10";
        PhpPlanQ1Sound.TranslateByKey = key => key == "price_mgmt" ? "Acme prices" : key;
        PhpPlanQ1Sound.QueryGroups = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 1, ["caption"] = "Acme Shop", ["order"] = 1 }
        ];
        PhpPlanQ1Sound.QueryItems = () =>
        [
            Item(21, 1, "price_mgmt", "/<backend>/shop/price-management", "fa-tags", 0)
        ];
        var acme = PhpPlanQ1Sound.Capture(PhpPlanQ1Sound.EpcCpgRenderPage);
        PhpPlanQ1Sound.IsSuperCp = () => false;
        PhpPlanQ1Sound.Backend = () => "beta";
        PhpPlanQ1Sound.TranslateByKey = key => key;
        PhpPlanQ1Sound.QueryGroups = () =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 1, ["caption"] = "Beta Users", ["order"] = 1 }
        ];
        PhpPlanQ1Sound.QueryItems = () =>
        [
            Item(31, 1, "Other page", "/<backend>/users/other", "fa-user", 0)
        ];
        var beta = PhpPlanQ1Sound.Capture(PhpPlanQ1Sound.EpcCpgRenderPage);
        return new object[] { acme, beta };
    }

    private static Dictionary<string, object?> Item(int id, int group, string caption, string url, string icon, int showAnyway)
        => new(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["items_group"] = group,
            ["caption"] = caption,
            ["url"] = url,
            ["order"] = id,
            ["fontawesome_class"] = icon,
            ["show_anyway"] = showAnyway
        };

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
