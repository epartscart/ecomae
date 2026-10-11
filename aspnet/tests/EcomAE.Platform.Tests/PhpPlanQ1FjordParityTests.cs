using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1FjordParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Fjord");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Fjord_MatchPhpGolden()
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
        => Assert.Equal("cp/content/control/portal/epc_marketing_broadcast_panel.php", PhpPlanQ1Fjord.MarketingBroadcastPanelPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Fjord.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Fjord.MarketingBroadcastPanelPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Fjord.Reset();
        return name switch
        {
            "gates" => Gates(),
            "hub" => Hub(),
            "compose" => Compose(),
            "history" => History(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Gates()
    {
        var noAdmin = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.IsAdmin = () => true;
        var noDb = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.TenantPdo = () => new object();
        PhpPlanQ1Fjord.Query["tab"] = "Email!";
        var email = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        return new object[] { noAdmin, noDb, email };
    }

    private static object Hub()
    {
        PhpPlanQ1Fjord.IsAdmin = () => true;
        PhpPlanQ1Fjord.TenantPdo = () => new object();
        PhpPlanQ1Fjord.UserId = () => 21;
        PhpPlanQ1Fjord.Query["tab"] = "guide";
        var acme = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.ShopContext = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["shop_name"] = "Beta Demo" };
        var beta = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.RequestMethod = "POST";
        PhpPlanQ1Fjord.Post["epc_mb_action"] = "send_email";
        PhpPlanQ1Fjord.VerifyCsrf = () => false;
        PhpPlanQ1Fjord.Query.Clear();
        PhpPlanQ1Fjord.Query["tab"] = "email";
        var csrf = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.VerifyCsrf = () => true;
        var ok = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        PhpPlanQ1Fjord.Post["epc_mb_action"] = "send_whatsapp";
        var wa = PhpPlanQ1Fjord.Capture(PhpPlanQ1Fjord.EpcMbRenderHub);
        return new object[] { acme, beta, csrf, ok, wa };
    }

    private static object Compose()
    {
        var groups = new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["id"] = 7, ["name"] = "O'Reilly trade" } };
        var templates = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["promo_sale"] = new(StringComparer.Ordinal) { ["label"] = "Sale" },
            ["blank"] = new(StringComparer.Ordinal) { ["label"] = "Blank" }
        };
        var wa = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["promo_bilingual"] = new(StringComparer.Ordinal) { ["label"] = "Promo EN+AR" }
        };
        var emailOk = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderEmailTab(templates, groups, "tok-1",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = 1, ["issues"] = new List<string>() }, "/cp/mail"));
        var emailBad = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderEmailTab(templates, groups, "tok-1",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = 0, ["issues"] = new List<string> { "host empty", "O'Reilly" } }, "/cp/mail"));
        var waOff = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderWhatsappTab(wa, groups, "tok-1", false));
        var waOn = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderWhatsappTab(wa, groups, "tok-1", true));
        return new object[] { emailOk, emailBad, waOff, waOn };
    }

    private static object History()
    {
        var empty = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderHistoryTab([]));
        var rows = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderHistoryTab(
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 9, ["channel"] = "whatsapp", ["audience_mode"] = "all", ["created_at"] = 1700000000,
                ["total_targets"] = 3, ["status"] = "done", ["sent_ok"] = 2, ["sent_fail"] = 1
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 8, ["channel"] = "email", ["audience_mode"] = "group", ["created_at"] = 1700000000,
                ["total_targets"] = 4, ["status"] = "sent", ["sent_ok"] = 4, ["sent_fail"] = 0
            }
        ]));
        var guideOff = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderGuideTab(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["shop_name"] = "O'Reilly Parts" },
            "/cp/mail", "/cp/mb?tab=guide", false, "/cp/int"));
        var guideOn = PhpPlanQ1Fjord.Capture(() => PhpPlanQ1Fjord.EpcMbRenderGuideTab(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["shop_name"] = "Beta Demo" },
            "/cp/mail", "/cp/mb?tab=guide", true, "/cp/int"));
        return new object[] { empty, rows, guideOff, guideOn };
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
