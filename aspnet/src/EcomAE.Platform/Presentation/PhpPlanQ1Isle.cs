using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-isle public REST API. PHP identifiers kept for the inventory:
/// <c>epc_api_v1_json</c>, <c>epc_api_v1_error</c>, <c>epc_api_v1_ok</c>,
/// <c>epc_api_v1_route_path</c>, <c>epc_api_v1_ensure_keys_table</c>,
/// <c>epc_api_v1_platform_pdo</c>, <c>epc_api_v1_extract_key</c>,
/// <c>epc_api_v1_parse_scopes</c>, <c>epc_api_v1_scope_allowed</c>,
/// <c>epc_api_v1_auth</c>, <c>epc_api_v1_tenant_pdo</c>,
/// <c>epc_api_v1_tenant_access_mode</c>, <c>epc_api_v1_handle_health</c>,
/// <c>epc_api_v1_handle_capabilities</c>, <c>epc_api_v1_handle_openapi</c>,
/// <c>epc_api_v1_handle_tenant_info</c>, <c>epc_api_v1_handle_orders</c>,
/// <c>epc_api_v1_handle_products_search</c>, <c>epc_api_v1_handle_erp_dashboard</c>,
/// <c>epc_api_v1_auth_powerbi</c>, <c>epc_api_v1_powerbi_respond</c>,
/// <c>epc_api_v1_handle_powerbi_catalog</c>, <c>epc_api_v1_handle_powerbi_kpis</c>,
/// <c>epc_api_v1_handle_powerbi_orders</c>, <c>epc_api_v1_handle_powerbi_report</c>,
/// <c>epc_api_v1_handle_powerbi_metrics</c>, <c>epc_api_v1_dispatch</c>.
/// Path: <c>content/general_pages/epc_api_v1.php</c>.
/// GET never mints a session cookie. Leftover portal, shared-ERP, and
/// platform-data stay injected and are not written as unique leftover basenames.
/// </summary>
public static class PhpPlanQ1Isle
{
    public const string ApiV1Path = "content/general_pages/epc_api_v1.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    public static Action<MySqlConnection>? DbEnsure { get; set; }
    public static Func<MySqlConnection?>? PlatformPdo { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>?>? GetTenant { get; set; }
    public static Func<Dictionary<string, object?>, MySqlConnection?>? TenantPdo { get; set; }
    public static Func<MySqlConnection, string>? ErpOrderStatusNameSql { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>>? ErpDashboard { get; set; }
    public static Func<Dictionary<string, object?>>? CapabilityCategories { get; set; }
    public static Func<int>? CapabilityCount { get; set; }
    public static Func<bool>? PowerBiWantsCsv { get; set; }
    public static Action<List<string>, List<object?[]>, string>? PowerBiEmitCsv { get; set; }
    public static Func<object>? PowerBiCapabilities { get; set; }
    public static Func<string, object>? PowerBiDatasetCatalog { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? PowerBiDatasetKpis { get; set; }
    public static Func<MySqlConnection, string, int, Dictionary<string, object?>>? PowerBiDatasetOrders { get; set; }
    public static Func<string, long, long>? PowerBiParseDate { get; set; }
    public static Func<MySqlConnection, string, long, long, Dictionary<string, object?>>? PowerBiDatasetReport { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? PowerBiDatasetMetrics { get; set; }
    public static Func<bool>? OpenApiPresent { get; set; }
    public static Func<string>? OpenApiBody { get; set; }
    public static Func<long>? Clock { get; set; }
    public static string RequestUri { get; set; } = "/";
    public static string HeaderXApiKey { get; set; } = "";
    public static string HeaderAuthorization { get; set; } = "";
    public static Dictionary<string, string> Query { get; } = new(StringComparer.Ordinal);
    public static string LastOutput { get; private set; } = "";
    public static int LastStatus { get; private set; }

    public static void Reset()
    {
        DbEnsure = null;
        PlatformPdo = null;
        GetTenant = null;
        TenantPdo = null;
        ErpOrderStatusNameSql = null;
        ErpDashboard = null;
        CapabilityCategories = null;
        CapabilityCount = null;
        PowerBiWantsCsv = null;
        PowerBiEmitCsv = null;
        PowerBiCapabilities = null;
        PowerBiDatasetCatalog = null;
        PowerBiDatasetKpis = null;
        PowerBiDatasetOrders = null;
        PowerBiParseDate = null;
        PowerBiDatasetReport = null;
        PowerBiDatasetMetrics = null;
        OpenApiPresent = null;
        OpenApiBody = null;
        Clock = null;
        RequestUri = "/";
        HeaderXApiKey = "";
        HeaderAuthorization = "";
        Query.Clear();
        LastOutput = "";
        LastStatus = 0;
    }

    public static void EpcApiV1Json(int status, Dictionary<string, object?> payload)
    {
        LastStatus = status;
        LastOutput = JsonSerializer.Serialize(payload, JsonOpts);
    }

    public static void EpcApiV1Error(int status, string code, string message)
        => EpcApiV1Json(status, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["error"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code, ["message"] = message }
        });

    public static void EpcApiV1Ok(Dictionary<string, object?> data, int status = 200)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
        foreach (var pair in data)
        {
            payload[pair.Key] = pair.Value;
        }

        EpcApiV1Json(status, payload);
    }

    public static string EpcApiV1RoutePath()
    {
        var uri = RequestUri;
        var q = uri.IndexOf('?', StringComparison.Ordinal);
        var path = q >= 0 ? uri[..q] : uri;
        if (path == "")
        {
            path = "/";
        }

        path = "/" + path.Replace('\\', '/').Trim('/');
        var m = Regex.Match(path, @"^/epc-api/v1(?:/(.*))?$");
        return m.Success ? (m.Groups[1].Value ?? "").Trim('/') : "";
    }

    public static void EpcApiV1EnsureKeysTable(MySqlConnection pdo)
    {
        DbEnsure?.Invoke(pdo);
        Exec(pdo,
            "CREATE TABLE IF NOT EXISTS `epc_api_keys` (" +
            "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "`tenant_site_key` VARCHAR(64) NOT NULL," +
            "`key_hash` CHAR(64) NOT NULL," +
            "`key_prefix` VARCHAR(16) NOT NULL DEFAULT ''," +
            "`label` VARCHAR(120) NOT NULL DEFAULT ''," +
            "`scopes_json` TEXT NOT NULL," +
            "`active` TINYINT(1) NOT NULL DEFAULT 1," +
            "`created_at` INT NOT NULL DEFAULT 0," +
            "`last_used_at` INT NOT NULL DEFAULT 0," +
            "UNIQUE KEY `key_hash` (`key_hash`)," +
            "KEY `tenant_site_key` (`tenant_site_key`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
    }

    public static MySqlConnection? EpcApiV1PlatformPdo()
        => PlatformPdo?.Invoke();

    public static string EpcApiV1ExtractKey()
    {
        if (!PhpEmpty(HeaderXApiKey))
        {
            return HeaderXApiKey.Trim();
        }

        var m = Regex.Match(HeaderAuthorization, @"^Bearer\s+(\S+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    public static List<string> EpcApiV1ParseScopes(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return doc.RootElement.EnumerateArray()
                .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : e.ToString())
                .Where(s => !PhpEmpty(s))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static bool EpcApiV1ScopeAllowed(IEnumerable<string> scopes, string need)
    {
        var list = scopes.ToList();
        return list.Contains("*") || list.Contains("read:*") || list.Contains(need);
    }

    public static Dictionary<string, object?>? EpcApiV1Auth(MySqlConnection platformPdo, string? requiredScope = null)
    {
        var raw = EpcApiV1ExtractKey();
        if (raw == "")
        {
            EpcApiV1Error(401, "missing_api_key", "Send X-API-Key header with a valid tenant API key.");
            return null;
        }

        EpcApiV1EnsureKeysTable(platformPdo);
        var hash = Sha256(raw);
        var row = QueryOne(platformPdo, "SELECT * FROM `epc_api_keys` WHERE `key_hash` = @h AND `active` = 1 LIMIT 1", ("@h", hash));
        if (row is null)
        {
            EpcApiV1Error(401, "invalid_api_key", "API key not recognized or revoked.");
            return null;
        }

        var scopes = EpcApiV1ParseScopes(Str(row.GetValueOrDefault("scopes_json"), "[]"));
        if (requiredScope is not null && !EpcApiV1ScopeAllowed(scopes, requiredScope))
        {
            EpcApiV1Error(403, "insufficient_scope", "This key lacks scope: " + requiredScope);
            return null;
        }

        Exec(platformPdo, "UPDATE `epc_api_keys` SET `last_used_at` = @t WHERE `id` = @id", ("@t", Now()), ("@id", ToInt(row.GetValueOrDefault("id"))));
        var tenant = GetTenant?.Invoke(platformPdo, Str(row.GetValueOrDefault("tenant_site_key")));
        if (tenant is null)
        {
            EpcApiV1Error(403, "tenant_not_found", "Tenant linked to this API key is not registered.");
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = row,
            ["scopes"] = scopes,
            ["tenant"] = tenant
        };
    }

    public static MySqlConnection? EpcApiV1TenantPdo(Dictionary<string, object?> tenantRow)
    {
        var injected = TenantPdo?.Invoke(tenantRow);
        if (injected is not null)
        {
            return injected;
        }

        return Str(tenantRow.GetValueOrDefault("db_name")).Trim() == "" ? null : null;
    }

    public static string EpcApiV1TenantAccessMode(MySqlConnection platformPdo, Dictionary<string, object?> tenantRow)
    {
        var host = Str(tenantRow.GetValueOrDefault("hostname")).Trim();
        if (host == "")
        {
            return "full";
        }

        var bare = Regex.Replace(host, @"^www\.", "");
        var mode = Str(Scalar(platformPdo, "SELECT `access_mode` FROM `epc_portal_site_settings` WHERE `host` = @h OR `host` = @b LIMIT 1", ("@h", host), ("@b", bare)));
        return mode != "" ? mode : "full";
    }

    public static void EpcApiV1HandleHealth()
        => EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["service"] = "epc-api",
            ["version"] = "v1",
            ["phase"] = 1,
            ["mode"] = "read-only",
            ["platform"] = "ECOM AE",
            ["time"] = Gmt(Now()),
            ["endpoints"] = new[]
            {
                "/epc-api/v1/health", "/epc-api/v1/capabilities", "/epc-api/v1/openapi.json",
                "/epc-api/v1/tenant/info", "/epc-api/v1/orders", "/epc-api/v1/products/search",
                "/epc-api/v1/erp/dashboard-summary", "/epc-api/v1/powerbi/catalog",
                "/epc-api/v1/powerbi/kpis", "/epc-api/v1/powerbi/orders",
                "/epc-api/v1/powerbi/sales", "/epc-api/v1/powerbi/stock",
                "/epc-api/v1/powerbi/gl", "/epc-api/v1/powerbi/metrics"
            }
        });

    public static void EpcApiV1HandleCapabilities()
    {
        var cats = CapabilityCategories?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal);
        var areas = cats.Select(p => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["area"] = p.Key,
            ["capability_count"] = ToInt(p.Value)
        }).ToList();
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["api_phase"] = 1,
            ["capability_areas"] = areas,
            ["total_capabilities"] = CapabilityCount?.Invoke() ?? 0,
            ["integrations"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["public_rest"] = "Phase 1 read-only JSON at /epc-api/v1/",
                ["power_bi"] = "Web connector datasets at /epc-api/v1/powerbi/* (JSON/CSV, X-API-Key)",
                ["erp_ajax"] = "Internal CP session — not public",
                ["future"] = new[] { "webhooks", "e-invoice submit", "d365 sync", "marketplace write APIs v2", "power_bi_azure_embed" }
            },
            ["docs"] = "https://www.ecomae.com/platform/api-documentation"
        });
    }

    public static void EpcApiV1HandleOpenapi()
    {
        if (OpenApiPresent?.Invoke() != true)
        {
            EpcApiV1Error(404, "spec_missing", "OpenAPI spec file not deployed.");
            return;
        }

        LastStatus = 200;
        LastOutput = OpenApiBody?.Invoke() ?? "";
    }

    public static void EpcApiV1HandleTenantInfo(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1Auth(platformPdo, "read:tenant");
        if (auth is null)
        {
            return;
        }

        var t = (Dictionary<string, object?>)auth["tenant"]!;
        var key = (Dictionary<string, object?>)auth["key"]!;
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = Str(t.GetValueOrDefault("site_key")),
                ["trade_name"] = Str(t.GetValueOrDefault("trade_name")),
                ["hostname"] = Str(t.GetValueOrDefault("hostname")),
                ["industry_code"] = Str(t.GetValueOrDefault("industry_code")),
                ["status"] = Str(t.GetValueOrDefault("status")),
                ["access_mode"] = EpcApiV1TenantAccessMode(platformPdo, t),
                ["erp_only_shared"] = !PhpEmpty(t.GetValueOrDefault("erp_only_shared"))
            },
            ["key_label"] = Str(key.GetValueOrDefault("label")),
            ["scopes"] = auth["scopes"]
        });
    }

    public static void EpcApiV1HandleOrders(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1Auth(platformPdo, "read:orders");
        if (auth is null)
        {
            return;
        }

        var tenantPdo = EpcApiV1TenantPdo((Dictionary<string, object?>)auth["tenant"]!);
        if (tenantPdo is null)
        {
            EpcApiV1Error(503, "tenant_db_unavailable", "Could not connect to tenant database.");
            return;
        }

        var limit = Math.Min(20, Math.Max(1, ToInt(Query.GetValueOrDefault("limit"), 20)));
        var statusSql = ErpOrderStatusNameSql?.Invoke(tenantPdo) ?? "''";
        var rows = QueryRows(tenantPdo,
            "SELECT `id`, `time`, `user_id`, `paid`, `paid_type`, `successfully_created`, " + statusSql +
            " AS status_name FROM `shop_orders` WHERE `successfully_created` = 1 ORDER BY `id` DESC LIMIT " + limit);
        var orders = rows.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = ToInt(row.GetValueOrDefault("id")),
            ["time"] = row.ContainsKey("time") && row["time"] is not null ? Gmt(ToLong(row["time"])) : null,
            ["user_id"] = ToInt(row.GetValueOrDefault("user_id")),
            ["paid"] = !PhpEmpty(row.GetValueOrDefault("paid")),
            ["paid_type"] = ToInt(row.GetValueOrDefault("paid_type")),
            ["status_name"] = Str(row.GetValueOrDefault("status_name"))
        }).ToList();
        var tenant = (Dictionary<string, object?>)auth["tenant"]!;
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_site_key"] = Str(tenant.GetValueOrDefault("site_key")),
            ["count"] = orders.Count,
            ["limit"] = limit,
            ["orders"] = orders
        });
    }

    public static void EpcApiV1HandleProductsSearch(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1Auth(platformPdo, "read:products");
        if (auth is null)
        {
            return;
        }

        var q = (Query.GetValueOrDefault("q") ?? "").Trim();
        if (q == "")
        {
            EpcApiV1Error(400, "missing_query", "Provide q= search term.");
            return;
        }

        var tenantPdo = EpcApiV1TenantPdo((Dictionary<string, object?>)auth["tenant"]!);
        if (tenantPdo is null)
        {
            EpcApiV1Error(503, "tenant_db_unavailable", "Could not connect to tenant database.");
            return;
        }

        var limit = Math.Min(20, Math.Max(1, ToInt(Query.GetValueOrDefault("limit"), 20)));
        var like = "%" + q + "%";
        var rows = QueryRows(tenantPdo,
            "SELECT `id`, `caption`, `alias`, `category_id`, `published_flag` FROM `shop_catalogue_products` " +
            "WHERE `published_flag` = 1 AND (`caption` LIKE @q OR `alias` LIKE @q2) ORDER BY `caption` ASC LIMIT " + limit,
            ("@q", like), ("@q2", like));
        var products = rows.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = ToInt(row.GetValueOrDefault("id")),
            ["caption"] = Str(row.GetValueOrDefault("caption")),
            ["alias"] = Str(row.GetValueOrDefault("alias")),
            ["category_id"] = ToInt(row.GetValueOrDefault("category_id"))
        }).ToList();
        var tenant = (Dictionary<string, object?>)auth["tenant"]!;
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_site_key"] = Str(tenant.GetValueOrDefault("site_key")),
            ["query"] = q,
            ["count"] = products.Count,
            ["products"] = products
        });
    }

    public static void EpcApiV1HandleErpDashboard(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1Auth(platformPdo, "read:erp");
        if (auth is null)
        {
            return;
        }

        var tenantPdo = EpcApiV1TenantPdo((Dictionary<string, object?>)auth["tenant"]!);
        if (tenantPdo is null)
        {
            EpcApiV1Error(503, "tenant_db_unavailable", "Could not connect to tenant database.");
            return;
        }

        if (ErpDashboard is null)
        {
            EpcApiV1Error(503, "erp_unavailable", "ERP helpers not available on this stack.");
            return;
        }

        var dash = ErpDashboard(tenantPdo);
        var tenant = (Dictionary<string, object?>)auth["tenant"]!;
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_site_key"] = Str(tenant.GetValueOrDefault("site_key")),
            ["period"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["from"] = Gmt(ToLong(dash.GetValueOrDefault("date_from"))),
                ["to"] = Gmt(ToLong(dash.GetValueOrDefault("date_to")))
            },
            ["kpis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["order_count"] = ToInt(dash.GetValueOrDefault("order_count")),
                ["revenue_ex_vat"] = Math.Round(ToFloat(dash.GetValueOrDefault("revenue_ex_vat")), 2, MidpointRounding.AwayFromZero),
                ["profit_ex_vat"] = Math.Round(ToFloat(dash.GetValueOrDefault("profit_ex_vat")), 2, MidpointRounding.AwayFromZero),
                ["receivable_due_orders"] = Math.Round(ToFloat(dash.GetValueOrDefault("receivable_due_orders")), 2, MidpointRounding.AwayFromZero),
                ["customer_ledger_balance"] = Math.Round(ToFloat(dash.GetValueOrDefault("customer_ledger_balance")), 2, MidpointRounding.AwayFromZero),
                ["payable_balance"] = Math.Round(ToFloat(dash.GetValueOrDefault("payable_balance")), 2, MidpointRounding.AwayFromZero),
                ["cash_bank_total"] = Math.Round(ToFloat(dash.GetValueOrDefault("cash_bank_total")), 2, MidpointRounding.AwayFromZero),
                ["vat_net_payable"] = Math.Round(ToFloat(dash.GetValueOrDefault("vat_net_payable")), 2, MidpointRounding.AwayFromZero),
                ["vat_net_status"] = Str(dash.GetValueOrDefault("vat_net_status"))
            }
        });
    }

    public static Dictionary<string, object?>? EpcApiV1AuthPowerbi(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1Auth(platformPdo);
        if (auth is null)
        {
            return null;
        }

        var scopes = (List<string>)auth["scopes"]!;
        if (!EpcApiV1ScopeAllowed(scopes, "read:bi") && !EpcApiV1ScopeAllowed(scopes, "read:erp"))
        {
            EpcApiV1Error(403, "insufficient_scope", "This key lacks scope: read:bi (or read:erp / read:*).");
            return null;
        }

        return auth;
    }

    public static void EpcApiV1PowerbiRespond(Dictionary<string, object?> dataset, string datasetId, string siteKey)
    {
        var headers = dataset.GetValueOrDefault("headers") as IEnumerable<object?> ?? [];
        var headerList = headers.Select(h => Str(h)).ToList();
        var rows = dataset.GetValueOrDefault("rows") as IEnumerable<object?> ?? [];
        if (PowerBiWantsCsv?.Invoke() == true)
        {
            var rowArrays = rows.Select(r => r is object?[] a ? a : r is List<object?> l ? l.ToArray() : []).ToList();
            if (PowerBiEmitCsv is not null)
            {
                PowerBiEmitCsv(headerList, rowArrays, "powerbi_" + datasetId + ".csv");
                return;
            }

            LastOutput = "CSV:powerbi_" + datasetId + ".csv:" + string.Join(",", headerList);
            return;
        }

        var objects = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var cells = row is object?[] a ? a : row is List<object?> l ? l.ToArray() : [];
            var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < headerList.Count; i++)
            {
                obj[headerList[i]] = i < cells.Length ? cells[i] : null;
            }

            objects.Add(obj);
        }

        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_site_key"] = siteKey,
            ["dataset"] = datasetId,
            ["count"] = objects.Count,
            ["meta"] = dataset.GetValueOrDefault("meta") ?? new Dictionary<string, object?>(StringComparer.Ordinal),
            ["columns"] = headerList,
            ["rows"] = objects,
            ["power_bi"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["format_hint"] = "Add ?format=csv for Power BI Web connector CSV refresh",
                ["scope"] = "read:bi"
            }
        });
    }

    public static void EpcApiV1HandlePowerbiCatalog(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1AuthPowerbi(platformPdo);
        if (auth is null)
        {
            return;
        }

        var tenant = (Dictionary<string, object?>)auth["tenant"]!;
        EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenant_site_key"] = Str(tenant.GetValueOrDefault("site_key")),
            ["capabilities"] = PowerBiCapabilities?.Invoke() ?? new object[] { },
            ["datasets"] = PowerBiDatasetCatalog?.Invoke("https://www.ecomae.com") ?? new object[] { },
            ["auth"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["header"] = "X-API-Key",
                ["scopes_accepted"] = new[] { "read:bi", "read:erp", "read:*", "*" }
            }
        });
    }

    public static void EpcApiV1HandlePowerbiKpis(MySqlConnection platformPdo)
        => PowerBiDataset(platformPdo, (db, key) => PowerBiDatasetKpis?.Invoke(db, key) ?? EmptySet(), "kpis");

    public static void EpcApiV1HandlePowerbiOrders(MySqlConnection platformPdo)
    {
        var limit = Math.Min(200, Math.Max(1, ToInt(Query.GetValueOrDefault("limit"), 100)));
        PowerBiDataset(platformPdo, (db, key) => PowerBiDatasetOrders?.Invoke(db, key, limit) ?? EmptySet(), "orders");
    }

    public static void EpcApiV1HandlePowerbiReport(MySqlConnection platformPdo, string type)
    {
        var to = PowerBiParseDate?.Invoke("to", Now()) ?? Now();
        var fromDefault = to - 7776000;
        var from = PowerBiParseDate?.Invoke("from", fromDefault) ?? fromDefault;
        PowerBiDataset(platformPdo, (db, key) => PowerBiDatasetReport?.Invoke(db, type, from, to) ?? EmptySet(), type);
    }

    public static void EpcApiV1HandlePowerbiMetrics(MySqlConnection platformPdo)
    {
        var auth = EpcApiV1AuthPowerbi(platformPdo);
        if (auth is null)
        {
            return;
        }

        var siteKey = Str(((Dictionary<string, object?>)auth["tenant"]!).GetValueOrDefault("site_key"));
        EpcApiV1PowerbiRespond(PowerBiDatasetMetrics?.Invoke(platformPdo, siteKey) ?? EmptySet(), "metrics", siteKey);
    }

    public static void EpcApiV1Dispatch()
    {
        var route = EpcApiV1RoutePath();
        var platformPdo = EpcApiV1PlatformPdo();
        if (platformPdo is null)
        {
            EpcApiV1Error(503, "platform_db_unavailable", "Platform database unavailable.");
            return;
        }

        switch (route)
        {
            case "":
            case "health":
                EpcApiV1HandleHealth();
                return;
            case "capabilities":
                EpcApiV1HandleCapabilities();
                return;
            case "openapi.json":
                EpcApiV1HandleOpenapi();
                return;
            case "tenant/info":
                EpcApiV1HandleTenantInfo(platformPdo);
                return;
            case "orders":
                EpcApiV1HandleOrders(platformPdo);
                return;
            case "products/search":
                EpcApiV1HandleProductsSearch(platformPdo);
                return;
            case "erp/dashboard-summary":
                EpcApiV1HandleErpDashboard(platformPdo);
                return;
            case "powerbi/catalog":
                EpcApiV1HandlePowerbiCatalog(platformPdo);
                return;
            case "powerbi/kpis":
                EpcApiV1HandlePowerbiKpis(platformPdo);
                return;
            case "powerbi/orders":
                EpcApiV1HandlePowerbiOrders(platformPdo);
                return;
            case "powerbi/sales":
                EpcApiV1HandlePowerbiReport(platformPdo, "sales");
                return;
            case "powerbi/stock":
                EpcApiV1HandlePowerbiReport(platformPdo, "stock");
                return;
            case "powerbi/gl":
                EpcApiV1HandlePowerbiReport(platformPdo, "gl");
                return;
            case "powerbi/metrics":
                EpcApiV1HandlePowerbiMetrics(platformPdo);
                return;
            default:
                EpcApiV1Error(404, "not_found", "Unknown API route: " + route);
                return;
        }
    }

    public static Dictionary<string, object?>? LastPayload()
    {
        if (LastOutput == "" || LastOutput.StartsWith("CSV:", StringComparison.Ordinal))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(LastOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = false });
    }

    private static void PowerBiDataset(MySqlConnection platformPdo, Func<MySqlConnection, string, Dictionary<string, object?>> load, string id)
    {
        var auth = EpcApiV1AuthPowerbi(platformPdo);
        if (auth is null)
        {
            return;
        }

        var tenant = (Dictionary<string, object?>)auth["tenant"]!;
        var tenantPdo = EpcApiV1TenantPdo(tenant);
        if (tenantPdo is null)
        {
            EpcApiV1Error(503, "tenant_db_unavailable", "Could not connect to tenant database.");
            return;
        }

        var siteKey = Str(tenant.GetValueOrDefault("site_key"));
        EpcApiV1PowerbiRespond(load(tenantPdo, siteKey), id, siteKey);
    }

    private static Dictionary<string, object?> EmptySet()
        => new(StringComparer.Ordinal)
        {
            ["headers"] = new List<string>(),
            ["rows"] = new List<object?[]>(),
            ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };

    private static long Now()
        => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Gmt(long ts)
        => DateTimeOffset.FromUnixTimeSeconds(ts).ToString("yyyy-MM-ddTHH:mm:ss+00:00", CultureInfo.InvariantCulture);

    private static string Sha256(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void Exec(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
    }

    private static List<Dictionary<string, object?>> QueryRows(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Box(reader.GetValue(i));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static Dictionary<string, object?>? QueryOne(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
        => QueryRows(pdo, sql, args).FirstOrDefault();

    private static object? Scalar(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        var scalar = cmd.ExecuteScalar();
        return scalar is null or DBNull ? null : Box(scalar);
    }

    private static object? Box(object value)
        => value switch
        {
            decimal d => (double)d,
            bool b => b ? 1 : 0,
            sbyte sb => (int)sb,
            byte b => (int)b,
            short s => (int)s,
            ushort us => (int)us,
            uint ui => (int)ui,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            ulong ul when ul <= int.MaxValue => (int)ul,
            _ => value
        };

    private static bool PhpEmpty(object? value)
    {
        if (value is null or DBNull or false)
        {
            return true;
        }

        return value switch
        {
            string s => s is "" or "0",
            int i => i == 0,
            long l => l == 0,
            double d => d == 0,
            decimal m => m == 0,
            _ => false
        };
    }

    private static int ToInt(object? value, int fallback = 0)
    {
        if (value is null or DBNull or false or "")
        {
            return fallback;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static long ToLong(object? value)
    {
        if (value is null or DBNull)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static double ToFloat(object? value)
    {
        if (value is null or DBNull)
        {
            return 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string Str(object? value, string fallback = "")
        => value is null or DBNull ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
