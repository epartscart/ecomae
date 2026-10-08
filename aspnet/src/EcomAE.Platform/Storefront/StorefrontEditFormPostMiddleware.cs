using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Runs <see cref="StorefrontEditForm.PostAsync"/> for a form POST to the storefront profile edit page
/// (<c>/users/editform</c>, <c>/{lang}/users/editform</c>) before routing and writes its redirect script; a guest or a
/// POST without <c>edit_user</c> renders the page as a GET.
/// </summary>
public sealed class StorefrontEditFormPostMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontEditFormPostMiddleware(RequestDelegate next) => _next = next;

    public static bool IsEditFormPagePath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        return value == "/users/editform"
            || (value.Length == 18 && value[0] == '/' && char.IsAsciiLetterLower(value[1]) && char.IsAsciiLetterLower(value[2]) && value.EndsWith("/users/editform", StringComparison.Ordinal));
    }

    public static IReadOnlyDictionary<string, string> ReadConfig(HttpContext context)
    {
        var mailer = context.RequestServices.GetService<ICpPlatformMailer>();
        if (mailer is not null)
        {
            return mailer.ReadConfig();
        }

        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        return options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : CpPhpConfig.Read(options.Value);
    }

    public async Task InvokeAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !IsEditFormPagePath(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var post = new Dictionary<string, string>(StringComparer.Ordinal);
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            post = form.ToDictionary(p => p.Key, p => p.Value.Count > 0 ? p.Value[^1] ?? string.Empty : string.Empty, StringComparer.Ordinal);
        }

        string? body = null;
        if (StorefrontEditForm.IsEditPost(post) && connections.IsConfigured)
        {
            var request = new StorefrontProfileForm.Request(
                context.Request.Cookies["session"],
                context.Request.Cookies["u_id"],
                StorefrontProfilePostMiddleware.LangHref(context.Request.Path.Value ?? string.Empty),
                ReadConfig(context));
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            try
            {
                await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
                var translator = new StorefrontPhpTranslator(connection, request.LangHref.TrimStart('/'));
                body = await StorefrontEditForm.PostAsync(connection, request, post, key => translator.TextAsync(key, context.RequestAborted), context.RequestAborted).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("Database connection failed", context.RequestAborted).ConfigureAwait(false);
                return;
            }
        }

        if (body is not null)
        {
            context.Response.ContentType = "text/html; charset=UTF-8";
            await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        context.Request.Method = HttpMethods.Get;
        context.Request.ContentType = null;
        context.Request.ContentLength = 0;
        context.Request.Body = Stream.Null;
        await _next(context).ConfigureAwait(false);
    }
}
