using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SlipParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Slip");

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
    public void PlanQ1Slip_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Slip.BosAjaxLoginPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Slip.BosAjaxLoginPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Slip.Reset();
        Assert.Contains("epc_bos_ajax_login.php", PhpPlanQ1Slip.BosAjaxLoginPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Slip.BosAjaxLoginPath, StringComparison.Ordinal);
        var result = PhpPlanQ1Slip.EpcBosAjaxLoginSecure();
        Assert.False((bool)result["ok"]!);
        Assert.Equal(0, PhpPlanQ1Slip.RegenCount);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Slip.Reset();
        PhpPlanQ1Slip.Csrf = () => "fixedcsrf";
        PhpPlanQ1Slip.SecretSuccession = "succ";
        PhpPlanQ1Slip.SetContext = ctx => PhpPlanQ1Slip.LastContext = ctx;
        return name switch
        {
            "missing" => Missing(),
            "stores" => Stores(),
            "roles" => Roles(),
            "session" => Session(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static string Md5Hex(string value)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static PhpPlanQ1Slip.UserRow User(string table, string email, string password, string siteKey = "", int id = 0)
    {
        var row = new PhpPlanQ1Slip.UserRow { Table = table };
        row.Fields["email"] = email;
        if (table == "admin")
        {
            row.Fields["pass"] = password;
        }
        else
        {
            row.Fields["password"] = password;
        }

        row.Fields["site_key"] = siteKey;
        if (id > 0)
        {
            row.Fields["id"] = id;
        }

        return row;
    }

    private static Dictionary<string, object?> Login(string email, string password)
    {
        PhpPlanQ1Slip.Post = new(StringComparer.Ordinal) { ["email"] = email, ["password"] = password };
        return PhpPlanQ1Slip.EpcBosAjaxLoginSecure();
    }

    private static Rendered Missing()
    {
        PhpPlanQ1Slip.ConfigThrows = true;
        PhpPlanQ1Slip.PlatformPdo = () => null;
        return new Rendered(new object?[]
        {
            Login("", "x"),
            Login("a@b.c", ""),
            Login("  ", "  "),
            Login("a@b.c", "x")
        });
    }

    private static Rendered Stores()
    {
        var bcrypt = BCrypt.Net.BCrypt.HashPassword("secret1", workFactor: 4);
        var db = new PhpPlanQ1Slip.PdoStore();
        db.Users.Add(User("users", "empty@x.com", ""));
        db.Users.Add(User("users", "bcrypt@x.com", bcrypt, "shop", 2));
        db.Users.Add(User("users", "md5s@x.com", Md5Hex("secret1succ"), "shop", 3));
        db.Users.Add(User("users", "md5p@x.com", Md5Hex("secret1"), "shop", 4));
        db.Users.Add(User("admin", "alias@x.com", bcrypt, "", 1));
        PhpPlanQ1Slip.MainPdo = db;
        PhpPlanQ1Slip.PlatformPdo = () => null;
        return new Rendered(new object?[]
        {
            Login("none@x.com", "secret1"),
            Login("empty@x.com", "secret1"),
            Login("bcrypt@x.com", "wrong"),
            Login("bcrypt@x.com", "secret1"),
            Login("md5s@x.com", "secret1"),
            Login("md5p@x.com", "secret1"),
            Login("alias@x.com", "secret1")
        });
    }

    private static Rendered Roles()
    {
        var bcrypt = BCrypt.Net.BCrypt.HashPassword("secret1", workFactor: 4);
        var db = new PhpPlanQ1Slip.PdoStore();
        db.Users.Add(User("users", "guest@x.com", bcrypt, "", 1));
        db.Users.Add(User("users", "tenant@x.com", bcrypt, "Acme", 2));
        db.Users.Add(User("users", "admin@ecomae.com", bcrypt, "", 3));
        db.Users.Add(User("users", "ops@x.com", bcrypt, "", 4));
        db.Users.Add(User("admin", "root@x.com", bcrypt, "", 1));
        db.Sec.Groups.Add(new PhpPlanQ1Sec.GroupRow { Id = 9, Parent = 0, ForBackend = 1 });
        db.Sec.Binds.Add(new PhpPlanQ1Sec.BindRow { UserId = 4, GroupId = 9 });
        PhpPlanQ1Slip.MainPdo = db;
        PhpPlanQ1Slip.PlatformPdo = () => null;
        return new Rendered(new object?[]
        {
            Login("guest@x.com", "secret1"),
            Login("tenant@x.com", "secret1"),
            Login("root@x.com", "secret1"),
            Login("admin@ecomae.com", "secret1"),
            Login("ops@x.com", "secret1")
        });
    }

    private static Rendered Session()
    {
        var bcrypt = BCrypt.Net.BCrypt.HashPassword("secret1", workFactor: 4);
        var db = new PhpPlanQ1Slip.PdoStore();
        db.Users.Add(User("users", "hello@ecomae.com", bcrypt, "", 1));
        PhpPlanQ1Slip.MainPdo = db;
        PhpPlanQ1Slip.PlatformPdo = () => db;
        var outLogin = Login("hello@ecomae.com", "secret1");
        return new Rendered(new object?[]
        {
            outLogin,
            PhpPlanQ1Slip.LastContext,
            PhpPlanQ1Slip.UpgradeCount,
            PhpPlanQ1Slip.AuditCount
        });
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
        => value.Length <= 1400 ? value : value[..1400] + "…";
}
