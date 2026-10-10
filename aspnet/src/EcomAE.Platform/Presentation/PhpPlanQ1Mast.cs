using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-mast REST API v2. PHP identifiers kept for the inventory:
/// <c>epc_api_ensure_schema</c>, <c>epc_api_key_generate</c>,
/// <c>epc_api_key_validate</c>, <c>epc_api_key_revoke</c>,
/// <c>epc_api_keys_list</c>, <c>epc_api_rate_check</c>, <c>epc_api_log</c>,
/// <c>epc_api_v2_endpoints</c>, <c>epc_api_has_scope</c>,
/// <c>epc_api_v2_handle</c>, <c>epc_api_v2_error</c>,
/// <c>epc_api_v2_openapi_spec</c>, <c>epc_api_fleet_stats</c>,
/// <c>epc_api_usage_by_endpoint</c>, <c>EPC_REST_API_V2_VERSION</c>.
/// GET never mints a session cookie. Keys stay per <c>site_key</c>.
/// </summary>
public static class PhpPlanQ1Mast
{
    public const string RestApiPath = "content/general_pages/epc_rest_api_v2.php";
    public const string Version = "2.0.0";

    public sealed class ApiKeyRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string KeyHash { get; set; } = "";
        public string KeyPrefix { get; set; } = "";
        public string Label { get; set; } = "";
        public string ScopesJson { get; set; } = "[]";
        public int RateLimit { get; set; } = 1000;
        public int Active { get; set; } = 1;
        public string? LastUsed { get; set; }
        public string? ExpiresAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class RateRow
    {
        public int KeyId { get; set; }
        public string WindowStart { get; set; } = "";
        public int RequestCount { get; set; }
    }

    public sealed class LogRow
    {
        public int KeyId { get; set; }
        public string SiteKey { get; set; } = "";
        public string Method { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public int StatusCode { get; set; } = 200;
        public int ResponseMs { get; set; }
        public string IpAddress { get; set; } = "";
        public string UserAgent { get; set; } = "";
        public string RequestBody { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public sealed class ApiStore
    {
        public bool SchemaReady { get; set; }
        public bool ThrowOnLog { get; set; }
        public int NextKeyId { get; set; } = 1;
        public List<ApiKeyRow> Keys { get; } = [];
        public List<RateRow> Rates { get; } = [];
        public List<LogRow> Logs { get; } = [];
    }

    public static ApiStore Store { get; set; } = new();
    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> Clock { get; set; } = () => 1_760_083_200;
    public static int Seq { get; set; }
    public static Func<byte[]> RandomBytes { get; set; } = NextBytes;
    public static double Micro { get; set; } = 1000.0;

    public static void Reset()
    {
        Store = new();
        Server = new(StringComparer.Ordinal);
        Clock = () => 1_760_083_200;
        Seq = 0;
        RandomBytes = NextBytes;
        Micro = 1000.0;
    }

    private static byte[] NextBytes()
    {
        var bytes = Enumerable.Repeat((byte)0x0A, 24).ToArray();
        bytes[23] = (byte)(Seq++ & 0xFF);
        return bytes;
    }

    /// <summary>PHP <c>epc_api_ensure_schema</c>.</summary>
    public static void EpcApiEnsureSchema() => Store.SchemaReady = true;

    /// <summary>PHP <c>epc_api_key_generate</c>.</summary>
    public static Dictionary<string, object?> EpcApiKeyGenerate(string siteKey, Dictionary<string, object?>? opts = null)
    {
        EpcApiEnsureSchema();
        opts ??= new(StringComparer.Ordinal);
        var rawKey = "epc_" + Convert.ToHexString(RandomBytes()).ToLowerInvariant();
        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var hash = Sha256Hex(rawKey);
        var scopes = opts.TryGetValue("scopes", out var sc) && sc != null ? sc : new[] { "read" };
        var rateLimit = PhpInt(opts.TryGetValue("rate_limit", out var rl) ? rl : 1000);
        var label = Convert.ToString(opts.TryGetValue("label", out var lb) ? lb : "API Key") ?? "API Key";
        var expiresAt = !PhpEmpty(opts.TryGetValue("expires_at", out var ex) ? ex : null)
            ? Convert.ToString(ex)
            : null;
        var createdBy = PhpInt(opts.TryGetValue("created_by", out var cb) ? cb : 0);
        var id = Store.NextKeyId++;
        Store.Keys.Add(new ApiKeyRow
        {
            Id = id,
            SiteKey = siteKey,
            KeyHash = hash,
            KeyPrefix = prefix,
            Label = label,
            ScopesJson = PhpJsonEncode(scopes),
            RateLimit = rateLimit,
            Active = 1,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            CreatedAt = FormatDt(Clock())
        });
        return new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["key_id"] = id,
            ["api_key"] = rawKey,
            ["prefix"] = prefix,
            ["scopes"] = scopes,
            ["warning"] = "Store this key securely — it cannot be retrieved again."
        };
    }

    /// <summary>PHP <c>epc_api_key_validate</c>.</summary>
    public static Dictionary<string, object?> EpcApiKeyValidate(string rawKey)
    {
        EpcApiEnsureSchema();
        var hash = Sha256Hex(rawKey);
        var key = Store.Keys.FirstOrDefault(k => k.KeyHash == hash && k.Active == 1);
        if (key == null)
        {
            return new(StringComparer.Ordinal) { ["valid"] = false, ["error"] = "Invalid API key" };
        }

        if (!string.IsNullOrEmpty(key.ExpiresAt) && PhpStrtotime(key.ExpiresAt) < Clock())
        {
            return new(StringComparer.Ordinal) { ["valid"] = false, ["error"] = "API key expired" };
        }

        var snapshot = KeyAssoc(key, decodeScopes: true);
        key.LastUsed = FormatDt(Clock());
        return new(StringComparer.Ordinal) { ["valid"] = true, ["key"] = snapshot };
    }

    /// <summary>PHP <c>epc_api_key_revoke</c>.</summary>
    public static bool EpcApiKeyRevoke(int keyId)
    {
        var key = Store.Keys.FirstOrDefault(k => k.Id == keyId);
        if (key != null)
        {
            key.Active = 0;
        }

        return true;
    }

    /// <summary>PHP <c>epc_api_keys_list</c>.</summary>
    public static List<Dictionary<string, object?>> EpcApiKeysList(string siteKey)
    {
        EpcApiEnsureSchema();
        return Store.Keys
            .Where(k => k.SiteKey == siteKey)
            .OrderByDescending(k => k.Id)
            .Select(k => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = k.Id,
                ["key_prefix"] = k.KeyPrefix,
                ["label"] = k.Label,
                ["scopes"] = DecodeScopes(k.ScopesJson),
                ["rate_limit"] = k.RateLimit,
                ["active"] = k.Active,
                ["last_used"] = k.LastUsed,
                ["expires_at"] = k.ExpiresAt,
                ["created_at"] = k.CreatedAt
            })
            .ToList();
    }

    /// <summary>PHP <c>epc_api_rate_check</c>.</summary>
    public static Dictionary<string, object?> EpcApiRateCheck(int keyId, int limit = 1000)
    {
        var windowStart = FormatWindow(Clock());
        var row = Store.Rates.FirstOrDefault(r => r.KeyId == keyId && r.WindowStart == windowStart);
        if (row == null)
        {
            row = new RateRow { KeyId = keyId, WindowStart = windowStart, RequestCount = 1 };
            Store.Rates.Add(row);
        }
        else
        {
            row.RequestCount++;
        }

        var count = row.RequestCount;
        return new(StringComparer.Ordinal)
        {
            ["allowed"] = count <= limit,
            ["remaining"] = Math.Max(0, limit - count),
            ["limit"] = limit,
            ["reset"] = PhpStrtotime(windowStart + " +1 hour")
        };
    }

    /// <summary>PHP <c>epc_api_log</c>.</summary>
    public static void EpcApiLog(Dictionary<string, object?> data)
    {
        try
        {
            if (Store.ThrowOnLog)
            {
                throw new InvalidOperationException("log");
            }

            Store.Logs.Add(new LogRow
            {
                KeyId = PhpInt(data.TryGetValue("key_id", out var kid) ? kid : 0),
                SiteKey = Convert.ToString(data.TryGetValue("site_key", out var sk) ? sk : "") ?? "",
                Method = Convert.ToString(data.TryGetValue("method", out var m) ? m : "GET") ?? "GET",
                Endpoint = Convert.ToString(data.TryGetValue("endpoint", out var ep) ? ep : "") ?? "",
                StatusCode = PhpInt(data.TryGetValue("status_code", out var sc) ? sc : 200),
                ResponseMs = PhpInt(data.TryGetValue("response_ms", out var ms) ? ms : 0),
                IpAddress = Convert.ToString(data.TryGetValue("ip_address", out var ip) ? ip : "") ?? "",
                UserAgent = PhpSubstr(Convert.ToString(data.TryGetValue("user_agent", out var ua) ? ua : "") ?? "", 255),
                RequestBody = Convert.ToString(data.TryGetValue("request_body", out var rb) ? rb : "") ?? "",
                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(Clock()).UtcDateTime
            });
        }
        catch (Exception)
        {
            // logging should not break API calls
        }
    }

    /// <summary>PHP <c>epc_api_v2_endpoints</c>.</summary>
    public static List<Dictionary<string, object?>> EpcApiV2Endpoints()
        =>
        [
            Ep("GET", "/api/v2/products", "read", "List products with pagination"),
            Ep("GET", "/api/v2/products/{id}", "read", "Get product by ID"),
            Ep("POST", "/api/v2/products", "write", "Create a new product"),
            Ep("PUT", "/api/v2/products/{id}", "write", "Update a product"),
            Ep("DELETE", "/api/v2/products/{id}", "write", "Delete a product"),
            Ep("GET", "/api/v2/orders", "read", "List orders with filters"),
            Ep("GET", "/api/v2/orders/{id}", "read", "Get order by ID"),
            Ep("POST", "/api/v2/orders", "write", "Create a new order"),
            Ep("PUT", "/api/v2/orders/{id}/status", "write", "Update order status"),
            Ep("GET", "/api/v2/customers", "read", "List customers"),
            Ep("GET", "/api/v2/customers/{id}", "read", "Get customer by ID"),
            Ep("POST", "/api/v2/customers", "write", "Create a new customer"),
            Ep("GET", "/api/v2/inventory", "read", "Get inventory levels"),
            Ep("PUT", "/api/v2/inventory/{sku}", "write", "Update stock for SKU"),
            Ep("GET", "/api/v2/invoices", "finance", "List invoices"),
            Ep("POST", "/api/v2/invoices", "finance", "Create invoice"),
            Ep("GET", "/api/v2/reports/sales", "reports", "Sales summary report"),
            Ep("GET", "/api/v2/reports/inventory", "reports", "Inventory report"),
            Ep("GET", "/api/v2/webhooks", "admin", "List webhook endpoints"),
            Ep("POST", "/api/v2/webhooks", "admin", "Register webhook endpoint")
        ];

    /// <summary>PHP <c>epc_api_has_scope</c>.</summary>
    public static bool EpcApiHasScope(IEnumerable<object?> keyScopes, string requiredScope)
    {
        var list = keyScopes.Select(s => Convert.ToString(s) ?? "").ToList();
        return list.Contains("admin") || list.Contains("*") || list.Contains(requiredScope);
    }

    /// <summary>PHP <c>epc_api_v2_handle</c>.</summary>
    public static Dictionary<string, object?> EpcApiV2Handle(string method, string path, Dictionary<string, object?>? paramsIn = null)
    {
        paramsIn ??= new(StringComparer.Ordinal);
        var start = Micro;
        var authHeader = Server.TryGetValue("HTTP_AUTHORIZATION", out var ah) ? ah : "";
        var apiKey = "";
        if (authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            apiKey = authHeader[7..];
        }
        else if (!PhpEmpty(paramsIn.TryGetValue("api_key", out var pk) ? pk : null))
        {
            apiKey = Convert.ToString(pk) ?? "";
        }

        if (apiKey == "")
        {
            return EpcApiV2Error(401, "Missing API key. Use Authorization: Bearer <key>");
        }

        var auth = EpcApiKeyValidate(apiKey);
        if (!(auth.TryGetValue("valid", out var valid) && valid is true))
        {
            return EpcApiV2Error(401, Convert.ToString(auth.TryGetValue("error", out var err) ? err : "") ?? "");
        }

        var key = (Dictionary<string, object?>)auth["key"]!;
        var rateCheck = EpcApiRateCheck(PhpInt(key["id"]), PhpInt(key["rate_limit"]));
        if (rateCheck["allowed"] is not true)
        {
            return EpcApiV2Error(429, "Rate limit exceeded", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["X-RateLimit-Limit"] = rateCheck["limit"],
                ["X-RateLimit-Remaining"] = 0,
                ["X-RateLimit-Reset"] = rateCheck["reset"]
            });
        }

        Dictionary<string, object?>? matched = null;
        var pathParams = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var ep in EpcApiV2Endpoints())
        {
            if (Convert.ToString(ep["method"]) != method.ToUpperInvariant())
            {
                continue;
            }

            var pattern = Regex.Replace(Convert.ToString(ep["path"]) ?? "", @"\{([^}]+)\}", "(?<$1>[^/]+)");
            var hit = Regex.Match(path, "^" + pattern + "$");
            if (!hit.Success)
            {
                continue;
            }

            matched = ep;
            foreach (Group g in hit.Groups)
            {
                if (g.Name.Length > 0 && !char.IsDigit(g.Name[0]) && g.Name != "0")
                {
                    pathParams[g.Name] = g.Value;
                }
            }

            break;
        }

        if (matched == null)
        {
            var missMs = (int)((Micro - start) * 1000);
            EpcApiLog(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key_id"] = key["id"],
                ["site_key"] = key["site_key"],
                ["method"] = method,
                ["endpoint"] = path,
                ["status_code"] = 404,
                ["response_ms"] = missMs
            });
            return EpcApiV2Error(404, "Endpoint not found");
        }

        var scopes = key["scopes"] as IEnumerable<object?> ?? [];
        if (!EpcApiHasScope(scopes, Convert.ToString(matched["scope"]) ?? ""))
        {
            return EpcApiV2Error(403, "Insufficient scope. Required: " + matched["scope"]);
        }

        var ms = (int)((Micro - start) * 1000);
        var response = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["api"] = "v2",
            ["endpoint"] = matched["path"],
            ["method"] = matched["method"],
            ["site_key"] = key["site_key"],
            ["params"] = pathParams.Count == 0 ? Array.Empty<object>() : pathParams,
            ["data"] = Array.Empty<object>(),
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["page"] = PhpInt(paramsIn.TryGetValue("page", out var pg) ? pg : 1),
                ["per_page"] = PhpInt(paramsIn.TryGetValue("per_page", out var pp) ? pp : 25),
                ["response_ms"] = ms,
                ["rate_limit"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["remaining"] = rateCheck["remaining"],
                    ["limit"] = rateCheck["limit"],
                    ["reset"] = rateCheck["reset"]
                }
            }
        };
        EpcApiLog(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key_id"] = key["id"],
            ["site_key"] = key["site_key"],
            ["method"] = method,
            ["endpoint"] = path,
            ["status_code"] = 200,
            ["response_ms"] = ms
        });
        return response;
    }

    /// <summary>PHP <c>epc_api_v2_error</c>.</summary>
    public static Dictionary<string, object?> EpcApiV2Error(int code, string message, Dictionary<string, object?>? headers = null)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code, ["message"] = message },
            ["headers"] = headers == null || headers.Count == 0 ? Array.Empty<object>() : headers
        };

    /// <summary>PHP <c>epc_api_v2_openapi_spec</c>.</summary>
    public static Dictionary<string, object?> EpcApiV2OpenapiSpec()
    {
        var paths = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var ep in EpcApiV2Endpoints())
        {
            var pathKey = Convert.ToString(ep["path"]) ?? "";
            if (!paths.TryGetValue(pathKey, out var existing) || existing is not Dictionary<string, object?> bucket)
            {
                bucket = new(StringComparer.Ordinal);
                paths[pathKey] = bucket;
            }

            var parts = pathKey.Split('/');
            var tag = parts.Length > 3 ? parts[3] : "general";
            bucket[(Convert.ToString(ep["method"]) ?? "").ToLowerInvariant()] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["summary"] = ep["description"],
                ["tags"] = new[] { tag },
                ["security"] = new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["BearerAuth"] = Array.Empty<object>() } },
                ["parameters"] = Array.Empty<object>(),
                ["responses"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["200"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["description"] = "Success" },
                    ["401"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["description"] = "Unauthorized" },
                    ["403"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["description"] = "Forbidden — insufficient scope" },
                    ["429"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["description"] = "Rate limit exceeded" }
                }
            };
        }

        return new(StringComparer.Ordinal)
        {
            ["openapi"] = "3.0.3",
            ["info"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "ecomae Platform API",
                ["version"] = Version,
                ["description"] = "Multi-tenant ERP/Commerce API with per-tenant scoping, API key auth, and rate limiting."
            },
            ["servers"] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["url"] = "https://www.ecomae.com",
                    ["description"] = "Production"
                }
            },
            ["components"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["securitySchemes"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["BearerAuth"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer"
                    }
                }
            },
            ["paths"] = paths
        };
    }

    /// <summary>PHP <c>epc_api_fleet_stats</c>.</summary>
    public static List<Dictionary<string, object?>> EpcApiFleetStats()
    {
        EpcApiEnsureSchema();
        var since = DateTimeOffset.FromUnixTimeSeconds(Clock()).UtcDateTime.AddHours(-24);
        return Store.Logs
            .Where(l => l.CreatedAt >= since)
            .GroupBy(l => l.SiteKey)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = g.Key,
                ["total_requests"] = g.Count(),
                ["errors"] = g.Count(l => l.StatusCode >= 400).ToString(CultureInfo.InvariantCulture),
                ["avg_response_ms"] = FormatAvg(g.Average(l => (double)l.ResponseMs)),
                ["active_keys"] = g.Select(l => l.KeyId).Distinct().Count(),
                ["last_request"] = g.Max(l => l.CreatedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            })
            .OrderByDescending(r => Convert.ToInt32(r["total_requests"], CultureInfo.InvariantCulture))
            .ThenBy(r => (string)r["site_key"]!, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>PHP <c>epc_api_usage_by_endpoint</c>.</summary>
    public static List<Dictionary<string, object?>> EpcApiUsageByEndpoint(string siteKey, int hours = 24)
    {
        var since = DateTimeOffset.FromUnixTimeSeconds(Clock()).UtcDateTime.AddHours(-hours);
        return Store.Logs
            .Where(l => l.SiteKey == siteKey && l.CreatedAt >= since)
            .GroupBy(l => (l.Endpoint, l.Method))
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["endpoint"] = g.Key.Endpoint,
                ["method"] = g.Key.Method,
                ["count"] = g.Count(),
                ["avg_ms"] = FormatAvg(g.Average(l => (double)l.ResponseMs)),
                ["errors"] = g.Count(l => l.StatusCode >= 400).ToString(CultureInfo.InvariantCulture)
            })
            .OrderByDescending(r => Convert.ToInt32(r["count"], CultureInfo.InvariantCulture))
            .ThenBy(r => (string)r["endpoint"]!, StringComparer.Ordinal)
            .ToList();
    }

    private static Dictionary<string, object?> Ep(string method, string path, string scope, string description)
        => new(StringComparer.Ordinal)
        {
            ["method"] = method,
            ["path"] = path,
            ["scope"] = scope,
            ["description"] = description
        };

    private static Dictionary<string, object?> KeyAssoc(ApiKeyRow key, bool decodeScopes)
        => new(StringComparer.Ordinal)
        {
            ["id"] = key.Id,
            ["site_key"] = key.SiteKey,
            ["key_hash"] = key.KeyHash,
            ["key_prefix"] = key.KeyPrefix,
            ["label"] = key.Label,
            ["scopes"] = decodeScopes ? DecodeScopes(key.ScopesJson) : key.ScopesJson,
            ["rate_limit"] = key.RateLimit,
            ["active"] = key.Active,
            ["last_used"] = key.LastUsed,
            ["expires_at"] = key.ExpiresAt,
            ["created_by"] = key.CreatedBy,
            ["created_at"] = key.CreatedAt
        };

    private static object? DecodeScopes(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<object?>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return doc.RootElement.EnumerateArray().Select(e => (object?)e.GetString()).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string PhpJsonEncode(object? value)
    {
        if (value is IEnumerable<object?> list && value is not string)
        {
            value = list.Select(v => v).ToArray();
        }

        if (value is string[] sa)
        {
            value = sa;
        }

        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("/", "\\/", StringComparison.Ordinal);
    }

    private static string Sha256Hex(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    private static string FormatDt(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string FormatWindow(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd HH:00:00", CultureInfo.InvariantCulture);

    private static string FormatAvg(double value)
        => value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static long PhpStrtotime(string value)
    {
        if (value.EndsWith(" +1 hour", StringComparison.Ordinal))
        {
            var baseDt = DateTime.ParseExact(value[..^8], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
            return new DateTimeOffset(baseDt).ToUnixTimeSeconds() + 3600;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        return 0;
    }

    private static string PhpSubstr(string value, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return bytes.Length <= maxBytes ? value : Encoding.UTF8.GetString(bytes, 0, maxBytes);
    }

    private static int PhpInt(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (text == "")
        {
            return 0;
        }

        var n = 0;
        var sign = 1;
        var p = 0;
        while (p < text.Length && char.IsWhiteSpace(text[p]))
        {
            p++;
        }

        if (p < text.Length && (text[p] == '+' || text[p] == '-'))
        {
            sign = text[p] == '-' ? -1 : 1;
            p++;
        }

        var any = false;
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            any = true;
            n = (n * 10) + (text[p] - '0');
            p++;
        }

        return any ? sign * n : 0;
    }

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
}
