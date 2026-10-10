using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1TideParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Tide");
    private const string FrozenIso = "2026-10-10T00:00:00+00:00";
    private const long FrozenUnix = 1_775_000_000;

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
    public void PlanQ1Tide_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Tide.FailoverPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Tide.FailoverPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Tide.Reset();
        Assert.Contains("epc_platform_failover.php", PhpPlanQ1Tide.FailoverPath, StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Tide.SessionStarted);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Tide.FailoverPath, StringComparison.Ordinal);
        PhpPlanQ1Tide.Get["preview"] = "1";
        Assert.True(PhpPlanQ1Tide.EpcFailoverSplashPreviewRequested());
        Assert.False(PhpPlanQ1Tide.SessionStarted);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Tide.Reset();
        PhpPlanQ1Tide.ClockIso = () => FrozenIso;
        PhpPlanQ1Tide.UnixNow = () => FrozenUnix;
        PhpPlanQ1Tide.DefinedDocroot = "/docroot";
        PhpPlanQ1Tide.FallbackDocroot = "/docroot";
        PhpPlanQ1Tide.StoreDir("/docroot");
        PhpPlanQ1Tide.ProbeHttp = (_, _) => new PhpPlanQ1Tide.ProbeHit(false, 0, "blocked", "");
        return name switch
        {
            "pure" => Pure(),
            "files" => Files(),
            "probe" => Probe(),
            "status" => Status(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var modes = PhpPlanQ1Tide.EpcFailoverValidModes();
        var health = new Dictionary<string, object?>(StringComparer.Ordinal);
        var env = new Dictionary<string, object?>(StringComparer.Ordinal);
        var splash = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var m in modes.Concat(["unknown"]))
        {
            health[m] = PhpPlanQ1Tide.EpcFailoverPrimaryHealthForMode(m);
            env[m] = PhpPlanQ1Tide.EpcFailoverEnvLabel(m);
            splash[m] = PhpPlanQ1Tide.EpcFailoverShouldShowSplash(m);
        }

        var def = PhpPlanQ1Tide.EpcFailoverDefaultConfig();
        var cfg = PhpPlanQ1Tide.EpcFailoverReadConfig();
        var ttl = PhpPlanQ1Tide.EpcFailoverStatusCacheTtl();
        PhpPlanQ1Tide.Env["EPC_FAILOVER_STATUS_TTL"] = "15";
        var ttl15 = PhpPlanQ1Tide.EpcFailoverStatusCacheTtl();
        PhpPlanQ1Tide.Env["EPC_FAILOVER_STATUS_TTL"] = "12";
        var ttl12 = PhpPlanQ1Tide.EpcFailoverStatusCacheTtl();
        PhpPlanQ1Tide.Env["EPC_FAILOVER_STATUS_TTL"] = "abc";
        var ttlBad = PhpPlanQ1Tide.EpcFailoverStatusCacheTtl();
        PhpPlanQ1Tide.Env.Remove("EPC_FAILOVER_STATUS_TTL");
        var url = PhpPlanQ1Tide.EpcFailoverPrimaryProbeUrl();
        PhpPlanQ1Tide.Env["EPC_FAILOVER_PRIMARY_URL"] = " https://backup.example/ping ";
        var urlEnv = PhpPlanQ1Tide.EpcFailoverPrimaryProbeUrl();
        PhpPlanQ1Tide.Env.Remove("EPC_FAILOVER_PRIMARY_URL");
        PhpPlanQ1Tide.Server["HTTP_HOST"] = "shop.example:8443";
        var hostPort = PhpPlanQ1Tide.EpcFailoverHostLabel();
        PhpPlanQ1Tide.Server.Remove("HTTP_HOST");
        var hostDef = PhpPlanQ1Tide.EpcFailoverHostLabel();
        PhpPlanQ1Tide.Get.Clear();
        var prev0 = PhpPlanQ1Tide.EpcFailoverSplashPreviewRequested();
        PhpPlanQ1Tide.Get["preview"] = "0";
        var prevEmpty = PhpPlanQ1Tide.EpcFailoverSplashPreviewRequested();
        PhpPlanQ1Tide.Get["epc_splash_preview"] = "1";
        var prevOk = PhpPlanQ1Tide.EpcFailoverSplashPreviewRequested();
        var fast = PhpPlanQ1Tide.EpcFailoverReadModeFast();
        var mode = PhpPlanQ1Tide.EpcFailoverReadModeFile();
        var json = PhpPlanQ1Tide.EpcFailoverReadJsonMirror();
        var age = PhpPlanQ1Tide.EpcFailoverJsonMirrorAgeSec();
        var resolved = PhpPlanQ1Tide.EpcFailoverResolveMode(false);
        var splashNull = PhpPlanQ1Tide.EpcFailoverShouldShowSplash(null);
        var paths = new object[]
        {
            PhpPlanQ1Tide.EpcFailoverModePaths(),
            PhpPlanQ1Tide.EpcFailoverJsonPaths(),
            PhpPlanQ1Tide.EpcFailoverConfigPaths()
        };
        var doc = PhpPlanQ1Tide.EpcFailoverDocroot();
        return new Rendered(new object?[]
        {
            new object?[] { modes, health, env, splash },
            def,
            cfg,
            ttl,
            ttl15,
            ttl12,
            ttlBad,
            url,
            urlEnv,
            hostPort,
            hostDef,
            prev0,
            prevEmpty,
            prevOk,
            fast,
            mode,
            json,
            age,
            resolved,
            splashNull,
            paths,
            doc
        });
    }

    private static Rendered Files()
    {
        var bad = PhpPlanQ1Tide.EpcFailoverWriteModeFile("nope");
        var ok = PhpPlanQ1Tide.EpcFailoverWriteModeFile("backup_active", new(StringComparer.Ordinal) { ["note"] = "standby" });
        var read = PhpPlanQ1Tide.EpcFailoverReadModeFile();
        var fast = PhpPlanQ1Tide.EpcFailoverReadModeFast();
        var mirror = PhpPlanQ1Tide.EpcFailoverReadJsonMirror();
        var st = PhpPlanQ1Tide.EpcFailoverBuildStatus("failback_redirect", new(StringComparer.Ordinal)
        {
            ["redirect_seconds"] = "9",
            ["ping"] = true
        });
        var st2 = PhpPlanQ1Tide.EpcFailoverBuildStatus("unknown-mode");
        var cfgW = PhpPlanQ1Tide.EpcFailoverWriteConfig(new(StringComparer.Ordinal)
        {
            ["backup_base_url"] = "https://backup.local/",
            ["primary_url"] = "https://cloud.example",
            ["poll_interval_sec"] = 5,
            ["show_cloud_primary_badge"] = "0",
            ["extra"] = "keep"
        });
        var cfg = PhpPlanQ1Tide.EpcFailoverReadConfig();
        var cfgRawPoll = cfg["poll_interval_sec"];
        var cfg2w = PhpPlanQ1Tide.EpcFailoverWriteConfig(new(StringComparer.Ordinal)
        {
            ["poll_interval_sec"] = 90,
            ["show_cloud_primary_badge"] = 1
        });
        var cfg2 = PhpPlanQ1Tide.EpcFailoverReadConfig();
        var cur = PhpPlanQ1Tide.EpcFailoverCurrentStatus(false);
        var resolveFile = PhpPlanQ1Tide.EpcFailoverResolveMode(true);
        return new Rendered(new object?[]
        {
            bad, ok, read, fast, mirror, st, st2, cfgW, cfg, cfgRawPoll, cfg2w, cfg2, cur, resolveFile
        });
    }

    private static Rendered Probe()
    {
        PhpPlanQ1Tide.Server["HTTP_HOST"] = "www.ecomae.com";
        var local = PhpPlanQ1Tide.EpcFailoverProbePrimary(4);
        PhpPlanQ1Tide.Server["HTTP_HOST"] = "ecomae.com:443";
        var www = PhpPlanQ1Tide.EpcFailoverProbePrimary(4);
        PhpPlanQ1Tide.Server["HTTP_HOST"] = "www.ecomae.com";
        var auto = PhpPlanQ1Tide.EpcFailoverResolveMode(true);
        PhpPlanQ1Tide.Get.Clear();
        PhpPlanQ1Tide.Post.Clear();
        PhpPlanQ1Tide.Session.Clear();
        var no = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.Get["token"] = "0";
        var zero = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.DeployTokenDefined = true;
        PhpPlanQ1Tide.DeployTokenValue = "tide-secret";
        PhpPlanQ1Tide.Get["token"] = "tide-secret";
        var tokOk = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.Get.Clear();
        PhpPlanQ1Tide.Post["token"] = "nope";
        var tokBad = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.Post.Clear();
        PhpPlanQ1Tide.Session["user_id"] = 0;
        var uid0 = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.Session["user_id"] = 7;
        var noPortal = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        PhpPlanQ1Tide.PutFile("/docroot/content/general_pages/" + "epc_portal" + "." + "php", "stub");
        PhpPlanQ1Tide.LoadSuperCp = () => true;
        var super = PhpPlanQ1Tide.EpcFailoverProbeAuthorized();
        return new Rendered(new object?[] { local, www, auto, no, zero, tokOk, tokBad, uid0, noPortal, super });
    }

    private static Rendered Status()
    {
        PhpPlanQ1Tide.EpcFailoverWriteModeFile("primary_down");
        var fresh = PhpPlanQ1Tide.EpcFailoverCurrentStatus(false);
        var ageFresh = PhpPlanQ1Tide.EpcFailoverJsonMirrorAgeSec();
        var json = PhpPlanQ1Tide.EpcFailoverJsonPaths()[0];
        var staleMirror = PhpPlanQ1Tide.EpcFailoverBuildStatus("primary_ok");
        staleMirror["updated_at"] = "OLD";
        PhpPlanQ1Tide.PutFile(json, JsonSerializer.Serialize(staleMirror, JsonOpts) + "\n");
        var cached = PhpPlanQ1Tide.EpcFailoverCurrentStatus(false);
        PhpPlanQ1Tide.Touch(json, FrozenUnix - 200);
        var ageStale = PhpPlanQ1Tide.EpcFailoverJsonMirrorAgeSec();
        var rebuilt = PhpPlanQ1Tide.EpcFailoverCurrentStatus(false);
        PhpPlanQ1Tide.PutFile(json, "{\"mode\":\"backup_active\",\"label\":\"cached-auto\"}\n");
        var autoHit = PhpPlanQ1Tide.EpcFailoverCurrentStatus(true);
        PhpPlanQ1Tide.PutFile(json, "{\"mode\":\"0\"}\n");
        var emptyMode = PhpPlanQ1Tide.EpcFailoverReadJsonMirror();
        return new Rendered(new object?[]
        {
            fresh,
            ageFresh is < 5 ? 0 : ageFresh,
            cached,
            ageStale,
            rebuilt,
            autoHit,
            emptyMode
        });
    }

    private static object? Freeze(object? value)
    {
        switch (value)
        {
            case string s:
                if (s.StartsWith("2026-10-10T", StringComparison.Ordinal) || (s.Length >= 19 && s[4] == '-' && s[10] == 'T'))
                {
                    return FrozenIso;
                }

                return s.Replace("/docroot", "DOCROOT", StringComparison.Ordinal);
            case int n when n is >= 150 and <= 250:
                return 200;
            case Dictionary<string, object?> map:
                return map.ToDictionary(kv => kv.Key, kv => Freeze(kv.Value), StringComparer.Ordinal);
            case Dictionary<string, string> map:
                return map.ToDictionary(kv => kv.Key, kv => (object?)Freeze(kv.Value), StringComparer.Ordinal);
            case List<string> list:
                return list.Select(Freeze).ToList();
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
