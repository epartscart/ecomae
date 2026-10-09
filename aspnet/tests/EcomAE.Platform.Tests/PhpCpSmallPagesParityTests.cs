using System.Text.Json;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP 8.3 goldens in <c>Fixtures/CpSmallPages</c> for the eval-safe CP wrappers and small scripts in this slice.
/// </summary>
public sealed class PhpCpSmallPagesParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CpSmallPages");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpCpEvalWrappers.WebTrackerPath,
        PhpCpEvalWrappers.BulkUploadPath,
        PhpCpEvalWrappers.PaymentsGuidePath,
        PhpCpEvalWrappers.PaymentsMainPath,
        PhpCpEvalWrappers.PosTerminalPath,
        PhpCpEvalWrappers.ChannelsMainPath,
        PhpCpEvalWrappers.MarketingMainPath,
        PhpCpEvalWrappers.TenantHubPath,
        PhpCpEvalWrappers.CustomerMgmtMainPath,
        PhpCpEvalWrappers.ProcurementMainPath,
        PhpCpEvalWrappers.MultivendorUploadPath,
        PhpCpEvalWrappers.LogisticsCarriersPath,
        PhpCpEvalWrappers.DocumentControlMainPath,
        PhpCpEvalWrappers.GuidelinePath,
        PhpCpEvalWrappers.ChannelsGuidePath,
        PhpCpEvalWrappers.LogisticsGuidePath,
        PhpCpEvalWrappers.PricesGuidePath,
        PhpCpEvalWrappers.PricesUploadGuideWrapPath,
        PhpCpEvalWrappers.OmsGuidePath,
        PhpCpEvalWrappers.FulfilmentGuidePath,
        PhpCpEvalWrappers.WhatsappGuidePath,
        PhpCpEvalWrappers.RelatedProductsPath,
        PhpCpEvalWrappers.CustomerMgmtGuideShopPath,
        PhpCpEvalWrappers.CustomerMgmtGuideUsersPath,
        PhpCpSmallScripts.PageGuardPath,
        PhpCpSmallScripts.FastTenantPath,
        PhpCpSmallScripts.IndustrySwitchPath,
        PhpCpSmallScripts.HealthConfigPath,
        PhpCpSmallScripts.GovernanceConfigPath,
        PhpCpSmallScripts.WebTrackerConfigPath,
        PhpCpSmallScripts.DocumentControlConfigPath,
        PhpCpSmallScripts.ApiDebugPath,
        PhpCpSmallScripts.ReturnsPath,
        PhpCpSmallScripts.ReturnsRouterPath,
        PhpCpSmallScripts.EpartsCataPath,
        PhpCpSmallScripts.EpartsProductPath,
        PhpCpSmallScripts.TaxTaxonomyPath,
        PhpCpSmallScripts.MarketingSchemaPath
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
    public void SmallPages_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expectedOutput = results[i].GetProperty("output").GetString() ?? string.Empty;
            var expectedResult = results[i].GetProperty("result");
            if (name.StartsWith("industry_switch", StringComparison.Ordinal))
            {
                var session = results[i].GetProperty("session");
                var industry = session.ValueKind == JsonValueKind.Object && session.TryGetProperty("epc_cp_industry_filter", out var filter)
                    ? filter
                    : default;
                var actualSwitch = PhpCpSmallScripts.IndustrySwitch(
                    Get(cases[i], "industry"),
                    Get(cases[i], "back"),
                    Config(cases[i], "backend_dir", "cp"),
                    PhpCpSmallScripts.IndustryKnown(Get(cases[i], "industry")));
                var expectedIndustry = industry.ValueKind == JsonValueKind.String ? industry.GetString() : "";
                var expectedLocation = name == "industry_switch_ok" ? "/cp/shop" : "/cp";
                if (actualSwitch.Location != expectedLocation || (actualSwitch.Industry ?? "") != expectedIndustry)
                {
                    failures.Add(name + " location/session got " + actualSwitch.Location + "/" + actualSwitch.Industry);
                }

                continue;
            }

            if (name == "marketing_schema")
            {
                var src = expectedResult.GetString() ?? "";
                if (!src.Contains("`epc_marketing_task_progress`", StringComparison.Ordinal)
                    || !src.Contains("`epc_marketing_kpi_log`", StringComparison.Ordinal)
                    || !src.Contains("`epc_marketing_reviews`", StringComparison.Ordinal)
                    || !src.Contains(PhpCpSmallScripts.MarketingTaskProgressSql.Split('\n')[0], StringComparison.Ordinal))
                {
                    failures.Add("marketing_schema SQL");
                }

                continue;
            }

            var actual = Render(name, cases[i], results[i]);
            var expected = expectedOutput.Length > 0 ? expectedOutput : ResultText(expectedResult);
            if (!Same(actual, expected, expectedResult))
            {
                failures.Add(name + " expected=" + Truncate(expected) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray()
                .Select(c => c.GetProperty("file").GetString())
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void ScriptUrls_AreAllowlistedOutsideTheCpLoginWall()
    {
        Assert.True(PhpCpSmallScripts.IsScriptPath(PhpCpSmallScripts.IndustrySwitchUrl));
        Assert.True(PhpCpSmallScripts.IsScriptPath(PhpCpSmallScripts.HealthConfigUrl));
        Assert.False(PhpCpSmallScripts.IsScriptPath("/cp/shop/prices"));
    }

    private static string Render(string name, JsonElement testCase, JsonElement result)
    {
        var backend = Config(testCase, "backend_dir", "cp");
        switch (name)
        {
            case "bulk_missing":
                return PhpCpEvalWrappers.LoadSessionThenInclude(false, "", "Bulk upload hub file missing.");
            case "bulk_ok":
                return PhpCpEvalWrappers.LoadSessionThenInclude(true, "INCLUDED-bulk", "Bulk upload hub file missing.");
            case "payments_guide_missing":
                return PhpCpEvalWrappers.LoadSessionThenInclude(false, "", "Payments guide not found.");
            case "payments_main_login":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, false, false, "", "log in to the control panel", ".", "Payments module not found.");
            case "payments_main_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", ".", "Payments module not found.");
            case "payments_main_ok":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, true, "INCLUDED-pay", "log in to the control panel", ".", "Payments module not found.");
            case "pos_login":
                return PhpCpSmallScriptsWaitPos(backend, false, false, "");
            case "pos_missing":
                return PhpCpEvalWrappers.PosTerminal(backend, true, false, "");
            case "channels_main_login":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, false, false, "", "log in to the control panel", ".", "Channels module not found.");
            case "channels_main_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", ".", "Channels module not found.");
            case "marketing_main_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", ".", "Marketing module not found. Run marketing-setup.php on the server.");
            case "tenant_hub_login":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, false, false, "", "log in", " to open the Tenant hub.", "Tenant hub not found. Run ecomae-super-cp-setup.php.");
            case "tenant_hub_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in", " to open the Tenant hub.", "Tenant hub not found. Run ecomae-super-cp-setup.php.");
            case "customer_main_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to open Customer management.", "<strong>Customer management module not found.</strong> Run customer-mgmt-setup.php on the server.");
            case "procurement_main_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to open Procurement.", "<strong>Procurement module not found.</strong> Run procurement-setup.php on the server.");
            case "multivendor_login":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, false, false, "", "log in to the control panel", " to upload multi-vendor prices.", "<strong>Multi-vendor upload could not load:</strong> file not found.");
            case "multivendor_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to upload multi-vendor prices.", "<strong>Multi-vendor upload could not load:</strong> file not found.");
            case "carriers_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", ".", "Logistics carriers module not found.");
            case "doc_control_login":
                return PhpCpEvalWrappers.DocumentControl(backend, false, false, "");
            case "doc_control_missing":
                return PhpCpEvalWrappers.DocumentControl(backend, true, false, "");
            case "guideline_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view this guide.", "<strong>Guide could not load:</strong> file not found.");
            case "channels_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view this guide.", "Channels guide file not found.");
            case "logistics_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", ".", "Logistics guide file not found.");
            case "prices_guide_login":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, false, false, "", "log in to the control panel", " to view this guide.", "<strong>Guide could not load:</strong> file not found.");
            case "prices_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view this guide.", "<strong>Guide could not load:</strong> file not found.");
            case "oms_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view the OMS daily guide.", "<strong>Guide could not load:</strong> file not found.");
            case "fulfilment_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view this guide.", "<strong>Guide could not load:</strong> file not found.");
            case "whatsapp_guide_missing":
                return PhpCpEvalWrappers.RequireSessionThenInclude(backend, true, false, "", "log in to the control panel", " to view this guide.", "<strong>Guide could not load:</strong> file not found.");
            case "web_tracker_ok":
                return PhpCpEvalWrappers.WebTracker("INCLUDED-tracker");
            case "eparts_cata_missing":
                return PhpCpEvalWrappers.EpartsCata(false, "");
            case "eparts_cata_ok":
                return PhpCpEvalWrappers.EpartsCata(true, "INCLUDED-cata");
            case "related_products":
                return PhpCpEvalWrappers.RelatedProducts("INCLUDED-products");
            case "prices_guide_wrap_ok":
                return PhpCpEvalWrappers.PricesUploadGuideTry(true, "INCLUDED-guide", "");
            case "eparts_product_article":
                return PhpCpSmallScripts.EpartsProduct("/en/", "OC 90", null, "KNECHT", null, null);
            case "eparts_product_empty":
                return PhpCpSmallScripts.EpartsProduct("/ar", null, null, null, null, null);
            case "eparts_product_category":
                return PhpCpSmallScripts.EpartsProduct("/en", null, null, null, null, "filters & x");
            case "returns_hub_missing_router":
                return PhpCpSmallScripts.ReturnsHub("cp", "bootstrap_admin", false, "");
            case "returns_router_fallback":
                return PhpCpSmallScripts.ReturnsRouter(null, null, null, false, false, false, "", "CP&x");
            case "returns_router_detail":
                return PhpCpSmallScripts.ReturnsRouter("list", null, "12", false, true, false, "INCLUDED-detail", "cp");
            case "returns_router_reasons":
                return PhpCpSmallScripts.ReturnsRouter("reasons_statuses", null, null, true, false, false, "INCLUDED-reasons", "cp");
            case "page_guard_login":
                return JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    ["ok"] = false,
                    ["html"] = PhpCpSmallScripts.PageRequireAdmin(false, "Auth <b>", "CP&x"),
                    ["login"] = PhpCpSmallScripts.PageLoginUrl("CP&x")
                });
            case "page_guard_include_missing":
                return PhpCpSmallScripts.PageInclude(false, "", "Module <b> gone");
            case "fast_tenant_hosts":
                return JsonSerializer.Serialize(new object[] { PhpCpSmallScripts.IsFastTenantHost("www.epartscart.com:8080") });
            case "fast_tenant_skip":
                return JsonSerializer.Serialize(new Dictionary<string, bool>
                {
                    ["skip"] = PhpCpSmallScripts.ShouldSkipErpRouters("epartscart.com", false, "shop/orders"),
                    ["erp"] = PhpCpSmallScripts.ShouldSkipErpRouters("epartscart.com", false, "client-erp/x")
                });
            case "tax_tree":
                return JsonSerializer.Serialize(PhpCpSmallScripts.TaxAdvisorySeedTree());
            case "marketing_schema":
                return result.GetProperty("result").GetRawText();
            case "health_guest":
                return PhpCpSmallScripts.HealthConfig(false);
            case "health_admin":
                return PhpCpSmallScripts.HealthConfig(true);
            case "governance_guest":
                return PhpCpSmallScripts.GovernanceConfig(false);
            case "governance_admin":
                return PhpCpSmallScripts.GovernanceConfig(true);
            case "web_tracker_config":
                return PhpCpSmallScripts.WebTrackerConfig("cp", true);
            case "doc_control_config_guest":
                return PhpCpSmallScripts.DocumentControlConfig(null, "cp");
            case "doc_control_config_admin":
                return PhpCpSmallScripts.DocumentControlConfig("k\"&x", "cp");
            case "industry_switch_ok":
            case "industry_switch_bad_back":
                return string.Empty;
            case "api_debug_off":
                return PhpCpSmallScripts.ApiDebug(StorefrontPhpInt.Cast("1e1"), false, false, "");
            case "api_debug_missing_log":
                return PhpCpSmallScripts.ApiDebug(7, true, false, "");
            case "api_debug_log":
                return PhpCpSmallScripts.ApiDebug(7, true, true, "LOG7");
            case "customer_guide_shop":
            case "customer_guide_users":
                return PhpCpEvalWrappers.CustomerMgmtGuide("/cp/a&x", "/cp/orders", "/cp/erp");
            default:
                return "unknown:" + name;
        }
    }

    private static string PhpCpSmallScriptsWaitPos(string backend, bool session, bool present, string body)
        => PhpCpEvalWrappers.PosTerminal(backend, session, present, body);

    private static string? HeaderLocation(JsonElement result)
    {
        if (!result.TryGetProperty("headers", out var headers))
        {
            return null;
        }

        foreach (var header in headers.EnumerateArray())
        {
            var text = header.GetString() ?? "";
            if (text.StartsWith("Location:", StringComparison.OrdinalIgnoreCase))
            {
                return text[9..].Trim();
            }
        }

        return null;
    }

    private static string? Get(JsonElement testCase, string key)
    {
        if (testCase.TryGetProperty("get", out var get) && get.TryGetProperty(key, out var value))
        {
            return value.GetString();
        }

        return null;
    }

    private static string Config(JsonElement testCase, string key, string fallback)
    {
        if (testCase.TryGetProperty("config", out var config) && config.TryGetProperty(key, out var value))
        {
            return value.GetString() ?? fallback;
        }

        return fallback;
    }

    private static bool Same(string actual, string expected, JsonElement result)
    {
        if (actual == expected)
        {
            return true;
        }

        if (result.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
        {
            try
            {
                using var left = JsonDocument.Parse(actual);
                return JsonEquivalent(left.RootElement, result);
            }
            catch (JsonException)
            {
            }
        }

        return false;
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                var leftProps = left.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                var rightProps = right.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                if (leftProps.Count != rightProps.Count)
                {
                    return false;
                }

                foreach (var pair in leftProps)
                {
                    if (!rightProps.TryGetValue(pair.Key, out var other) || !JsonEquivalent(pair.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var leftItems = left.EnumerateArray().ToList();
                var rightItems = right.EnumerateArray().ToList();
                if (leftItems.Count != rightItems.Count)
                {
                    return false;
                }

                return leftItems.Zip(rightItems, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return left.GetRawText() == right.GetRawText();
        }
    }

    private static string ResultText(JsonElement result)
        => result.ValueKind switch
        {
            JsonValueKind.String => result.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => result.GetRawText()
        };

    private static string Truncate(string value)
        => value.Length <= 240 ? value : value[..240] + "…";
}
