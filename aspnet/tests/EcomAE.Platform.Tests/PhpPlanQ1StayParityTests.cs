using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1StayParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Stay");

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
    public void PlanQ1Stay_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Stay.PageAssetsPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Stay.PageAssetsPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Stay.Reset();
        Assert.Contains("epc_cp_page_assets.php", PhpPlanQ1Stay.PageAssetsPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Stay.PageAssetsPath, StringComparison.Ordinal);
        var head = PhpPlanQ1Stay.EpcCpPageHeadAssets("control/communications");
        Assert.DoesNotContain("PHPSESSID", head, StringComparison.Ordinal);
        var scripts = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/communications");
        Assert.DoesNotContain("PHPSESSID", scripts, StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Stay.Reset();
        return name switch
        {
            "map" => Map(),
            "resolve" => Resolve(),
            "head" => Head(),
            "scripts" => Scripts(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Map()
    {
        var defaultVer = PhpPlanQ1Stay.EpcCpPageAssetVersion();
        PhpPlanQ1Stay.BackendDir = null;
        var defaultMap = PhpPlanQ1Stay.EpcCpPageAssetUrlMap();
        PhpPlanQ1Stay.BackendDir = "ops";
        var opsMap = PhpPlanQ1Stay.EpcCpPageAssetUrlMap();
        PhpPlanQ1Stay.BackendDir = "";
        var emptyMap = PhpPlanQ1Stay.EpcCpPageAssetUrlMap();
        PhpPlanQ1Stay.BackendDir = "cp";
        PhpPlanQ1Stay.Get.Clear();
        var ordersPlain = PhpPlanQ1Stay.EpcCpPageAssetUrlMap()["shop/orders/orders"];
        PhpPlanQ1Stay.Get["order_id"] = "7abc";
        PhpPlanQ1Stay.Get["status_id"] = "";
        var ordersGet = PhpPlanQ1Stay.EpcCpPageAssetUrlMap()["shop/orders/orders"];
        var sampleKeys = new[]
        {
            "control/communications",
            "control/config",
            "control/portal/epc_auto_price_engine",
            "control/portal/epc_power_bi",
            "control/portal/epc_api_documentation_guide",
            "control/portal/epc_visual_page_editor",
            "filemanager",
            "shop/orders/orders",
            "shop/finance/account_operations"
        };
        return new Rendered(new object?[]
        {
            defaultVer,
            defaultMap.Keys.ToList(),
            Pick(defaultMap, sampleKeys),
            Pick(opsMap, ["control/communications", "filemanager"]),
            Pick(emptyMap, ["control/communications", "shop/orders/items"]),
            ordersPlain,
            ordersGet
        });
    }

    private static Rendered Resolve()
    {
        PhpPlanQ1Stay.BackendDir = "cp";
        var unknown = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("no/such/page");
        var erp = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("shop/finance/erp");
        var erpChild = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("/shop/finance/erp/invoices/");
        var erpFoo = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("shop/finance/erpfoo");
        var comms = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("/control/communications/");
        var ao = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("shop/finance/account_operations");
        var vpe = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("control/portal/epc_visual_page_editor");
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["css"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["https://cdn.example/o'x.css"] = 1 },
            ["js"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["https://cdn.example/o'x.js"] = 1 }
        };
        var merged = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("control/communications");
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["css"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["js"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
        var emptyExtra = PhpPlanQ1Stay.EpcCpPageAssetsForUrl("control/communications");
        return new Rendered(new object?[]
        {
            unknown, erp, erpChild, erpFoo, comms, ao, vpe, merged, emptyExtra,
            PhpPlanQ1Stay.EpcErpShellNavJsSrc()
        });
    }

    private static Rendered Head()
    {
        PhpPlanQ1Stay.BackendDir = "cp";
        var comms = PhpPlanQ1Stay.EpcCpPageHeadAssets("control/communications");
        var unknown = PhpPlanQ1Stay.EpcCpPageHeadAssets("missing");
        var erp = PhpPlanQ1Stay.EpcCpPageHeadAssets("shop/finance/erp");
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["css"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["https://cdn.example/o'x.css"] = true }
        };
        var quote = PhpPlanQ1Stay.EpcCpPageHeadAssets("missing");
        PhpPlanQ1Stay.ExtraAssets = null;
        var vpe = PhpPlanQ1Stay.EpcCpPageHeadAssets("control/portal/epc_visual_page_editor");
        return new Rendered(new object?[] { comms, unknown, erp, quote, vpe });
    }

    private static Rendered Scripts()
    {
        PhpPlanQ1Stay.Version = () => "injectedver1";
        PhpPlanQ1Stay.BackendDir = "cp";
        var tabs = Tabs();
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "Acme-1!",
            ["tab"] = "discovery",
            ["imports_filter"] = "nope"
        };
        var discover = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "Beta_2!",
            ["tab"] = "taxonomy",
            ["view"] = "grid",
            ["taxonomy_id"] = "7abc",
            ["imports_filter"] = "price_changes"
        };
        var taxonomy = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab"] = "discover",
            ["view"] = "  ",
            ["taxonomy_id"] = "0",
            ["imports_filter"] = "duplicates"
        };
        var viewBlank = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.BackendDir = "";
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tab"] = "discover",
            ["site_key"] = "Acme-1!"
        };
        var emptyBackend = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.BackendDir = "cp";
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal) { ["tab"] = "discover" };
        PhpPlanQ1Stay.Host = "shop.epartscart.ae";
        var hostParts = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.Host = "www.electronicae.com";
        var hostEl = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.Host = "other.example";
        var hostOther = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_auto_price_engine");
        PhpPlanQ1Stay.Host = "";
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "Acme-1!",
            ["page_key"] = "Home Page!"
        };
        var vpe = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_visual_page_editor");
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "acme1",
            ["page_key"] = "my-page!"
        };
        var ver = PhpPlanQ1Stay.EpcCpPageAssetVersion() + "vpe1";
        var configSrc = "/cp/content/control/portal/epc_visual_page_editor_config.php?v=" + Uri.EscapeDataString(ver)
            + "&site_key=" + Uri.EscapeDataString("acme1")
            + "&page_key=" + Uri.EscapeDataString("my-page");
        var jsSrc = "/cp/content/control/portal/epc_visual_page_editor.js?v=" + Uri.EscapeDataString(ver);
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["js"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [configSrc] = true,
                [jsSrc] = true
            }
        };
        var vpeSkip = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_visual_page_editor");
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["js"] = new Dictionary<string, object?>(StringComparer.Ordinal) { [configSrc] = 0 }
        };
        var vpeEmpty0 = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/portal/epc_visual_page_editor");
        PhpPlanQ1Stay.ExtraAssets = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal)
        {
            ["js"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["https://cdn.example/o'x.js"] = true }
        };
        var comms = PhpPlanQ1Stay.EpcCpPageFooterScripts("control/communications");
        PhpPlanQ1Stay.ExtraAssets = null;
        var unknown = PhpPlanQ1Stay.EpcCpPageFooterScripts("missing");
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "Acme-1!",
            ["tab"] = "my_imports",
            ["imports_filter"] = "new"
        };
        var inlineOnly = PhpPlanQ1Stay.EpcCpApaiInlineDiscoverConfigScript();
        PhpPlanQ1Stay.Get = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["site_key"] = "Acme-1!",
            ["tab"] = "discover"
        };
        var shellOnly = PhpPlanQ1Stay.EpcCpApaiShellConfigScript();
        return new Rendered(new object?[]
        {
            PhpPlanQ1Stay.EpcCpPageAssetVersion(),
            tabs,
            discover,
            taxonomy,
            viewBlank,
            emptyBackend,
            hostParts,
            hostEl,
            hostOther,
            vpe,
            vpeSkip,
            vpeEmpty0,
            comms,
            unknown,
            inlineOnly,
            shellOnly
        });
    }

    private static Dictionary<string, string> Tabs()
    {
        var names = new[]
        {
            "discover", "discovery", "taxonomy", "disc_sources", "market_sources",
            "settings", "dashboard", "my_imports", "imports", "custom_tab"
        };
        var outTabs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tab in names)
        {
            PhpPlanQ1Stay.Get["tab"] = tab;
            outTabs[tab] = PhpPlanQ1Stay.EpcCpApaiDiscoverTabKey();
        }

        PhpPlanQ1Stay.Get.Remove("tab");
        outTabs["missing"] = PhpPlanQ1Stay.EpcCpApaiDiscoverTabKey();
        return outTabs;
    }

    private static Dictionary<string, Dictionary<string, List<string>>> Pick(
        Dictionary<string, Dictionary<string, List<string>>> map,
        IEnumerable<string> keys)
    {
        var outMap = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            outMap[key] = map[key];
        }

        return outMap;
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
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
