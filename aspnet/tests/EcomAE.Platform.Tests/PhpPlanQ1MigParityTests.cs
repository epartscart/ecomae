using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1MigParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Mig");

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
    public void PlanQ1Mig_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mig.DbMigrationsPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mig.DbMigrationsPath });

    [Fact]
    public void MigrationsStatus_DoesNotStartASession()
    {
        var store = new PhpPlanQ1Mig.MigStore();
        var st = PhpPlanQ1Mig.EpcMigrationsStatus(store);
        Assert.Equal("000", st["current_version"]);
    }

    private static string Render(string name)
        => name switch
        {
            "mig_static" => Json(MigStatic()),
            "mig_flow" => Json(MigFlow()),
            _ => "unknown:" + name
        };

    private static object?[] MigStatic()
    {
        var r = PhpPlanQ1Mig.EpcMigrationsRegistry();
        return new object?[]
        {
            PhpPlanQ1Mig.EpcMigrationsVersion,
            r.GetArrayLength(),
            r[0].GetProperty("version").GetString(),
            r[0].GetProperty("name").GetString(),
            r[7].GetProperty("version").GetString(),
            r[7].GetProperty("name").GetString(),
            r[0].GetProperty("checksum").GetString(),
            r[7].GetProperty("checksum").GetString()
        };
    }

    private static object?[] MigFlow()
    {
        var store = new PhpPlanQ1Mig.MigStore();
        var st0 = PhpPlanQ1Mig.EpcMigrationsStatus(store);
        var dry0 = PhpPlanQ1Mig.EpcMigrationsDryRun(store);
        var registry = PhpPlanQ1Mig.EpcMigrationsRegistry();
        var first = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in registry[0].EnumerateObject())
        {
            first[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
        }

        var one = PhpPlanQ1Mig.EpcMigrationApply(store, first);
        var pend = PhpPlanQ1Mig.EpcMigrationsPending(store);
        var st1 = PhpPlanQ1Mig.EpcMigrationsStatus(store);
        var run = PhpPlanQ1Mig.EpcMigrationsRunAll(store);
        var st2 = PhpPlanQ1Mig.EpcMigrationsStatus(store);
        var ver = PhpPlanQ1Mig.EpcMigrationsVerify(store);
        var rb = PhpPlanQ1Mig.EpcMigrationRollback(store, "008");
        var miss = PhpPlanQ1Mig.EpcMigrationRollback(store, "999");
        var dry1 = PhpPlanQ1Mig.EpcMigrationsDryRun(store);
        var again = PhpPlanQ1Mig.EpcMigrationsRunAll(store);
        store.Applied.Add(new PhpPlanQ1Mig.AppliedRow { Version = "999", Name = "ghost", Checksum = "abc" });
        var ver2 = PhpPlanQ1Mig.EpcMigrationsVerify(store);
        var issues = (List<Dictionary<string, object?>>)ver2["issues"]!;
        var dry0Statements = (List<Dictionary<string, object?>>)dry0["statements"]!;
        var dry1Statements = (List<Dictionary<string, object?>>)dry1["statements"]!;
        return new object?[]
        {
            st0["current_version"],
            st0["pending"],
            dry0["count"],
            dry0Statements[0]["name"],
            one["ok"],
            one["version"],
            pend.Count,
            pend[0]["version"],
            st1["applied"],
            st1["current_version"],
            run["ok"],
            run["applied"],
            st2["applied"],
            st2["pending"],
            st2["current_version"],
            st2["latest_version"],
            ver["ok"],
            rb["ok"],
            rb.TryGetValue("rolled_back", out var rolled) ? rolled : null,
            miss["ok"],
            miss["error"],
            dry1["count"],
            dry1Statements[0]["version"],
            again["ok"],
            again["applied"],
            ver2["ok"],
            issues[0]["issue"]
        };
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
