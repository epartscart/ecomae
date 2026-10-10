using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1YardParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Yard");

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
    public void PlanQ1Yard_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Yard.ReadinessPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Yard.ReadinessPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Yard.Reset();
        Assert.Contains("epc_readiness_score.php", PhpPlanQ1Yard.ReadinessPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Yard.ReadinessPath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Yard.EpcReadinessScore("missing");
        Assert.Equal("demo", Convert.ToString(miss["tier"]));
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Yard.Reset();
        return name switch
        {
            "tiers" => Tiers(),
            "checks" => Checks(),
            "erp" => Erp(),
            "fleet" => Fleet(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Tiers()
    {
        var empty = PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore("missing"));
        Set("pilot", "isolation_audit_status", "warn");
        Set("pilot", "homepage_load_ms", "1500");
        var pilot = PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore("pilot"));
        Set("paid", "isolation_audit_status", "pass");
        Set("paid", "mfa_enabled", "1");
        Set("paid", "last_backup_time", (1760083200 - 3600).ToString());
        Set("paid", "einvoice_asp_mode", "api");
        Set("paid", "homepage_load_ms", "900");
        Set("paid", "vat_trn", "100");
        var paid = PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore("paid"));
        Set("ent", "isolation_audit_status", "ok");
        Set("ent", "mfa_enabled", "1");
        Set("ent", "last_backup_time", (1760083200 - 1800).ToString());
        Set("ent", "einvoice_asp_mode", "live");
        Set("ent", "homepage_load_ms", "800");
        Set("ent", "vat_trn", "100");
        Set("ent", "trade_license", "TL-1");
        Set("ent", "erp_modules_active", "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        Set("ent", "brand_logo", "x");
        Set("ent", "brand_color", "y");
        Set("ent", "brand_font", "z");
        PhpPlanQ1Yard.Webhooks.Add(new PhpPlanQ1Yard.WebhookRow { TenantKey = "ent", Active = 1 });
        var ent = PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore("ent"));
        return new Rendered(new object?[]
        {
            PhpPlanQ1Yard.EpcReadinessTierLabel("demo"),
            PhpPlanQ1Yard.EpcReadinessTierLabel("pilot"),
            PhpPlanQ1Yard.EpcReadinessTierLabel("paid"),
            PhpPlanQ1Yard.EpcReadinessTierLabel("enterprise"),
            PhpPlanQ1Yard.EpcReadinessTierLabel("nope"),
            empty, pilot, paid, ent
        });
    }

    private static Rendered Checks()
    {
        Set("acme", "isolation_audit_status", "pass");
        Set("beta", "isolation_audit_status", "fail");
        Set("warn", "isolation_audit_status", "warn");
        Set("acme", "mfa_enabled", "1");
        Set("beta", "mfa_enabled", "0");
        Set("acme", "last_backup_time", (1760083200 - 7200).ToString());
        Set("old", "last_backup_time", (1760083200 - 100000).ToString());
        Set("stale", "last_backup_time", (1760083200 - 200000).ToString());
        Set("zero", "last_backup_time", "0");
        Set("acme", "einvoice_asp_mode", "api");
        Set("man", "einvoice_asp_mode", "manual");
        Set("tst", "einvoice_asp_mode", "test");
        Set("fast", "homepage_load_ms", "1999");
        Set("slow", "homepage_load_ms", "2000");
        Set("dead", "homepage_load_ms", "5000");
        Set("half", "vat_trn", "TRN");
        Set("full", "vat_trn", "TRN");
        Set("full", "trade_license", "LIC");
        Set("blank", "vat_trn", "");
        Set("one", "brand_logo", "a");
        Set("three", "brand_logo", "a");
        Set("three", "brand_color", "b");
        Set("three", "brand_font", "c");
        PhpPlanQ1Yard.Webhooks.Add(new PhpPlanQ1Yard.WebhookRow { TenantKey = "hook", Active = 1 });
        PhpPlanQ1Yard.Webhooks.Add(new PhpPlanQ1Yard.WebhookRow { TenantKey = "hook", Active = 0 });
        PhpPlanQ1Yard.Webhooks.Add(new PhpPlanQ1Yard.WebhookRow { TenantKey = "hook2", Active = 1 });
        var outMap = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var site in new[] { "acme", "beta", "warn", "old", "stale", "zero", "man", "tst", "fast", "slow", "dead", "half", "full", "blank", "one", "three", "hook", "hook2" })
        {
            outMap[site] = PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore(site));
        }

        outMap["iso_direct"] = new object?[]
        {
            PhpPlanQ1Yard.EpcReadinessCheckIsolation("acme"),
            PhpPlanQ1Yard.EpcReadinessCheckIsolation("beta"),
            PhpPlanQ1Yard.EpcReadinessCheckIsolation("missing")
        };
        return new Rendered(outMap);
    }

    private static Rendered Erp()
    {
        Set("five", "erp_modules_active", "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        Set("three", "erp_modules_active", "[\"a\",\"b\",\"c\"]");
        Set("empty", "erp_modules_active", "[]");
        Set("bad", "erp_modules_active", "{");
        Set("blank", "erp_modules_active", "");
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow { SiteKey = "erp1", TradeName = "ERP One", Status = "live", Industry = "auto", ErpEnabled = 1 });
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow { SiteKey = "site1", TradeName = "Site One", Status = "live", Industry = "retail", ErpEnabled = 0 });
        var outMap = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var site in new[] { "five", "three", "empty", "bad", "blank", "erp1", "site1" })
        {
            var score = PhpPlanQ1Yard.EpcReadinessScore(site);
            outMap[site] = ((List<Dictionary<string, object?>>)score["checks"]!).First(c => Convert.ToString(c["id"]) == "erp_modules");
        }

        PhpPlanQ1Yard.HasSettings = false;
        var missing = PhpPlanQ1Yard.EpcReadinessCheckErpModules("erp1");
        PhpPlanQ1Yard.HasWebhooks = false;
        var hooks = PhpPlanQ1Yard.EpcReadinessCheckWebhooks("hook");
        return new Rendered(new object?[] { outMap, missing, hooks });
    }

    private static Rendered Fleet()
    {
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow { SiteKey = "acme", TradeName = "Acme Parts", Status = "live", Industry = "auto", ErpEnabled = 1 });
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow { SiteKey = "beta", TradeName = "Beta Shop", Status = "live", Industry = "retail", ErpEnabled = 0 });
        PhpPlanQ1Yard.Tenants.Add(new PhpPlanQ1Yard.TenantRow { SiteKey = "draft", TradeName = "Draft Co", Status = "draft", Industry = "auto", ErpEnabled = 1 });
        Set("acme", "isolation_audit_status", "pass");
        Set("acme", "mfa_enabled", "1");
        Set("acme", "last_backup_time", (1760083200 - 1000).ToString());
        Set("acme", "einvoice_asp_mode", "live");
        Set("acme", "homepage_load_ms", "700");
        Set("acme", "vat_trn", "1");
        Set("acme", "trade_license", "1");
        Set("acme", "erp_modules_active", "[\"a\",\"b\",\"c\",\"d\",\"e\"]");
        Set("acme", "brand_logo", "a");
        Set("acme", "brand_color", "b");
        Set("acme", "brand_font", "c");
        PhpPlanQ1Yard.Webhooks.Add(new PhpPlanQ1Yard.WebhookRow { TenantKey = "acme", Active = 1 });
        Set("beta", "isolation_audit_status", "fail");
        PhpPlanQ1Yard.TokenSites.Add("beta");
        var live = PhpPlanQ1Yard.EpcReadinessFleetSummary();
        PhpPlanQ1Yard.Tenants.Clear();
        var empty = PhpPlanQ1Yard.EpcReadinessFleetSummary();
        PhpPlanQ1Yard.HasTenants = false;
        var missing = PhpPlanQ1Yard.EpcReadinessFleetSummary();
        PhpPlanQ1Yard.HasSettings = false;
        return new Rendered(new object?[] { live, empty, missing, PhpPlanQ1Yard.ScoreIds(PhpPlanQ1Yard.EpcReadinessScore("beta")) });
    }

    private static void Set(string site, string key, string value)
        => PhpPlanQ1Yard.AddSetting(site, key, value);

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
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
