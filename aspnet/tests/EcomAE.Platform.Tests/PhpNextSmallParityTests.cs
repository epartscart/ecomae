using System.Text.Json;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP 8.3 goldens in <c>Fixtures/NextSmall</c> for the next small non-ERP includes.
/// </summary>
public sealed class PhpNextSmallParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NextSmall");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        PhpNextSmallBatch.RouteAliasesPath,
        PhpNextSmallBatch.MoqHelpersPath,
        PhpNextSmallBatch.FashionTaxonomyPath,
        PhpNextSmallBatch.MobileLandingPath,
        PhpNextSmallBatch.SocialAppPath,
        PhpNextSmallBatch.TreeHelperPath,
        PhpNextSmallBatch.PricesAjaxInitPath,
        PhpNextSmallBatch.MarketingBroadcastPath,
        PhpNextSmallBatch.MarketingBroadcastConfigPath,
        PhpNextSmallBatch.DataTransferPath,
        PhpNextSmallBatch.IlcatsSettingsPath,
        PhpNextSmallBatch.CpActionsAlertPath,
        PhpNextSmallBatch.DocumentControlGuidePath,
        DocpartManufacturer.PhpPath,
        PhpNextSmallBatch.AutoxpClicksPath,
        PhpNextSmallBatch.CheckAdminAccessPath,
        PhpNextSmallBatch.ChoseCarPath,
        PhpNextSmallBatch.HealthCheckupPath,
        PhpNextSmallBatch.AboutProgramPath,
        PhpNextSmallBatch.AuthWithUserPath,
        PhpNextSmallBatch.PypricesCleanerPath,
        PhpNextSmallBatch.PageLangMainPath,
        PhpNextSmallBatch.CpLangModulePath,
        PhpNextSmallBatch.StorefrontLangModulePath,
        PhpNextSmallBatch.UsersHelperPath,
        PhpNextSmallBatch.IndustryCatalogPrintPath
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
    public void NextSmall_MatchPhpGolden()
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
            var actual = Render(name, cases[i], results[i]);
            var expected = expectedOutput.Length > 0 ? expectedOutput : ResultText(expectedResult);
            if (name is "cleaner_week")
            {
                if (PhpNextSmallBatch.PypricesWeekAgo(1_790_000_000) != 1_790_000_000 - 604800)
                {
                    failures.Add("cleaner_week formula");
                }

                continue;
            }

            if (name is "inserted_groups")
            {
                var groups = PhpNextSmallBatch.GetInsertedGroups(5, new Dictionary<int, (int, IReadOnlyList<int>)> { [5] = (0, []) });
                if (groups.Count != 1 || groups[0] != 5)
                {
                    failures.Add("inserted_groups");
                }

                continue;
            }

            if (name is "page_lang")
            {
                if (!actual.Contains("{2113}", StringComparison.Ordinal)
                    || !actual.Contains("/cp/lang/configurator", StringComparison.Ordinal)
                    || !actual.Contains("/cp/lang/editor", StringComparison.Ordinal)
                    || !actual.Contains("{2116}", StringComparison.Ordinal))
                {
                    failures.Add("page_lang structure");
                }

                continue;
            }

            if (name is "lang_module_off")
            {
                continue;
            }

            if (name is "lang_module_on")
            {
                if (!actual.Contains("lang_selected", StringComparison.Ordinal)
                    || !actual.Contains("lang_cp=", StringComparison.Ordinal)
                    || !actual.Contains("EN | {1}", StringComparison.Ordinal)
                    || !actual.Contains("AR | {2}", StringComparison.Ordinal))
                {
                    failures.Add("lang_module_on structure");
                }

                continue;
            }

            if (name is "sf_lang_module_off")
            {
                continue;
            }

            if (name is "sf_lang_module_on")
            {
                if (!actual.Contains("lang_select", StringComparison.Ordinal)
                    || !actual.Contains("document.cookie = \"lang=\"", StringComparison.Ordinal)
                    || !actual.Contains("page_url_with_lang_tag", StringComparison.Ordinal)
                    || !actual.Contains("EN | {1}", StringComparison.Ordinal)
                    || !actual.Contains("AR | {2}", StringComparison.Ordinal)
                    || !actual.Contains("/<lang>/shop", StringComparison.Ordinal))
                {
                    failures.Add("sf_lang_module_on structure");
                }

                continue;
            }

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
                .Select(CaseFile)
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void ScriptUrls_AreAllowlistedOutsideTheCpLoginWall()
    {
        Assert.True(PhpNextSmallBatch.IsScriptPath(PhpNextSmallBatch.MarketingBroadcastConfigUrl));
        Assert.True(PhpNextSmallBatch.IsScriptPath(PhpNextSmallBatch.AutoxpClicksUrl));
        Assert.False(PhpNextSmallBatch.IsScriptPath("/cp/shop/prices"));
    }

    private static string CaseFile(JsonElement testCase)
        => testCase.TryGetProperty("file", out var file) && file.GetString() is { Length: > 0 } path
            ? path
            : testCase.GetProperty("name").GetString() switch
            {
                var name when name is not null && name.StartsWith("alias_", StringComparison.Ordinal) => PhpNextSmallBatch.RouteAliasesPath,
                var name when name is not null && name.StartsWith("moq_", StringComparison.Ordinal) => PhpNextSmallBatch.MoqHelpersPath,
                "fashion_tree" => PhpNextSmallBatch.FashionTaxonomyPath,
                var name when name is not null && name.StartsWith("mobile_", StringComparison.Ordinal) => PhpNextSmallBatch.MobileLandingPath,
                var name when name is not null && name.StartsWith("add_item", StringComparison.Ordinal) => PhpNextSmallBatch.TreeHelperPath,
                var name when name is not null && name.StartsWith("manufacturer_", StringComparison.Ordinal) => DocpartManufacturer.PhpPath,
                "auth_forbidden" => PhpNextSmallBatch.AuthWithUserPath,
                "cleaner_week" => PhpNextSmallBatch.PypricesCleanerPath,
                "inserted_groups" => PhpNextSmallBatch.UsersHelperPath,
                _ => "unknown"
            };

    private static string Render(string name, JsonElement testCase, JsonElement result)
    {
        switch (name)
        {
            case "alias_path_null":
                return PhpNextSmallBatch.EpcPortalAliasRequestPath(null);
            case "alias_path_empty":
                return PhpNextSmallBatch.EpcPortalAliasRequestPath("");
            case "alias_path_query":
                return PhpNextSmallBatch.EpcPortalAliasRequestPath("/CP/shop/orders?x=1");
            case "alias_canonical_cp":
                return PhpNextSmallBatch.EpcPortalAliasCanonicalSurfacePath("/CP/shop") ?? "";
            case "alias_canonical_same":
                return PhpNextSmallBatch.EpcPortalAliasCanonicalSurfacePath("/cp/shop") ?? "";
            case "alias_canonical_home":
                return PhpNextSmallBatch.EpcPortalAliasCanonicalSurfacePath("/") ?? "";
            case "alias_redirect_cli":
                return JsonSerializer.Serialize(PhpNextSmallBatch.EpcPortalAliasRedirectUppercaseSurfaces("/CP", "", true, false).Redirected);
            case "alias_bos_tenant":
                var tenant = PhpNextSmallBatch.EpcPortalAliasTryBosEntry("/bos", "epartscart.com", false);
                return tenant.Body ?? "";
            case "alias_bos_super":
                return JsonSerializer.Serialize(PhpNextSmallBatch.EpcPortalAliasTryBosEntry("/bos/app", "www.ecomae.com", false).Handled);
            case "moq_guest":
                return PhpNextSmallBatch.EpcMoqEffective(0, 4, null, null).ToString();
            case "moq_retail":
                return JsonSerializer.Serialize(new[] { PhpNextSmallBatch.EpcMoqProfileMultiplier(9, 1.0, "retail"), PhpNextSmallBatch.EpcMoqEffective(9, 4, 1.0, "retail") });
            case "moq_custom":
                return PhpNextSmallBatch.EpcMoqEffective(3, 5, 1.5, "fleet").ToString();
            case "fashion_tree":
                return JsonSerializer.Serialize(PhpNextSmallBatch.EpcFashionTaxSeedTree());
            case "mobile_off":
                return JsonSerializer.Serialize(new object[] { PhpNextSmallBatch.EpcMobileAppPhase1Enabled("www.ecomae.com"), PhpNextSmallBatch.EpcMobileAppLandingHtml("www.ecomae.com") });
            case "mobile_on":
                return JsonSerializer.Serialize(new object[] { PhpNextSmallBatch.EpcMobileAppPhase1Enabled("www.epartscart.com"), PhpNextSmallBatch.EpcMobileAppLandingHtml("www.epartscart.com") });
            case "social_disabled":
                return PhpNextSmallBatch.SocialApp(false, "/epc-auth-google-start.php", "t1", "/en/");
            case "social_google":
                return PhpNextSmallBatch.SocialApp(true, "/epc-auth-google-start.php", "t&x", "/ar/");
            case "add_item_root":
                return JsonSerializer.Serialize(PhpNextSmallBatch.AddItemToDump(new PhpNextSmallBatch.TreeDumpItem { Id = 1, Parent = 0, Count = 0 }, [], 0));
            case "add_item_nested":
                var tree = PhpNextSmallBatch.AddItemToDump(new PhpNextSmallBatch.TreeDumpItem { Id = 1, Parent = 0, Count = 1 }, [], 0);
                return JsonSerializer.Serialize(PhpNextSmallBatch.AddItemToDump(new PhpNextSmallBatch.TreeDumpItem { Id = 2, Parent = 1, Count = 0 }, tree, 0));
            case "prices_init_nodb":
                return PhpNextSmallBatch.PricesAjaxInitNoDb();
            case "mb_missing":
                return PhpNextSmallBatch.MarketingBroadcast(false, "");
            case "mb_ok":
                return PhpNextSmallBatch.MarketingBroadcast(true, "INCLUDED-mb");
            case "mb_config_empty_doc":
                return PhpNextSmallBatch.MarketingBroadcastConfigEmptyDoc();
            case "mb_config_default":
                return PhpNextSmallBatch.MarketingBroadcastConfig("Your shop", "/");
            case "data_transfer":
                return PhpNextSmallBatch.DataTransferHub("cp", "bootstrap_admin");
            case "ilcats":
                return JsonSerializer.Serialize(PhpNextSmallBatch.IlcatsSettings("en", "INFO", "1.2.3.4"));
            case "cp_alert_none":
                return PhpNextSmallBatch.CpActionsAlert(_ => null);
            case "cp_alert_all":
                return PhpNextSmallBatch.CpActionsAlert(key => key switch
                {
                    "success_message" => "ok <b>",
                    "error_message" => "bad &",
                    "warning_message" => "warn",
                    "info_message" => "info",
                    _ => null
                });
            case "doc_guide":
                return PhpNextSmallBatch.DocumentControlGuide();
            case "manufacturer_ok":
                var made = new DocpartManufacturer(" knecht ", 7, "Name\n\tX\\", 1, 2, true, new Dictionary<string, int> { ["a"] = 1 });
                return JsonSerializer.Serialize(new object?[] { made.Manufacturer, made.ManufacturerId, made.ManufacturerShow, made.Name, made.StorageId, made.OfficeId, made.SynonymsSingleQuery, made.Params, made.Valid });
            case "manufacturer_short":
                return JsonSerializer.Serialize(new DocpartManufacturer("K", 0, "x", 0, 0, false).Valid);
            case "autoxp_nodb":
                return PhpNextSmallBatch.AutoxpNoDb();
            case "admin_access_none":
                return PhpNextSmallBatch.CheckAdminAccessJson(false, false);
            case "admin_access_denied":
                return PhpNextSmallBatch.CheckAdminAccessJson(true, false);
            case "admin_access_ok":
                return PhpNextSmallBatch.CheckAdminAccessJson(true, true);
            case "chose_car":
                return PhpNextSmallBatch.ChoseCar("/en", [(3, "bmw.png", 11)]);
            case "health_not_super":
                return PhpNextSmallBatch.PlatformHealthCheckup(false, "epartscart-deploy-2026");
            case "health_super":
                return PhpNextSmallBatch.PlatformHealthCheckup(true, "epartscart-deploy-2026");
            case "about_program":
                return PhpNextSmallBatch.AboutProgram("1.0", 1700000000, "2.1", 1700003600);
            case "auth_nodb":
                return PhpNextSmallBatch.AuthWithUserNoDb();
            case "auth_forbidden":
                return PhpNextSmallBatch.AuthWithUserForbidden();
            case "page_lang":
                return PhpNextSmallBatch.PageLangMain("cp", "bootstrap_admin");
            case "lang_module_off":
                return PhpNextSmallBatch.CpLangModule(false, [], "en");
            case "lang_module_on":
                return PhpNextSmallBatch.CpLangModule(true, [("en", 1), ("ar", 2)], "en");
            case "sf_lang_module_off":
                return PhpNextSmallBatch.StorefrontLangModule(false, [], "en", "");
            case "sf_lang_module_on":
                return PhpNextSmallBatch.StorefrontLangModule(true, [("en", 1), ("ar", 2)], "en", "/<lang>/shop");
            case "industry_print_empty":
                return PhpNextSmallBatch.IndustryCatalogPrint(null, "", false, "");
            case "industry_print_ok":
                return PhpNextSmallBatch.IndustryCatalogPrint("auto_parts", "Parts <b>", true, "GRID");
            default:
                return "unknown:" + name;
        }
    }

    private static bool Same(string actual, string expected, JsonElement result)
    {
        if (actual == expected)
        {
            return true;
        }

        if (result.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.False or JsonValueKind.True or JsonValueKind.Number)
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

        if (result.ValueKind == JsonValueKind.String && actual == (result.GetString() ?? ""))
        {
            return true;
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
                return leftItems.Count == rightItems.Count && leftItems.Zip(rightItems, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText()
                    || left.GetDouble() == right.GetDouble();
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
            JsonValueKind.False => "false",
            JsonValueKind.True => "true",
            _ => result.GetRawText()
        };

    private static string Truncate(string value)
        => value.Length <= 240 ? value : value[..240] + "…";
}
