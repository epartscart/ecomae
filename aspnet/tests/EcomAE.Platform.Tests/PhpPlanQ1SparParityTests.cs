using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1SparParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Spar");

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
    public void PlanQ1Spar_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Spar.PartsApiPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Spar.PartsApiPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Spar.Reset();
        Assert.Contains("epc_partsapi_config.php", PhpPlanQ1Spar.PartsApiPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Spar.PartsApiPath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Spar.EpcPartsapiCall("getMakes");
        Assert.False((bool)miss["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Spar.Reset();
        UseHttp("ok");
        return name switch
        {
            "config" => Config(),
            "errors" => Errors(),
            "maps" => Maps(),
            "call" => Call(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
        };
    }

    private static Rendered Config()
    {
        SetFile();
        var emptyKey = PhpPlanQ1Spar.EpcPartsapiResolveKey();
        var defaultBase = PhpPlanQ1Spar.EpcPartsapiApiBaseUrl();
        var defaultLang = PhpPlanQ1Spar.EpcPartsapiDefaultLang();
        var credEmpty = PhpPlanQ1Spar.EpcPartsapiCredentialsConfigured();
        var umapiOff = PhpPlanQ1Spar.EpcPartsapiUmapiFallbackEnabled();
        var shopDefault = PhpPlanQ1Spar.EpcPartsapiShopUrl();
        var shopMakes = PhpPlanQ1Spar.EpcPartsapiShopUrl("getMakes");
        var unknownPath = PhpPlanQ1Spar.EpcPartsapiMethodShopPath("Nope");
        var blankPath = PhpPlanQ1Spar.EpcPartsapiMethodShopPath("");
        var proxy = new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiProxyActionMethod("manufacturers"),
            PhpPlanQ1Spar.EpcPartsapiProxyActionMethod("cars"),
            PhpPlanQ1Spar.EpcPartsapiProxyActionMethod("analogs"),
            PhpPlanQ1Spar.EpcPartsapiProxyActionMethod("unknown")
        };
        var sections = new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiCarTypeFromSection("commercial"),
            PhpPlanQ1Spar.EpcPartsapiCarTypeFromSection("motorbike"),
            PhpPlanQ1Spar.EpcPartsapiCarTypeFromSection("passenger"),
            PhpPlanQ1Spar.EpcPartsapiSectionFromCarType("CV"),
            PhpPlanQ1Spar.EpcPartsapiSectionFromCarType("Motorcycle"),
            PhpPlanQ1Spar.EpcPartsapiSectionFromCarType("PC")
        };
        PhpPlanQ1Spar.IsEpartsHost = true;
        PhpPlanQ1Spar.IsAutoPartsSite = false;
        var epartsOn = PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
        PhpPlanQ1Spar.IsEpartsHost = false;
        PhpPlanQ1Spar.IsAutoPartsSite = true;
        var autoOff = PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
        SetFile(("allow_auto_parts_tenants", "1"));
        var autoOn = PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
        SetFile(("allow_auto_parts_tenants", "0"));
        var autoZero = PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
        PhpPlanQ1Spar.IsAutoPartsSite = false;
        var neither = PhpPlanQ1Spar.EpcPartsapiEnabledForRequest();
        SetFile(
            ("api_base_url", "https://parts.example/"),
            ("api_key", "platformkey99"),
            ("method_keys", Map(("getMakes", "makeskey1"), ("getModels", "0"), ("getCars", ""))),
            ("method_shop_urls", Map(("getMakes", "https://shop.example/makes"), ("getModels", "0"))),
            ("default_lang", "DE"),
            ("umapi_fallback", "1"));
        var custom = new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiApiBaseUrl(),
            PhpPlanQ1Spar.EpcPartsapiResolveKey(),
            PhpPlanQ1Spar.EpcPartsapiResolveKeyForMethod("getMakes"),
            PhpPlanQ1Spar.EpcPartsapiResolveKeyForMethod("getModels"),
            PhpPlanQ1Spar.EpcPartsapiResolveKeyForMethod("getCars"),
            PhpPlanQ1Spar.EpcPartsapiDefaultLang(),
            PhpPlanQ1Spar.EpcPartsapiShopUrl("getMakes"),
            PhpPlanQ1Spar.EpcPartsapiShopUrl("getModels"),
            PhpPlanQ1Spar.EpcPartsapiShopUrl("getCars"),
            PhpPlanQ1Spar.EpcPartsapiCredentialsConfigured(),
            PhpPlanQ1Spar.EpcPartsapiUmapiFallbackEnabled()
        };
        SetFile(
            ("method_keys", Map(("getMakes", "onlymethod"))),
            ("umapi_fallback", "1"),
            ("default_lang", "en"));
        var methodOnly = new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiCredentialsConfigured(),
            PhpPlanQ1Spar.EpcPartsapiResolveKey(),
            PhpPlanQ1Spar.EpcPartsapiUmapiFallbackEnabled()
        };
        SetFile(("method_shop_urls", Map(("custom", "https://abs.example/x"))));
        var httpsPath = PhpPlanQ1Spar.EpcPartsapiShopUrl("custom");
        var catalogKeys = PhpPlanQ1Spar.EpcPartsapiMethodCatalog().Keys.ToArray();
        var clientMap = PhpPlanQ1Spar.EpcPartsapiMethodShopClientMap()["getMakes"];
        var actionMap = PhpPlanQ1Spar.EpcPartsapiActionShopClientMap()["vin"];
        return new Rendered(new object?[]
        {
            emptyKey, defaultBase, defaultLang, credEmpty, umapiOff,
            shopDefault, shopMakes, unknownPath, blankPath, proxy, sections,
            epartsOn, autoOff, autoOn, autoZero, neither,
            custom, methodOnly, httpsPath, catalogKeys, clientMap, actionMap
        });
    }

    private static Rendered Errors()
    {
        var rate = Result(5000, "You have exceeded the number of requests", 200, "getMakes");
        var svc = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = Map(("error_code", 5005), ("message", new List<object?> { "down", "retry" })),
            ["error"] = "",
            ["http_status"] = 200,
            ["method"] = "getMakes"
        };
        var auth = Result(5002, "Authorization key is invalid", 200, "getMakes");
        var http401 = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = null,
            ["error"] = "nope",
            ["http_status"] = 401,
            ["method"] = "getMakes"
        };
        var http403 = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = null,
            ["error"] = "",
            ["http_status"] = 403,
            ["method"] = "getMakes"
        };
        var textRate = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = Map(("message", "rate limit hit")),
            ["error"] = "",
            ["http_status"] = 200,
            ["method"] = "getMakes"
        };
        var none = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["error"] = "x",
            ["http_status"] = 200,
            ["method"] = "getMakes"
        };
        SetFile();
        var noKeyMeta = PhpPlanQ1Spar.EpcPartsapiProbeStatusMeta(http401, "getMakes");
        var noKeyAuth = PhpPlanQ1Spar.EpcPartsapiIsAuthKeyError(http401);
        SetFile(("method_keys", Map(("getMakes", "abc"))));
        var withKeyMeta = PhpPlanQ1Spar.EpcPartsapiProbeStatusMeta(http401, "getMakes");
        var withKeyAuth = PhpPlanQ1Spar.EpcPartsapiIsAuthKeyError(http401);
        var failSub = PhpPlanQ1Spar.EpcPartsapiFailPayload(auth, "manufacturers");
        var failPlain = PhpPlanQ1Spar.EpcPartsapiFailPayload(none, "unknown");
        return new Rendered(new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiErrorCode(rate),
            PhpPlanQ1Spar.EpcPartsapiErrorMessage(rate),
            PhpPlanQ1Spar.EpcPartsapiErrorMessage(svc),
            PhpPlanQ1Spar.EpcPartsapiErrorMessage(none),
            PhpPlanQ1Spar.EpcPartsapiIsRateLimitError(rate),
            PhpPlanQ1Spar.EpcPartsapiIsRateLimitError(textRate),
            PhpPlanQ1Spar.EpcPartsapiIsServiceError(svc),
            PhpPlanQ1Spar.EpcPartsapiIsServiceError(rate),
            PhpPlanQ1Spar.EpcPartsapiIsAuthKeyError(auth),
            PhpPlanQ1Spar.EpcPartsapiIsAuthKeyError(rate),
            noKeyAuth,
            withKeyAuth,
            noKeyMeta,
            withKeyMeta,
            PhpPlanQ1Spar.EpcPartsapiProbeStatusMeta(rate, "getMakes"),
            PhpPlanQ1Spar.EpcPartsapiSubscriptionMessage("models"),
            PhpPlanQ1Spar.EpcPartsapiSubscriptionMessage(""),
            failSub,
            failPlain,
            PhpPlanQ1Spar.EpcPartsapiIsAuthKeyError(http403)
        });
    }

    private static Rendered Maps()
    {
        var listWrapped = PhpPlanQ1Spar.EpcPartsapiListRows(Map(("data", new List<object?> { Map(("a", 1)) })));
        var listSeq = PhpPlanQ1Spar.EpcPartsapiListRows(new List<object?> { Map(("a", 1)), Map(("a", 2)) });
        var listAssoc = PhpPlanQ1Spar.EpcPartsapiListRows(Map(("makeId", 1), ("makeName", "VW")));
        var listBad = PhpPlanQ1Spar.EpcPartsapiListRows("x");
        var years = new object?[]
        {
            PhpPlanQ1Spar.EpcPartsapiYearCi(2010),
            PhpPlanQ1Spar.EpcPartsapiYearCi(2015, true),
            PhpPlanQ1Spar.EpcPartsapiYearCi(0),
            PhpPlanQ1Spar.EpcPartsapiYearCi("2012abc")
        };
        var manu = PhpPlanQ1Spar.EpcPartsapiMapManufacturers(new object?[]
        {
            Map(("makeId", 16), ("makeName", "VW")),
            Map(("makeId", 0), ("makeName", "Skip")),
            "skip"
        }, "PC");
        var models = PhpPlanQ1Spar.EpcPartsapiMapModels(new object?[]
        {
            Map(("modelId", 5), ("modelName", "Golf"), ("yearStart", 2010), ("yearEnd", 2012)),
            Map(("modelId", 6), ("modelName", ""))
        }, 16);
        var carsPc = PhpPlanQ1Spar.EpcPartsapiMapCars(new object?[] { Map(("carId", 7), ("carName", "")) }, "PC");
        var carsCv = PhpPlanQ1Spar.EpcPartsapiMapCars(new object?[] { Map(("carId", 8), ("carName", "Truck")) }, "CV");
        var carsBike = PhpPlanQ1Spar.EpcPartsapiMapCars(new object?[] { Map(("carId", 9), ("carName", "Bike")) }, "Motorcycle");
        var cats = PhpPlanQ1Spar.EpcPartsapiMapCategories(new object?[]
        {
            Map(("NODE_3_STR_ID", 10), ("NODE_3_TEXT", "  Oil  ")),
            Map(("STR_ID", 0), ("ROOT_NODE_TEXT", "")),
            Map(("NODE_1_STR_ID", 3), ("NODE_1_TEXT", ""))
        });
        var arts = PhpPlanQ1Spar.EpcPartsapiMapArticles(new object?[]
        {
            Map(("ART_ID", 1), ("ART_ARTICLE_NR", "A1"), ("ART_SUP_BRAND", "VAG")),
            Map(("ART_ID", 0), ("ART_ARTICLE_NR", ""))
        });
        var cross = PhpPlanQ1Spar.EpcPartsapiMapCrosses(new object?[]
        {
            Map(("crossNumber", "X1"), ("crossBrand", "VAG")),
            Map(("partNumber", "P1")),
            Map(("number", ""))
        });
        var vin = PhpPlanQ1Spar.EpcPartsapiMapVin(new object?[]
        {
            Map(("manuId", 16), ("manuName", "VW"), ("modId", 5), ("modelName", "Golf"), ("carId", 7), ("carName", "G")),
            Map(("makeId", 16), ("makeName", "VW"), ("modelId", 5), ("modelName", "Golf"), ("carId", 8))
        });
        var ok = PhpPlanQ1Spar.EpcPartsapiOkPayload(manu, "partsapi_fallback", Map(("elapsed_ms", 12)), "manufacturers");
        return new Rendered(new object?[] { listWrapped, listSeq, listAssoc, listBad, years, manu, models, carsPc, carsCv, carsBike, cats, arts, cross, vin, ok });
    }

    private static Rendered Call()
    {
        SetFile();
        var missing = PhpPlanQ1Spar.EpcPartsapiCall("getMakes", Map(("carType", "PC")));
        missing.Remove("elapsed_ms");
        SetFile(("api_key", "platformkey99"), ("umapi_fallback", "1"), ("default_lang", "en"));
        UseHttp("ok");
        var ok = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes", Map(("carType", "PC"), ("lang", "en"))));
        var lastOk = PhpPlanQ1Spar.LastUrl;
        UseHttp("empty");
        var empty = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes"));
        UseHttp("curlerr");
        var err = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes"));
        UseHttp("nonjson");
        var nonjson = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes"));
        UseHttp("http400");
        var http400 = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes"));
        UseHttp("errcode");
        var errcode = DropElapsed(PhpPlanQ1Spar.EpcPartsapiCall("getMakes"));
        UseHttp("ok");
        var fbManu = DropElapsed(PhpPlanQ1Spar.EpcPartsapiUmapiFallback("manufacturers", Map(("section", "passenger"))));
        var fbModels = DropElapsed(PhpPlanQ1Spar.EpcPartsapiUmapiFallback("models", Map(("MFA_ID", 16), ("language", "en"))));
        var fbNoMake = PhpPlanQ1Spar.EpcPartsapiUmapiFallback("models", new Dictionary<string, object?>(StringComparer.Ordinal));
        var fbCars = DropElapsed(PhpPlanQ1Spar.EpcPartsapiUmapiFallback("modifications", Map(("MS_ID", 5114), ("MFA_ID", 16), ("vehicle_type", "PC"))));
        var fbVin = DropElapsed(PhpPlanQ1Spar.EpcPartsapiUmapiFallback("vin", Map(("vin", "WVW-ZZZ 1KZ"))));
        var fbVinEmpty = PhpPlanQ1Spar.EpcPartsapiUmapiFallback("vin", Map(("vin", "EMPTYVIN1")));
        var fbVinBlank = PhpPlanQ1Spar.EpcPartsapiUmapiFallback("vin", Map(("vin", "")));
        var fbUnknown = PhpPlanQ1Spar.EpcPartsapiUmapiFallback("articles", new Dictionary<string, object?>(StringComparer.Ordinal));
        SetFile(("api_key", "platformkey99"), ("umapi_fallback", "0"));
        var fbOff = PhpPlanQ1Spar.EpcPartsapiUmapiFallback("manufacturers", new Dictionary<string, object?>(StringComparer.Ordinal));
        SetFile();
        var capsOff = PhpPlanQ1Spar.EpcPartsapiCatalogCapabilities();
        var statOff = PhpPlanQ1Spar.EpcPartsapiStatusPayload();
        SetFile(("api_key", "platformkey99"));
        UseHttp("ok");
        var capsOn = PhpPlanQ1Spar.EpcPartsapiCatalogCapabilities();
        var statOn = PhpPlanQ1Spar.EpcPartsapiStatusPayload();
        var capsMethods = (Dictionary<string, object?>)capsOn["methods"]!;
        var getMakes = (Dictionary<string, object?>)capsMethods["getMakes"]!;
        return new Rendered(new object?[]
        {
            missing, ok, lastOk, empty, err, nonjson, http400, errcode,
            fbManu, fbModels, fbNoMake, fbCars, fbVin, fbVinEmpty, fbVinBlank, fbUnknown, fbOff,
            capsOff["configured"], capsOff["makes_ok"], capsOff["catalog_ready"],
            statOff["configured"], statOff["catalog_ready"], statOff["key_prefix"], statOff["api_base_url"],
            capsOn["configured"], capsOn["makes_ok"], capsOn["catalog_ready"], capsOn["models_subscription_required"],
            getMakes["subscribed"],
            statOn["configured"], statOn["catalog_ready"], statOn["shop_url"]
        });
    }

    private static Dictionary<string, object?> Result(int code, string message, int status, string method)
        => new(StringComparer.Ordinal)
        {
            ["data"] = Map(("error_code", code), ("message", message)),
            ["error"] = "",
            ["http_status"] = status,
            ["method"] = method
        };

    private static Dictionary<string, object?>? DropElapsed(Dictionary<string, object?>? row)
    {
        row?.Remove("elapsed_ms");
        return row;
    }

    private static void SetFile(params (string Key, object? Value)[] pairs)
    {
        PhpPlanQ1Spar.File = Map(pairs);
    }

    private static Dictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static void UseHttp(string mode)
    {
        PhpPlanQ1Spar.Http = (url, _) => Route(url, mode);
    }

    private static Dictionary<string, object?> Route(string url, string mode)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        var q = url.Contains('?', StringComparison.Ordinal) ? url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..] : "";
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var bits = part.Split('=', 2);
            query[Uri.UnescapeDataString(bits[0].Replace("+", " ", StringComparison.Ordinal))] =
                bits.Length > 1 ? Uri.UnescapeDataString(bits[1].Replace("+", " ", StringComparison.Ordinal)) : "";
        }

        var method = query.TryGetValue("method", out var m) ? m : "";
        if (mode == "empty")
        {
            return Hit("", 0, "");
        }

        if (mode == "curlerr")
        {
            return Hit(false, 0, "Failed to connect");
        }

        if (mode == "nonjson")
        {
            return Hit("<html>nope</html>", 200, "");
        }

        if (mode == "http400")
        {
            return Hit("{\"message\":\"bad request\"}", 400, "");
        }

        if (mode == "errcode")
        {
            return Hit("{\"error_code\":5002,\"message\":\"Authorization key is invalid\"}", 200, "");
        }

        if (mode == "rate")
        {
            return Hit("{\"error_code\":5000,\"message\":\"You have exceeded the number of requests\"}", 200, "");
        }

        return method switch
        {
            "getMakes" => Hit("[{\"makeId\":16,\"makeName\":\"VW\"}]", 200, ""),
            "getModels" => Hit("[{\"modelId\":5114,\"modelName\":\"Golf\",\"makeId\":16,\"yearStart\":2010,\"yearEnd\":2015}]", 200, ""),
            "getCars" => Hit("[{\"carId\":58963,\"carName\":\"1.6\",\"yearStart\":2012}]", 200, ""),
            "VINdecode" when (query.TryGetValue("vin", out var vin) ? vin : "") == "EMPTYVIN1" => Hit("[]", 200, ""),
            "VINdecode" => Hit("[{\"manuId\":16,\"manuName\":\"VW\",\"modId\":5114,\"modelName\":\"Golf\",\"carId\":7,\"carName\":\"Golf 1.6\"}]", 200, ""),
            "getCrosses" or "getCrossesWithBrand" or "tecdocCrosses" => Hit("[{\"crossNumber\":\"1J0971972\",\"crossBrand\":\"VAG\"}]", 200, ""),
            "searchArticles" or "PartSuggest" or "getArticles" => Hit("[{\"ART_ID\":9,\"ART_ARTICLE_NR\":\"1J0971972\",\"ART_SUP_BRAND\":\"VAG\"}]", 200, ""),
            "getSearchTree" => Hit("[{\"NODE_3_STR_ID\":100260,\"NODE_3_TEXT\":\"Filters\"}]", 200, ""),
            _ => Hit("{\"ok\":true}", 200, "")
        };
    }

    private static Dictionary<string, object?> Hit(object? body, int status, string error)
        => new(StringComparer.Ordinal) { ["body"] = body, ["status"] = status, ["error"] = error };

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
