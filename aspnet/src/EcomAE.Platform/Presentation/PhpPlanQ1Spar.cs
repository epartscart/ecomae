using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-spar PartsAPI config. PHP identifiers kept for the inventory:
/// <c>epc_partsapi_file_config</c>, <c>epc_partsapi_api_base_url</c>,
/// <c>epc_partsapi_resolve_key</c>, <c>epc_partsapi_resolve_key_for_method</c>,
/// <c>epc_partsapi_method_catalog</c>, <c>epc_partsapi_method_shop_path</c>,
/// <c>epc_partsapi_proxy_action_method</c>, <c>epc_partsapi_shop_url</c>,
/// <c>epc_partsapi_method_shop_client_map</c>, <c>epc_partsapi_action_shop_client_map</c>,
/// <c>epc_partsapi_error_code</c>, <c>epc_partsapi_error_message</c>,
/// <c>epc_partsapi_message_has_rate_limit</c>, <c>epc_partsapi_is_rate_limit_error</c>,
/// <c>epc_partsapi_is_service_error</c>, <c>epc_partsapi_is_auth_key_error</c>,
/// <c>epc_partsapi_probe_status_meta</c>, <c>epc_partsapi_subscription_message</c>,
/// <c>epc_partsapi_fail_payload</c>, <c>epc_partsapi_default_lang</c>,
/// <c>epc_partsapi_umapi_fallback_enabled</c>, <c>epc_partsapi_enabled_for_request</c>,
/// <c>epc_partsapi_credentials_configured</c>, <c>epc_partsapi_car_type_from_section</c>,
/// <c>epc_partsapi_section_from_car_type</c>, <c>epc_partsapi_call</c>,
/// <c>epc_partsapi_list_rows</c>, <c>epc_partsapi_year_ci</c>,
/// <c>epc_partsapi_map_manufacturers</c>, <c>epc_partsapi_map_models</c>,
/// <c>epc_partsapi_map_cars</c>, <c>epc_partsapi_map_categories</c>,
/// <c>epc_partsapi_map_articles</c>, <c>epc_partsapi_map_crosses</c>,
/// <c>epc_partsapi_map_vin</c>, <c>epc_partsapi_ok_payload</c>,
/// <c>epc_partsapi_catalog_capabilities</c>, <c>epc_partsapi_status_payload</c>,
/// <c>epc_partsapi_umapi_fallback</c>.
/// GET never mints a session cookie. HTTP and leftover catalog/portal parents stay injected.
/// </summary>
public static class PhpPlanQ1Spar
{
    public const string PartsApiPath = "content/general_pages/epc_partsapi_config.php";

    public static Dictionary<string, object?> File { get; set; } = new(StringComparer.Ordinal);
    public static Func<string, int, Dictionary<string, object?>> Http { get; set; } =
        (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["body"] = "",
            ["status"] = 0,
            ["error"] = ""
        };
    public static Func<string, int, string> SanitizeCategoryName { get; set; } =
        (name, id) =>
        {
            name = name.Trim();
            return name != "" ? name : "Cat " + id;
        };
    public static bool IsEpartsHost { get; set; }
    public static bool IsAutoPartsSite { get; set; }
    public static int ElapsedMs { get; set; }
    public static string LastUrl { get; set; } = "";

    public static void Reset()
    {
        File = new Dictionary<string, object?>(StringComparer.Ordinal);
        Http = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["body"] = "",
            ["status"] = 0,
            ["error"] = ""
        };
        SanitizeCategoryName = (name, id) =>
        {
            name = name.Trim();
            return name != "" ? name : "Cat " + id;
        };
        IsEpartsHost = false;
        IsAutoPartsSite = false;
        ElapsedMs = 0;
        LastUrl = "";
    }

    public static Dictionary<string, object?> EpcPartsapiFileConfig()
        => File;

    public static string EpcPartsapiApiBaseUrl()
    {
        var file = EpcPartsapiFileConfig();
        var baseUrl = (file.TryGetValue("api_base_url", out var raw) ? Convert.ToString(raw) : "")?.Trim() ?? "";
        return baseUrl != "" ? baseUrl.TrimEnd('/') : "https://api.partsapi.ru";
    }

    public static string EpcPartsapiResolveKey()
        => EpcPartsapiResolveKeyForMethod("");

    public static string EpcPartsapiResolveKeyForMethod(string method = "")
    {
        var file = EpcPartsapiFileConfig();
        method = method.Trim();
        if (method != "")
        {
            var methodKeys = AsMap(file.TryGetValue("method_keys", out var mk) ? mk : null);
            if (methodKeys.TryGetValue(method, out var keyed) && !PhpEmpty(keyed))
            {
                var key = (Convert.ToString(keyed) ?? "").Trim();
                if (key != "")
                {
                    return key;
                }
            }
        }

        return (file.TryGetValue("api_key", out var api) ? Convert.ToString(api) : "")?.Trim() ?? "";
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPartsapiMethodCatalog()
        => new(StringComparer.Ordinal)
        {
            ["getMakes"] = Meta("Vehicle makes", "/account/shop?method=getMakes", Probe(("carType", "PC"), ("lang", "en"))),
            ["getModels"] = Meta("Vehicle models", "/account/shop?method=getModels", Probe(("carType", "PC"), ("makeId", 16), ("lang", "en"))),
            ["getCars"] = Meta("Vehicle modifications", "/account/shop?method=getCars", Probe(("carType", "PC"), ("makeId", 16), ("modelId", 5114), ("lang", "en"))),
            ["getSearchTree"] = Meta("Product categories", "/account/shop?method=getSearchTree", Probe(("carType", "PC"), ("carId", 58963), ("lang", "en"))),
            ["getArticles"] = Meta("Catalog articles", "/account/shop?method=getArticles", Probe(("carType", "PC"), ("carId", 58963), ("strId", 100260), ("lang", "en"))),
            ["searchArticles"] = Meta("Part number search", "/account/shop?method=searchArticles", Probe(("SEARCH_NUMBER", "1J0971972"), ("LANG", "en"))),
            ["PartSuggest"] = Meta("Part suggestions", "/account/shop?method=PartSuggest", Probe(("oem", "1J0971972"))),
            ["getCrosses"] = Meta("Cross references", "/account/shop?method=getCrosses", Probe(("number", "1J0971972"))),
            ["getCrossesWithBrand"] = Meta("Cross references (with brand)", "/account/shop?method=getCrossesWithBrand", Probe(("number", "1J0971972"), ("brand", "VAG"))),
            ["tecdocCrosses"] = Meta("TecDoc crosses", "/account/shop?method=tecdocCrosses", Probe(("number", "1J0971972"), ("brand", "VAG"))),
            ["VINdecode"] = Meta("VIN decode", "/account/shop?method=VINdecode", Probe(("vin", "WVWZZZ1KZAW123456"), ("lang", "en")))
        };

    public static string EpcPartsapiMethodShopPath(string method = "")
    {
        method = method.Trim();
        if (method == "")
        {
            return "/account/shop";
        }

        var catalog = EpcPartsapiMethodCatalog();
        if (catalog.TryGetValue(method, out var meta))
        {
            var path = (meta.TryGetValue("shop_path", out var raw) ? Convert.ToString(raw) : "")?.Trim() ?? "";
            if (path != "")
            {
                return path;
            }
        }

        return "/account/shop?method=" + Uri.EscapeDataString(method);
    }

    public static string EpcPartsapiProxyActionMethod(string action)
    {
        action = action.ToLowerInvariant();
        return action switch
        {
            "manufacturers" => "getMakes",
            "models" => "getModels",
            "modifications" or "cars" => "getCars",
            "categories" => "getSearchTree",
            "articles" or "products" => "getArticles",
            "part_search" or "search" => "searchArticles",
            "crosses" or "analogs" => "getCrosses",
            "vin" => "VINdecode",
            _ => ""
        };
    }

    public static string EpcPartsapiShopUrl(string method = "")
    {
        var file = EpcPartsapiFileConfig();
        method = method.Trim();
        if (method != "")
        {
            var overrides = AsMap(file.TryGetValue("method_shop_urls", out var raw) ? raw : null);
            if (overrides.TryGetValue(method, out var over) && !PhpEmpty(over))
            {
                var url = (Convert.ToString(over) ?? "").Trim();
                if (url != "")
                {
                    return url;
                }
            }
        }

        var path = EpcPartsapiMethodShopPath(method);
        if (Regex.IsMatch(path, "^https?://", RegexOptions.IgnoreCase))
        {
            return path;
        }

        return "https://partsapi.ru" + (path != "" ? path : "/account/shop");
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPartsapiMethodShopClientMap()
    {
        var outMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        foreach (var (method, meta) in EpcPartsapiMethodCatalog())
        {
            outMap[method] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = Convert.ToString(meta.TryGetValue("label", out var label) ? label : method) ?? method,
                ["shop_url"] = EpcPartsapiShopUrl(method)
            };
        }

        return outMap;
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcPartsapiActionShopClientMap()
    {
        var outMap = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var actions = new[] { "manufacturers", "models", "modifications", "cars", "categories", "articles", "products", "part_search", "search", "crosses", "analogs", "vin" };
        var catalog = EpcPartsapiMethodCatalog();
        foreach (var action in actions)
        {
            var method = EpcPartsapiProxyActionMethod(action);
            if (method == "")
            {
                continue;
            }

            outMap[action] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["method"] = method,
                ["label"] = Convert.ToString(catalog.TryGetValue(method, out var meta) && meta.TryGetValue("label", out var label) ? label : method) ?? method,
                ["shop_url"] = EpcPartsapiShopUrl(method)
            };
        }

        return outMap;
    }

    public static int EpcPartsapiErrorCode(Dictionary<string, object?> result)
    {
        if (result.TryGetValue("data", out var data) && data is Dictionary<string, object?> map && map.TryGetValue("error_code", out var code))
        {
            return PhpInt(code);
        }

        return 0;
    }

    public static string EpcPartsapiErrorMessage(Dictionary<string, object?> result)
    {
        if (result.TryGetValue("data", out var data) && data is Dictionary<string, object?> map && map.TryGetValue("message", out var message) && !PhpEmpty(message))
        {
            if (message is List<object?> list)
            {
                return string.Join("; ", list.Select(x => Convert.ToString(x) ?? ""));
            }

            return Convert.ToString(message) ?? "";
        }

        return result.TryGetValue("error", out var err) ? Convert.ToString(err) ?? "" : "";
    }

    public static bool EpcPartsapiMessageHasRateLimit(string message)
    {
        message = message.ToLowerInvariant();
        return message != "" && (message.Contains("exceeded the number of requests", StringComparison.Ordinal) || message.Contains("rate limit", StringComparison.Ordinal));
    }

    public static bool EpcPartsapiIsRateLimitError(Dictionary<string, object?> result)
        => EpcPartsapiErrorCode(result) == 5000 || EpcPartsapiMessageHasRateLimit(EpcPartsapiErrorMessage(result));

    public static bool EpcPartsapiIsServiceError(Dictionary<string, object?> result)
    {
        var code = EpcPartsapiErrorCode(result);
        return code is 5005 or 5007;
    }

    public static bool EpcPartsapiIsAuthKeyError(Dictionary<string, object?> result)
    {
        if (EpcPartsapiIsRateLimitError(result) || EpcPartsapiIsServiceError(result))
        {
            return false;
        }

        var code = EpcPartsapiErrorCode(result);
        if (code == 5002)
        {
            return true;
        }

        if (code is 5000 or 5005 or 5007)
        {
            return false;
        }

        var message = EpcPartsapiErrorMessage(result).ToLowerInvariant();
        if (message != "" && (message.Contains("authorization key", StringComparison.Ordinal) || message.Contains("авторизац", StringComparison.Ordinal)))
        {
            return true;
        }

        var status = PhpInt(result.TryGetValue("http_status", out var st) ? st : 0);
        if (status is 401 or 403)
        {
            var method = (result.TryGetValue("method", out var m) ? Convert.ToString(m) : "")?.Trim() ?? "";
            if (method != "" && EpcPartsapiResolveKeyForMethod(method) != "")
            {
                return false;
            }

            return true;
        }

        return false;
    }

    public static Dictionary<string, object?> EpcPartsapiProbeStatusMeta(Dictionary<string, object?> result, string method = "")
    {
        method = method.Trim();
        if (method == "" && result.TryGetValue("method", out var raw) && !PhpEmpty(raw))
        {
            method = Convert.ToString(raw) ?? "";
        }

        var keyConfigured = method != "" && EpcPartsapiResolveKeyForMethod(method) != "";
        var rateLimited = EpcPartsapiIsRateLimitError(result);
        var serviceError = EpcPartsapiIsServiceError(result);
        var authError = EpcPartsapiIsAuthKeyError(result);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["error_code"] = EpcPartsapiErrorCode(result),
            ["rate_limited"] = rateLimited,
            ["service_error"] = serviceError,
            ["key_authenticated"] = keyConfigured && !authError,
            ["subscription_required"] = !(result.TryGetValue("ok", out var ok) && IsTrue(ok)) && authError
        };
    }

    public static string EpcPartsapiSubscriptionMessage(string action = "")
    {
        var method = EpcPartsapiProxyActionMethod(action);
        if (method == "")
        {
            method = action.Trim();
        }

        var catalog = EpcPartsapiMethodCatalog();
        var label = catalog.TryGetValue(method, out var meta) && meta.TryGetValue("label", out var raw)
            ? Convert.ToString(raw) ?? method
            : method != "" ? method : "this API method";
        var shop = EpcPartsapiShopUrl(method);
        return "Subscribe to " + label + " (" + (method != "" ? method : "method") + ") at " + shop + " and add the method key to config.epc-partsapi.php under method_keys.";
    }

    public static Dictionary<string, object?> EpcPartsapiFailPayload(Dictionary<string, object?> result, string action = "")
    {
        var method = EpcPartsapiProxyActionMethod(action);
        if (method == "" && result.TryGetValue("method", out var raw) && !PhpEmpty(raw))
        {
            method = Convert.ToString(raw) ?? "";
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = result.TryGetValue("error", out var err) ? Convert.ToString(err) ?? "Request failed." : "Request failed.",
            ["http_status"] = PhpInt(result.TryGetValue("http_status", out var st) ? st : 502),
            ["elapsed_ms"] = PhpInt(result.TryGetValue("elapsed_ms", out var el) ? el : 0)
        };
        if (method != "")
        {
            payload["method"] = method;
        }

        var meta = EpcPartsapiProbeStatusMeta(result, method);
        payload["error_code"] = meta["error_code"];
        payload["rate_limited"] = meta["rate_limited"];
        payload["service_error"] = meta["service_error"];
        if (!PhpEmpty(meta["subscription_required"]))
        {
            payload["subscription_required"] = true;
            payload["error"] = EpcPartsapiSubscriptionMessage(action);
            payload["shop_url"] = EpcPartsapiShopUrl(method);
        }

        return payload;
    }

    public static string EpcPartsapiDefaultLang()
    {
        var lang = ((EpcPartsapiFileConfig().TryGetValue("default_lang", out var raw) ? Convert.ToString(raw) : "en") ?? "en").Trim().ToLowerInvariant();
        return Regex.IsMatch(lang, "^[a-z]{2}$") ? lang : "en";
    }

    public static bool EpcPartsapiUmapiFallbackEnabled()
    {
        var file = EpcPartsapiFileConfig();
        return !PhpEmpty(file.TryGetValue("umapi_fallback", out var raw) ? raw : null) && EpcPartsapiResolveKey() != "";
    }

    public static bool EpcPartsapiEnabledForRequest()
    {
        if (IsEpartsHost)
        {
            return true;
        }

        if (IsAutoPartsSite)
        {
            return !PhpEmpty(EpcPartsapiFileConfig().TryGetValue("allow_auto_parts_tenants", out var raw) ? raw : null);
        }

        return false;
    }

    public static bool EpcPartsapiCredentialsConfigured()
    {
        if (EpcPartsapiResolveKey() != "")
        {
            return true;
        }

        var methodKeys = AsMap(EpcPartsapiFileConfig().TryGetValue("method_keys", out var raw) ? raw : null);
        foreach (var key in methodKeys.Values)
        {
            if ((Convert.ToString(key) ?? "").Trim() != "")
            {
                return true;
            }
        }

        return false;
    }

    public static string EpcPartsapiCarTypeFromSection(string section)
    {
        section = section.ToLowerInvariant();
        return section switch
        {
            "commercial" => "CV",
            "motorbike" => "Motorcycle",
            _ => "PC"
        };
    }

    public static string EpcPartsapiSectionFromCarType(string carType)
        => carType switch
        {
            "CV" => "commercial",
            "Motorcycle" => "motorbike",
            _ => "passenger"
        };

    public static Dictionary<string, object?> EpcPartsapiCall(string method, Dictionary<string, object?>? parameters = null, int timeout = 25)
    {
        var key = EpcPartsapiResolveKeyForMethod(method);
        if (key == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http_status"] = 503,
                ["error"] = "PartsAPI key is not configured for " + method + ".",
                ["data"] = null,
                ["elapsed_ms"] = 0,
                ["method"] = method
            };
        }

        var query = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["method"] = method,
            ["key"] = key
        };
        if (parameters != null)
        {
            foreach (var (k, v) in parameters)
            {
                query[k] = v;
            }
        }

        var url = EpcPartsapiApiBaseUrl() + "?" + PhpQuery(query);
        LastUrl = url;
        var hit = Http(url, timeout);
        var body = hit.TryGetValue("body", out var rawBody) ? rawBody : "";
        var status = PhpInt(hit.TryGetValue("status", out var st) ? st : 0);
        var curlError = hit.TryGetValue("error", out var err) ? Convert.ToString(err) ?? "" : "";
        var elapsedMs = ElapsedMs;
        if (body is false || body is null || Convert.ToString(body) == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http_status"] = status != 0 ? status : 502,
                ["error"] = curlError != "" ? curlError : "Empty response from PartsAPI.",
                ["data"] = null,
                ["elapsed_ms"] = elapsedMs
            };
        }

        var bodyText = Convert.ToString(body) ?? "";
        object? data;
        try
        {
            data = DecodeJson(bodyText);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http_status"] = status != 0 ? status : 502,
                ["error"] = "Non-JSON response from PartsAPI.",
                ["data"] = null,
                ["elapsed_ms"] = elapsedMs
            };
        }

        if (status >= 400)
        {
            var msg = EpcPartsapiErrorMessage(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["error"] = "PartsAPI HTTP " + status,
                ["data"] = data
            });
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http_status"] = status,
                ["error"] = msg,
                ["data"] = data,
                ["elapsed_ms"] = elapsedMs,
                ["method"] = method
            };
        }

        if (data is Dictionary<string, object?> errMap && errMap.TryGetValue("error_code", out var code) && PhpInt(code) > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["http_status"] = status != 0 ? status : 502,
                ["error"] = EpcPartsapiErrorMessage(new Dictionary<string, object?>(StringComparer.Ordinal) { ["data"] = errMap }),
                ["data"] = errMap,
                ["elapsed_ms"] = elapsedMs,
                ["method"] = method
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["http_status"] = status,
            ["error"] = "",
            ["data"] = data,
            ["elapsed_ms"] = elapsedMs,
            ["method"] = method
        };
    }

    public static List<object?> EpcPartsapiListRows(object? data)
    {
        if (data is not Dictionary<string, object?> and not List<object?>)
        {
            return [];
        }

        if (data is Dictionary<string, object?> map && map.TryGetValue("data", out var inner) && inner is List<object?> innerList)
        {
            return innerList;
        }

        if (data is List<object?> list)
        {
            return list;
        }

        return [data];
    }

    public static string EpcPartsapiYearCi(object? year, bool end = false)
    {
        var n = PhpInt(year);
        return n > 0 ? (end ? n.ToString("0000", CultureInfo.InvariantCulture) + "-12-31" : n.ToString("0000", CultureInfo.InvariantCulture) + "-01-01") : "";
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapManufacturers(IEnumerable<object?> rows, string carType)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var id = PhpInt(Get(row, "makeId"));
            var name = Str(Get(row, "makeName")).Trim();
            if (id <= 0 || name == "")
            {
                continue;
            }

            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["MFA_ID"] = id,
                ["MANUFACTURER"] = name,
                ["makeId"] = id,
                ["makeName"] = name,
                ["EPART_TYPES"] = new List<object?> { carType }
            });
        }

        return outRows;
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapModels(IEnumerable<object?> rows, int makeId)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var id = PhpInt(Get(row, "modelId"));
            var name = Str(Get(row, "modelName")).Trim();
            if (id <= 0 || name == "")
            {
                continue;
            }

            var item = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["MS_ID"] = id,
                ["MODEL_SERIES"] = name,
                ["modelId"] = id,
                ["modelName"] = name,
                ["MFA_ID"] = PhpInt(Get(row, "makeId", makeId))
            };
            var from = EpcPartsapiYearCi(Get(row, "yearStart"));
            if (from != "")
            {
                item["CI_FROM"] = from;
            }

            var to = EpcPartsapiYearCi(Get(row, "yearEnd"), true);
            if (to != "")
            {
                item["CI_TO"] = to;
            }

            outRows.Add(item);
        }

        return outRows;
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapCars(IEnumerable<object?> rows, string carType)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var carId = PhpInt(Get(row, "carId"));
            var name = Str(Get(row, "carName")).Trim();
            if (carId <= 0)
            {
                continue;
            }

            var item = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ID"] = carId,
                ["carId"] = carId,
                ["carName"] = name,
                ["MODIFICATION"] = name != "" ? name : "Vehicle " + carId,
                ["POWER_KW"] = Get(row, "POWER_KW", ""),
                ["POWER_PS"] = Get(row, "POWER_PS", ""),
                ["FUEL_TYPE"] = Get(row, "ENGINE_TYPE_EN", Get(row, "ENGINE_TYPE_RU", "")),
                ["CAPACITY"] = Get(row, "CAPACITY", "")
            };
            var from = EpcPartsapiYearCi(Get(row, "yearStart"));
            if (from != "")
            {
                item["CI_FROM"] = from;
            }

            var to = EpcPartsapiYearCi(Get(row, "yearEnd"), true);
            if (to != "")
            {
                item["CI_TO"] = to;
            }

            if (carType == "CV")
            {
                item["CV_ID"] = carId;
                item["COMMERCIAL_VEHICLE"] = item["MODIFICATION"];
            }
            else if (carType == "Motorcycle")
            {
                item["MTB_ID"] = carId;
                item["MOTORBIKE"] = item["MODIFICATION"];
            }
            else
            {
                item["PC_ID"] = carId;
                item["PASSENGER_CAR"] = item["MODIFICATION"];
            }

            outRows.Add(item);
        }

        return outRows;
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapCategories(IEnumerable<object?> rows)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var strId = PhpInt(Get(row, "NODE_3_STR_ID", Get(row, "NODE_2_STR_ID", Get(row, "NODE_1_STR_ID", Get(row, "STR_ID")))));
            var name = SanitizeCategoryName(
                Str(Get(row, "NODE_3_TEXT", Get(row, "NODE_2_TEXT", Get(row, "NODE_1_TEXT", Get(row, "ROOT_NODE_TEXT"))))).Trim(),
                strId);
            if (strId <= 0 && name == "")
            {
                continue;
            }

            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["CATEGORY_ID"] = strId,
                ["CATEGORY_NAME"] = name != "" ? name : "Category " + strId,
                ["STR_ID"] = strId,
                ["STR_LEVEL"] = Get(row, "STR_LEVEL", ""),
                ["NODE_1_TEXT"] = Get(row, "NODE_1_TEXT", ""),
                ["NODE_2_TEXT"] = Get(row, "NODE_2_TEXT", ""),
                ["NODE_3_TEXT"] = Get(row, "NODE_3_TEXT", "")
            });
        }

        return outRows;
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapArticles(IEnumerable<object?> rows)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var artId = PhpInt(Get(row, "ART_ID"));
            var article = Str(Get(row, "ART_ARTICLE_NR")).Trim();
            var brand = Str(Get(row, "ART_SUP_BRAND", Get(row, "SUP_BRAND"))).Trim();
            if (artId <= 0 && article == "")
            {
                continue;
            }

            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ART_ID"] = artId,
                ["ART_ARTICLE_NR"] = article,
                ["ART_SUP_BRAND"] = brand,
                ["SUP_BRAND"] = brand,
                ["PRODUCT_GROUP"] = Get(row, "PRODUCT_GROUP", ""),
                ["PT_ID"] = Get(row, "PT_ID", ""),
                ["SUP_ID"] = Get(row, "SUP_ID", "")
            });
        }

        return outRows;
    }

    public static List<Dictionary<string, object?>> EpcPartsapiMapCrosses(IEnumerable<object?> rows)
    {
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var num = Str(Get(row, "crossNumber", Get(row, "partNumber", Get(row, "number")))).Trim();
            var brand = Str(Get(row, "crossBrand", Get(row, "brand"))).Trim();
            if (num == "")
            {
                continue;
            }

            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ART_ARTICLE_NR"] = num,
                ["ART_SUP_BRAND"] = brand,
                ["crossBrand"] = brand,
                ["crossNumber"] = num
            });
        }

        return outRows;
    }

    public static Dictionary<string, object?> EpcPartsapiMapVin(IEnumerable<object?> rows)
    {
        var manufacturers = new List<Dictionary<string, object?>>();
        var models = new List<Dictionary<string, object?>>();
        var vehicles = new List<Dictionary<string, object?>>();
        var manuSeen = new HashSet<int>();
        var modelSeen = new HashSet<int>();
        foreach (var raw in rows)
        {
            if (raw is not Dictionary<string, object?> row)
            {
                continue;
            }

            var manuId = PhpInt(Get(row, "manuId", Get(row, "makeId")));
            var modelId = PhpInt(Get(row, "modId", Get(row, "modelId")));
            var carId = PhpInt(Get(row, "carId"));
            var manuName = Str(Get(row, "manuName", Get(row, "makeName"))).Trim();
            var modelName = Str(Get(row, "modelName")).Trim();
            var carName = Str(Get(row, "carName")).Trim();
            if (manuId > 0 && manuName != "" && manuSeen.Add(manuId))
            {
                manufacturers.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["manuId"] = manuId,
                    ["manuName"] = manuName
                });
            }

            if (modelId > 0 && modelName != "" && modelSeen.Add(modelId))
            {
                models.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["modelId"] = modelId,
                    ["modelName"] = modelName,
                    ["manuId"] = manuId
                });
            }

            if (carId > 0)
            {
                vehicles.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["carId"] = carId,
                    ["manuId"] = manuId,
                    ["modelId"] = modelId,
                    ["carName"] = carName != "" ? carName : (manuName + " " + modelName).Trim(),
                    ["vehicleTypeDescription"] = carName,
                    ["linkageTargetId"] = carId
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["matchingManufacturers"] = manufacturers,
            ["matchingModels"] = models,
            ["matchingVehicles"] = vehicles
        };
    }

    public static Dictionary<string, object?> EpcPartsapiOkPayload(IEnumerable<object?> rows, string source, Dictionary<string, object?> result, string action = "")
    {
        var list = rows.ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["action"] = action,
            ["rows"] = list.Count,
            ["data"] = list,
            ["source"] = source,
            ["elapsed_ms"] = PhpInt(result.TryGetValue("elapsed_ms", out var el) ? el : 0)
        };
    }

    public static Dictionary<string, object?> EpcPartsapiCatalogCapabilities()
    {
        var configured = EpcPartsapiCredentialsConfigured();
        var makesOk = false;
        var modelsOk = false;
        var methodStatus = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (configured)
        {
            foreach (var (method, meta) in EpcPartsapiMethodCatalog())
            {
                var probe = AsMap(meta.TryGetValue("probe", out var raw) ? raw : null);
                var r = EpcPartsapiCall(method, probe, 8);
                if (method == "getMakes")
                {
                    makesOk = IsTrue(r["ok"]);
                }

                if (method == "getModels")
                {
                    modelsOk = IsTrue(r["ok"]);
                }

                var statusMeta = EpcPartsapiProbeStatusMeta(r, method);
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = Convert.ToString(meta.TryGetValue("label", out var label) ? label : method) ?? method,
                    ["shop_url"] = EpcPartsapiShopUrl(method),
                    ["shop_path"] = EpcPartsapiMethodShopPath(method),
                    ["key_configured"] = EpcPartsapiResolveKeyForMethod(method) != "",
                    ["probe"] = probe,
                    ["subscribed"] = r["ok"],
                    ["http_status"] = PhpInt(r.TryGetValue("http_status", out var hs) ? hs : 0),
                    ["error"] = Convert.ToString(r.TryGetValue("error", out var er) ? er : "") ?? ""
                };
                foreach (var (k, v) in statusMeta)
                {
                    row[k] = v;
                }

                methodStatus[method] = row;
            }
        }
        else
        {
            foreach (var (method, meta) in EpcPartsapiMethodShopClientMap())
            {
                methodStatus[method] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = meta["label"],
                    ["shop_url"] = meta["shop_url"],
                    ["shop_path"] = EpcPartsapiMethodShopPath(method),
                    ["key_configured"] = false,
                    ["subscribed"] = false,
                    ["subscription_required"] = false
                };
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["configured"] = configured,
            ["makes_ok"] = makesOk,
            ["catalog_ready"] = modelsOk,
            ["models_subscription_required"] = configured && methodStatus.TryGetValue("getModels", out var gm) && gm is Dictionary<string, object?> gmMap && !PhpEmpty(gmMap.TryGetValue("subscription_required", out var sr) ? sr : null),
            ["shop_url"] = EpcPartsapiShopUrl("getModels"),
            ["subscription_message"] = EpcPartsapiSubscriptionMessage("models"),
            ["methods"] = methodStatus,
            ["action_methods"] = EpcPartsapiActionShopClientMap()
        };
    }

    public static Dictionary<string, object?> EpcPartsapiStatusPayload()
    {
        var configured = EpcPartsapiCredentialsConfigured();
        var methodTests = new Dictionary<string, object?>(StringComparer.Ordinal);
        var carTypeTests = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (configured)
        {
            foreach (var carType in new[] { "PC", "CV", "Motorcycle" })
            {
                var r = EpcPartsapiCall("getMakes", new Dictionary<string, object?>(StringComparer.Ordinal) { ["carType"] = carType }, 20);
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = r["ok"],
                    ["http_status"] = r["http_status"],
                    ["count"] = IsTrue(r["ok"]) ? EpcPartsapiListRows(r["data"]).Count : 0,
                    ["elapsed_ms"] = r["elapsed_ms"],
                    ["error"] = r["error"]
                };
                foreach (var (k, v) in EpcPartsapiProbeStatusMeta(r, "getMakes"))
                {
                    row[k] = v;
                }

                carTypeTests[carType] = row;
            }

            foreach (var (method, meta) in EpcPartsapiMethodCatalog())
            {
                var probe = AsMap(meta.TryGetValue("probe", out var raw) ? raw : null);
                var r = EpcPartsapiCall(method, probe, 20);
                var key = EpcPartsapiResolveKeyForMethod(method);
                var row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = r["ok"],
                    ["http_status"] = r["http_status"],
                    ["count"] = IsTrue(r["ok"]) ? EpcPartsapiListRows(r["data"]).Count : 0,
                    ["elapsed_ms"] = r["elapsed_ms"],
                    ["error"] = r["error"],
                    ["probe"] = probe,
                    ["key_prefix"] = key != "" ? key[..Math.Min(8, key.Length)] + "…" : "",
                    ["label"] = Convert.ToString(meta.TryGetValue("label", out var label) ? label : method) ?? method,
                    ["shop_url"] = EpcPartsapiShopUrl(method)
                };
                foreach (var (k, v) in EpcPartsapiProbeStatusMeta(r, method))
                {
                    row[k] = v;
                }

                methodTests[method] = row;
            }
        }

        var modelsOk = methodTests.TryGetValue("getModels", out var models) && models is Dictionary<string, object?> modelsMap && !PhpEmpty(modelsMap.TryGetValue("ok", out var ok) ? ok : null);
        var resolve = EpcPartsapiResolveKey();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["configured"] = configured,
            ["key_prefix"] = configured ? resolve[..Math.Min(8, resolve.Length)] + "…" : "",
            ["api_base_url"] = EpcPartsapiApiBaseUrl(),
            ["catalog_ready"] = modelsOk,
            ["getMakes"] = carTypeTests,
            ["methods"] = methodTests,
            ["shop_url"] = EpcPartsapiShopUrl()
        };
    }

    public static Dictionary<string, object?>? EpcPartsapiUmapiFallback(string action, Dictionary<string, object?>? ctx = null)
    {
        if (!EpcPartsapiUmapiFallbackEnabled())
        {
            return null;
        }

        ctx ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var lang = EpcPartsapiDefaultLang();
        if (ctx.TryGetValue("language", out var language) && Regex.IsMatch((Convert.ToString(language) ?? "").ToLowerInvariant(), "^[a-z]{2}$"))
        {
            lang = (Convert.ToString(language) ?? "").ToLowerInvariant();
        }

        var carType = EpcPartsapiCarTypeFromSection(Str(Get(ctx, "section", "passenger")));
        if (ctx.TryGetValue("vehicle_type", out var vtRaw))
        {
            var vt = Convert.ToString(vtRaw) ?? "";
            if (vt is "CV" or "Motorcycle")
            {
                carType = vt;
            }
        }

        switch (action)
        {
            case "manufacturers":
            {
                var r = EpcPartsapiCall("getMakes", new Dictionary<string, object?>(StringComparer.Ordinal) { ["carType"] = carType, ["lang"] = lang });
                return IsTrue(r["ok"]) ? EpcPartsapiOkPayload(EpcPartsapiMapManufacturers(EpcPartsapiListRows(r["data"]), carType), "partsapi_fallback", r, action) : null;
            }
            case "models":
            {
                var makeId = PhpInt(Get(ctx, "MFA_ID"));
                if (makeId <= 0)
                {
                    return null;
                }

                var r = EpcPartsapiCall("getModels", new Dictionary<string, object?>(StringComparer.Ordinal) { ["carType"] = carType, ["makeId"] = makeId, ["lang"] = lang });
                return IsTrue(r["ok"]) ? EpcPartsapiOkPayload(EpcPartsapiMapModels(EpcPartsapiListRows(r["data"]), makeId), "partsapi_fallback", r, action) : null;
            }
            case "modifications":
            {
                var modelId = PhpInt(Get(ctx, "MS_ID"));
                if (modelId <= 0)
                {
                    return null;
                }

                var p = new Dictionary<string, object?>(StringComparer.Ordinal) { ["carType"] = carType, ["modelId"] = modelId, ["lang"] = lang };
                if (PhpInt(Get(ctx, "MFA_ID")) > 0)
                {
                    p["makeId"] = PhpInt(Get(ctx, "MFA_ID"));
                }

                var r = EpcPartsapiCall("getCars", p);
                return IsTrue(r["ok"]) ? EpcPartsapiOkPayload(EpcPartsapiMapCars(EpcPartsapiListRows(r["data"]), carType), "partsapi_fallback", r, action) : null;
            }
            case "vin":
            {
                var vin = Regex.Replace(Str(Get(ctx, "vin")).ToUpperInvariant(), "[^A-Z0-9]", "");
                if (vin == "")
                {
                    return null;
                }

                var r = EpcPartsapiCall("VINdecode", new Dictionary<string, object?>(StringComparer.Ordinal) { ["vin"] = vin, ["lang"] = lang }, 30);
                if (!IsTrue(r["ok"]))
                {
                    return null;
                }

                var mapped = EpcPartsapiMapVin(EpcPartsapiListRows(r["data"]));
                var vehicles = mapped["matchingVehicles"] as List<Dictionary<string, object?>>;
                return vehicles == null || vehicles.Count == 0
                    ? null
                    : new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["ok"] = true,
                        ["data"] = mapped,
                        ["source"] = "partsapi_fallback",
                        ["elapsed_ms"] = r["elapsed_ms"]
                    };
            }
        }

        return null;
    }

    private static Dictionary<string, object?> Meta(string label, string path, Dictionary<string, object?> probe)
        => new(StringComparer.Ordinal) { ["label"] = label, ["shop_path"] = path, ["probe"] = probe };

    private static Dictionary<string, object?> Probe(params (string Key, object? Value)[] pairs)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    private static Dictionary<string, object?> AsMap(object? value)
    {
        if (value is Dictionary<string, object?> map)
        {
            return map;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static object? Get(Dictionary<string, object?> row, string key, object? fallback = null)
        => row.TryGetValue(key, out var value) ? value : fallback;

    private static string Str(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static bool IsTrue(object? value)
        => value is true || value is 1 || value is 1L || value is "1";

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static int PhpInt(object? value)
    {
        if (value is int n)
        {
            return n;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is bool b)
        {
            return b ? 1 : 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        var p = 0;
        var sign = 1;
        if (p < text.Length && text[p] == '-')
        {
            sign = -1;
            p++;
        }

        var n2 = 0;
        var any = false;
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            any = true;
            n2 = (n2 * 10) + (text[p] - '0');
            p++;
        }

        return any ? sign * n2 : 0;
    }

    private static string PhpQuery(Dictionary<string, object?> query)
        => string.Join("&", query.Select(kv => PhpUrlEncode(kv.Key) + "=" + PhpUrlEncode(Str(kv.Value))));

    private static string PhpUrlEncode(string value)
    {
        var chars = new char[value.Length * 3];
        var n = 0;
        foreach (var ch in value)
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.')
            {
                chars[n++] = ch;
            }
            else if (ch == ' ')
            {
                chars[n++] = '+';
            }
            else
            {
                var b = (byte)ch;
                chars[n++] = '%';
                chars[n++] = "0123456789ABCDEF"[b >> 4];
                chars[n++] = "0123456789ABCDEF"[b & 15];
            }
        }

        return new string(chars, 0, n);
    }

    private static object? DecodeJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return FromJson(doc.RootElement);
    }

    private static object? FromJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => FromJson(p.Value), StringComparer.Ordinal),
            JsonValueKind.Array => el.EnumerateArray().Select(FromJson).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number when el.TryGetInt32(out var n) => n,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
}
