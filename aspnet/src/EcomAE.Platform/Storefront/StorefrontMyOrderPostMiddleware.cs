using System.Data.Common;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>Handles the action POST on the signed-in customer order parity page.</summary>
public sealed class StorefrontMyOrderPostMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontMyOrderPostMiddleware(RequestDelegate next) => _next = next;

    public static bool IsPagePath(PathString path)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/');
        return value == StorefrontMyOrder.PagePath
            || (value.Length == StorefrontMyOrder.PagePath.Length + 3
                && value[0] == '/'
                && char.IsAsciiLetterLower(value[1])
                && char.IsAsciiLetterLower(value[2])
                && value.EndsWith(StorefrontMyOrder.PagePath, StringComparison.Ordinal));
    }

    public async Task InvokeAsync(HttpContext context, ITenantDbConnectionFactory connections, IShopOrderProtocolService protocol)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !IsPagePath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Request.HasFormContentType)
        {
            var posted = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            form = posted.ToDictionary(p => p.Key, p => p.Value.Count > 0 ? p.Value[^1] ?? string.Empty : string.Empty, StringComparer.Ordinal);
        }

        if (StorefrontMyOrder.IsActionPost(form) && connections.IsConfigured)
        {
            try
            {
                var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
                var lang = StorefrontPhpHomeLinks.LangHref(context);
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                var translator = new StorefrontPhpTranslator(connection, lang.TrimStart('/'));
                Func<string, Task<string>> translate = key => translator.TextAsync(key, context.RequestAborted);
                var strings = new Dictionary<int, string>();
                foreach (var id in StorefrontActionsAlert.StringIds)
                {
                    strings[id] = await translate(id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }

                var cookies = context.Request.Cookies;
                var answer = await StorefrontMyOrder.PostAsync(
                    connection,
                    new(
                        form,
                        cookies["session"],
                        cookies["u_id"],
                        cookies["admin_session"],
                        cookies["admin_u_id"],
                        context.Request.Headers.Referer.ToString(),
                        lang,
                        StorefrontActionsAlert.Render(key => StorefrontActionsAlert.Last(context.Request.Query, key), id => strings[id])),
                    translate,
                    async (orderId, status) => (await protocol.SetOrderStatusAsync(connection, [orderId], status, ShopProtocolActor.Robot, context.RequestAborted).ConfigureAwait(false)).Status,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    context.RequestAborted).ConfigureAwait(false);
                context.Response.ContentType = answer.ContentType;
                await context.Response.WriteAsync(answer.Body, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            catch (DbException)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Database connection failed", context.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        context.Request.Method = HttpMethods.Get;
        context.Request.ContentType = null;
        context.Request.ContentLength = 0;
        context.Request.Body = Stream.Null;
        await _next(context).ConfigureAwait(false);
    }
}
