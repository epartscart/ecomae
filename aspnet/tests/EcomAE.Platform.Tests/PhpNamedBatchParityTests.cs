using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpNamedBatchParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NamedBatch");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpNamedBatch.AutoTaxonomyPath,
        PhpNamedBatch.ProductHashPath,
        PhpNamedBatch.GuideDataPath,
        PhpNamedBatch.CrossbaseCachePath,
        PhpNamedBatch.ComplementaryPath,
        PhpNamedBatch.SessionSecurityPath,
        PhpNamedBatch.LegalPagesPath,
        PhpNamedBatch.BrandingPath
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
    public void NamedBatch_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expectedResult = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(actual, expectedResult))
            {
                failures.Add(name + " expected=" + Truncate(expectedResult.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray()
                .Select(CaseFile)
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void SessionHardenIni_DoesNotStartASession()
    {
        var policy = PhpNamedBatch.EpcSessionHardenIni(true);
        Assert.True(policy.CookiesOnly);
        Assert.True(policy.HttpOnly);
        Assert.True(policy.Secure);
        Assert.Equal("Lax", policy.SameSite);
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.GetProperty("name").GetString() switch
        {
            "tax_tree" => PhpNamedBatch.AutoTaxonomyPath,
            var name when name is not null && name.StartsWith("hash_", StringComparison.Ordinal) => PhpNamedBatch.ProductHashPath,
            var name when name is not null && name.StartsWith("guide_", StringComparison.Ordinal) => PhpNamedBatch.GuideDataPath,
            var name when name is not null && name.StartsWith("cache_", StringComparison.Ordinal) => PhpNamedBatch.CrossbaseCachePath,
            var name when name is not null && name.StartsWith("comp_", StringComparison.Ordinal) => PhpNamedBatch.ComplementaryPath,
            var name when name is not null && name.StartsWith("sess_", StringComparison.Ordinal) => PhpNamedBatch.SessionSecurityPath,
            var name when name is not null && name.StartsWith("brand_", StringComparison.Ordinal) => PhpNamedBatch.BrandingPath,
            var name when name is not null && name.StartsWith("legal_", StringComparison.Ordinal) => PhpNamedBatch.LegalPagesPath,
            _ => "unknown"
        };

    private static string Render(string name)
    {
        var empty = new PhpNamedBatch.BrandSite();
        var named = new PhpNamedBatch.BrandSite(SystemName: "Shop&Co", HubName: "hub", Tagline: "Wear it", ContactTradeName: "Trade");
        var hosted = new PhpNamedBatch.BrandSite(ClientHost: true);
        return name switch
        {
            "tax_tree" => Json(PhpNamedBatch.EpcAutoTaxSeedTree()),
            "hash_type2" => Json(PhpNamedBatch.DocpartType2CartCheckHash(
                new Dictionary<string, object?>
                {
                    ["manufacturer"] = "Bosch",
                    ["article"] = "0 986",
                    ["article_show"] = "0986",
                    ["name"] = "Pad & rotor",
                    ["exist"] = "4",
                    ["time_to_exe"] = "1",
                    ["time_to_exe_guaranteed"] = "2",
                    ["storage"] = "DXB",
                    ["min_order"] = "1",
                    ["probability"] = "95",
                    ["office_id"] = "3",
                    ["storage_id"] = "7",
                    ["price_purchase"] = "10.5",
                    ["markup"] = "20",
                    ["json_params"] = new Dictionary<string, object?> { ["a"] = "b" }
                },
                12.3,
                "tech")),
            "hash_type1" => Json(PhpNamedBatch.DocpartRefreshProductCartHashes(
                new Dictionary<string, object?>
                {
                    ["product_type"] = 1,
                    ["product_id"] = 9,
                    ["office_id"] = 2,
                    ["storage_id"] = 3,
                    ["storage_record_id"] = 4,
                    ["price"] = 237.7,
                    ["price_purchase"] = 100.1,
                    ["markup"] = 3.7
                },
                "k")),
            "hash_groups" => Json(PhpNamedBatch.DocpartRefreshProductsCartHashes(
                [
                    new Dictionary<string, object?>
                    {
                        ["product_type"] = 2,
                        ["manufacturer"] = "A",
                        ["article"] = "B",
                        ["price"] = 10,
                        ["groups_price"] = new Dictionary<string, object?> { ["5"] = 11.2 },
                        ["groups_markup"] = new Dictionary<string, object?> { ["5"] = 8 }
                    }
                ],
                "k")),
            "guide_channels" => Json(PhpNamedBatch.EpcGuideChannelDefinitions(new Dictionary<string, object?>
            {
                ["backend_dir"] = "cp",
                ["domain_path"] = "https://x.test",
                ["tech_key"] = "tk"
            })),
            "guide_snapshot" => Json(PhpNamedBatch.EpcGuideSnapshot(
                [
                    new PhpNamedBatch.GuidePriceRow(1, "Alpha", 1, "2026-01-02 03:04:05", 2),
                    new PhpNamedBatch.GuidePriceRow(2, "Beta FTP", 2, "2026-02-01 00:00:00", 1)
                ],
                new Dictionary<string, PhpNamedBatch.GuideHistoryRow>
                {
                    ["ftp"] = new(2, "2026-03-02 12:00:00"),
                    ["manual"] = new(1, "2026-03-03 00:00:00")
                },
                2,
                3,
                1,
                new Dictionary<string, object?> { ["backend_dir"] = "cp" })),
            "cache_key" => Json(new[]
            {
                PhpNamedBatch.EpcCrossbaseCacheKeyForArticle("FILTER-OIL-01"),
                PhpNamedBatch.EpcCrossbaseCacheKeyForArticle(""),
                "/content/shop/docpart/cache/crossbase/" + PhpNamedBatch.EpcCrossbaseCacheKeyForArticle("A B") + ".html"
            }),
            "cache_rw" => CacheRw(),
            "comp_key" => Json(PhpNamedBatch.EpcComplementaryNormalizeKey(" Bosch ", "0 986-1")),
            "comp_url" => Json(new[]
            {
                PhpNamedBatch.EpcComplementarySearchUrl("Bosch", "A B", "/en"),
                PhpNamedBatch.EpcComplementarySearchUrl("", "X", "/en")
            }),
            "comp_html" => Json(PhpNamedBatch.EpcComplementaryRenderHtml(
                [new Dictionary<string, string> { ["brand"] = "A&B", ["article"] = "1", ["label"] = "A&B 1" }],
                "Need <x>",
                "/ar")),
            "comp_empty" => Json(PhpNamedBatch.EpcComplementaryRenderHtml([])),
            "sess_ok" => SessOk(),
            "sess_idle" => Json(PhpNamedBatch.EpcSessionValidate(
                new Dictionary<string, object?>
                {
                    ["epc_session_created"] = 1900,
                    ["epc_session_last_active"] = 100,
                    ["epc_session_ua"] = "UA"
                },
                true,
                2200,
                "UA")),
            "sess_ua" => Json(PhpNamedBatch.EpcSessionValidate(
                new Dictionary<string, object?>
                {
                    ["epc_session_created"] = 2000,
                    ["epc_session_last_active"] = 2000,
                    ["epc_session_ua"] = "old"
                },
                true,
                2000,
                "new")),
            "sess_meta" => SessMeta(),
            "brand_defaults" => Json(new object[]
            {
                PhpNamedBatch.EpcBrandSystemName(empty),
                PhpNamedBatch.EpcBrandHubName(empty),
                PhpNamedBatch.EpcBrandDesignerName(),
                PhpNamedBatch.EpcBrandTradeName(empty),
                PhpNamedBatch.EpcBrandCopyrightHtml(),
                PhpNamedBatch.EpcBrandTaglineHtml(empty),
                PhpNamedBatch.EpcBrandMandatoryLineApplies(empty),
                PhpNamedBatch.EpcBrandHostedByHtml(empty)
            }),
            "brand_named" => Json(new object[]
            {
                PhpNamedBatch.EpcBrandSystemName(named),
                PhpNamedBatch.EpcBrandTradeName(named),
                PhpNamedBatch.EpcBrandTaglineHtml(named)
            }),
            "brand_hosted" => Json(new object[]
            {
                PhpNamedBatch.EpcBrandMandatoryLineApplies(hosted),
                PhpNamedBatch.EpcBrandHostedByCssLinkHtml(hosted),
                PhpNamedBatch.EpcBrandHostedByHtml(hosted)
            }),
            "brand_context" => Json(PhpNamedBatch.EpcBrandCpContext(empty)),
            "legal_meta_hub" => Json(PhpNamedBatch.EpcEcomaeLegalMeta(null, null)),
            "legal_meta_slug" => Json(PhpNamedBatch.EpcEcomaeLegalMeta(null, "privacy")),
            "legal_canonical" => Json(new[]
            {
                PhpNamedBatch.EpcEcomaeLegalCanonicalPath(null),
                PhpNamedBatch.EpcEcomaeLegalCanonicalPath("privacy!!")
            }),
            "legal_related" => Json(PhpNamedBatch.EpcEcomaeLegalRelatedLinksHtml("privacy")),
            _ => "unknown:" + name
        };
    }

    private static string CacheRw()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_named_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(doc);
        try
        {
            var html = new string('X', 401);
            var ok = PhpNamedBatch.EpcCrossbaseCacheWrite(doc, "AB-1", html);
            var fresh = PhpNamedBatch.EpcCrossbaseCacheRead(doc, "AB-1");
            var shortWrite = PhpNamedBatch.EpcCrossbaseCacheWrite(doc, "AB-1", "tiny");
            var stats = PhpNamedBatch.EpcCrossbaseCacheStats(doc);
            return Json(new object[] { ok, fresh.Length, shortWrite, stats });
        }
        finally
        {
            if (Directory.Exists(doc))
            {
                Directory.Delete(doc, true);
            }
        }
    }

    private static string SessOk()
    {
        var session = new Dictionary<string, object?>
        {
            ["epc_session_created"] = 1900,
            ["epc_session_last_active"] = 1990,
            ["epc_session_ua"] = "UA"
        };
        var ok = PhpNamedBatch.EpcSessionValidate(session, true, 2000, "UA");
        var last = session.TryGetValue("epc_session_last_active", out var value) && Convert.ToInt64(value) >= 1990;
        return Json(new object[] { ok, last });
    }

    private static string SessMeta()
    {
        var meta = PhpNamedBatch.EpcSessionMetadata(
            new Dictionary<string, object?> { ["epc_session_created"] = 100, ["epc_session_last_active"] = 150 },
            200);
        return Json(new object[] { meta["created"]!, meta["last_active"]!, Convert.ToInt64(meta["idle_seconds"]) >= 0, Convert.ToInt64(meta["lifetime_seconds"]) >= 0 });
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
            if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
            {
                return left.GetDouble() == right.GetDouble();
            }

            return false;
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
        => value.Length <= 220 ? value : value[..220] + "…";
}
