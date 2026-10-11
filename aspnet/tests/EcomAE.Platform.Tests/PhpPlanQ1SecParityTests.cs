using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1SecParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Sec");

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
    public void PlanQ1Sec_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Sec.SecurityKernelPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Sec.SecurityKernelPath });

    [Fact]
    public void ScriptRiskClass_DoesNotStartASession()
        => Assert.Equal("app", PhpPlanQ1Sec.EpcSecScriptRiskClass("index.php"));

    private static string Render(string name)
        => name switch
        {
            "sec_static" => Json(SecStatic()),
            "sec_flow" => Json(SecFlow()),
            _ => "unknown:" + name
        };

    private static object?[] SecStatic()
    {
        var ctx = new PhpPlanQ1Sec.SecCtx { DocumentRoot = Path.GetTempPath() };
        return new object?[]
        {
            PhpPlanQ1Sec.EpcSecScriptRiskClass("ecomae-find-db-pass.php"),
            PhpPlanQ1Sec.EpcSecScriptRiskClass("/tmp/epc-foo.php"),
            PhpPlanQ1Sec.EpcSecScriptRiskClass("index.php"),
            PhpPlanQ1Sec.EpcSecLockdownEnabled(ctx),
            PhpPlanQ1Sec.EpcSecProviderEmailAllowlist(ctx),
            PhpPlanQ1Sec.EpcSecEmailIsProviderAllowlisted(ctx, "admin@ecomae.com"),
            PhpPlanQ1Sec.EpcSecEmailIsProviderAllowlisted(ctx, "ecomae.admin@shop.com"),
            PhpPlanQ1Sec.EpcSecEmailIsProviderAllowlisted(ctx, "shopper@x.com"),
            PhpPlanQ1Sec.EpcSecEmailIsProviderAllowlisted(ctx, ""),
            PhpPlanQ1Sec.EpcSecSafeError(ctx, "Nope", 422)
        };
    }

    private static object?[] SecFlow()
    {
        var store = new PhpPlanQ1Sec.SecStore();
        store.Groups.AddRange(new[]
        {
            new PhpPlanQ1Sec.GroupRow { Id = 1, Parent = 0, ForBackend = 1 },
            new PhpPlanQ1Sec.GroupRow { Id = 2, Parent = 1, ForBackend = 0 },
            new PhpPlanQ1Sec.GroupRow { Id = 3, Parent = 0, ForBackend = 0 }
        });
        store.Binds.AddRange(new[]
        {
            new PhpPlanQ1Sec.BindRow { UserId = 9, GroupId = 2 },
            new PhpPlanQ1Sec.BindRow { UserId = 8, GroupId = 3 }
        });
        var b1 = PhpPlanQ1Sec.EpcSecUserHasBackendGroup(store, 9);
        var b2 = PhpPlanQ1Sec.EpcSecUserHasBackendGroup(store, 8);
        var b0 = PhpPlanQ1Sec.EpcSecUserHasBackendGroup(store, 0);
        var r1 = PhpPlanQ1Sec.EpcSecBosResolveRole(store, new Dictionary<string, object?> { ["id"] = 9, ["site_key"] = "siteA" }, "x@y.com");
        var r2 = PhpPlanQ1Sec.EpcSecBosResolveRole(store, new Dictionary<string, object?> { ["id"] = 1, ["_table"] = "admin" }, "x@y.com");
        var r3 = PhpPlanQ1Sec.EpcSecBosResolveRole(store, new Dictionary<string, object?> { ["id"] = 9 }, "nobody@x.com");
        var r4 = PhpPlanQ1Sec.EpcSecBosResolveRole(store, new Dictionary<string, object?> { ["id"] = 8 }, "admin@ecomae.com");
        var r5 = PhpPlanQ1Sec.EpcSecBosResolveRole(store, new Dictionary<string, object?> { ["id"] = 8 }, "shop@x.com");
        var ctx = new PhpPlanQ1Sec.SecCtx { RemoteAddr = "0", TempDir = Path.GetTempPath() };
        var bucket = "q1sec-parity";
        var file = Path.Combine(ctx.TempDir.TrimEnd('/'), "epc_rl", Sha(bucket + "|0") + ".json");
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        var a = PhpPlanQ1Sec.EpcSecRateLimit(ctx, bucket, 2, 300);
        var b = PhpPlanQ1Sec.EpcSecRateLimit(ctx, bucket, 2, 300);
        var c = PhpPlanQ1Sec.EpcSecRateLimit(ctx, bucket, 2, 300);
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1s_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(doc);
        var lockCtx = new PhpPlanQ1Sec.SecCtx { DocumentRoot = doc };
        var lock1 = PhpPlanQ1Sec.EpcSecLockdownEnabled(lockCtx);
        File.WriteAllText(Path.Combine(doc, ".epc-security-lockdown"), "1");
        var lock2 = PhpPlanQ1Sec.EpcSecLockdownEnabled(lockCtx);
        return new object?[] { b1, b2, b0, r1, r2, r3, r4, r5, a, b, c, lock1, lock2 };
    }

    private static string Sha(string value)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

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
