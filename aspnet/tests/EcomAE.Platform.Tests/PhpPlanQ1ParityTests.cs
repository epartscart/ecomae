using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1ParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpPlanQ1.BootstrapLightPath,
        PhpPlanQ1.DeployAuthPath,
        PhpPlanQ1.BosSecurityPath,
        PhpPlanQ1.PhpReferenceRouterPath,
        PhpPlanQ1.CommonParityPath,
        PhpPlanQ1.OfficeStorageMetaPath,
        PhpPlanQ1.PerfCachePath,
        PhpPlanQ1.TenantBrandPath,
        PhpPlanQ1.GenuineManufacturersPath
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
    public void PlanQ1_MatchPhpGolden()
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
    public void BosEntryGuard_DoesNotStartASession()
    {
        var denied = PhpPlanQ1.EpcBosAjaxEntryGuard("", "guest", 0, "GET", "", false, false);
        Assert.Equal(400, Convert.ToInt32(denied["http"]));
        var login = PhpPlanQ1.EpcBosAjaxEntryGuard("login", "guest", 0, "POST", "", false, false);
        Assert.Equal("bos_login", login["rate_limit"]);
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.GetProperty("name").GetString() switch
        {
            var name when name is not null && name.StartsWith("boot_", StringComparison.Ordinal) => PhpPlanQ1.BootstrapLightPath,
            var name when name is not null && name.StartsWith("dep_", StringComparison.Ordinal) => PhpPlanQ1.DeployAuthPath,
            var name when name is not null && name.StartsWith("bos_", StringComparison.Ordinal) => PhpPlanQ1.BosSecurityPath,
            var name when name is not null && name.StartsWith("ref_", StringComparison.Ordinal) => PhpPlanQ1.PhpReferenceRouterPath,
            var name when name is not null && name.StartsWith("par_", StringComparison.Ordinal) => PhpPlanQ1.CommonParityPath,
            var name when name is not null && name.StartsWith("meta_", StringComparison.Ordinal) => PhpPlanQ1.OfficeStorageMetaPath,
            var name when name is not null && name.StartsWith("cache_", StringComparison.Ordinal) => PhpPlanQ1.PerfCachePath,
            var name when name is not null && name.StartsWith("brand_", StringComparison.Ordinal) => PhpPlanQ1.TenantBrandPath,
            var name when name is not null && name.StartsWith("gen_", StringComparison.Ordinal) => PhpPlanQ1.GenuineManufacturersPath,
            _ => "unknown"
        };

    private static string Render(string name)
    {
        var targets = PhpPlanQ1.EpcCpCommonParityTargets();
        var storages = new Dictionary<int, PhpPlanQ1.StorageRow>
        {
            [10] = new(5, 90, "#f00", "1.0000", "Warehouse", "WH", 0),
            [11] = new(6, 0, "", "1.0000", "Closed", "CL", 1),
            [12] = new(0, 0, "", "1.0000", "NoPrice", "NP", 0)
        };
        var offices = new Dictionary<int, string> { [1] = "Dubai", [2] = "Sharjah" };
        var extra = new Dictionary<string, int> { ["1:10"] = 48 };
        var marks = new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["1:10"] =
            [
                new() { ["min_point"] = 0, ["max_point"] = 100, ["markup"] = "0.20000000" },
                new() { ["min_point"] = 100, ["max_point"] = 999, ["markup"] = "0.15000000" }
            ]
        };
        var umapi = new List<(string Manufacturer, string Section)>
        {
            ("Bosch", "passenger"),
            ("Bosch", "commercial"),
            ("Febi", "passenger"),
            ("", "motorbike")
        };
        var synonyms = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["BOSCH"] = ["BOSCH", "BOSCH-K"],
            ["BOSCH-K"] = ["BOSCH", "BOSCH-K"]
        };
        return name switch
        {
            "boot_route" => Json(new[]
            {
                PhpPlanQ1.EpcCpRequestRoute("/cp/control/users?x=1"),
                PhpPlanQ1.EpcCpRequestRoute("/cp"),
                PhpPlanQ1.EpcCpRequestRoute("/cp/"),
                PhpPlanQ1.EpcCpRequestRoute("/other")
            }),
            "boot_cookies" => Json(new[]
            {
                PhpPlanQ1.EpcCpHasAdminCookies(null, null),
                PhpPlanQ1.EpcCpHasAdminCookies("abc", "2"),
                PhpPlanQ1.EpcCpHasAdminCookies("abc", "0"),
                PhpPlanQ1.EpcCpHasAdminCookies("", "3")
            }),
            "boot_login" => Json(new[]
            {
                PhpPlanQ1.EpcCpIsLoginRequest("GET", "control", false, false),
                PhpPlanQ1.EpcCpIsLoginRequest("POST", "control", false, true),
                PhpPlanQ1.EpcCpIsLoginRequest("POST", "control", false, false)
            }),
            "boot_init" => Json(Init()),
            "dep_token_default" => Json(PhpPlanQ1.EpcDeployToken(null)),
            "dep_token_env" => Json(PhpPlanQ1.EpcDeployToken("secret-token")),
            "dep_ip" => Json(new[]
            {
                PhpPlanQ1.EpcDeployClientIp(new Dictionary<string, string> { ["REMOTE_ADDR"] = "8.8.8.8", ["HTTP_CF_CONNECTING_IP"] = "1.2.3.4" }),
                PhpPlanQ1.EpcDeployClientIp(new Dictionary<string, string> { ["REMOTE_ADDR"] = "127.0.0.1", ["HTTP_CF_CONNECTING_IP"] = "1.2.3.4" }),
                PhpPlanQ1.EpcDeployClientIp(new Dictionary<string, string> { ["REMOTE_ADDR"] = "10.0.0.1", ["HTTP_X_FORWARDED_FOR"] = "9.9.9.9, 8.8.8.8" }),
                PhpPlanQ1.EpcDeployClientIp(new Dictionary<string, string>())
            }),
            "dep_allowed" => Json(new object[]
            {
                PhpPlanQ1.EpcDeployAllowedIps(null),
                PhpPlanQ1.EpcDeployAllowedIps("1.1.1.1, 2.2.2.2,")
            }),
            "dep_lock" => Lockdown(),
            "dep_redir" => Json(new[]
            {
                PhpPlanQ1.EpcRedirectSafeTarget(""),
                PhpPlanQ1.EpcRedirectSafeTarget("/"),
                PhpPlanQ1.EpcRedirectSafeTarget("/cp/users"),
                PhpPlanQ1.EpcRedirectSafeTarget("https://evil.test/x"),
                PhpPlanQ1.EpcRedirectSafeTarget("//evil"),
                PhpPlanQ1.EpcRedirectSafeTarget("cp/users"),
                PhpPlanQ1.EpcRedirectSafeTarget("/cp\r\nLocation: x")
            }),
            "bos_lists" => Json(new object[] { PhpPlanQ1.EpcBosPublicActions(), PhpPlanQ1.EpcBosProviderOnlyActions() }),
            "bos_action" => Json(new[]
            {
                PhpPlanQ1.EpcBosAjaxActionName("tenant_list", null, false),
                PhpPlanQ1.EpcBosAjaxActionName("", "  login  ", false),
                PhpPlanQ1.EpcBosAjaxActionName("", "", true),
                PhpPlanQ1.EpcBosAjaxActionName("", "", false)
            }),
            "ref_surface" => Json(new[]
            {
                PhpPlanQ1.EpcPhpReferenceSurface("CP"),
                PhpPlanQ1.EpcPhpReferenceSurface("nope"),
                PhpPlanQ1.EpcPhpReferenceSurface("")
            }),
            "ref_deep" => Json(new[]
            {
                PhpPlanQ1.EpcPhpReferenceApplyDeepUri("cp", "CP/shop/orders", new Dictionary<string, string> { ["area"] = "1" }),
                PhpPlanQ1.EpcPhpReferenceApplyDeepUri("erp", "nope", new Dictionary<string, string>()),
                PhpPlanQ1.EpcPhpReferenceApplyDeepUri("bos", "/x", new Dictionary<string, string>())
            }),
            "ref_host" => Json(new[]
            {
                PhpPlanQ1.EpcPhpReferenceIsSuperCpHost("www.ecomae.com:443"),
                PhpPlanQ1.EpcPhpReferenceIsSuperCpHost("cp.ecomae.com"),
                PhpPlanQ1.EpcPhpReferenceIsSuperCpHost("epartscart.com")
            }),
            "ref_try" => Json(PhpPlanQ1.EpcPhpReferenceTryRoute("")),
            "par_all" => Json(new object[]
            {
                targets,
                PhpPlanQ1.EpcCpCommonParityPacks(),
                PhpPlanQ1.EpcCpCommonParityPackApplies("vehicle_catalog", targets["epartscart"]),
                PhpPlanQ1.EpcCpCommonParityPackApplies("vehicle_catalog", targets["electronicae"]),
                PhpPlanQ1.EpcCpCommonParityPackApplies("missing", targets["ecomae"]),
                PhpPlanQ1.EpcCpCommonParityHostMap()
            }),
            "meta_empty" => Json(PhpPlanQ1.EpcPricesBuildOfficeStorageDataInfo([], storages, offices, extra, marks, new Dictionary<string, string>())),
            "meta_one" => Json(PhpPlanQ1.EpcPricesBuildOfficeStorageDataInfo(
                [
                    new() { ["office_id"] = 1, ["storage_id"] = 10 },
                    new() { ["office_id"] = 1, ["storage_id"] = 11 },
                    new() { ["office_id"] = 2, ["storage_id"] = 10 }
                ],
                storages,
                offices,
                extra,
                marks,
                new Dictionary<string, string>())),
            "meta_mgr" => Json(PhpPlanQ1.EpcPricesBuildOfficeStorageDataInfo(
                [new() { ["office_id"] = 1, ["storage_id"] = 10 }],
                storages,
                offices,
                extra,
                marks,
                new Dictionary<string, string> { ["1:10"] = "Warehouse" })),
            "cache_key" => Json(new[]
            {
                PhpPlanQ1.EpcPerfCacheKeySafe("Menu:Rows v1"),
                PhpPlanQ1.EpcPerfCacheKeySafe("ok_1.2-A")
            }),
            "cache_rw" => CacheRw(),
            "cache_menu" => Json(new object[]
            {
                PhpPlanQ1.EpcCpMenuCache(
                    [new Dictionary<string, object?> { ["id"] = 1, ["name"] = "Shop", ["order"] = 1 }],
                    [new Dictionary<string, object?> { ["id"] = 2, ["name"] = "Orders", ["order"] = 1, ["group_id"] = 1 }]),
                1
            }),
            "brand_catalog" => Json(new object[]
            {
                PhpPlanQ1.EpcPortalTenantBrandCatalog(),
                PhpPlanQ1.EpcPortalTenantBrandCssHref(),
                PhpPlanQ1.EpcPortalTenantBrandCssVersion()
            }),
            "brand_key_profile" => Json(PhpPlanQ1.EpcPortalTenantBrandSiteKey("Electronicae!", "x.test")),
            "brand_key_host" => Json(PhpPlanQ1.EpcPortalTenantBrandSiteKey("", "www.stylenlook.com")),
            "brand_key_none" => Json(PhpPlanQ1.EpcPortalTenantBrandSiteKey("", "nope.test")),
            "brand_on_tax" => Json(PhpPlanQ1.EpcPortalTenantBrandEnabled(true, "taxofinca", "tax_advisory", "www.taxofinca.com")),
            "brand_on_parts" => Json(PhpPlanQ1.EpcPortalTenantBrandEnabled(true, "epartscart", "electronics", "www.epartscart.com")),
            "brand_on_auto" => Json(PhpPlanQ1.EpcPortalTenantBrandEnabled(true, "electronicae", "auto_parts", "www.electronicae.com")),
            "brand_on_offhost" => Json(PhpPlanQ1.EpcPortalTenantBrandEnabled(false, "electronicae", null, "electronicae.com")),
            "brand_markup" => Json(new[]
            {
                PhpPlanQ1.EpcPortalTenantBrandMarkup("header", "electronicae", "electronicae.com"),
                PhpPlanQ1.EpcPortalTenantBrandMarkup("compact", "electronicae", "electronicae.com"),
                PhpPlanQ1.EpcPortalTenantBrandMarkup("nope", "electronicae", "electronicae.com")
            }),
            "brand_hero" => Json(new[]
            {
                PhpPlanQ1.EpcPortalTenantBrandEnqueue(true),
                PhpPlanQ1.EpcPortalTenantBrandHeroBlock(true, "electronicae", "electronicae.com")
            }),
            "gen_meta" => Json(new object?[]
            {
                Path.GetFileName(PhpPlanQ1.EpcGenuineCachePath("/tmp/x")),
                PhpPlanQ1.EpcGenuineCachePath("/tmp/x").EndsWith("/cache/epc_genuine_manufacturers.json", StringComparison.Ordinal),
                PhpPlanQ1.EpcGenuineSiteBaseUrl("https://x.test/shop/", true, "y.test"),
                PhpPlanQ1.EpcGenuineSiteBaseUrl(null, true, "y.test"),
                PhpPlanQ1.EpcGenuineCountUmapiRows(umapi),
                PhpPlanQ1.EpcGenuineSectionCounts(umapi),
                PhpPlanQ1.EpcGenuineLoadManufacturerNames(umapi),
                null
            }),
            "gen_index" => Json(new object[]
            {
                PhpPlanQ1.EpcGenuineBuildFrontendIndex(umapi, synonyms, "/catalog"),
                true
            }),
            _ => "unknown:" + name
        };
    }

    private static object[] Init()
    {
        var result = PhpPlanQ1.EpcCpBootstrapLightInit("GET", "control", true, false);
        return [result.Light, !result.SessionCleared ? true : false, !result.SessionCleared];
    }

    private static string Lockdown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ecomae_q1_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(dir);
        try
        {
            var off = PhpPlanQ1.EpcDeployLockdownEnabled(dir);
            File.WriteAllText(Path.Combine(dir, ".epc-security-lockdown"), "1");
            var on = PhpPlanQ1.EpcDeployLockdownEnabled(dir);
            return Json(new[] { off, on });
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CacheRw()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_q1c_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(doc);
        try
        {
            const string key = "q1_plan_cache_rw_20261009";
            PhpPlanQ1.EpcPerfCacheDelete(doc, key);
            var miss = PhpPlanQ1.EpcPerfCacheGet(doc, key);
            PhpPlanQ1.EpcPerfCacheSet(doc, key, new Dictionary<string, object?> { ["n"] = 2, ["s"] = "ok" }, 300);
            var hit = PhpPlanQ1.EpcPerfCacheGet(doc, key);
            var rem = PhpPlanQ1.EpcPerfCacheRemember(doc, key, 300, () => "nope");
            var del = PhpPlanQ1.EpcPerfCacheDelete(doc, key);
            var after = PhpPlanQ1.EpcPerfCacheGet(doc, key);
            return Json(new object?[] { miss, hit, rem, del, after });
        }
        finally
        {
            if (Directory.Exists(doc))
            {
                Directory.Delete(doc, true);
            }
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
