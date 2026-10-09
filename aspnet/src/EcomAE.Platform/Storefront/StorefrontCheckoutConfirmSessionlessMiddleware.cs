using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>checkout_confirm.php</c> exits with raw JSON before CMS chrome when a guest has no existing session.
/// Razor cannot reproduce that exit from inside <c>PhpChromeLayout</c>, so this narrow pre-routing interception
/// handles only the two canonical confirm routes (plus their two-letter language form).
/// </summary>
public sealed class StorefrontCheckoutConfirmSessionlessMiddleware(RequestDelegate next)
{
    private const string JsonContentType = "application/json; charset=utf-8";

    public async Task InvokeAsync(
        HttpContext context,
        ILegacySessionValidator sessions,
        IStorefrontGuestSessionService guests,
        ITenantDbConnectionFactory connections)
    {
        if (!TryMatch(context.Request.Method, context.Request.Path.Value, out var lang))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var customer = await sessions.ValidateCustomerAsync(context, context.RequestAborted).ConfigureAwait(false);
        if (customer.Kind == LegacySessionKind.Customer && customer.UserId > 0)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // PHP does not create a session on this GET. Existing guest sessions pass through to the Razor renderer.
        var guest = await guests.ResolveAsync(context, createIfMissing: false, context.RequestAborted).ConfigureAwait(false);
        if (guest.HasGuestCart)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
        var message = await new StorefrontPhpTranslator(connection, lang).TextAsync(4460, context.RequestAborted).ConfigureAwait(false);
        var json = "{\"status\":false,\"code\":\"incorrect_session\",\"message\":"
                   + EcomAE.Platform.Auth.OAuthStart.PhpJsonString(message) + "}";
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = JsonContentType;
        context.Response.ContentLength = bytes.Length;
        if (HttpMethods.IsGet(context.Request.Method))
        {
            await context.Response.Body.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);
        }
    }

    public static bool TryMatch(string? method, string? path, out string lang)
    {
        lang = "en";
        if (!string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(method, HttpMethods.Head, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = (path ?? string.Empty).TrimEnd('/');
        const string canonical = "/shop/checkout/confirm";
        if (value.Equals(canonical, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.Length == canonical.Length + 3
            && value[0] == '/'
            && value[3..].Equals(canonical, StringComparison.OrdinalIgnoreCase)
            && char.IsAsciiLetter(value[1])
            && char.IsAsciiLetter(value[2]))
        {
            lang = value.Substring(1, 2).ToLowerInvariant();
            return true;
        }

        return false;
    }
}
