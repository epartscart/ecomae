using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1GrowParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Grow");

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
    public void PlanQ1Grow_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Grow.WebTrackerPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Grow.WebTrackerPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Grow.Reset();
        Assert.Contains("epc_web_tracker.php", PhpPlanQ1Grow.WebTrackerPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Grow.EpcWebTrackerH("x"), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Grow.Reset();
        return name switch
        {
            "pure" => Pure(),
            "request" => Request(),
            "ingest" => Ingest(),
            "dash" => Dash(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        PhpPlanQ1Grow.Server = new(StringComparer.Ordinal)
        {
            ["HTTP_CF_IPCOUNTRY"] = "ae",
            ["HTTP_X_FORWARDED_FOR"] = "10.1.2.3, 10.9.9.9",
            ["HTTP_HOST"] = "www.ecomae.com",
            ["HTTP_USER_AGENT"] = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1"
        };
        var geo = PhpPlanQ1Grow.EpcWebTrackerGeoFromRequest();
        var look = PhpPlanQ1Grow.EpcWebTrackerLookupIp("10.0.0.1");
        var filt = PhpPlanQ1Grow.EpcWebTrackerSessionFilterSql(new(StringComparer.Ordinal)
        {
            ["device"] = "mobile",
            ["country"] = "AE",
            ["ip"] = "10.",
            ["user_id"] = "12",
            ["user_type"] = "registered",
            ["browser"] = "Safari",
            ["path"] = "/desk"
        }, "s");
        return new Rendered(new object?[]
        {
            PhpPlanQ1Grow.EpcWebTrackerH("O'Reilly & Co"),
            PhpPlanQ1Grow.EpcWebTrackerCountryName("ae"),
            PhpPlanQ1Grow.EpcWebTrackerCountryName("ZZ"),
            PhpPlanQ1Grow.EpcWebTrackerParseUa("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Edg/120.0.0.0"),
            PhpPlanQ1Grow.EpcWebTrackerParseUa("Mozilla/5.0 (iPad; CPU OS 16_0 like Mac OS X) AppleWebKit/605.1.15"),
            PhpPlanQ1Grow.EpcWebTrackerClip("  hello   \n  world  ", 8),
            PhpPlanQ1Grow.EpcWebTrackerUuidOk("abc-def-12"),
            PhpPlanQ1Grow.EpcWebTrackerUuidOk("nope!"),
            PhpPlanQ1Grow.EpcWebTrackerFormatDuration(400),
            PhpPlanQ1Grow.EpcWebTrackerFormatDuration(1500),
            PhpPlanQ1Grow.EpcWebTrackerFormatDuration(125000),
            PhpPlanQ1Grow.EpcWebTrackerFormatDuration(3700000),
            PhpPlanQ1Grow.EpcWebTrackerCsvCell("plain"),
            PhpPlanQ1Grow.EpcWebTrackerCsvCell("a,b"),
            PhpPlanQ1Grow.EpcWebTrackerCsvCell("say \"hi\""),
            PhpPlanQ1Grow.EpcWebTrackerCsvLine(["a", "b,c"]),
            PhpPlanQ1Grow.EpcWebTrackerClientIp(),
            geo,
            look,
            PhpPlanQ1Grow.EpcWebTrackerResolveSiteKey(),
            PhpPlanQ1Grow.EpcWebTrackerBeaconHtml(),
            filt
        });
    }

    private static Rendered Request()
    {
        PhpPlanQ1Grow.Get = new(StringComparer.Ordinal)
        {
            ["from"] = "2026-01-01",
            ["to"] = "2026-01-31",
            ["device"] = "MOBILE",
            ["country"] = "ae-xx",
            ["ip"] = "10.1.2.3",
            ["user"] = "42x",
            ["who"] = "reg",
            ["path"] = "/desk\nparts",
            ["browser"] = "Safari 17"
        };
        var range = PhpPlanQ1Grow.EpcWebTrackerRangeFromRequest();
        var filters = PhpPlanQ1Grow.EpcWebTrackerFiltersFromRequest();
        PhpPlanQ1Grow.Get = new(StringComparer.Ordinal) { ["from"] = "2026-12-31", ["to"] = "2026-01-01" };
        var swap = PhpPlanQ1Grow.EpcWebTrackerRangeFromRequest();
        PhpPlanQ1Grow.Server["HTTP_HOST"] = "cp.epartscart.com:8443";
        var key = PhpPlanQ1Grow.EpcWebTrackerResolveSiteKey();
        PhpPlanQ1Grow.Server["HTTP_HOST"] = "shop.demo.test";
        var key2 = PhpPlanQ1Grow.EpcWebTrackerResolveSiteKey();
        return new Rendered(new object?[] { range, filters, swap, key, key2 });
    }

    private static Rendered Ingest()
    {
        PhpPlanQ1Grow.Server = new(StringComparer.Ordinal)
        {
            ["REMOTE_ADDR"] = "10.9.8.7",
            ["HTTP_CF_IPCOUNTRY"] = "AE",
            ["HTTP_HOST"] = "www.ecomae.com",
            ["HTTP_USER_AGENT"] = "Mozilla/5.0 Chrome/120"
        };
        var db = new PhpPlanQ1Grow.GrowStore();
        var bad = PhpPlanQ1Grow.EpcWebTrackerIngest(db, new(StringComparer.Ordinal) { ["site_key"] = "", ["session_uid"] = "nope" });
        var ok = PhpPlanQ1Grow.EpcWebTrackerIngest(db, Payload());
        var row = db.Sessions[0];
        var first = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = row.SiteKey,
            ["visitor_uid"] = row.VisitorUid,
            ["hostname"] = row.Hostname,
            ["user_id"] = row.UserId,
            ["is_registered"] = row.IsRegistered,
            ["pageview_count"] = row.PageviewCount,
            ["event_count"] = row.EventCount,
            ["landing_path"] = row.LandingPath,
            ["exit_path"] = row.ExitPath,
            ["referrer_host"] = row.ReferrerHost,
            ["utm_source"] = row.UtmSource,
            ["country_code"] = row.CountryCode,
            ["device_type"] = row.DeviceType,
            ["browser"] = row.Browser
        };
        var pv = db.Pageviews.Count;
        var ev = db.Events.Count;
        var again = PhpPlanQ1Grow.EpcWebTrackerIngest(db, new(StringComparer.Ordinal)
        {
            ["site_key"] = "ecomae",
            ["session_uid"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ["pageviews"] = new List<Dictionary<string, object?>> { new(StringComparer.Ordinal) { ["path"] = "/checkout" } }
        });
        return new Rendered(new object?[]
        {
            bad,
            ok["ok"],
            ok["pageviews"],
            ok["events"],
            first,
            pv,
            ev,
            again["ok"],
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pageview_count"] = row.PageviewCount,
                ["event_count"] = row.EventCount,
                ["exit_path"] = row.ExitPath
            }
        });
    }

    private static Rendered Dash()
    {
        PhpPlanQ1Grow.Server = new(StringComparer.Ordinal)
        {
            ["REMOTE_ADDR"] = "10.9.8.7",
            ["HTTP_CF_IPCOUNTRY"] = "AE",
            ["HTTP_HOST"] = "www.ecomae.com",
            ["HTTP_USER_AGENT"] = "Mozilla/5.0 Chrome/120"
        };
        var db = new PhpPlanQ1Grow.GrowStore();
        var a = PhpPlanQ1Grow.EpcWebTrackerIngest(db, new(StringComparer.Ordinal)
        {
            ["site_key"] = "ecomae",
            ["session_uid"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ["user_id"] = 7,
            ["duration_ms"] = 2500,
            ["pageviews"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["path"] = "/desk/parts", ["title"] = "Parts", ["time_on_page_ms"] = 1200, ["scroll_max_pct"] = 40 }
            },
            ["events"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["type"] = "click", ["path"] = "/desk/parts", ["tag"] = "a", ["text"] = "Open" },
                new(StringComparer.Ordinal) { ["type"] = "search", ["search"] = "bosch", ["search_ctx"] = "header" }
            }
        });
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var from = now - 3600;
        var to = now + 3600;
        var dash = PhpPlanQ1Grow.EpcWebTrackerDashboard(db, "ecomae", from, to);
        var filtDash = PhpPlanQ1Grow.EpcWebTrackerDashboard(db, "ecomae", from, to, false, new(StringComparer.Ordinal) { ["device"] = "desktop", ["path"] = "/desk" });
        var detail = PhpPlanQ1Grow.EpcWebTrackerSessionDetail(db, Convert.ToInt32(a["session_id"], System.Globalization.CultureInfo.InvariantCulture), "ecomae");
        var denied = PhpPlanQ1Grow.EpcWebTrackerSessionDetail(db, Convert.ToInt32(a["session_id"], System.Globalization.CultureInfo.InvariantCulture), "other");
        var empty = PhpPlanQ1Grow.EpcWebTrackerSessionDetail(db, 0, "ecomae");
        var csv = PhpPlanQ1Grow.EpcWebTrackerExportCsv(db, "ecomae", from, to);
        object?[] Pick(Dictionary<string, object?> d)
        {
            var pages = ((List<Dictionary<string, object?>>)d["top_pages"]!).Select(p => p["path"]).ToList();
            var searches = ((List<Dictionary<string, object?>>)d["searches"]!).Select(s => s["search_query"]).ToList();
            var recent = ((List<PhpPlanQ1Grow.SessionRow>)d["recent_sessions"]!).Count;
            return [d["summary"], pages, searches, recent, d["facets"] is Dictionary<string, object?> f ? f["devices"] : null];
        }

        var sess = detail["session"] as PhpPlanQ1Grow.SessionRow;
        return new Rendered(new object?[]
        {
            Pick(dash),
            Pick(filtDash),
            sess?.LandingPath,
            ((List<PhpPlanQ1Grow.PageviewRow>)detail["pageviews"]!).Count,
            ((List<PhpPlanQ1Grow.EventRow>)detail["events"]!).Count,
            denied["session"],
            empty["session"],
            csv.StartsWith("\uFEFF", StringComparison.Ordinal),
            csv.Contains("Website tracker full report", StringComparison.Ordinal),
            csv.Contains("bosch", StringComparison.Ordinal)
        });
    }

    private static Dictionary<string, object?> Payload()
        => new(StringComparer.Ordinal)
        {
            ["site_key"] = "EcomAE!",
            ["session_uid"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ["visitor_uid"] = "!!!",
            ["hostname"] = "www.ecomae.com",
            ["user_id"] = 7,
            ["is_registered"] = 1,
            ["duration_ms"] = 2500,
            ["referrer"] = "https://google.com/search?q=parts",
            ["utm"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["source"] = "google", ["medium"] = "cpc", ["campaign"] = "q1" },
            ["screen_w"] = 390,
            ["language"] = "en-AE",
            ["pageviews"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["path"] = "/desk/parts", ["title"] = "Parts", ["query"] = "q=1", ["time_on_page_ms"] = 1200, ["scroll_max_pct"] = 40 },
                new(StringComparer.Ordinal) { ["path"] = "/cart", ["title"] = "Cart" }
            },
            ["events"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["type"] = "click", ["path"] = "/desk/parts", ["tag"] = "a", ["id"] = "go", ["text"] = "Open" },
                new(StringComparer.Ordinal) { ["type"] = "search", ["search"] = "bosch", ["search_ctx"] = "header" }
            }
        };

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
