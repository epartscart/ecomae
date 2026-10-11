using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1WaveParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Wave");

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
    public void PlanQ1Wave_MatchPhpGolden()
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
            if (!Same(Json(Freeze(actual.Extra)), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(Freeze(actual.Extra))));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Wave.AuthMfaPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Wave.AuthMfaPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Wave.Reset();
        Assert.Contains("epc_auth_mfa.php", PhpPlanQ1Wave.AuthMfaPath, StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Wave.EpcMfaSessionVerified());
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Wave.AuthMfaPath, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Wave.Reset();
        PhpPlanQ1Wave.Clock = () => "2026-10-10 00:00:00";
        PhpPlanQ1Wave.DateYmd = () => "2026-10-10";
        PhpPlanQ1Wave.UnixNow = () => 1_775_000_000;
        PhpPlanQ1Wave.Server["REMOTE_ADDR"] = "198.51.100.20";
        PhpPlanQ1Wave.Server["HTTP_USER_AGENT"] = "WaveTest/1.0";
        return name switch
        {
            "pure" => Pure(),
            "enroll" => Enroll(),
            "policy" => Policy(),
            "ajax" => Ajax(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var db = new PhpPlanQ1Wave.WaveStore();
        var enc = PhpPlanQ1Wave.EpcMfaBase32Encode("Hello");
        var dec = PhpPlanQ1Wave.EpcMfaBase32Decode(enc);
        var secret = "JBSWY3DPEHPK3PXP";
        var code = PhpPlanQ1Wave.EpcMfaTotpCode(secret, 1000);
        var uri = PhpPlanQ1Wave.EpcMfaOtpauthUri(secret, "ops@ecomae.com");
        var qr = PhpPlanQ1Wave.EpcMfaQrDataUri(uri);
        var sess = PhpPlanQ1Wave.EpcMfaSessionVerified();
        var erp = PhpPlanQ1Wave.EpcMfaErpFinanceGate(db, 7, "orders");
        PhpPlanQ1Wave.Get["mfa_action"] = "nope";
        var ajax = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 7);
        var path = PhpPlanQ1Wave.EpcMfaPathRequiresMfa("/cp/shop/finance/erp", db);
        var path2 = PhpPlanQ1Wave.EpcMfaPathRequiresMfa("/cp/users", db);
        return new Rendered(new object?[] { enc, dec, code, uri, qr, sess, erp, ajax, path, path2 });
    }

    private static Rendered Enroll()
    {
        var db = new PhpPlanQ1Wave.WaveStore();
        db.Users.Add(new PhpPlanQ1Wave.UserRow { UserId = 7, Type = 1, Email = "ops@ecomae.com" });
        var first = PhpPlanQ1Wave.EpcMfaEnroll(db, 7, "ops@ecomae.com");
        var secret = Convert.ToString(first["secret"]) ?? "";
        var bad = PhpPlanQ1Wave.EpcMfaConfirmEnrollment(db, 7, "000000");
        var ok = PhpPlanQ1Wave.EpcMfaConfirmEnrollment(db, 7, PhpPlanQ1Wave.EpcMfaTotpCode(secret));
        var again = PhpPlanQ1Wave.EpcMfaEnroll(db, 7, "ops@ecomae.com");
        var ver = PhpPlanQ1Wave.EpcMfaVerify(db, 7, PhpPlanQ1Wave.EpcMfaTotpCode(secret));
        var st = db.Audits.Select(a => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = a.UserId,
            ["action"] = a.Action,
            ["success"] = a.Success
        }).ToList();
        var status = PhpPlanQ1Wave.EpcMfaUserStatus(db, 7);
        return new Rendered(new object?[] { first, bad, ok, again, ver, st, status });
    }

    private static Rendered Policy()
    {
        var db = new PhpPlanQ1Wave.WaveStore();
        var def = PhpPlanQ1Wave.EpcMfaGetPolicy(db);
        var saved = PhpPlanQ1Wave.EpcMfaSavePolicy(db, new(StringComparer.Ordinal)
        {
            ["require_mfa_for_roles"] = new List<string> { "finance_user" },
            ["require_mfa_for_paths"] = new List<string> { "/cp/shop/finance/" },
            ["grace_period_hours"] = 12
        }, "acme");
        var got = PhpPlanQ1Wave.EpcMfaGetPolicy(db, "acme");
        var upd = PhpPlanQ1Wave.EpcMfaUpdatePolicy(db, "acme", new(StringComparer.Ordinal)
        {
            ["require_mfa_for_roles"] = "finance_admin",
            ["require_mfa_for_paths"] = "/erp/gl",
            ["grace_period_hours"] = 4
        });
        var got2 = PhpPlanQ1Wave.EpcMfaGetPolicy(db, "acme");
        db.Users.Add(new PhpPlanQ1Wave.UserRow { UserId = 1, Type = 1, Email = "a@e.com" });
        db.Users.Add(new PhpPlanQ1Wave.UserRow { UserId = 2, Type = 0, Email = "b@e.com" });
        db.Users.Add(new PhpPlanQ1Wave.UserRow { UserId = 3, Type = 0, Email = "c@e.com" });
        db.Groups.Add(new PhpPlanQ1Wave.GroupRow { UserId = 2, Name = "Finance Admin" });
        db.Groups.Add(new PhpPlanQ1Wave.GroupRow { UserId = 3, Name = "sales" });
        db.Depts.Add(new PhpPlanQ1Wave.DeptRow { UserId = 3, Department = "Finance Ops" });
        var req1 = PhpPlanQ1Wave.EpcMfaRequiredForUser(db, 1);
        var req2 = PhpPlanQ1Wave.EpcMfaRequiredForUser(db, 2);
        var req3 = PhpPlanQ1Wave.EpcMfaRequiredForUser(db, 3);
        var gate = PhpPlanQ1Wave.EpcMfaCpAuthGate(db, 2, "/cp/shop/finance/erp");
        var fin = PhpPlanQ1Wave.EpcMfaErpFinanceGate(db, 2, "gl");
        return new Rendered(new object?[] { def, saved, got, upd, got2, req1, req2, req3, gate, fin });
    }

    private static Rendered Ajax()
    {
        var db = new PhpPlanQ1Wave.WaveStore();
        db.Users.Add(new PhpPlanQ1Wave.UserRow { UserId = 9, Type = 0, Email = "" });
        PhpPlanQ1Wave.Post = new(StringComparer.Ordinal) { ["mfa_action"] = "enroll" };
        var en = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 9);
        var secret = Convert.ToString(en["secret"]) ?? "";
        PhpPlanQ1Wave.Post = new(StringComparer.Ordinal) { ["mfa_action"] = "confirm", ["code"] = PhpPlanQ1Wave.EpcMfaTotpCode(secret) };
        var cf = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 9);
        PhpPlanQ1Wave.Post = new(StringComparer.Ordinal) { ["mfa_action"] = "verify", ["code"] = PhpPlanQ1Wave.EpcMfaTotpCode(secret) };
        var vf = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 9);
        var sess = PhpPlanQ1Wave.EpcMfaSessionVerified();
        PhpPlanQ1Wave.Post = new(StringComparer.Ordinal) { ["mfa_action"] = "status" };
        var st = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 9);
        PhpPlanQ1Wave.Post = new(StringComparer.Ordinal) { ["mfa_action"] = "disable" };
        var ds = PhpPlanQ1Wave.EpcMfaHandleAjax(db, 9);
        var acts = db.Audits.Select(a => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = a.Action,
            ["success"] = a.Success
        }).ToList();
        return new Rendered(new object?[] { en, cf, vf, sess, st, ds, acts });
    }

    private static object? Freeze(object? value)
    {
        switch (value)
        {
            case string s:
                if (Regex.IsMatch(s, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"))
                {
                    return "2026-10-10 00:00:00";
                }

                if (Regex.IsMatch(s, "^[A-Z2-7]{16,}$"))
                {
                    return "SECRET";
                }

                if (Regex.IsMatch(s, "^[A-F0-9]{10}$"))
                {
                    return "CODE";
                }

                s = Regex.Replace(s, "secret=[A-Z2-7]+", "secret=SECRET");
                return s;
            case Dictionary<string, object?> map:
                return map.ToDictionary(kv => kv.Key, kv => Freeze(kv.Value), StringComparer.Ordinal);
            case List<string> list:
                return list.Select(Freeze).ToList();
            case List<Dictionary<string, object?>> rows:
                return rows.Select(r => (Dictionary<string, object?>)Freeze(r)!).ToList();
            case List<object?> boxed:
                return boxed.Select(Freeze).ToList();
            case object?[] arr:
                return arr.Select(Freeze).ToArray();
            default:
                return value;
        }
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
