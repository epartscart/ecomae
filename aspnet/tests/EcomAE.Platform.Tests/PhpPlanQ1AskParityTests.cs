using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1AskParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Ask");

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
    public void PlanQ1Ask_MatchPhpGolden()
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
                PhpPlanQ1Ask.CopilotPath,
                PhpPlanQ1Ask.AiServicePath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Ask.CopilotPath,
                PhpPlanQ1Ask.AiServicePath
            });

    [Fact]
    public void ParseIntent_DoesNotStartASession()
        => Assert.Equal("unknown", PhpPlanQ1Ask.EpcCopilotParseIntent("???")["intent"]);

    private static string Render(string name)
        => name switch
        {
            "copilot_parse" => Json(CopilotParse()),
            "copilot_exec" => Json(CopilotExec()),
            "ai_pii" => Json(AiPii()),
            "ai_query" => Json(AiQuery()),
            _ => "unknown:" + name
        };

    private static object?[] CopilotParse()
        => new object?[]
        {
            PhpPlanQ1Ask.EpcCopilotParseIntent("Total Revenue this MONTH"),
            PhpPlanQ1Ask.EpcCopilotParseIntent("how many orders today"),
            PhpPlanQ1Ask.EpcCopilotParseIntent("best sellers this week"),
            PhpPlanQ1Ask.EpcCopilotParseIntent("invoices last year"),
            PhpPlanQ1Ask.EpcCopilotParseIntent("overdue quarter"),
            PhpPlanQ1Ask.EpcCopilotParseIntent("???"),
            PhpPlanQ1Ask.EpcCopilotGenerateSql(PhpPlanQ1Ask.EpcCopilotParseIntent("total revenue this month"), "acme"),
            PhpPlanQ1Ask.EpcCopilotGenerateSql(PhpPlanQ1Ask.EpcCopilotParseIntent("???"), "acme"),
            PhpPlanQ1Ask.EpcCopilotGenerateSql(PhpPlanQ1Ask.EpcCopilotParseIntent("orders today"), "O'Brien\\x"),
            PhpPlanQ1Ask.EpcCopilotIntents().Keys.ToArray()
        };

    private static object?[] CopilotExec()
    {
        var store = new PhpPlanQ1Ask.CopilotStore();
        var a = PhpPlanQ1Ask.EpcCopilotExecute(store, "acme", "???", 3);
        a.Remove("exec_ms");
        var b = PhpPlanQ1Ask.EpcCopilotExecute(store, "acme", "total revenue this month", 3);
        b.Remove("exec_ms");
        var c = PhpPlanQ1Ask.EpcCopilotExecute(store, "beta", "how many orders today", 0);
        c.Remove("exec_ms");
        return new object?[] { a, b, c, PhpPlanQ1Ask.CopilotLogRows(store), PhpPlanQ1Ask.EpcCopilotFleetStats(store) };
    }

    private static object?[] AiPii()
    {
        var store = new PhpPlanQ1Ask.AiStore();
        return new object?[]
        {
            PhpPlanQ1Ask.EpcAiStripPii("hello"),
            PhpPlanQ1Ask.EpcAiStripPii("mail me@x.com and +971 50 1234567"),
            PhpPlanQ1Ask.EpcAiStripPii("TRN 123456789012345 card 1234 5678 9012 3456"),
            PhpPlanQ1Ask.EpcAiStripPii("IBAN AE070331234567890123456 passport A1234567"),
            PhpPlanQ1Ask.EpcAiDetectIntent("Show revenue please"),
            PhpPlanQ1Ask.EpcAiDetectIntent("stock levels"),
            PhpPlanQ1Ask.EpcAiDetectIntent("zzz"),
            PhpPlanQ1Ask.EpcAiClassifyParts("Need an oil filter"),
            PhpPlanQ1Ask.EpcAiClassifyParts("widget"),
            PhpPlanQ1Ask.EpcAiDetectAnomaly("x"),
            PhpPlanQ1Ask.EpcAiNlReport("vat return", "s1"),
            PhpPlanQ1Ask.EpcAiCopilotRespond("help me", "s1"),
            PhpPlanQ1Ask.EpcAiRouteQuery(store, "orders", "nope", "s1")
        };
    }

    private static object?[] AiQuery()
    {
        var store = new PhpPlanQ1Ask.AiStore();
        var block = PhpPlanQ1Ask.EpcAiServiceQuery(store, "a@b.com a@b.com a@b.com a@b.com a@b.com a@b.com", "copilot", "s1", 9);
        block.Remove("exec_ms");
        var ok = PhpPlanQ1Ask.EpcAiServiceQuery(store, "total revenue", "copilot", "s1", 9);
        ok.Remove("exec_ms");
        var cls = PhpPlanQ1Ask.EpcAiServiceQuery(store, "brake pad set", "classify", "s1", 1);
        cls.Remove("exec_ms");
        var bad = PhpPlanQ1Ask.EpcAiServiceQuery(store, "x", "nope", "s1", 1);
        bad.Remove("exec_ms");
        return new object?[] { block, ok, cls, bad, PhpPlanQ1Ask.EpcAiServiceStats(store), PhpPlanQ1Ask.AiLogRows(store) };
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
        => value.Length <= 400 ? value : value[..400] + "…";
}
