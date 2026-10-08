using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// POSTs to the guest order page (<see cref="StorefrontOrderNotAuthorized.PagePath"/>, with or without a language prefix)
/// that carry an <c>action</c>: answered by <see cref="StorefrontOrderNotAuthorized.PostAsync"/>. Any other POST is
/// rendered as the GET page, as PHP does.
/// </summary>
public sealed class StorefrontOrderNotAuthorizedPostMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontOrderNotAuthorizedPostMiddleware(RequestDelegate next) => _next = next;

    public static bool IsPagePath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        const string page = StorefrontOrderNotAuthorized.PagePath;
        return value == page
            || (value.Length == page.Length + 3 && value[0] == '/' && char.IsAsciiLetterLower(value[1]) && char.IsAsciiLetterLower(value[2]) && value.EndsWith(page, StringComparison.Ordinal));
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

        if (StorefrontOrderNotAuthorized.IsActionPost(form) && connections.IsConfigured)
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            var lang = StorefrontPhpHomeLinks.LangHref(context);
            try
            {
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                var translator = new StorefrontPhpTranslator(connection, lang.TrimStart('/'));
                var t = (Func<string, Task<string>>)(key => translator.TextAsync(key, context.RequestAborted));
                var alertStrings = new Dictionary<int, string>();
                foreach (var id in StorefrontActionsAlert.StringIds)
                {
                    alertStrings[id] = await t(id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }

                var cookies = context.Request.Cookies;
                var answer = await StorefrontOrderNotAuthorized.PostAsync(
                    connection,
                    new StorefrontOrderNotAuthorized.PostInput(
                        form,
                        cookies["session"],
                        cookies["u_id"],
                        cookies["admin_session"],
                        cookies["admin_u_id"],
                        context.Request.Headers.Referer.ToString(),
                        lang,
                        StorefrontActionsAlert.Render(key => StorefrontActionsAlert.Last(context.Request.Query, key), id => alertStrings[id])),
                    t,
                    async (orderId, status) => (await protocol.SetOrderStatusAsync(connection, [orderId], status, ShopProtocolActor.Robot, context.RequestAborted).ConfigureAwait(false)).Status,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    context.RequestAborted).ConfigureAwait(false);
                context.Response.ContentType = answer.ContentType;
                await context.Response.WriteAsync(answer.Body, context.RequestAborted).ConfigureAwait(false);
                return;
            }
            catch (System.Data.Common.DbException)
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
