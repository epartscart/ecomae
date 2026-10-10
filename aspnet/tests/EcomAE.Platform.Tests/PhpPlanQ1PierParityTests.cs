using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1PierParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Pier");

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
    public void PlanQ1Pier_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Pier.PlatformJobsPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Pier.PlatformJobsPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Pier.Reset();
        Assert.Contains("epc_platform_jobs.php", PhpPlanQ1Pier.PlatformJobsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Pier.PlatformJobsPath, StringComparison.Ordinal);
        Assert.Equal(0, PhpPlanQ1Pier.EpcPlatformJobsEnqueue(""));
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Pier.Reset();
        return name switch
        {
            "enqueue" => Enqueue(),
            "claim" => Claim(),
            "dispatch" => Dispatch(),
            "batch" => Batch(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Enqueue()
    {
        var db = new PhpPlanQ1Pier.PierStore();
        PhpPlanQ1Pier.UseStore(db);
        var empty = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("");
        var a = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("NoOp", "Alpha", new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 1 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["priority"] = 10 });
        var b = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("noop", "alpha", new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 2 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dedupe"] = 1 });
        var c = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("scan", "beta", new Dictionary<string, object?>(StringComparer.Ordinal), new Dictionary<string, object?>(StringComparer.Ordinal) { ["delay_sec"] = 0, ["max_attempts"] = 2 });
        var d = PhpPlanQ1Pier.EpcPlatformJobsEnqueue("scan", "gamma", new Dictionary<string, object?>(StringComparer.Ordinal) { ["x"] = "y" }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dedupe"] = "0" });
        return new Rendered(new object?[] { empty, a, b, c, d, Snap(db, a), Snap(db, c), Snap(db, d) });
    }

    private static Rendered Claim()
    {
        var db = new PhpPlanQ1Pier.PierStore();
        PhpPlanQ1Pier.UseStore(db);
        PhpPlanQ1Pier.EpcPlatformJobsEnqueue("noop", "alpha", new Dictionary<string, object?>(StringComparer.Ordinal) { ["a"] = 1 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["priority"] = 50 });
        PhpPlanQ1Pier.EpcPlatformJobsEnqueue("noop", "beta", new Dictionary<string, object?>(StringComparer.Ordinal) { ["b"] = 1 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["priority"] = 10 });
        var claimed = PhpPlanQ1Pier.EpcPlatformJobsClaim("w1!", 1);
        var ids = claimed.Select(r => Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture)).ToList();
        if (ids.Count > 0)
        {
            PhpPlanQ1Pier.EpcPlatformJobsComplete(ids[0], new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true });
        }

        PhpPlanQ1Pier.EpcPlatformJobsEnqueue("noop", "gamma", new Dictionary<string, object?>(StringComparer.Ordinal), new Dictionary<string, object?>(StringComparer.Ordinal) { ["priority"] = 1 });
        var claimed2 = PhpPlanQ1Pier.EpcPlatformJobsClaim("worker-two", 5);
        var failRetry = Convert.ToInt32(claimed2[0]["id"], System.Globalization.CultureInfo.InvariantCulture);
        var failDead = Convert.ToInt32(claimed2[1]["id"], System.Globalization.CultureInfo.InvariantCulture);
        PhpPlanQ1Pier.EpcPlatformJobsFail(failRetry, "boom", true);
        PhpPlanQ1Pier.EpcPlatformJobsFail(failDead, new string('e', 12), false);
        return new Rendered(new object?[]
        {
            claimed.Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = r["id"],
                ["tenant_key"] = r["tenant_key"],
                ["status"] = r["status"],
                ["locked_by"] = r["locked_by"],
                ["priority"] = r["priority"]
            }).ToList(),
            Snap(db, ids.FirstOrDefault()),
            claimed2.Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = r["id"],
                ["tenant_key"] = r["tenant_key"],
                ["attempts"] = r["attempts"]
            }).ToList(),
            Snap(db, failRetry),
            Snap(db, failDead)
        });
    }

    private static Rendered Dispatch()
    {
        var db = new PhpPlanQ1Pier.PierStore();
        PhpPlanQ1Pier.UseStore(db);
        PhpPlanQ1Pier.TenantGet = key => key == "alpha"
            ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "alpha", ["db_name"] = "alpha", ["dedicated_db"] = 1 }
            : null;
        PhpPlanQ1Pier.BlockchainBatch = (t, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["anchored"] = 1, ["tenant"] = t }
        };
        PhpPlanQ1Pier.EpcPlatformJobsRegisterHandler("custom", (tenant, payload, _) =>
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["tenant"] = tenant,
                    ["n"] = payload.TryGetValue("n", out var n) ? n : 0
                }
            });
        var noop = PhpPlanQ1Pier.EpcPlatformJobsDispatch(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["job_type"] = "noop",
            ["tenant_key"] = "alpha",
            ["payload_json"] = "{\"echo\":1}"
        });
        var custom = PhpPlanQ1Pier.EpcPlatformJobsDispatch(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["job_type"] = "custom",
            ["tenant_key"] = "Alpha",
            ["payload_json"] = "{\"n\":7}"
        });
        var unk = PhpPlanQ1Pier.EpcPlatformJobsDispatch(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["job_type"] = "nope",
            ["tenant_key"] = "alpha"
        });
        var miss = PhpPlanQ1Pier.EpcPlatformJobsHandleTenantHealthPing("", new Dictionary<string, object?>(StringComparer.Ordinal));
        var none = PhpPlanQ1Pier.EpcPlatformJobsHandleTenantHealthPing("beta", new Dictionary<string, object?>(StringComparer.Ordinal));
        var ok = PhpPlanQ1Pier.EpcPlatformJobsHandleTenantHealthPing("alpha", new Dictionary<string, object?>(StringComparer.Ordinal));
        var warm = PhpPlanQ1Pier.EpcPlatformJobsHandleTenantWarmup("alpha", new Dictionary<string, object?>(StringComparer.Ordinal));
        var chain = PhpPlanQ1Pier.EpcPlatformJobsDispatch(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["job_type"] = "blockchain_anchor_batch",
            ["tenant_key"] = "alpha",
            ["payload_json"] = "{}"
        });
        return new Rendered(new object?[] { noop, custom, unk, miss, none, ok, warm, chain });
    }

    private static Rendered Batch()
    {
        var db = new PhpPlanQ1Pier.PierStore();
        PhpPlanQ1Pier.UseStore(db);
        PhpPlanQ1Pier.EpcPlatformJobsEnqueue("noop", "alpha", new Dictionary<string, object?>(StringComparer.Ordinal) { ["z"] = 1 });
        PhpPlanQ1Pier.EpcPlatformJobsEnqueue("nope", "beta");
        var output = PhpPlanQ1Pier.EpcPlatformJobsRunBatch(10, "cron-test");
        var rows = db.Jobs.OrderBy(j => j.Id).Select(j => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["job_type"] = j.JobType,
            ["tenant_key"] = j.TenantKey,
            ["status"] = j.Status,
            ["has_err"] = j.LastError != null ? 1 : 0
        }).ToList();
        return new Rendered(new object?[] { output, rows });
    }

    private static Dictionary<string, object?> Snap(PhpPlanQ1Pier.PierStore db, int id)
    {
        var row = db.Jobs.First(j => j.Id == id);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["job_type"] = row.JobType,
            ["tenant_key"] = row.TenantKey,
            ["payload_json"] = row.PayloadJson,
            ["status"] = row.Status,
            ["priority"] = row.Priority,
            ["attempts"] = row.Attempts,
            ["max_attempts"] = row.MaxAttempts,
            ["locked_by"] = row.LockedBy,
            ["last_error"] = row.LastError,
            ["result_json"] = row.ResultJson
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
        => value.Length <= 1400 ? value : value[..1400] + "…";
}
