using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Data;
using EcomAE.Platform.Services;
using EcomAE.Platform.Storefront;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Small Control Panel PHP includes closed in this slice, verified against PHP 8.3 by
/// <c>Fixtures/TinyPages/golden.json</c>:
/// <list type="bullet">
/// <item><c>cp/content/shop/prices_upload/commerce_data_page.php</c></item>
/// <item><c>cp/content/users/statistics/modal.php</c></item>
/// <item><c>cp/content/control/set_edit_mode_cookie.php</c> (JSONP at the PHP URL)</item>
/// <item><c>cp/modules/logout/module.php</c></item>
/// </list>
/// </summary>
public static class PhpCpTinyPages
{
    public const string CommercePagePath = "cp/content/shop/prices_upload/commerce_data_page.php";
    public const string CustomerModalPath = "cp/content/users/statistics/modal.php";
    public const string EditModeUrl = "/cp/content/control/set_edit_mode_cookie.php";
    public const string LogoutModulePath = "cp/modules/logout/module.php";
    public const string EditModeCookie = "edit_mode";
    public const string Backend = "cp";

    private static readonly HashSet<string> ScriptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        EditModeUrl
    };

    public static bool IsScriptPath(string? path) => path is not null && ScriptPaths.Contains(path);

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(EditModeUrl, ["GET", "HEAD", "POST"], (HttpContext context, ITenantDbConnectionFactory connections)
                => ServeEditModeAsync(context, connections))
            .DisableAntiforgery()
            .AllowAnonymous();
    }

    public static string CommerceRetiredNotice(string? backendDir)
    {
        var backend = StorefrontTinyPages.HtmlSpecialChars(backendDir ?? Backend);
        var mvUrl = "/" + backend + "/shop/prices/multivendor";
        var pricesUrl = "/" + backend + "/shop/prices";
        return "<div class=\"col-lg-12\">"
            + "<div class=\"alert alert-info\" style=\"margin-top:16px;\">"
            + "<h3 style=\"margin-top:0;\">Commerce data upload removed</h3>"
            + "<p>Use <strong>Multi-vendor upload</strong> instead — one file for many vendors, with warehouses created automatically.</p>"
            + "<p>"
            + "<a class=\"btn btn-success\" href=\"" + mvUrl + "\"><i class=\"fa fa-upload\"></i> Open Multi-vendor upload</a> "
            + "<a class=\"btn btn-default\" href=\"" + pricesUrl + "\"><i class=\"fa fa-list\"></i> Price lists</a>"
            + "</p>"
            + "</div></div>";
    }

    public static string CustomerModal(object? customerId)
    {
        if (!PhpGreaterThanZero(customerId))
        {
            return string.Empty;
        }

        var id = FormatId(customerId);
        return "    <button class=\"btn btn-xs btn-info btn-circle\" onclick=\"showCustomerModalInfo(" + id + ")\">\r\n"
            + "        <i class=\"fa fa-info\"></i>\r\n"
            + "    </button>\r\n"
            + "    <div class=\"customer-modal-info-wrapper\" id=\"customer-modal-info-" + id
            + "\" onclick=\"closeCustomerModalInfo(" + id + ")\">\r\n"
            + "    </div>\r\n";
    }

    public static string LogoutModule(string name, string csrf, Func<int, string>? translate = null)
    {
        string T(int id) => (translate ?? (key => "{" + key.ToString(CultureInfo.InvariantCulture) + "}"))(id);
        return "\n<div id=\"logout_form_container\">\n        \n    <div>" + T(3995) + ", " + name + "!</div>\n"
            + "    <form id=\"logout_form\" method=\"POST\" name=\"logout_form\">\n"
            + "        <input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + csrf + "\" />\n"
            + "\t\t<input type=\"hidden\" name=\"logout\" value=\"logout\" />\n"
            + "        <a href=\"javascript:void(0);\" onclick=\"document.forms['logout_form'].submit();\">"
            + T(3996) + "</a>\n    </form>\n</div>";
    }

    public static string NoDbConnectJson() => "{\"status\":false,\"message\":\"No DB connect\"}";

    public static string CsrfJson(string message)
        => "{\"error\":" + PhpDefaultJson(message) + ",\"message\":" + PhpDefaultJson(message) + ",\"status\":false}";

    public static string EditModeJsonp(string? callback, string? editMode)
        => (callback ?? string.Empty) + "(" + PhpDefaultJson(editMode) + ")";

    public static CookieOptions EditModeCookieOptions()
        => new()
        {
            Path = "/",
            HttpOnly = false,
            SameSite = SameSiteMode.Unspecified
        };

    /// <summary>PHP default <c>json_encode</c> of a string or null (slashes and non-ASCII escaped).</summary>
    public static string PhpDefaultJson(string? value)
    {
        if (value is null)
        {
            return "null";
        }

        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '/': builder.Append("\\/"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < 0x20 || ch > 0x7E)
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

    private static async Task ServeEditModeAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var response = context.Response;
        response.ContentType = "text/html; charset=utf-8";
        response.Headers.CacheControl = "no-store, no-cache, must-revalidate";

        string? adminCsrf = null;
        var dbOk = connections.IsConfigured;
        if (dbOk)
        {
            try
            {
                var tenant = context.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                adminCsrf = await PhpCpConfigScripts.AdminCsrfAsync(
                    connection,
                    context.Request.Cookies["admin_session"],
                    context.Request.Cookies["admin_u_id"],
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (DbException)
            {
                dbOk = false;
            }
        }

        if (!dbOk)
        {
            await response.WriteAsync(NoDbConnectJson(), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var posted = StorefrontPhpQuery.Get(context.Request, "csrf_guard_key");
        if (!posted.Present)
        {
            await response.WriteAsync(CsrfJson("Error! CSRF 1"), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var key = posted.IsArray ? "Array" : posted.Scalar ?? string.Empty;
        if (IsPhpEmpty(key))
        {
            await response.WriteAsync(CsrfJson("Error! CSRF 3"), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (adminCsrf is null)
        {
            await response.WriteAsync(CsrfJson("Error! CSRF 3.1"), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(adminCsrf, key, StringComparison.Ordinal))
        {
            await response.WriteAsync(CsrfJson("Error! CSRF 4"), context.RequestAborted).ConfigureAwait(false);
            return;
        }

        var mode = StorefrontPhpQuery.Get(context.Request, "edit_mode");
        var modeValue = mode.Present && !mode.IsArray ? mode.Scalar : null;
        if (modeValue is not null)
        {
            response.Cookies.Append(EditModeCookie, modeValue, EditModeCookieOptions());
        }

        var callback = StorefrontPhpQuery.Get(context.Request, "callback");
        var name = callback.Present && !callback.IsArray ? callback.Scalar : string.Empty;
        await response.WriteAsync(EditModeJsonp(name, modeValue), context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsPhpEmpty(string value)
        => value.Length == 0 || value == "0" || value == "0.0";

    private static bool PhpGreaterThanZero(object? value)
        => value switch
        {
            null => false,
            bool flag => flag,
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToDouble(value, CultureInfo.InvariantCulture) > 0,
            string text => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number > 0,
            _ => false
        };

    private static string FormatId(object? value)
        => value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? "0",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0"
        };
}
