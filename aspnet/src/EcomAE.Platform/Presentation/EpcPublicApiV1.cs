using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>epc_api_v1_dispatch</c> for anonymous and key-gated reads.
/// Health, capabilities, and OpenAPI need a live platform database connection, as PHP does.
/// Missing or unknown keys return PHP's JSON errors instead of an empty 404.
/// </summary>
public static class EpcPublicApiV1
{
    public const string MissingKeyMessage = "Send X-API-Key header with a valid tenant API key.";

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] PublicEndpoints =
    [
        "/epc-api/v1/health",
        "/epc-api/v1/capabilities",
        "/epc-api/v1/openapi.json",
        "/epc-api/v1/tenant/info",
        "/epc-api/v1/orders",
        "/epc-api/v1/products/search",
        "/epc-api/v1/erp/dashboard-summary",
        "/epc-api/v1/powerbi/catalog",
        "/epc-api/v1/powerbi/kpis",
        "/epc-api/v1/powerbi/orders",
        "/epc-api/v1/powerbi/sales",
        "/epc-api/v1/powerbi/stock",
        "/epc-api/v1/powerbi/gl",
        "/epc-api/v1/powerbi/metrics",
    ];

    private static readonly HashSet<string> KeyedRoutes = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenant/info",
        "orders",
        "products/search",
        "erp/dashboard-summary",
        "powerbi/catalog",
        "powerbi/kpis",
        "powerbi/orders",
        "powerbi/sales",
        "powerbi/stock",
        "powerbi/gl",
        "powerbi/metrics",
    };

    private static readonly Dictionary<string, string> RouteScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tenant/info"] = "read:tenant",
        ["orders"] = "read:orders",
        ["products/search"] = "read:products",
        ["erp/dashboard-summary"] = "read:erp",
    };

    public static string RouteOf(string? path)
    {
        var value = PublicAnonymousPages.NormalizePath(path);
        const string prefix = "/epc-api/v1";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var rest = value[prefix.Length..].Trim('/');
        return rest;
    }

    public static bool IsApiPath(string? path)
    {
        var value = PublicAnonymousPages.NormalizePath(path);
        return value.Equals("/epc-api/v1", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/epc-api/v1/", StringComparison.OrdinalIgnoreCase);
    }

    public static string HealthJson(DateTimeOffset utcNow)
    {
        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["service"] = "epc-api",
            ["version"] = "v1",
            ["phase"] = 1,
            ["mode"] = "read-only",
            ["platform"] = "ECOM AE",
            ["time"] = utcNow.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+00:00",
            ["endpoints"] = PublicEndpoints,
        };
        return JsonSerializer.Serialize(payload, Pretty);
    }

    public static string CapabilitiesJson()
    {
        var areas = CapabilityAreas();
        var payload = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["api_phase"] = 1,
            ["capability_areas"] = areas.Select(area => new Dictionary<string, object?>
            {
                ["area"] = area.Area,
                ["capability_count"] = area.Count,
            }).ToArray(),
            ["total_capabilities"] = areas.Sum(area => area.Count),
            ["integrations"] = new Dictionary<string, object?>
            {
                ["public_rest"] = "Phase 1 read-only JSON at /epc-api/v1/",
                ["power_bi"] = "Web connector datasets at /epc-api/v1/powerbi/* (JSON/CSV, X-API-Key)",
                ["erp_ajax"] = "Internal CP session — not public",
                ["future"] = new[] { "webhooks", "e-invoice submit", "d365 sync", "marketplace write APIs v2", "power_bi_azure_embed" },
            },
            ["docs"] = "https://www.ecomae.com/platform/api-documentation",
        };
        return JsonSerializer.Serialize(payload, Pretty);
    }

    public static string ErrorJson(string code, string message)
        => JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = false,
            ["error"] = new Dictionary<string, string>
            {
                ["code"] = code,
                ["message"] = message,
            },
        }, Pretty);

    public static string? RequiredScope(string route)
        => RouteScopes.TryGetValue(route, out var scope) ? scope : null;

    public static bool IsPowerBiRoute(string route)
        => route.StartsWith("powerbi/", StringComparison.OrdinalIgnoreCase);

    public static bool ScopeAllowed(IEnumerable<string> scopes, string need)
    {
        var list = scopes as IReadOnlyCollection<string> ?? scopes.ToArray();
        return list.Contains("*", StringComparer.Ordinal) || list.Contains("read:*", StringComparer.Ordinal) || list.Contains(need, StringComparer.Ordinal);
    }

    public static bool PowerBiScopeAllowed(IEnumerable<string> scopes)
        => ScopeAllowed(scopes, "read:bi") || ScopeAllowed(scopes, "read:erp");

    public static string ExtractKey(HttpRequest request)
    {
        var header = request.Headers["X-API-Key"].ToString().Trim();
        if (header.Length > 0)
        {
            return header;
        }

        var authorization = request.Headers.Authorization.ToString();
        var match = Regex.Match(authorization, @"^Bearer\s+(\S+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public static string Sha256Hex(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

    public static IReadOnlyList<string> ParseScopes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json);
            return values is null ? [] : values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsKeyedRoute(string route) => KeyedRoutes.Contains(route);

    public static string? OpenApiPath()
    {
        var starts = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var path = Path.Combine(dir.FullName, "docs", "epc-api-v1-openapi.json");
                if (File.Exists(path))
                {
                    return path;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    public static IReadOnlyList<(string Area, int Count)> CapabilityAreas()
    {
        var path = CapabilitiesCatalogPath();
        if (path is null)
        {
            return [];
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), @"'category'\s*=>\s*'([^']+)'"))
        {
            var area = match.Groups[1].Value;
            counts[area] = counts.TryGetValue(area, out var count) ? count + 1 : 1;
        }

        return counts.Select(pair => (pair.Key, pair.Value)).ToArray();
    }

    public const string EnsureKeysTableSql = """
        CREATE TABLE IF NOT EXISTS `epc_api_keys` (
            `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
            `tenant_site_key` VARCHAR(64) NOT NULL,
            `key_hash` CHAR(64) NOT NULL,
            `key_prefix` VARCHAR(16) NOT NULL DEFAULT '',
            `label` VARCHAR(120) NOT NULL DEFAULT '',
            `scopes_json` TEXT NOT NULL,
            `active` TINYINT(1) NOT NULL DEFAULT 1,
            `created_at` INT NOT NULL DEFAULT 0,
            `last_used_at` INT NOT NULL DEFAULT 0,
            UNIQUE KEY `key_hash` (`key_hash`),
            KEY `tenant_site_key` (`tenant_site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    private static string? CapabilitiesCatalogPath()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var path = Path.Combine(dir.FullName, "content", "general_pages", "epc_ecomae_platform_capabilities_catalog.php");
                if (File.Exists(path))
                {
                    return path;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }
}
