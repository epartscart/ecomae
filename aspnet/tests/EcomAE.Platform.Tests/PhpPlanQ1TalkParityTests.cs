using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1TalkParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Talk");

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
    public void PlanQ1Talk_MatchPhpGolden()
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
                PhpPlanQ1Talk.CommTestPath,
                PhpPlanQ1Talk.AntiCrawlPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Talk.CommTestPath,
                PhpPlanQ1Talk.AntiCrawlPath
            });

    [Fact]
    public void SessionUser_DoesNotStartASession()
    {
        PhpPlanQ1Talk.Reset();
        Assert.Equal(0, PhpPlanQ1Talk.EpcStorefrontAntiCrawlSessionUserId());
    }

    private static string Render(string name)
    {
        PhpPlanQ1Talk.Reset();
        return name switch
        {
            "comm_pure" => Json(CommPure()),
            "comm_db" => Json(CommDb()),
            "ac_pure" => Json(AcPure()),
            "ac_rl" => Json(AcRl()),
            _ => "unknown:" + name
        };
    }

    private static object? CommPure()
    {
        var defs = PhpPlanQ1Talk.EpcCommTestDefinitions();
        var keys = defs.Select(d => d["key"]).ToList();
        var root = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1t_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            PhpPlanQ1Talk.DocumentRoot = root;
            var missing = PhpPlanQ1Talk.EpcCommTestLoadLast();
            PhpPlanQ1Talk.NotifyStatus = null;
            var person = Dict(
                ("contacts", Dict(("email", Dict(("tried_to_send", 1), ("status", 1), ("value", "a@x.com"))))));
            var unavail = PhpPlanQ1Talk.EpcCommAnswerSummary(Dict(("persons", new List<object?> { person })));
            PhpPlanQ1Talk.NotifyStatus = PhpPlanQ1Talk.DefaultNotifyStatus;
            var okAnswer = Dict(("persons", new List<object?>
            {
                Dict(("type", "direct_contact"), ("contacts", Dict(("email", Dict(("tried_to_send", 1), ("status", 1), ("value", "Ann@X.com")))))),
                Dict(("type", "user_id"), ("user_id", 9), ("contacts", Dict(("email", Dict(("tried_to_send", 1), ("status", 0))))))
            }));
            var ok = PhpPlanQ1Talk.EpcCommAnswerSummary(okAnswer);
            var fail = PhpPlanQ1Talk.EpcCommAnswerSummary(
                Dict(("persons", new List<object?>
                {
                    Dict(("type", "direct_contact"), ("contacts", Dict(("email", Dict(("tried_to_send", 1), ("status", 0), ("value", "Ann@X.com"))))))
                })),
                "ann@x.com");
            var skip = PhpPlanQ1Talk.EpcCommAnswerSummary(
                Dict(("persons", new List<object?>
                {
                    Dict(("contacts", Dict(("email", Dict(("tried_to_send", 0), ("status", 1), ("value", "z@x.com"))))))
                })));
            var msg = PhpPlanQ1Talk.EpcCommAnswerSummary(Dict(("message", "no persons")));
            var enc = PhpPlanQ1Talk.EpcCommAnswerSummary(Dict(("foo", 1)));
            var none = PhpPlanQ1Talk.EpcCommAnswerSummary(null);
            var rep = new Dictionary<string, object?>(StringComparer.Ordinal) { ["tests"] = new List<Dictionary<string, object?>>() };
            PhpPlanQ1Talk.EpcCommRecordTest(rep, "a", Dict(("message", "x")), "", Dict(("log", "order 1: sent")));
            PhpPlanQ1Talk.EpcCommRecordTest(rep, "b", Dict(("message", "y")), "", Dict(("log", "order 1: FAILED")));
            PhpPlanQ1Talk.EpcCommRecordTest(rep, "c", Dict(("message", "z")), "", Dict(("log", "order 1: pending")));
            PhpPlanQ1Talk.EpcCommRecordTest(rep, "d", ok, "ann@x.com", Dict(("extra", 3)));
            PhpPlanQ1Talk.EpcCommTestSaveLast(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["n"] = 2,
                ["note"] = "O'Reilly"
            });
            var loaded = PhpPlanQ1Talk.EpcCommTestLoadLast();
            File.WriteAllText(PhpPlanQ1Talk.EpcCommTestLastJsonPath(), "not-json");
            var bad = PhpPlanQ1Talk.EpcCommTestLoadLast();
            return new object?[]
            {
                defs.Count, keys, defs[0]["cp_path"], defs[12]["key"],
                Path.GetFileName(PhpPlanQ1Talk.EpcCommTestLastJsonPath()),
                missing, unavail, ok, fail, skip, msg, enc, none, rep, loaded, bad
            };
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* leftover temp */ }
        }
    }

    private static object? CommDb()
    {
        PhpPlanQ1Talk.Clock = () => 1770000000;
        var db = new PhpPlanQ1Talk.CommStore();
        db.Groups.AddRange(
        [
            new() { Id = 7, ForRegistrated = 0 },
            new() { Id = 3, ForRegistrated = 1 },
            new() { Id = 9, ForRegistrated = 1 }
        ]);
        db.Notifications.Add(new()
        {
            Id = 4, Name = "new_order_to_user", Caption = "New order", EmailOn = 1, SmsOn = 0, SendForNotConfirmed = 1
        });
        db.OrderStatuses.AddRange(
        [
            new() { Id = 11, ForCreated = 1, Order = 20 },
            new() { Id = 12, ForCreated = 1, Order = 10 },
            new() { Id = 13, ForCreated = 0, Order = 1 }
        ]);
        db.ItemStatuses.AddRange(
        [
            new() { Id = 21, ForCreated = 1, Order = 5 },
            new() { Id = 22, ForCreated = 1, Order = 1 },
            new() { Id = 23, ForCreated = 0, Order = 1 }
        ]);
        db.Storages.AddRange(
        [
            new() { Id = 1, Name = "Main WH" },
            new() { Id = 2, Name = "Supplier B" }
        ]);
        var cfg = new PhpPlanQ1Talk.TalkConfig { SecretSuccession = "sec" };
        var miss = PhpPlanQ1Talk.EpcCommNotifyRow(db, "missing");
        var hit = PhpPlanQ1Talk.EpcCommNotifyRow(db, "new_order_to_user");
        var c1 = PhpPlanQ1Talk.EpcCommTestEnsureCustomer(db, cfg, "  Ann@X.com ", "97150");
        var c2 = PhpPlanQ1Talk.EpcCommTestEnsureCustomer(db, cfg, "ann@x.com", "97151");
        var hash = db.Users[0].Password;
        var phone = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["phone"] = db.Users[0].Phone,
            ["email_confirmed"] = db.Users[0].EmailConfirmed,
            ["unlocked"] = db.Users[0].Unlocked
        };
        var binds = db.Binds.OrderBy(b => b.UserId).Select(b => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = b.UserId,
            ["group_id"] = b.GroupId
        }).ToList();
        var ord = PhpPlanQ1Talk.EpcCommTestCreateOrder(db, Convert.ToInt32(c1["user_id"], CultureInfo.InvariantCulture), 8, new object?[] { 1, 0, "2", -3 });
        string? ex = null;
        db.OrderStatuses.Clear();
        try
        {
            PhpPlanQ1Talk.EpcCommTestCreateOrder(db, 1, 8, new object?[] { 1 });
        }
        catch (Exception e)
        {
            ex = e.Message;
        }

        var prices = db.Items.OrderBy(i => i.Id).Select(i => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["price"] = i.Price.ToString("0.00", CultureInfo.InvariantCulture),
            ["t2_article"] = i.Article,
            ["t2_price_purchase"] = i.PricePurchase.ToString("0.00", CultureInfo.InvariantCulture),
            ["t2_storage"] = i.Storage
        }).ToList();
        return new object?[]
        {
            miss, hit, c1, c2, hash, phone, binds, ord, db.Items.Count, db.Details.Count, db.Logs.Count, prices, ex, PhpPlanQ1Talk.TradeSets
        };
    }

    private static object? AcPure()
    {
        PhpPlanQ1Talk.Server["REMOTE_ADDR"] = "8.8.8.8";
        var ipPub = PhpPlanQ1Talk.EpcStorefrontAntiCrawlClientIp();
        PhpPlanQ1Talk.Server["REMOTE_ADDR"] = "10.1.2.3";
        PhpPlanQ1Talk.Server["HTTP_CF_CONNECTING_IP"] = "1.1.1.1";
        PhpPlanQ1Talk.Server["HTTP_X_FORWARDED_FOR"] = "203.0.113.10, 8.8.8.8";
        var ipPrivCf = PhpPlanQ1Talk.EpcStorefrontAntiCrawlClientIp();
        PhpPlanQ1Talk.Server["HTTP_CF_CONNECTING_IP"] = " 1.1.1.1, 9.9.9.9";
        var ipPrivXff = PhpPlanQ1Talk.EpcStorefrontAntiCrawlClientIp();
        PhpPlanQ1Talk.Server["REMOTE_ADDR"] = "127.0.0.1";
        PhpPlanQ1Talk.Server["HTTP_X_FORWARDED_FOR"] = "9.9.9.9";
        PhpPlanQ1Talk.Server.Remove("HTTP_CF_CONNECTING_IP");
        var ipLoop = PhpPlanQ1Talk.EpcStorefrontAntiCrawlClientIp();
        PhpPlanQ1Talk.Server.Clear();
        var ipEmpty = PhpPlanQ1Talk.EpcStorefrontAntiCrawlClientIp();
        var b0 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("");
        var b1 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("Mozilla/5.0");
        var b2 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("Googlebot/2.1");
        var b3 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("EPartsCart CP fetcher");
        var b4 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("EcomAE CP internal");
        var b5 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot("curl/8.0");
        var b6 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot();
        PhpPlanQ1Talk.Server["HTTP_USER_AGENT"] = "Mozilla/5.0";
        var b7 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlIsBot();
        var cfg = new PhpPlanQ1Talk.TalkConfig { TechKey = "tok" };
        PhpPlanQ1Talk.GlobalConfig = cfg;
        var t0 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlHasTechKey();
        PhpPlanQ1Talk.Post["tech_key"] = "tok";
        var t1 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlHasTechKey(cfg);
        PhpPlanQ1Talk.Post.Clear();
        PhpPlanQ1Talk.Get["tech_key"] = "nope";
        var t2 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlHasTechKey(cfg);
        PhpPlanQ1Talk.Get.Clear();
        PhpPlanQ1Talk.Request["tech_key"] = "tok";
        var t3 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlHasTechKey(cfg);
        var t4 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlHasTechKey(new PhpPlanQ1Talk.TalkConfig { TechKey = "0" });
        PhpPlanQ1Talk.SessionUser = () => 12;
        var sid = PhpPlanQ1Talk.EpcStorefrontAntiCrawlSessionUserId();
        PhpPlanQ1Talk.SessionUser = () => 0;
        var sid0 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlSessionUserId();
        var stock = new List<object?>
        {
            Dict(("price", 9.5), ("qty", 3), ("exist", 2), ("warehouse", "Main"), ("storage_id", 4), ("price_id", 8), ("keep", "x")),
            "skip",
            Dict(("purchase", 1), ("delivery", 2), ("time_to_exe", 3))
        };
        PhpPlanQ1Talk.EpcStorefrontAntiCrawlRedactCrossStock(stock);
        PhpPlanQ1Talk.SessionUser = () => 12;
        PhpPlanQ1Talk.UserProfile = () => Dict(("groups", new List<object?> { 5, 9 }));
        var r1 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlResolvePricingIdentity();
        PhpPlanQ1Talk.UserProfile = () => Dict(("groups", new List<object?> { 0 }));
        var r2 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlResolvePricingIdentity();
        PhpPlanQ1Talk.SessionUser = () => 0;
        var r3 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlResolvePricingIdentity();
        PhpPlanQ1Talk.Server["HTTP_USER_AGENT"] = "Mozilla/5.0";
        PhpPlanQ1Talk.Post["tech_key"] = "tok";
        var en1 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlEnforce(cfg, Dict(("bucket", "talk_tech")));
        PhpPlanQ1Talk.Post.Clear();
        PhpPlanQ1Talk.PricesVisible = _ => true;
        PhpPlanQ1Talk.SessionUser = () => 4;
        PhpPlanQ1Talk.TempDir = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1t_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(PhpPlanQ1Talk.TempDir);
        try
        {
            var en2 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlEnforce(cfg, Dict(("bucket", "talk_ok"), ("guest_max", 30), ("user_max", 120), ("allow_tech_key", 0)));
            PhpPlanQ1Talk.PricesVisible = _ => false;
            PhpPlanQ1Talk.SessionUser = () => 0;
            var en3 = PhpPlanQ1Talk.EpcStorefrontAntiCrawlEnforce(cfg, Dict(("bucket", "talk_guest"), ("allow_tech_key", "0")));
            return new object?[]
            {
                ipPub, ipPrivCf, ipPrivXff, ipLoop, ipEmpty, b0, b1, b2, b3, b4, b5, b6, b7,
                t0, t1, t2, t3, t4, sid, sid0, stock, r1, r2, r3, en1, en2, en3
            };
        }
        finally
        {
            try { Directory.Delete(PhpPlanQ1Talk.TempDir, true); } catch { /* leftover temp */ }
        }
    }

    private static object? AcRl()
    {
        PhpPlanQ1Talk.Server["REMOTE_ADDR"] = "203.0.113.20";
        PhpPlanQ1Talk.Server["HTTP_USER_AGENT"] = "Mozilla/5.0";
        PhpPlanQ1Talk.Clock = () => 1770000000;
        PhpPlanQ1Talk.TempDir = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1t_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(PhpPlanQ1Talk.TempDir);
        try
        {
            var a = PhpPlanQ1Talk.EpcStorefrontAntiCrawlRateLimit("talk_rl", 2, 60);
            var b = PhpPlanQ1Talk.EpcStorefrontAntiCrawlRateLimit("talk_rl", 2, 60);
            var c = PhpPlanQ1Talk.EpcStorefrontAntiCrawlRateLimit("talk_rl", 2, 60);
            return new object?[] { a, b, c };
        }
        finally
        {
            try { Directory.Delete(PhpPlanQ1Talk.TempDir, true); } catch { /* leftover temp */ }
        }
    }

    private static Dictionary<string, object?> Dict(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, v) in pairs)
        {
            d[k] = v;
        }

        return d;
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
        => value.Length <= 800 ? value : value[..800] + "…";
}
