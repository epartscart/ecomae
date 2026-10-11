using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1QuayParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Quay");

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
    public void PlanQ1Quay_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Quay.TenantPdoPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Quay.TenantPdoPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Quay.Reset();
        Assert.Contains("epc_tenant_pdo.php", PhpPlanQ1Quay.TenantPdoPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Quay.TenantPdoPath, StringComparison.Ordinal);
        var (pdo, err) = PhpPlanQ1Quay.EpcTenantPdo("", "db", "u", "p");
        Assert.Null(pdo);
        Assert.Equal("Missing DB connection fields", err);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Quay.Reset();
        return name switch
        {
            "fields" => Fields(),
            "from_row" => FromRow(),
            "connect" => Connect(),
            "pool" => Pool(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] pairs)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            row[key] = value;
        }

        return row;
    }

    private static object Pack(PhpPlanQ1Quay.PdoHandle? pdo, string err)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = pdo != null ? 1 : 0,
            ["err"] = err is "Missing DB connection fields" or "Tenant DB credentials incomplete" or "" ? err : "SQLSTATE"
        };

    private static void UseHandles(Func<string, string, string, bool> allow)
    {
        var next = 1;
        PhpPlanQ1Quay.Open = (host, db, user, _, _) =>
        {
            if (!allow(host, db, user))
            {
                return (null, "SQLSTATE");
            }

            return (new PhpPlanQ1Quay.PdoHandle { Id = next++, Host = host, Db = db, User = user }, "");
        };
        PhpPlanQ1Quay.Ping = handle => !handle.Dead;
    }

    private static Rendered Fields()
    {
        var (a, e) = PhpPlanQ1Quay.EpcTenantPdo("", "db", "u", "p");
        var (b, f) = PhpPlanQ1Quay.EpcTenantPdo("h", "", "u", "p");
        var (c, g) = PhpPlanQ1Quay.EpcTenantPdo("h", "db", "", "p");
        var (d, h) = PhpPlanQ1Quay.EpcTenantPdo("  ", " db ", "  ", "p");
        var hostA = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row(("db_host", " Host.Example ")));
        var hostB = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row(("db_host", "0")));
        PhpPlanQ1Quay.DpConfigHost = () => "from-global";
        var hostC = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row(("db_host", "")));
        var hostD = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row(("db_host", "   ")));
        PhpPlanQ1Quay.DpConfigHost = () => "";
        PhpPlanQ1Quay.DocumentConfigHost = () => "from-file";
        var hostE = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row());
        PhpPlanQ1Quay.DpConfigHost = null;
        PhpPlanQ1Quay.DocumentConfigHost = null;
        var hostF = PhpPlanQ1Quay.EpcTenantPdoResolveHost(Row());
        var flags = new object[]
        {
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("dedicated_db", 1), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("dedicated_db", "1"), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("dedicated_db", 0), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("dedicated_db", "0"), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("scale_policy", "Dedicated_MySQL"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("scale_policy", " shared "))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("erp_only_shared", 1), ("db_name", "acme"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("erp_only_shared", "0"), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("erp_only_shared", "1"), ("db_name", "docpart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("db_name", "tenant_acme"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("db", "OtherDb"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("db_name", "DocPart"))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row(("db_name", ""))) ? 1 : 0,
            PhpPlanQ1Quay.EpcTenantRowUsesDedicatedDb(Row()) ? 1 : 0
        };
        return new Rendered(new object?[]
        {
            Pack(a, e), Pack(b, f), Pack(c, g), Pack(d, h),
            hostA, hostB, hostC, hostD, hostE, hostF,
            flags,
            PhpPlanQ1Quay.EpcTenantPdoPoolStats()
        });
    }

    private static Rendered FromRow()
    {
        var creds = new Dictionary<string, object?>(StringComparer.Ordinal);
        PhpPlanQ1Quay.ResolveSharedCreds = () => creds;
        UseHandles((_, db, user) => user == "ecomae" && db == "tenant_ok");
        var (a, e) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row());
        var (b, f) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "x")));
        var (c, g) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "no_such_db_quay"), ("db_user", "u"), ("db_password", "p")), Row(("timeout", 1)));
        var (d, h) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db", "no_such_db_quay"), ("user", "u"), ("password", "p")), Row(("timeout", 1)));
        var (i, j) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "no_such_db_quay"), ("db_user", "u"), ("db_pass", "p")), Row(("timeout", 1)));
        var (k, l) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "tenant_ok"), ("db_user", "ecomae"), ("db_password", "pw")), Row(("timeout", 2)));
        creds["user"] = "ecomae";
        creds["password"] = "pw";
        creds["db"] = "tenant_ok";
        var (m, n) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "docpart"), ("db_user", "ignored"), ("db_password", "")), Row(("timeout", 2)));
        creds.Clear();
        var (o, p) = PhpPlanQ1Quay.EpcTenantPdoFromRow(Row(("db_host", "127.0.0.1"), ("db_name", "docpart"), ("db_user", "ecomae"), ("db_password", "")), Row(("timeout", 1)));
        return new Rendered(new object?[]
        {
            Pack(a, e), Pack(b, f), Pack(c, g), Pack(d, h), Pack(i, j), Pack(k, l), Pack(m, n), Pack(o, p)
        });
    }

    private static Rendered Connect()
    {
        UseHandles((_, db, user) => user == "ecomae" && db == "tenant_ok");
        var (a, e1) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "tenant_ok", "ecomae", "pw", Row(("timeout", 2)));
        var id1 = a?.Id ?? 0;
        var (b, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "tenant_ok", "ecomae", "pw");
        var id2 = b?.Id ?? 0;
        var (c, e3) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "no_such_db_quay", "ecomae", "pw", Row(("timeout", 1)));
        if (a != null)
        {
            a.Dead = true;
        }

        var (d, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "tenant_ok", "ecomae", "pw");
        var id4 = d?.Id ?? 0;
        var (e5p, e5) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "tenant_ok", "Ecomae", "pw", Row(("timeout", 1)));
        return new Rendered(new object?[]
        {
            Pack(a, e1),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["reused"] = id1 != 0 && id1 == id2 ? 1 : 0 },
            Pack(c, e3),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["reconnected"] = id4 != 0 && id4 != id1 ? 1 : 0 },
            Pack(e5p, e5)
        });
    }

    private static Rendered Pool()
    {
        PhpPlanQ1Quay.PoolMax = 2;
        UseHandles((_, _, _) => true);
        var (a, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_a", "ecomae", "pw");
        var idA1 = a?.Id ?? 0;
        var (b, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_b", "ecomae", "pw");
        var idB1 = b?.Id ?? 0;
        var (c, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_c", "ecomae", "pw");
        var idC1 = c?.Id ?? 0;
        var (b2, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_b", "ecomae", "pw");
        var idB2 = b2?.Id ?? 0;
        var (c2, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_c", "ecomae", "pw");
        var idC2 = c2?.Id ?? 0;
        var (a2, _) = PhpPlanQ1Quay.EpcTenantPdo("127.0.0.1", "db_a", "ecomae", "pw");
        var idA2 = a2?.Id ?? 0;
        return new Rendered(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stats"] = PhpPlanQ1Quay.EpcTenantPdoPoolStats(),
            ["a_evicted"] = idA2 != 0 && idA2 != idA1 ? 1 : 0,
            ["b_reused"] = idB2 != 0 && idB2 == idB1 ? 1 : 0,
            ["c_reused"] = idC2 != 0 && idC2 == idC1 ? 1 : 0
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
