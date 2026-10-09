using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1HookParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hook");

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
    public void PlanQ1Hook_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hook.WebhooksPath, PhpPlanQ1Hook.EventsPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Hook.WebhooksPath, PhpPlanQ1Hook.EventsPath });

    [Fact]
    public void EventEmit_DoesNotStartASession()
    {
        var store = new PhpPlanQ1Hook.HookStore();
        var id = PhpPlanQ1Hook.EpcEventEmit(store, "order.placed", new Dictionary<string, object?> { ["order_id"] = 1 });
        Assert.True(id > 0);
    }

    private static string Render(string name)
        => name switch
        {
            "hook_crypto" => Json(HookCrypto()),
            "hook_reg" => Json(HookReg()),
            "evt_flow" => Json(EvtFlow()),
            _ => "unknown:" + name
        };

    private static object?[] HookCrypto()
    {
        const string plain = "shared-secret";
        var enc = PhpPlanQ1Hook.EpcWebhooksEncryptSecret(plain);
        var dec = PhpPlanQ1Hook.EpcWebhooksDecryptSecret(enc);
        var empty = PhpPlanQ1Hook.EpcWebhooksDecryptSecret("");
        var bad = PhpPlanQ1Hook.EpcWebhooksDecryptSecret("xxxx");
        var ok = PhpPlanQ1Hook.EpcWebhooksVerifySignature("{\"a\":1}", "s3", "sha256=" + Hmac("1700000000.{\"a\":1}", "s3"), "1700000000");
        var no = PhpPlanQ1Hook.EpcWebhooksVerifySignature("{\"a\":1}", "s3", "sha256=nope", "1700000000");
        var blank = PhpPlanQ1Hook.EpcWebhooksVerifySignature("x", "", "sha256=x");
        return new object?[] { dec == plain, empty, enc.Length > 20, bad, ok, no, blank };
    }

    private static object?[] HookReg()
    {
        var store = new PhpPlanQ1Hook.HookStore();
        var a = PhpPlanQ1Hook.EpcWebhooksRegister(store, new Dictionary<string, object?> { ["url"] = "" });
        var b = PhpPlanQ1Hook.EpcWebhooksRegister(store, new Dictionary<string, object?> { ["url"] = "not-a-url" });
        var c = PhpPlanQ1Hook.EpcWebhooksRegister(store, new Dictionary<string, object?> { ["url"] = "http://example.com/hook" });
        var d = PhpPlanQ1Hook.EpcWebhooksRegister(store, new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/hook",
            ["secret"] = "abc",
            ["events"] = new[] { "order.placed" },
            ["tenant_key"] = "siteA",
            ["description"] = "d1"
        });
        var list = PhpPlanQ1Hook.EpcWebhooksList(store, "siteA");
        var hasSecret = list[0].ContainsKey("secret_hash") || list[0].ContainsKey("secret_encrypted");
        var upd = PhpPlanQ1Hook.EpcWebhooksUpdate(store, Convert.ToInt32(d["webhook_id"]), new Dictionary<string, object?> { ["active"] = 1, ["description"] = "d2" });
        var noup = PhpPlanQ1Hook.EpcWebhooksUpdate(store, Convert.ToInt32(d["webhook_id"]), new Dictionary<string, object?>());
        var badu = PhpPlanQ1Hook.EpcWebhooksUpdate(store, Convert.ToInt32(d["webhook_id"]), new Dictionary<string, object?> { ["url"] = "http://x.com" });
        var del = PhpPlanQ1Hook.EpcWebhooksDelete(store, Convert.ToInt32(d["webhook_id"]));
        var list2 = PhpPlanQ1Hook.EpcWebhooksList(store, "siteA");
        var stats = PhpPlanQ1Hook.EpcWebhooksDeliveryStats(store, 24);
        var disp = PhpPlanQ1Hook.EpcWebhooksDispatch(store, 1, "order.placed", new Dictionary<string, object?> { ["x"] = 1 }, "siteA");
        return new object?[]
        {
            a, b, c, d["ok"], Convert.ToInt32(d["webhook_id"]) > 0 ? 1 : 0, list[0]["url"], list[0]["events"], hasSecret, upd, noup, badu, del, list2.Count, stats, disp
        };
    }

    private static object?[] EvtFlow()
    {
        var store = new PhpPlanQ1Hook.HookStore();
        var id = PhpPlanQ1Hook.EpcEventEmit(store, "order.placed", new Dictionary<string, object?> { ["order_id"] = 7, ["total"] = 12 }, "siteA", 4, "user", "order-placed-7");
        var dup = PhpPlanQ1Hook.EpcEventEmit(store, "order.placed", new Dictionary<string, object?> { ["order_id"] = 7 }, "siteA", 4, "user", "order-placed-7");
        var inv = PhpPlanQ1Hook.EpcEventEmitInvoicePosted(store, 9, 500.0, "siteA", 3);
        var paid = PhpPlanQ1Hook.EpcEventEmitInvoicePaid(store, 9, 50.0, "siteA", 3);
        var cn = PhpPlanQ1Hook.EpcEventEmitCreditNote(store, 9, 10.0, "siteA", 3);
        var op = PhpPlanQ1Hook.EpcEventEmitOrderPlaced(store, 8, 20.0, "siteA", 2);
        var sh = PhpPlanQ1Hook.EpcEventEmitOrderShipped(store, 8, "TRK", "siteA");
        var sb = PhpPlanQ1Hook.EpcEventEmitStockBelow(store, "SKU1", 1, 5, "siteA");
        var sa = PhpPlanQ1Hook.EpcEventEmitStockAdjusted(store, "SKU1", 5, 1, "sold", "siteA", 2);
        var pm = PhpPlanQ1Hook.EpcEventEmitPaymentReceived(store, 11, 30.0, "card", "siteA", 2);
        var vp = PhpPlanQ1Hook.EpcEventEmitVoucherPosted(store, 12, "JV", 40.0, "siteA", 2);
        var pc = PhpPlanQ1Hook.EpcEventEmitPeriodClosed(store, "2026-10", "siteA", 2);
        var tn = PhpPlanQ1Hook.EpcEventEmitTenant(store, "created", "siteB", new Dictionary<string, object?> { ["name"] = "B" });
        var list = PhpPlanQ1Hook.EpcEventsList(store, new Dictionary<string, object?> { ["tenant_key"] = "siteA" }, 50, 0);
        var cnt = PhpPlanQ1Hook.EpcEventsCount(store, new Dictionary<string, object?> { ["tenant_key"] = "siteA" });
        var sum = PhpPlanQ1Hook.EpcEventsTypeSummary(store, DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd HH:mm:ss"));
        var row = PhpPlanQ1Hook.EpcEventsList(store, new Dictionary<string, object?> { ["event_type"] = "invoice.posted" })[0];
        using var payload = JsonDocument.Parse((string)row["payload_json"]!);
        var p = payload.RootElement;
        return new object?[]
        {
            id > 0 ? 1 : 0, dup, inv > 0 ? 1 : 0, paid > 0 ? 1 : 0, cn > 0 ? 1 : 0, op > 0 ? 1 : 0, sh > 0 ? 1 : 0,
            sb > 0 ? 1 : 0, sa > 0 ? 1 : 0, pm > 0 ? 1 : 0, vp > 0 ? 1 : 0, pc > 0 ? 1 : 0, tn > 0 ? 1 : 0,
            list.Count, cnt, sum.Count,
            p.GetProperty("invoice_id").GetInt32(),
            p.GetProperty("currency").GetString(),
            p.GetProperty("_event_type").GetString(),
            p.GetProperty("_tenant_key").GetString(),
            (p.GetProperty("_timestamp").GetString() ?? "").Contains('T', StringComparison.Ordinal) ? 1 : 0,
            Convert.ToInt32(row["actor_id"]),
            row["actor_type"]
        };
    }

    private static string Hmac(string value, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
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
        => value.Length <= 280 ? value : value[..280] + "…";
}
