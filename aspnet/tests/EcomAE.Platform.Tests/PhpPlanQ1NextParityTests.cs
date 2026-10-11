using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1NextParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Next");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpPlanQ1Next.StorefrontLogoPath,
        PhpPlanQ1Next.IndustryPacksPath,
        PhpPlanQ1Next.PromotionsEnginePath
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
    public void PlanQ1Next_MatchPhpGolden()
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
    public void LogoEnqueue_DoesNotStartASession()
    {
        var html = PhpPlanQ1Next.EpcPortalStorefrontHubLogoEnqueue(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            CommerceEnabled = true
        });
        Assert.Equal("", html);
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.GetProperty("name").GetString() switch
        {
            var name when name is not null && name.StartsWith("logo_", StringComparison.Ordinal) => PhpPlanQ1Next.StorefrontLogoPath,
            var name when name is not null && name.StartsWith("pack_", StringComparison.Ordinal) => PhpPlanQ1Next.IndustryPacksPath,
            var name when name is not null && name.StartsWith("promo_", StringComparison.Ordinal) => PhpPlanQ1Next.PromotionsEnginePath,
            _ => "unknown"
        };

    private static string Render(string name)
    {
        return name switch
        {
            "logo_setting" => Json(LogoSetting()),
            "logo_hub" => Json(LogoHub()),
            "logo_trade" => Json(new[]
            {
                PhpPlanQ1Next.EpcPortalStorefrontLogoShowTradeLabel(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = false }),
                PhpPlanQ1Next.EpcPortalStorefrontLogoShowTradeLabel(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = false, OperatorHost = true })
            }),
            "logo_enq" => Json(new[]
            {
                PhpPlanQ1Next.EpcPortalStorefrontHubLogoEnqueue(new PhpPlanQ1Next.StorefrontLogoCtx { TenantBrandEnabled = true, CommerceEnabled = true }),
                PhpPlanQ1Next.EpcPortalStorefrontHubLogoEnqueue(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = false }),
                PhpPlanQ1Next.EpcPortalStorefrontHubLogoEnqueue(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = true })
            }),
            "logo_mark" => Json(LogoMark()),
            "logo_svg" => Json(PhpPlanQ1Next.EpcPortalStorefrontEpartscartSvgMarkup()),
            "pack_version" => Json(new object[] { PhpPlanQ1Next.EpcIndustryPacksVersion(), PhpPlanQ1Next.EpcIndustryBuiltinPacks().Keys.ToArray() }),
            "pack_builtin" => Json(PhpPlanQ1Next.EpcIndustryBuiltinPacks()),
            "pack_seed" => Json(PackSeed()),
            "pack_assign" => Json(PackAssign()),
            "promo_version" => Json(PhpPlanQ1Next.EpcPromoVersion()),
            "promo_flow" => Json(PromoFlow()),
            _ => "unknown:" + name
        };
    }

    private static object?[] LogoSetting()
    {
        var missing = PhpPlanQ1Next.EpcPortalStorefrontHubLogoSetting(new PhpPlanQ1Next.StorefrontLogoCtx());
        var on = PhpPlanQ1Next.EpcPortalStorefrontHubLogoSetting(Contact(1));
        var offInt = PhpPlanQ1Next.EpcPortalStorefrontHubLogoSetting(Contact(0));
        var offStr = PhpPlanQ1Next.EpcPortalStorefrontHubLogoSetting(Contact("0"));
        var emptyContact = PhpPlanQ1Next.EpcPortalStorefrontHubLogoSetting(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Settings = new Dictionary<string, object?> { ["contact"] = new Dictionary<string, object?>() }
        });
        return new object?[] { missing, on, offInt, offStr, emptyContact };
    }

    private static object[] LogoHub()
    {
        var demo = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(new PhpPlanQ1Next.StorefrontLogoCtx { DemoStorefront = true, CommerceEnabled = true });
        var demoOn = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(Contact(1, demo: true, commerce: true));
        var op = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(new PhpPlanQ1Next.StorefrontLogoCtx { OperatorHost = true, CommerceEnabled = true });
        var exp = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(Contact(1, commerce: true));
        var ncom = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = false });
        var man = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = true, MandatoryLine = true });
        var off = PhpPlanQ1Next.EpcPortalStorefrontHubEnabled(new PhpPlanQ1Next.StorefrontLogoCtx { CommerceEnabled = true });
        return [demo, demoOn, op, exp, ncom, man, off];
    }

    private static object[] LogoMark()
    {
        var anim = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "fashion_retail_namshi",
            AnimatedHtml = "ANIM",
            CommerceEnabled = true,
            TradeName = "Stylenlook",
            Industry = "fashion"
        });
        var hubOnly = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "automotive_spareparts_pro",
            CommerceEnabled = false,
            OperatorHost = true,
            TradeName = "Acme & Co",
            HubHtml = "HUB"
        });
        var hubLab = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "automotive_spareparts_pro",
            CommerceEnabled = false,
            TradeName = "Acme & Co",
            HubHtml = "HUB"
        });
        var brand = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "automotive_spareparts_pro",
            CommerceEnabled = true,
            TenantBrandEnabled = true,
            TenantBrandHtml = "TENANT",
            TradeName = "Acme & Co"
        });
        var parts = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "automotive_spareparts_pro",
            CommerceEnabled = true,
            Industry = "auto_parts"
        });
        var elec = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            Package = "electronics_retail_virgin",
            CommerceEnabled = true,
            Industry = "electronics",
            TradeName = "EpartsCart"
        });
        var text = PhpPlanQ1Next.EpcPortalStorefrontLogoMarkup(new PhpPlanQ1Next.StorefrontLogoCtx
        {
            CommerceEnabled = true,
            Industry = "fashion",
            TradeName = "Plain <Name>"
        });
        return [anim, hubOnly, hubLab, brand, parts, elec, text];
    }

    private static object[] PackSeed()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var first = PhpPlanQ1Next.EpcIndustrySeedPacks(keys);
        var second = PhpPlanQ1Next.EpcIndustrySeedPacks(keys);
        return [first, second];
    }

    private static object[] PackAssign()
    {
        var rows = new List<PhpPlanQ1Next.IndustryAssignment>();
        var nextId = 1;
        var a = PhpPlanQ1Next.EpcIndustryAssignPack(rows, "acme", "fashion", 7, ref nextId);
        var b = PhpPlanQ1Next.EpcIndustryAssignPack(rows, "acme", "fashion", 9, ref nextId);
        var c = PhpPlanQ1Next.EpcIndustryAssignPack(rows, "acme", "electronics", 7, ref nextId);
        var d = PhpPlanQ1Next.EpcIndustryAssignPack(rows, "beta", "fashion", 1, ref nextId);
        return [a, b, c, d, PhpPlanQ1Next.EpcIndustryTenantPacks(rows, "acme"), PhpPlanQ1Next.EpcIndustryFleetStats(rows)];
    }

    private static Dictionary<string, object?> PromoFlow()
    {
        var rows = new List<PhpPlanQ1Next.PromoRow>();
        var now = new DateTime(2026, 10, 9, 17, 0, 0, DateTimeKind.Utc);
        var creates = new[]
        {
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Save10",
                ["code"] = "save10",
                ["type"] = "percentage",
                ["value"] = 10,
                ["min_order"] = 50,
                ["max_discount"] = 20,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31",
                ["usage_limit"] = 2,
                ["per_customer"] = 1,
                ["stackable"] = 1,
                ["priority"] = 5,
                ["conditions"] = new Dictionary<string, object?> { ["x"] = 1 },
                ["applies_to"] = Array.Empty<object>(),
                ["customer_segments"] = new[] { "vip" },
                ["created_by"] = 3
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Flat",
                ["code"] = "FLAT",
                ["type"] = "fixed",
                ["value"] = 15,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31",
                ["priority"] = 4
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Ship",
                ["code"] = "SHIP",
                ["type"] = "free_shipping",
                ["value"] = 0,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31",
                ["priority"] = 3
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Bogo",
                ["code"] = "BOGO",
                ["type"] = "bogo",
                ["value"] = 0,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31",
                ["max_discount"] = 40,
                ["priority"] = 2
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "other", new Dictionary<string, object?>
            {
                ["name"] = "Other",
                ["code"] = "OTH",
                ["type"] = "percentage",
                ["value"] = 5,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31"
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Old",
                ["code"] = "OLD",
                ["type"] = "percentage",
                ["value"] = 10,
                ["start_date"] = "2010-01-01",
                ["end_date"] = "2011-01-01",
                ["priority"] = 1
            }),
            PhpPlanQ1Next.EpcPromoCreate(rows, "acme", new Dictionary<string, object?>
            {
                ["name"] = "Tier",
                ["code"] = "TIER",
                ["type"] = "tiered",
                ["value"] = 8,
                ["start_date"] = "2020-01-01",
                ["end_date"] = "2099-12-31",
                ["priority"] = 0
            })
        };

        var codes = PhpPlanQ1Next.EpcPromoList(rows, "acme", false, now).Select(r => (string)r["code"]!).ToArray();
        var active = PhpPlanQ1Next.EpcPromoList(rows, "acme", true, now).Select(r => (string)r["code"]!).ToArray();
        var apply = new[]
        {
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "NOPE", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "save10", 40, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 1000, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "FLAT", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SHIP", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "BOGO", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "TIER", 100, 0, now),
            PhpPlanQ1Next.EpcPromoApply(rows, "acme", "OLD", 100, 0, now)
        };
        var first = PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 100, 9, now);
        var rec = PhpPlanQ1Next.EpcPromoRecordUsage(rows[0], "acme", 9, "SO1", Convert.ToDouble(first["discount"]));
        var again = PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 100, 9, now);
        var other = PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 100, 8, now);
        PhpPlanQ1Next.EpcPromoRecordUsage(rows[0], "acme", 8, "SO2", Convert.ToDouble(other["discount"]));
        var limit = PhpPlanQ1Next.EpcPromoApply(rows, "acme", "SAVE10", 100, 8, now);
        return new Dictionary<string, object?>
        {
            ["creates"] = creates,
            ["codes"] = codes,
            ["active"] = active,
            ["apply"] = apply,
            ["record"] = rec,
            ["again"] = again,
            ["other"] = other,
            ["limit"] = limit,
            ["fleet"] = PhpPlanQ1Next.EpcPromoFleetStats(rows, now)
        };
    }

    private static PhpPlanQ1Next.StorefrontLogoCtx Contact(object flag, bool demo = false, bool commerce = false)
        => new()
        {
            DemoStorefront = demo,
            CommerceEnabled = commerce,
            Settings = new Dictionary<string, object?>
            {
                ["contact"] = new Dictionary<string, object?> { ["use_animated_hub_logo"] = flag }
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
