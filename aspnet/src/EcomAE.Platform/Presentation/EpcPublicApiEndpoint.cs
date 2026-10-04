using System.Data.Common;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Presentation;

/// <summary>HTTP adapter for <see cref="EpcPublicApiV1"/>.</summary>
public static class EpcPublicApiEndpoint
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<IResult> Handle(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var route = EpcPublicApiV1.RouteOf(context.Request.Path.Value);
        DbConnection registry;
        try
        {
            registry = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var ping = registry.CreateCommand();
            ping.CommandText = "SELECT 1";
            await ping.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiJson(503, EpcPublicApiV1.ErrorJson("platform_db_unavailable", "Platform database unavailable."));
        }

        await using (registry)
        {
            if (route is "" or "health")
            {
                return ApiJson(200, EpcPublicApiV1.HealthJson(DateTimeOffset.UtcNow));
            }

            if (route.Equals("capabilities", StringComparison.OrdinalIgnoreCase))
            {
                return ApiJson(200, EpcPublicApiV1.CapabilitiesJson());
            }

            if (route.Equals("openapi.json", StringComparison.OrdinalIgnoreCase))
            {
                var spec = EpcPublicApiV1.OpenApiPath();
                if (spec is null)
                {
                    return ApiJson(404, EpcPublicApiV1.ErrorJson("spec_missing", "OpenAPI spec file not deployed."));
                }

                context.Response.Headers["X-ECOM-API-Version"] = "v1";
                context.Response.Headers.CacheControl = "public, max-age=300";
                return Results.File(spec, "application/json; charset=utf-8");
            }

            if (!EpcPublicApiV1.IsKeyedRoute(route))
            {
                return ApiJson(404, EpcPublicApiV1.ErrorJson("not_found", "Unknown API route: " + route));
            }

            var auth = await AuthenticateAsync(registry, context, route, cancellationToken).ConfigureAwait(false);
            if (auth.Error is not null)
            {
                return auth.Error;
            }

            if (route.Equals("powerbi/catalog", StringComparison.OrdinalIgnoreCase))
            {
                return ApiJson(200, PowerBiCatalogJson(auth.SiteKey));
            }

            if (route.Equals("erp/dashboard-summary", StringComparison.OrdinalIgnoreCase)
                || (EpcPublicApiV1.IsPowerBiRoute(route) && !route.Equals("powerbi/catalog", StringComparison.OrdinalIgnoreCase)))
            {
                return ApiJson(503, EpcPublicApiV1.ErrorJson("erp_unavailable", "ERP helpers not available on this stack."));
            }

            if (route.Equals("tenant/info", StringComparison.OrdinalIgnoreCase))
            {
                return await TenantInfoAsync(registry, auth, cancellationToken).ConfigureAwait(false);
            }

            if (route.Equals("orders", StringComparison.OrdinalIgnoreCase))
            {
                return await OrdersAsync(connections, context, auth, cancellationToken).ConfigureAwait(false);
            }

            if (route.Equals("products/search", StringComparison.OrdinalIgnoreCase))
            {
                return await ProductsAsync(connections, context, auth, cancellationToken).ConfigureAwait(false);
            }

            return ApiJson(404, EpcPublicApiV1.ErrorJson("not_found", "Unknown API route: " + route));
        }
    }

    private static async Task<AuthHold> AuthenticateAsync(
        DbConnection registry,
        HttpContext context,
        string route,
        CancellationToken cancellationToken)
    {
        var raw = EpcPublicApiV1.ExtractKey(context.Request);
        if (raw.Length == 0)
        {
            return AuthHold.Fail(ApiJson(401, EpcPublicApiV1.ErrorJson("missing_api_key", EpcPublicApiV1.MissingKeyMessage)));
        }

        try
        {
            await using (var ensure = registry.CreateCommand())
            {
                ensure.CommandText = EpcPublicApiV1.EnsureKeysTableSql;
                await ensure.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var lookup = registry.CreateCommand();
            lookup.CommandText = """
                SELECT `id`, `tenant_site_key`, `label`, `scopes_json`
                FROM `epc_api_keys`
                WHERE `key_hash` = @hash AND `active` = 1
                LIMIT 1
                """;
            Add(lookup, "@hash", EpcPublicApiV1.Sha256Hex(raw));
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return AuthHold.Fail(ApiJson(401, EpcPublicApiV1.ErrorJson("invalid_api_key", "API key not recognized or revoked.")));
            }

            var id = reader.GetInt32(0);
            var siteKey = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var label = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var scopes = EpcPublicApiV1.ParseScopes(reader.IsDBNull(3) ? "[]" : reader.GetString(3));
            await reader.CloseAsync().ConfigureAwait(false);

            if (EpcPublicApiV1.IsPowerBiRoute(route))
            {
                if (!EpcPublicApiV1.PowerBiScopeAllowed(scopes))
                {
                    return AuthHold.Fail(ApiJson(403, EpcPublicApiV1.ErrorJson(
                        "insufficient_scope",
                        "This key lacks scope: read:bi (or read:erp / read:*).")));
                }
            }
            else if (EpcPublicApiV1.RequiredScope(route) is { Length: > 0 } need
                     && !EpcPublicApiV1.ScopeAllowed(scopes, need))
            {
                return AuthHold.Fail(ApiJson(403, EpcPublicApiV1.ErrorJson("insufficient_scope", "This key lacks scope: " + need)));
            }

            await using var touch = registry.CreateCommand();
            touch.CommandText = "UPDATE `epc_api_keys` SET `last_used_at` = @ts WHERE `id` = @id";
            Add(touch, "@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Add(touch, "@id", id);
            await touch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new AuthHold(siteKey, label, scopes, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return AuthHold.Fail(ApiJson(500, EpcPublicApiV1.ErrorJson("internal_error", "API request failed.")));
        }
    }

    private static async Task<IResult> TenantInfoAsync(DbConnection registry, AuthHold auth, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = registry.CreateCommand();
            command.CommandText = """
                SELECT `site_key`, `trade_name`, `hostname`, `industry_code`, `status`, IFNULL(`erp_only_shared`, 0)
                FROM `epc_portal_tenants`
                WHERE `site_key` = @key
                LIMIT 1
                """;
            Add(command, "@key", SanitizeSiteKey(auth.SiteKey));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ApiJson(403, EpcPublicApiV1.ErrorJson("tenant_not_found", "Tenant linked to this API key is not registered."));
            }

            var hostname = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var erpOnly = !reader.IsDBNull(5) && Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) != 0;
            var tenant = new Dictionary<string, object?>
            {
                ["site_key"] = reader.IsDBNull(0) ? "" : reader.GetString(0),
                ["trade_name"] = reader.IsDBNull(1) ? "" : reader.GetString(1),
                ["hostname"] = hostname,
                ["industry_code"] = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ["status"] = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ["erp_only_shared"] = erpOnly,
            };
            await reader.DisposeAsync().ConfigureAwait(false);
            tenant["access_mode"] = await AccessModeAsync(registry, hostname, cancellationToken).ConfigureAwait(false);
            var body = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["tenant"] = tenant,
                ["key_label"] = auth.Label,
                ["scopes"] = auth.Scopes,
            }, Pretty);
            return ApiJson(200, body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiJson(500, EpcPublicApiV1.ErrorJson("internal_error", "API request failed."));
        }
    }

    private static async Task<string> AccessModeAsync(DbConnection registry, string hostname, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return "full";
        }

        var bare = hostname.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? hostname[4..] : hostname;
        await using var command = registry.CreateCommand();
        command.CommandText = """
            SELECT `access_mode` FROM `epc_portal_site_settings`
            WHERE `host` = @host OR `host` = @bare
            LIMIT 1
            """;
        Add(command, "@host", hostname);
        Add(command, "@bare", bare);
        var mode = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return string.IsNullOrWhiteSpace(mode) ? "full" : mode;
    }

    private static async Task<IResult> OrdersAsync(
        ITenantDbConnectionFactory connections,
        HttpContext context,
        AuthHold auth,
        CancellationToken cancellationToken)
    {
        var tenantDb = await OpenTenantAsync(connections, auth.SiteKey, cancellationToken).ConfigureAwait(false);
        if (tenantDb.Error is not null)
        {
            return tenantDb.Error;
        }

        await using var connection = tenantDb.Connection!;
        var limit = Clamp(context.Request.Query["limit"].ToString(), 20, 20);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, `time`, `user_id`, `paid`, `paid_type`
                FROM `shop_orders`
                WHERE `successfully_created` = 1
                ORDER BY `id` DESC
                LIMIT 
                """ + limit.ToString(CultureInfo.InvariantCulture);
            var orders = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var unix = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                orders.Add(new Dictionary<string, object?>
                {
                    ["id"] = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["time"] = unix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+00:00"
                        : null,
                    ["user_id"] = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    ["paid"] = !reader.IsDBNull(3) && Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) != 0,
                    ["paid_type"] = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    ["status_name"] = "",
                });
            }

            return ApiJson(200, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["tenant_site_key"] = auth.SiteKey,
                ["count"] = orders.Count,
                ["limit"] = limit,
                ["orders"] = orders,
            }, Pretty));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiJson(500, EpcPublicApiV1.ErrorJson("internal_error", "API request failed."));
        }
    }

    private static async Task<IResult> ProductsAsync(
        ITenantDbConnectionFactory connections,
        HttpContext context,
        AuthHold auth,
        CancellationToken cancellationToken)
    {
        var query = context.Request.Query["q"].ToString().Trim();
        if (query.Length == 0)
        {
            return ApiJson(400, EpcPublicApiV1.ErrorJson("missing_query", "Provide q= search term."));
        }

        var tenantDb = await OpenTenantAsync(connections, auth.SiteKey, cancellationToken).ConfigureAwait(false);
        if (tenantDb.Error is not null)
        {
            return tenantDb.Error;
        }

        await using var connection = tenantDb.Connection!;
        var limit = Clamp(context.Request.Query["limit"].ToString(), 20, 20);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `id`, `caption`, `alias`, `category_id`
                FROM `shop_catalogue_products`
                WHERE `published_flag` = 1 AND (`caption` LIKE @q OR `alias` LIKE @q)
                ORDER BY `caption` ASC
                LIMIT 
                """ + limit.ToString(CultureInfo.InvariantCulture);
            Add(command, "@q", "%" + query + "%");
            var products = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                products.Add(new Dictionary<string, object?>
                {
                    ["id"] = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["caption"] = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ["alias"] = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ["category_id"] = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                });
            }

            return ApiJson(200, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["ok"] = true,
                ["tenant_site_key"] = auth.SiteKey,
                ["query"] = query,
                ["count"] = products.Count,
                ["products"] = products,
            }, Pretty));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiJson(500, EpcPublicApiV1.ErrorJson("internal_error", "API request failed."));
        }
    }

    private static async Task<(DbConnection? Connection, IResult? Error)> OpenTenantAsync(
        ITenantDbConnectionFactory connections,
        string siteKey,
        CancellationToken cancellationToken)
    {
        string database;
        string user;
        string password;
        try
        {
            await using var registry = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = registry.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`db_name`, ''), IFNULL(`db_user`, ''), IFNULL(`db_password`, '')
                FROM `epc_portal_tenants`
                WHERE `site_key` = @key
                LIMIT 1
                """;
            Add(command, "@key", SanitizeSiteKey(siteKey));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (null, ApiJson(403, EpcPublicApiV1.ErrorJson("tenant_not_found", "Tenant linked to this API key is not registered.")));
            }

            database = reader.GetString(0).Trim();
            user = reader.GetString(1);
            password = reader.GetString(2);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ApiJson(500, EpcPublicApiV1.ErrorJson("internal_error", "API request failed.")));
        }

        if (database.Length == 0)
        {
            return (null, ApiJson(503, EpcPublicApiV1.ErrorJson("tenant_db_unavailable", "Could not connect to tenant database.")));
        }

        try
        {
            var connection = await connections.OpenAsync(
                database,
                string.IsNullOrWhiteSpace(user) ? null : user,
                string.IsNullOrWhiteSpace(password) ? null : password,
                cancellationToken).ConfigureAwait(false);
            return (connection, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ApiJson(503, EpcPublicApiV1.ErrorJson("tenant_db_unavailable", "Could not connect to tenant database.")));
        }
    }

    private static string PowerBiCatalogJson(string siteKey)
    {
        const string root = "https://www.ecomae.com/epc-api/v1/powerbi";
        object Dataset(string id, string name, string description, string[] formats, string refresh, Dictionary<string, string>? parameters = null)
        {
            var row = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = name,
                ["description"] = description,
                ["path"] = root + "/" + id,
                ["formats"] = formats,
                ["scope"] = "read:bi",
                ["refresh"] = refresh,
            };
            if (parameters is not null)
            {
                row["params"] = parameters;
            }

            return row;
        }

        var body = new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["tenant_site_key"] = siteKey,
            ["capabilities"] = new Dictionary<string, object?>
            {
                ["version"] = "1.0.0",
                ["available_now"] = new Dictionary<string, bool>
                {
                    ["web_connector_json"] = true,
                    ["web_connector_csv"] = true,
                    ["api_key_auth"] = true,
                    ["tenant_isolation"] = true,
                    ["workspace_config_storage"] = true,
                    ["url_embed_iframe"] = true,
                    ["native_erp_dashboard"] = true,
                    ["metabase_embed_parallel"] = true,
                },
                ["needs_customer_credentials"] = new Dictionary<string, string>
                {
                    ["azure_ad_app"] = "Azure AD application (client id/secret) in their Microsoft tenant",
                    ["power_bi_pro_or_embedded"] = "Power BI Pro / Premium / Embedded capacity for secure embed tokens",
                    ["workspace_access"] = "Workspace + report IDs from their Power BI service",
                },
                ["not_in_scope_phase_a"] = new[]
                {
                    "azure_embed_token_generation",
                    "power_bi_rest_admin_apis",
                    "row_level_security_via_azure",
                },
            },
            ["datasets"] = new object[]
            {
                Dataset("catalog", "Dataset catalog", "Lists all Power BI–ready endpoints for this tenant.", ["json"], "manual"),
                Dataset("kpis", "ERP KPI snapshot", "Flat KPI rows (revenue, AR, AP, cash, VAT) for cards and scorecards.", ["json", "csv"], "scheduled"),
                Dataset("orders", "Recent orders", "Successfully created shop orders (tabular).", ["json", "csv"], "scheduled", new Dictionary<string, string> { ["limit"] = "1–200 (default 100)" }),
                Dataset("sales", "Sales register", "Completed order sales ex-VAT with paid/due amounts.", ["json", "csv"], "scheduled", new Dictionary<string, string> { ["from"] = "YYYY-MM-DD", ["to"] = "YYYY-MM-DD" }),
                Dataset("stock", "Inventory stock", "On-hand qty, average cost, and stock value by SKU/warehouse.", ["json", "csv"], "scheduled"),
                Dataset("gl", "GL trial balance", "Chart-of-accounts trial balance lines.", ["json", "csv"], "scheduled", new Dictionary<string, string> { ["to"] = "YYYY-MM-DD optional as-of date" }),
                Dataset("metrics", "BI metric snapshots", "Latest materialized KPI snapshots from the BI metrics engine (when computed).", ["json", "csv"], "scheduled"),
            },
            ["auth"] = new Dictionary<string, object?>
            {
                ["header"] = "X-API-Key",
                ["scopes_accepted"] = new[] { "read:bi", "read:erp", "read:*", "*" },
            },
        };
        return JsonSerializer.Serialize(body, Pretty);
    }

    private static int Clamp(string raw, int fallback, int max)
    {
        if (!int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, 1, max);
    }

    private static string SanitizeSiteKey(string siteKey)
    {
        var chars = siteKey.Trim().ToLowerInvariant().Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_').ToArray();
        return new string(chars);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static IResult ApiJson(int status, string body)
    {
        return new VersionedJsonResult(status, body);
    }

    private sealed class VersionedJsonResult(int status, string body) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-store";
            httpContext.Response.Headers["X-ECOM-API-Version"] = "v1";
            await httpContext.Response.WriteAsync(body).ConfigureAwait(false);
        }
    }

    private sealed record AuthHold(string SiteKey, string Label, IReadOnlyList<string> Scopes, IResult? Error)
    {
        public static AuthHold Fail(IResult error) => new("", "", [], error);
    }
}
