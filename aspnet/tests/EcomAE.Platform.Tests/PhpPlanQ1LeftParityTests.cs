using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1LeftParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Left");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpPlanQ1Left.PageCachePath,
        PhpPlanQ1Left.IsolationAnomalyPath,
        PhpPlanQ1Left.IntegrationsHubCssPath,
        PhpPlanQ1Left.IndustrySettingsCssPath,
        PhpPlanQ1Left.MarketingBroadcastCssPath,
        PhpPlanQ1Left.BrochureInventoryPath,
        PhpPlanQ1Left.CapabilityGuidesPath
    };

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
    public void PlanQ1Left_MatchPhpGolden()
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
        => Assert.Equal(CoveredFiles, new HashSet<string>(StringComparer.Ordinal)
        {
            CaseFile("cache_enabled"),
            CaseFile("anom_empty"),
            CaseFile("css_inthub"),
            CaseFile("css_inds"),
            CaseFile("css_mb"),
            CaseFile("brochure_inv"),
            CaseFile("cap_guides")
        });

    [Fact]
    public void PageCacheEnabled_DoesNotStartASession()
    {
        var req = new PhpPlanQ1Left.PageCacheRequest { Method = "GET", RequestUri = "/en/shop" };
        Assert.True(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        Assert.Empty(req.Cookies);
    }

    private static string CaseFile(string name)
        => name.StartsWith("cache_", StringComparison.Ordinal) ? PhpPlanQ1Left.PageCachePath
            : name.StartsWith("anom_", StringComparison.Ordinal) ? PhpPlanQ1Left.IsolationAnomalyPath
            : name == "css_inthub" ? PhpPlanQ1Left.IntegrationsHubCssPath
            : name == "css_inds" ? PhpPlanQ1Left.IndustrySettingsCssPath
            : name == "css_mb" ? PhpPlanQ1Left.MarketingBroadcastCssPath
            : name == "brochure_inv" ? PhpPlanQ1Left.BrochureInventoryPath
            : name == "cap_guides" ? PhpPlanQ1Left.CapabilityGuidesPath
            : "unknown";

    private static string Render(string name)
        => name switch
        {
            "cache_enabled" => Json(CacheEnabled()),
            "cache_key" => Json(CacheKey()),
            "cache_dir" => Json(CacheDir()),
            "cache_serve_flush" => Json(CacheServeFlush()),
            "anom_empty" => Json(AnomEmpty()),
            "anom_seed" => Json(AnomSeed()),
            "css_inthub" => Json(CssTriple(PhpPlanQ1Left.EpcIntegrationsHubCss(), 24, 20)),
            "css_inds" => Json(CssTriple(PhpPlanQ1Left.EpcIndustrySettingsCss(), 20, 20)),
            "css_mb" => Json(CssTriple(PhpPlanQ1Left.EpcMarketingBroadcastCss(), 28, 20)),
            "brochure_inv" => Json(BrochureInv()),
            "cap_guides" => Json(CapGuides()),
            _ => "unknown:" + name
        };

    private static object[] CacheEnabled()
    {
        var req = NewCacheReq();
        req.Method = "GET";
        req.RequestUri = "/en/shop";
        var o = new List<object> { PhpPlanQ1Left.EpcPageCacheEnabled(req) };
        req.Method = "POST";
        o.Add(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        req.Method = "GET";
        req.Post["x"] = "1";
        o.Add(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        req.Post.Clear();
        req.Cookies["PHPSESSID"] = "abc";
        o.Add(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        req.Cookies.Remove("PHPSESSID");
        req.Cookies["admin_hash"] = "h";
        o.Add(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        req.Cookies.Remove("admin_hash");
        req.Cookies["epc_user_id"] = "1";
        o.Add(PhpPlanQ1Left.EpcPageCacheEnabled(req));
        req.Cookies.Clear();
        foreach (var u in new[]
                 {
                     "/cp/orders", "/bos", "/api/x", "/epc-api/v1", "/admin", "/en/brochure", "/brochure-cp",
                     "/ar/brochure/cp", "/shop?nocache=1", "/shop?epc_debug=1", "/en/shop/search"
                 })
        {
            req.Get.Clear();
            req.RequestUri = u;
            if (u.Contains("nocache", StringComparison.Ordinal))
            {
                req.Get["nocache"] = "1";
            }

            if (u.Contains("epc_debug", StringComparison.Ordinal))
            {
                req.Get["epc_debug"] = "1";
            }

            o.Add(u + "=" + (PhpPlanQ1Left.EpcPageCacheEnabled(req) ? "1" : "0"));
        }

        return o.ToArray();
    }

    private static object[] CacheKey()
    {
        var req = NewCacheReq();
        req.HttpHost = "Auto.ecomae.com:443";
        req.RequestUri = "/en/shop/search?q=hi";
        req.Cookies["lang"] = "EN-US";
        var a = PhpPlanQ1Left.EpcPageCacheKey(req);
        req.HttpHost = "bad host!";
        req.RequestUri = "/x y/z";
        req.Cookies.Clear();
        var b = PhpPlanQ1Left.EpcPageCacheKey(req);
        return new object[] { a, b, a.StartsWith("page_", StringComparison.Ordinal) };
    }

    private static object CacheDir()
    {
        var req = NewCacheReq();
        var dir = PhpPlanQ1Left.EpcPageCacheDir(req);
        return dir.EndsWith("/content/files/epc_page_cache", StringComparison.Ordinal) && Directory.Exists(dir);
    }

    private static object[] CacheServeFlush()
    {
        var req = NewCacheReq();
        req.Method = "GET";
        req.RequestUri = "/en/ok";
        req.HttpHost = "t.ecomae.com";
        var dir = PhpPlanQ1Left.EpcPageCacheDir(req);
        var file = dir + "/" + PhpPlanQ1Left.EpcPageCacheKey(req) + ".html";
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        if (File.Exists(file + ".meta"))
        {
            File.Delete(file + ".meta");
        }

        var miss = PhpPlanQ1Left.EpcPageCacheTryServe(req, out _);
        File.WriteAllText(file, "short");
        var shortHit = PhpPlanQ1Left.EpcPageCacheTryServe(req, out _);
        File.WriteAllText(file, new string('x', 120));
        var noDoctype = PhpPlanQ1Left.EpcPageCacheTryServe(req, out _);
        File.WriteAllText(file, "<!doctype html><html><body class=\"epc-platform-status\">Service update</body></html>");
        var splash = PhpPlanQ1Left.EpcPageCacheTryServe(req, out _);
        File.WriteAllText(file, "<!doctype html><html><body>Hello cache page content here</body></html>");
        File.WriteAllText(file + ".meta", JsonSerializer.Serialize(new Dictionary<string, object?> { ["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60 }));
        var hit = PhpPlanQ1Left.EpcPageCacheTryServe(req, out var hitHtml);
        File.WriteAllText(file + ".meta", JsonSerializer.Serialize(new Dictionary<string, object?> { ["exp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10 }));
        var exp = PhpPlanQ1Left.EpcPageCacheTryServe(req, out _);
        req.Active = true;
        req.Ttl = 90;
        var html = "<!doctype html><html><body>" + new string('Z', 200) + "</html>";
        PhpPlanQ1Left.EpcPageCacheFlush(req, html);
        var flushed = File.Exists(file) && Encoding.UTF8.GetByteCount(File.ReadAllText(file)) > 200;
        var purged = PhpPlanQ1Left.EpcPageCachePurgeAll(req);
        return new object[] { miss, shortHit, noDoctype, splash, hit, hitHtml.Length >= 15 ? hitHtml[..15] : hitHtml, exp, flushed, purged > 0 };
    }

    private static object?[] AnomEmpty()
    {
        var store = new PhpPlanQ1Left.AnomalyStore { ViolationsTable = false, MfaTable = false };
        var r = PhpPlanQ1Left.EpcAnomalyScan(store);
        r.Remove("scan_time");
        return new object[] { PhpPlanQ1Left.EpcIsolationAnomalyVersion, r, PhpPlanQ1Left.EpcAnomalyFleetStats(store) };
    }

    private static object?[] AnomSeed()
    {
        var now = new DateTime(2026, 10, 9, 18, 0, 0, DateTimeKind.Utc);
        var store = new PhpPlanQ1Left.AnomalyStore { Clock = () => now };
        for (var i = 0; i < 12; i++)
        {
            store.Violations.Add(new PhpPlanQ1Left.ViolationRow { SiteKey = "siteA", Ip = "1.1.1.1", CreatedAt = now });
        }

        for (var i = 0; i < 3; i++)
        {
            store.Violations.Add(new PhpPlanQ1Left.ViolationRow { SiteKey = "siteB", Ip = "1.1.1.1", CreatedAt = now });
        }

        var night = new DateTime(2026, 10, 9, 3, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 6; i++)
        {
            store.Violations.Add(new PhpPlanQ1Left.ViolationRow { SiteKey = "siteC", Ip = "2.2.2.2", CreatedAt = night });
        }

        for (var i = 0; i < 7; i++)
        {
            store.MfaLog.Add(new PhpPlanQ1Left.MfaAuditRow { UserId = 9, Action = "verify_failed", Success = 0, CreatedAt = now });
        }

        var r = PhpPlanQ1Left.EpcAnomalyScan(store);
        var details = (Dictionary<string, object?>)r["details"]!;
        var spikes = (Dictionary<string, object?>)details["violation_spikes"]!;
        var spikeAnoms = (List<Dictionary<string, object?>>)spikes["anomalies"]!;
        var multi = (Dictionary<string, object?>)details["multi_tenant_ip"]!;
        var multiAnoms = (List<Dictionary<string, object?>>)multi["anomalies"]!;
        var after = (Dictionary<string, object?>)details["after_hours"]!;
        var afterAnoms = (List<Dictionary<string, object?>>)after["anomalies"]!;
        var mfa = (Dictionary<string, object?>)details["mfa_bypass"]!;
        var mfaAnoms = (List<Dictionary<string, object?>>)mfa["anomalies"]!;
        var fleet = PhpPlanQ1Left.EpcAnomalyFleetStats(store);
        var id = PhpPlanQ1Left.EpcAnomalyRecord(store, "siteZ", "manual", new Dictionary<string, object?>
        {
            ["severity"] = "low",
            ["description"] = "note",
            ["risk_score"] = 3
        });
        var res = PhpPlanQ1Left.EpcAnomalyResolve(store, id, 7, "ok");
        var fleet2 = PhpPlanQ1Left.EpcAnomalyFleetStats(store);
        return new object?[]
        {
            r["scanned"],
            r["anomalies_found"],
            spikes["check"],
            spikeAnoms.Count,
            spikeAnoms.Count > 0 ? spikeAnoms[0]["type"] : null,
            multiAnoms.Count > 0 ? multiAnoms[0]["type"] : null,
            afterAnoms.Count > 0 ? afterAnoms[0]["severity"] : null,
            mfaAnoms.Count > 0 ? mfaAnoms[0]["type"] : null,
            fleet["unresolved_total"],
            id > 0,
            res,
            fleet2["unresolved_total"]
        };
    }

    private static object[] CssTriple(string css, int head, int tail)
        => new object[]
        {
            Encoding.UTF8.GetByteCount(css),
            css.Length >= head ? css[..head] : css,
            css.Length >= tail ? css[^tail..] : css
        };

    private static object?[] BrochureInv()
    {
        var inv = PhpPlanQ1Left.EpcCpBrochureInventory();
        var keys = inv.EnumerateObject().Select(p => p.Name).ToArray();
        var n = inv.EnumerateObject().Sum(p => p.Value.GetArrayLength());
        var portal0 = inv.GetProperty("Portal")[0];
        return new object?[] { keys, n, portal0.GetProperty("name").GetString(), portal0.GetProperty("scope").GetString(), portal0.GetProperty("url").GetString() };
    }

    private static object?[] CapGuides()
    {
        var g = PhpPlanQ1Left.EpcEcomaePlatformCapabilityGuides();
        var keys = g.EnumerateObject().Select(p => p.Name).ToArray();
        var first = g.GetProperty(keys[0]);
        var last = g.GetProperty(keys[^1]);
        return new object?[]
        {
            keys.Length,
            keys[0],
            first.GetProperty("guide_area").GetString(),
            first.GetProperty("steps").GetArrayLength(),
            keys[^1],
            last.GetProperty("image").GetString()
        };
    }

    private static PhpPlanQ1Left.PageCacheRequest NewCacheReq()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1l_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "files", "epc_page_cache"));
        return new PhpPlanQ1Left.PageCacheRequest { DocumentRoot = doc };
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
