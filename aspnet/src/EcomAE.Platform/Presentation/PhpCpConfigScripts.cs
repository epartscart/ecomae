using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// The PHP Control Panel page scripts that answer with one JavaScript assignment instead of a page, plus the one public
/// CSV download of the prices module:
/// <list type="bullet">
/// <item><c>cp/content/shop/prices_upload/epc_storefront_storage_toggle_config.php</c> (<c>window.EPC_STOREFRONT_STORAGE_TOGGLE</c>)</item>
/// <item><c>cp/content/shop/prices_upload/epc_prices_upload_history_config.php</c> (<c>window.EPC_PRICES_UPLOAD_HISTORY</c>)</item>
/// <item><c>cp/content/shop/prices_upload/epc_commerce_cp_config.php</c> (<c>window.EPC_COMMERCE_CP</c>)</item>
/// <item><c>cp/content/shop/order_process/orders_items_edit_config.php</c> (<c>window.EPC_OI_EDIT</c>)</item>
/// <item><c>cp/content/shop/prices_upload/epc_multivendor_sample_file.php</c> (the multi-vendor sample CSV, no session)</item>
/// </list>
/// Each config script answers <c>window.NAME={};</c> without an admin session (the PHP scripts run outside the CP login
/// wall) and <c>window.NAME={json};</c> with the <c>csrf_guard_key</c> of the admin session row otherwise, exactly like
/// PHP <c>DP_User::getAdminSession()</c> + <c>json_encode(..., JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES)</c>.
/// </summary>
public static class PhpCpConfigScripts
{
    public const string StorageToggleUrl = "/cp/content/shop/prices_upload/epc_storefront_storage_toggle_config.php";
    public const string UploadHistoryUrl = "/cp/content/shop/prices_upload/epc_prices_upload_history_config.php";
    public const string CommerceUrl = "/cp/content/shop/prices_upload/epc_commerce_cp_config.php";
    public const string OrderItemEditUrl = "/cp/content/shop/order_process/orders_items_edit_config.php";
    public const string MultivendorSampleUrl = "/cp/content/shop/prices_upload/epc_multivendor_sample_file.php";

    /// <summary>The backend directory of the PHP <c>DP_Config</c>; every ASP.NET CP URL lives under it.</summary>
    public const string Backend = "cp";

    private static readonly HashSet<string> Urls = new(StringComparer.OrdinalIgnoreCase)
    {
        StorageToggleUrl,
        UploadHistoryUrl,
        CommerceUrl,
        OrderItemEditUrl,
        MultivendorSampleUrl,
    };

    /// <summary>True for the URLs PHP answers outside the CP login wall (anonymous callers get the empty config).</summary>
    public static bool IsScriptPath(string? path) => path is not null && Urls.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(StorageToggleUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAsync(context, connections, "EPC_STOREFRONT_STORAGE_TOGGLE", (csrf, _) => StorageToggleConfig(csrf), needsOrder: false))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(UploadHistoryUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAsync(context, connections, "EPC_PRICES_UPLOAD_HISTORY", (csrf, _) => UploadHistoryConfig(csrf), needsOrder: false))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(CommerceUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAsync(context, connections, "EPC_COMMERCE_CP", (csrf, _) => CommerceConfig(csrf), needsOrder: false))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(OrderItemEditUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeAsync(context, connections, "EPC_OI_EDIT", (_, orderId) => OrderItemEditConfig(QueryInt(context, "id"), orderId), needsOrder: true))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(MultivendorSampleUrl, ["GET", "HEAD", "POST"], (HttpContext context) => ServeSampleAsync(context))
            .DisableAntiforgery().AllowAnonymous();
    }

    /// <summary><c>window.NAME={};</c> for a request without a matching admin session.</summary>
    public static string Empty(string variable) => "window." + variable + "={};";

    public static string Script(string variable, string json) => "window." + variable + "=" + json + ";";

    public static string StorageToggleConfig(string csrf)
        => Object(("ajaxUrl", Text("/" + Backend + "/content/shop/prices_upload/ajax_epc_storefront_storage_toggle.php")), ("csrfKey", Text(csrf)));

    public static string UploadHistoryConfig(string csrf)
        => Object(("ajaxUrl", Text("/" + Backend + "/content/shop/prices_upload/ajax_epc_price_upload_history.php")), ("csrfKey", Text(csrf)));

    public static string CommerceConfig(string csrf)
        => Object(
            ("ajaxUrl", Text("/" + Backend + "/content/shop/prices_upload/ajax_epc_commerce_ingest.php")),
            ("csrfKey", Text(csrf)),
            ("backend", Text(Backend)),
            ("pricesUrl", Text("/" + Backend + "/shop/prices")));

    public static string OrderItemEditConfig(long itemId, long orderId)
        => Object(
            ("backend", Text(Backend)),
            ("itemId", itemId.ToString(CultureInfo.InvariantCulture)),
            ("orderId", orderId.ToString(CultureInfo.InvariantCulture)),
            ("urls", Object(
                ("order", Text("/" + Backend + "/shop/orders/order?order_id=" + orderId.ToString(CultureInfo.InvariantCulture))),
                ("items", Text("/" + Backend + "/shop/orders/items")))));

    /// <summary>PHP 8.3 <c>(int)</c> of a query value. See <see cref="StorefrontPhpInt.Cast"/>.</summary>
    public static long PhpInt(string value, bool present = true)
        => present ? StorefrontPhpInt.Cast(value) : 0;

    /// <summary>
    /// PHP <c>json_encode</c> of one string with <c>JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES</c>: quotes, backslash and
    /// control characters escaped, U+2028/U+2029 kept escaped (PHP only keeps them raw with JSON_UNESCAPED_LINE_TERMINATORS).
    /// </summary>
    public static string Text(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\u2028': builder.Append("\\u2028"); break;
                case '\u2029': builder.Append("\\u2029"); break;
                default:
                    if (ch < 0x20)
                    {
                        builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(ch);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    /// <summary>The <c>csrf_guard_key</c> of the admin <c>sessions</c> row, or null when the cookies match no admin session.</summary>
    public static async Task<string?> AdminCsrfAsync(DbConnection connection, string? session, string? userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `user_id` = ? AND `type` = ? LIMIT 1");
        ErpDb.AddParameters(command, session, userId, 1);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
    }

    /// <summary>PHP <c>SELECT order_id FROM shop_orders_items WHERE id = ?</c> cast with <c>(int)</c>; 0 when missing or on any error.</summary>
    public static async Task<long> OrderIdOfItemAsync(DbConnection connection, long itemId, CancellationToken cancellationToken)
    {
        if (itemId <= 0)
        {
            return 0;
        }

        try
        {
            var raw = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1"), cancellationToken, itemId)
                .ConfigureAwait(false);
            return raw is null ? 0 : PhpInt(raw);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static long QueryInt(HttpContext context, string name)
    {
        var value = StorefrontPhpQuery.Get(context.Request, name);
        if (!value.Present)
        {
            return 0;
        }

        return value.IsArray ? StorefrontPhpInt.CastArray(true, value.ArrayEmpty) : StorefrontPhpInt.Cast(value.Scalar);
    }

    private static string Object(params (string Key, string Json)[] pairs)
        => "{" + string.Join(",", pairs.Select(p => Text(p.Key) + ":" + p.Json)) + "}";

    private static async Task ServeAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        string variable,
        Func<string, long, string> config,
        bool needsOrder)
    {
        var response = context.Response;
        response.ContentType = "application/javascript; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

        string? csrf = null;
        long orderId = 0;
        if (connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                csrf = await AdminCsrfAsync(connection, context.Request.Cookies["admin_session"], context.Request.Cookies["admin_u_id"], context.RequestAborted)
                    .ConfigureAwait(false);
                if (csrf is not null && needsOrder)
                {
                    var itemId = QueryInt(context, "id");
                    orderId = await OrderIdOfItemAsync(connection, itemId, context.RequestAborted).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
                csrf = null;
            }
        }

        await response.WriteAsync(csrf is null ? Empty(variable) : Script(variable, config(csrf, orderId)), context.RequestAborted).ConfigureAwait(false);
    }

    private static async Task ServeSampleAsync(HttpContext context)
    {
        var response = context.Response;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = "attachment; filename=\"epc-multivendor-sample.csv\"";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        await response.WriteAsync(MultivendorSampleCsv(), context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_multivendor_sample_csv()</c>: every <c>fputcsv</c> line ends in a line feed.</summary>
    public static string MultivendorSampleCsv() => EcomAE.Platform.Storefront.StorefrontPhpAjax.MultivendorSampleCsv + "\n";
}
