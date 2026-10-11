using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1LeadParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Lead");

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
    public void PlanQ1Lead_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Lead.ClassificationPath,
                PhpPlanQ1Lead.MarketingPagesPath,
                PhpPlanQ1Lead.TopAlertsPath
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                PhpPlanQ1Lead.ClassificationPath,
                PhpPlanQ1Lead.MarketingPagesPath,
                PhpPlanQ1Lead.TopAlertsPath
            });

    [Fact]
    public void Classify_DoesNotStartASession()
    {
        PhpPlanQ1Lead.Reset();
        Assert.Equal("Uncategorized", PhpPlanQ1Lead.EpcAiClassify("")["category"]);
    }

    private static string Render(string name)
    {
        PhpPlanQ1Lead.Reset();
        return name switch
        {
            "class_pure" => Json(ClassPure()),
            "class_db" => Json(ClassDb()),
            "mkt_bits" => Json(MktBits()),
            "mkt_pages" => Json(MktPages()),
            "alerts" => Json(Alerts()),
            _ => "unknown:" + name
        };
    }

    private static object?[] ClassPure()
    {
        var rules = PhpPlanQ1Lead.EpcAiCategoryRules();
        return
        [
            rules.Count,
            rules[0]["category"],
            rules[0]["hs_prefix"],
            PhpPlanQ1Lead.EpcAiClassify(""),
            PhpPlanQ1Lead.EpcAiClassify("   "),
            PhpPlanQ1Lead.EpcAiClassify("brake pad disc rotor"),
            PhpPlanQ1Lead.EpcAiClassify("gold diamond ring necklace"),
            PhpPlanQ1Lead.EpcAiClassify("xyzzy"),
            PhpPlanQ1Lead.EpcAiClassify("oil")
        ];
    }

    private static object?[] ClassDb()
    {
        var db = new PhpPlanQ1Lead.ClassStore();
        var seed1 = PhpPlanQ1Lead.EpcAiSeedHsCodes(db);
        var seed2 = PhpPlanQ1Lead.EpcAiSeedHsCodes(db);
        var store = PhpPlanQ1Lead.EpcAiClassifyAndStore(db, "site-a", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = 11,
            ["sku"] = "SKU-1",
            ["name"] = "Brake disc pad",
            ["description"] = "rotor caliper",
            ["brand"] = "Brembo"
        });
        var batch = PhpPlanQ1Lead.EpcAiClassifyBatch(db, "site-a", new[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["product_id"] = 12, ["sku"] = "P", ["name"] = "unknown widget" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 13, ["name"] = "laptop computer tablet" }
        });
        var hsCode = PhpPlanQ1Lead.EpcAiHsLookup(db, "8409");
        var hsText = PhpPlanQ1Lead.EpcAiHsLookup(db, "filter");
        var stats = PhpPlanQ1Lead.EpcAiClassStats(db, "site-a");
        var fleet = PhpPlanQ1Lead.EpcAiClassFleetStats(db);
        var ok = PhpPlanQ1Lead.EpcAiReview(db, Convert.ToInt32(store["classification_id"], System.Globalization.CultureInfo.InvariantCulture), "Auto Parts", "Brakes", "8708", 9);
        var after = PhpPlanQ1Lead.EpcAiClassStats(db, "site-a");
        return
        [
            seed1,
            seed2,
            store["ok"],
            store["category"],
            store["method"],
            Convert.ToInt32(store["classification_id"], System.Globalization.CultureInfo.InvariantCulture) > 0,
            batch["total"],
            batch["classified"],
            batch["uncategorized"],
            hsCode.Select(r => r["code"]).ToList(),
            hsText.Select(r => r["code"]).ToList(),
            stats,
            fleet[0]["site_key"],
            fleet[0]["total_classified"],
            ok,
            after
        ];
    }

    private static object?[] MktBits()
    {
        var docs = FirstKey(PhpPlanQ1Done.EpcEcomaeDocsCatalog());
        var cmp = FirstKey(PhpPlanQ1Done.EpcEcomaeCompareCatalog());
        var bos = FirstKey(PhpPlanQ1Done.EpcEcomaeBosArticlesCatalog());
        var sol = FirstKey(PhpPlanQ1Done.EpcEcomaeSolutionsCatalog());
        var faqs = new List<object?[]> { new object?[] { "Q1", "A1 & more" }, new object?[] { "Q2", "A2" } };
        return
        [
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("nope"),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("docs"),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("docs", Dict("slug", docs)),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("compare"),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("compare", Dict("slug", cmp)),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("bos"),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("solution"),
            PhpPlanQ1Lead.EpcEcomaeMarketingMeta("docs", Dict("slug", "Weird Slug!")),
            PhpPlanQ1Lead.EpcEcomaeMarketingFaqJsonld(Array.Empty<object?[]>()),
            PhpPlanQ1Lead.EpcEcomaeMarketingFaqJsonld(faqs),
            PhpPlanQ1Lead.EpcEcomaeMarketingArticleJsonld("Head", "Desc", "https://x.test/a"),
            PhpPlanQ1Lead.EpcEcomaeMarketingFaqHtml(Array.Empty<object?[]>()),
            PhpPlanQ1Lead.EpcEcomaeMarketingFaqHtml(faqs),
            PhpPlanQ1Lead.EpcEcomaeMarketingCrumb("Here", "/hub", "Hub"),
            docs,
            cmp,
            bos,
            sol
        ];
    }

    private static object?[] MktPages()
    {
        var docs = FirstKey(PhpPlanQ1Done.EpcEcomaeDocsCatalog());
        var cmp = FirstKey(PhpPlanQ1Done.EpcEcomaeCompareCatalog());
        var bos = FirstKey(PhpPlanQ1Done.EpcEcomaeBosArticlesCatalog());
        var sol = FirstKey(PhpPlanQ1Done.EpcEcomaeSolutionsCatalog());
        var d0 = PhpPlanQ1Lead.EpcEcomaePlatformPageDocs();
        var d1 = PhpPlanQ1Lead.EpcEcomaePlatformPageDocs(Dict("slug", docs));
        var c0 = PhpPlanQ1Lead.EpcEcomaePlatformPageCompare();
        var c1 = PhpPlanQ1Lead.EpcEcomaePlatformPageCompare(Dict("slug", cmp));
        var b0 = PhpPlanQ1Lead.EpcEcomaePlatformPageBos();
        var b1 = PhpPlanQ1Lead.EpcEcomaePlatformPageBos(Dict("slug", bos));
        var s0 = PhpPlanQ1Lead.EpcEcomaePlatformPageSolution();
        var s1 = PhpPlanQ1Lead.EpcEcomaePlatformPageSolution(Dict("slug", sol));
        return
        [
            Pick(d0), Pick(d1), Pick(c0), Pick(c1), Pick(b0), Pick(b1), Pick(s0), Pick(s1),
            d1.Contains("Blockchain BOS overview", StringComparison.Ordinal) || d1.Contains("documentation", StringComparison.Ordinal),
            c1.Contains("ECOM AE", StringComparison.Ordinal),
            s1.Contains("Get a demo", StringComparison.Ordinal)
        ];
    }

    private static object?[] Alerts()
    {
        var root = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1l_cs_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(root, "content"));
        try
        {
            PhpPlanQ1Lead.DocumentRoot = root;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            PhpPlanQ1Lead.Clock = () => now;
            var db = new PhpPlanQ1Lead.AlertStore();
            PhpPlanQ1Lead.DpConfig = null;
            var empty = PhpPlanQ1Lead.EpcCpHttpsRedirectIsConfigured(null);
            PhpPlanQ1Lead.DpConfig = new Dictionary<string, object?>(StringComparer.Ordinal) { ["epc_https_redirect_external"] = "1" };
            var https1 = PhpPlanQ1Lead.EpcCpHttpsRedirectIsConfigured(PhpPlanQ1Lead.DpConfig);
            PhpPlanQ1Lead.DpConfig = new Dictionary<string, object?>(StringComparer.Ordinal) { ["domain_path"] = "https://shop.test" };
            PhpPlanQ1Lead.Server["HTTPS"] = "on";
            var https2 = PhpPlanQ1Lead.EpcCpHttpsRedirectIsConfigured(PhpPlanQ1Lead.DpConfig);
            PhpPlanQ1Lead.Server.Remove("HTTPS");
            PhpPlanQ1Lead.DpConfig = new Dictionary<string, object?>(StringComparer.Ordinal);
            File.WriteAllText(Path.Combine(root, ".htaccess"), "RewriteEngine On\nRewriteRule ^(.*)$ https://%{HTTP_HOST}%{REQUEST_URI} [L,R=301]\n");
            var https3 = PhpPlanQ1Lead.EpcCpHttpsRedirectIsConfigured(PhpPlanQ1Lead.DpConfig);
            File.Delete(Path.Combine(root, ".htaccess"));
            File.WriteAllText(Path.Combine(root, "content", "epc_https_redirect.ok"), "1");
            PhpPlanQ1Lead.DpConfig = new Dictionary<string, object?>(StringComparer.Ordinal);
            var https4 = PhpPlanQ1Lead.EpcCpHttpsRedirectIsConfigured(PhpPlanQ1Lead.DpConfig);
            var broken = new Dictionary<string, object?>(StringComparer.Ordinal);
            var ok = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var k in new[] { "from_name", "from_email", "smtp_mode", "smtp_encryption", "smtp_host", "smtp_port", "smtp_username", "smtp_password" })
            {
                ok[k] = "x";
            }

            var st3 = PhpPlanQ1Lead.EpcCpEmailNotificationState(db, broken);
            var st4 = PhpPlanQ1Lead.EpcCpEmailNotificationState(db, ok);
            db.Debug.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "email", ["status"] = 1, ["time"] = now });
            var st1 = PhpPlanQ1Lead.EpcCpEmailNotificationState(db, ok);
            db.Debug[0]["time"] = now - 90000;
            var st2 = PhpPlanQ1Lead.EpcCpEmailNotificationState(db, ok);
            var sms3 = PhpPlanQ1Lead.EpcCpSmsNotificationState(db);
            db.SmsActive = 1;
            var sms4 = PhpPlanQ1Lead.EpcCpSmsNotificationState(db);
            db.Debug.Add(new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "sms", ["status"] = 1, ["time"] = now });
            var sms1 = PhpPlanQ1Lead.EpcCpSmsNotificationState(db);
            return
            [
                PhpPlanQ1Lead.EpcCpTopAlertsUseProfessionalHeader(),
                PhpPlanQ1Lead.EpcCpTopAlertsShowInHeader(),
                empty, https1, https2, https3, https4,
                PhpPlanQ1Lead.EpcCpTopAlertsSuppressEmailSms(),
                st3, st4, st1, st2, sms3, sms4, sms1,
                PhpPlanQ1Lead.EpcCpTopAlertsEmailStyles(1),
                PhpPlanQ1Lead.EpcCpTopAlertsEmailStyles(3),
                PhpPlanQ1Lead.EpcCpTopAlertsSmsStyles(4),
                PhpPlanQ1Lead.EpcCpTopAlertsRenderSslItem(),
                PhpPlanQ1Lead.EpcCpTopAlertsRenderEmailItem(db, ok),
                PhpPlanQ1Lead.EpcCpTopAlertsRenderSmsItem(db, ok)
            ];
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static object?[] Pick(string html)
        =>
        [
            Count(html, "epm-card"),
            html.Contains("application/ld+json", StringComparison.Ordinal),
            html.Contains("Frequently asked questions", StringComparison.Ordinal),
            html.Contains("https://www.ecomae.com", StringComparison.Ordinal)
        ];

    private static int Count(string haystack, string needle)
    {
        var n = 0;
        var i = 0;
        while (true)
        {
            var found = haystack.IndexOf(needle, i, StringComparison.Ordinal);
            if (found < 0)
            {
                return n;
            }

            n++;
            i = found + 1;
        }
    }

    private static string FirstKey(JsonElement obj) => obj.EnumerateObject().First().Name;

    private static Dictionary<string, object?> Dict(string key, object? value)
        => new(StringComparer.Ordinal) { [key] = value };

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
        => value.Length <= 500 ? value : value[..500] + "…";
}
