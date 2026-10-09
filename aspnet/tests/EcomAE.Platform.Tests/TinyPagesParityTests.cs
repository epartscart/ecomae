using System.Text.Json;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// PHP 8.3 goldens in <c>Fixtures/TinyPages</c> for the small storefront and CP includes closed in this slice.
/// </summary>
public sealed class TinyPagesParityTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "TinyPages");

    private static readonly HashSet<string> CoveredFiles = new(StringComparer.Ordinal)
    {
        StorefrontTinyPages.UsersAgreementPath,
        StorefrontTinyPages.CataBridgePath,
        StorefrontTinyPages.EpartsProductRoutePath,
        StorefrontTinyPages.VehicleIndexPath,
        StorefrontTinyPages.PosShellPath,
        StorefrontTinyPages.DocpartHrefPath,
        StorefrontTinyPages.ErrorPagesPath,
        PhpCpTinyPages.CommercePagePath,
        PhpCpTinyPages.CustomerModalPath,
        "cp/content/control/set_edit_mode_cookie.php",
        PhpCpTinyPages.LogoutModulePath
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
    public void TinyPages_MatchPhpGolden()
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
            var expectedVars = results[i].GetProperty("vars");
            var actual = Render(cases[i], expectedResult);
            var expected = expectedOutput.Length > 0 ? expectedOutput : ResultText(expectedResult);
            if (!Same(actual, expected, expectedResult))
            {
                failures.Add(name + " expected=" + expected + " got=" + actual);
            }

            if (expectedVars.ValueKind == JsonValueKind.Object && expectedVars.EnumerateObject().Any() && !VarsMatch(name, expectedVars))
            {
                failures.Add(name + " vars");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Subset(
            JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement
                .GetProperty("cases").EnumerateArray()
                .Select(c => c.TryGetProperty("file", out var file) ? file.GetString() : "cp/content/control/set_edit_mode_cookie.php")
                .ToHashSet(StringComparer.Ordinal)!,
            CoveredFiles);

    [Fact]
    public void EditModeUrl_IsAllowlistedOutsideTheCpLoginWall()
    {
        Assert.True(PhpCpTinyPages.IsScriptPath(PhpCpTinyPages.EditModeUrl));
        Assert.False(PhpCpTinyPages.IsScriptPath("/cp/shop/prices"));
    }

    private static string Render(JsonElement testCase, JsonElement expectedResult)
    {
        var name = testCase.GetProperty("name").GetString()!;
        var get = ToMap(testCase, "get");
        switch (name)
        {
            case "users_agreement":
                return StorefrontTinyPages.UsersAgreementPage();
            case "cata_bridge_config":
                return JsonSerializer.Serialize(StorefrontTinyPages.CataBridgeJsConfig);
            case "eparts_url_empty":
                return JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["vin"] = StorefrontTinyPages.VinLabel(),
                    ["url"] = StorefrontTinyPages.EpartsProductUrl("/en")
                });
            case "eparts_url_slash":
                return StorefrontTinyPages.EpartsProductUrl("/en/");
            case "eparts_url_params":
                return StorefrontTinyPages.EpartsProductUrl("/ar", new Dictionary<string, string?>
                {
                    ["article"] = "OC 90",
                    ["name"] = "ж & x",
                    ["empty"] = string.Empty,
                    ["skip"] = null
                });
            case "ucatalog_index_plain":
                return StorefrontTinyPages.VehicleIndexHtml(false, null);
            case "ucatalog_index_garage":
            case "ucatalog_index_garage_raw":
                return StorefrontTinyPages.VehicleIndexHtml(true, get["UCatalog_get_garage"]);
            case "pos_footer_unknown":
                return StorefrontTinyPages.PosFooterScripts("shop/orders");
            case "pos_footer_terminal":
                return StorefrontTinyPages.PosFooterScripts("/shop/pos/terminal/");
            case "pos_footer_tenant":
                return StorefrontTinyPages.PosFooterScripts("control/portal/epc_pos_tenant_manage");
            case "commerce_default_backend":
                return PhpCpTinyPages.CommerceRetiredNotice(null);
            case "commerce_custom_backend":
                return PhpCpTinyPages.CommerceRetiredNotice(testCase.GetProperty("config").GetProperty("backend_dir").GetString());
            case "modal_hidden":
                return PhpCpTinyPages.CustomerModal(testCase.GetProperty("vars").GetProperty("customer_id").GetInt32());
            case "modal_shown":
                return PhpCpTinyPages.CustomerModal(testCase.GetProperty("vars").GetProperty("customer_id").GetInt32());
            case "modal_string_id":
                return PhpCpTinyPages.CustomerModal(testCase.GetProperty("vars").GetProperty("customer_id").GetString());
            case "docpart_href_no_license":
            case "docpart_href_odd_license":
            case "docpart_href_even_license":
                return string.Empty;
            case "error_pages_absent":
            case "error_pages_404":
                return string.Empty;
            case "logout_module":
                return PhpCpTinyPages.LogoutModule(
                    testCase.GetProperty("admin_profile").GetProperty("name").GetString()!,
                    testCase.GetProperty("admin_session").GetProperty("csrf_guard_key").GetString()!);
            case "edit_mode_jsonp_frontend":
                return PhpCpTinyPages.EditModeJsonp(get["callback"], get["edit_mode"]);
            case "edit_mode_jsonp_unicode":
                return PhpCpTinyPages.EditModeJsonp(get["callback"], get["edit_mode"]);
            case "edit_mode_jsonp_missing":
                return PhpCpTinyPages.EditModeJsonp(null, null);
            case "edit_mode_csrf_1":
                return PhpCpTinyPages.CsrfJson("Error! CSRF 1");
            case "edit_mode_no_db":
                return PhpCpTinyPages.NoDbConnectJson();
            default:
                return "unknown:" + name;
        }
    }

    private static bool VarsMatch(string name, JsonElement expectedVars)
    {
        if (name.StartsWith("docpart_href", StringComparison.Ordinal))
        {
            var loaded = StorefrontTinyPages.DocpartHref();
            return loaded.Href == expectedVars.GetProperty("docpart_href").GetString()
                && loaded.Title == expectedVars.GetProperty("docpart_title").GetString();
        }

        if (name == "error_pages_absent")
        {
            return StorefrontTinyPages.ErrorPage(null, new Dictionary<string, string?>()) is null;
        }

        if (name == "error_pages_404")
        {
            var page = StorefrontTinyPages.ErrorPage(
                "404",
                new Dictionary<string, string?>
                {
                    ["404_value"] = "2220",
                    ["404_title_tag"] = "2221",
                    ["404_description_tag"] = "2222",
                    ["404_keywords_tag"] = "2223",
                    ["404_author_tag"] = "2224",
                    ["404_content_type"] = "2225",
                    ["404_content"] = "2226"
                });
            var body = expectedVars.GetProperty("DP_Content");
            return page is not null
                && page.Value == body.GetProperty("value").GetString()
                && page.TitleTag == body.GetProperty("title_tag").GetString()
                && page.DescriptionTag == body.GetProperty("description_tag").GetString()
                && page.KeywordsTag == body.GetProperty("keywords_tag").GetString()
                && page.AuthorTag == body.GetProperty("author_tag").GetString()
                && page.ContentType == body.GetProperty("content_type").GetString()
                && page.Content == body.GetProperty("content").GetString();
        }

        return true;
    }

    private static bool Same(string actual, string expected, JsonElement result)
    {
        if (actual == expected)
        {
            return true;
        }

        if (result.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, result);
        }
        catch (JsonException)
        {
            return false;
        }
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

    private static Dictionary<string, string?> ToMap(JsonElement element, string name)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!element.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in obj.EnumerateObject())
        {
            map[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
        }

        return map;
    }
}
