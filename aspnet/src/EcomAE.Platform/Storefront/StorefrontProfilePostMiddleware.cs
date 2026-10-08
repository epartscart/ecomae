using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Runs <see cref="StorefrontProfileForm.PostAsync"/> for a form POST to the storefront profile page
/// (<c>/users/profile</c>, <c>/{lang}/users/profile</c>) before routing. A CSRF refusal writes stop_csrf's JSON; anything
/// else renders the page as a GET, with the currency change alert in <see cref="StorefrontProfileForm.AlertItemKey"/>.
/// </summary>
public sealed class StorefrontProfilePostMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontProfilePostMiddleware(RequestDelegate next) => _next = next;

    public static bool IsProfilePagePath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        return value == "/users/profile"
            || (value.Length == 17 && value[0] == '/' && char.IsAsciiLetterLower(value[1]) && char.IsAsciiLetterLower(value[2]) && value.EndsWith("/users/profile", StringComparison.Ordinal));
    }

    /// <summary>The <c>$multilang_params['lang_href']</c> of a storefront path: its two-letter prefix, else <c>/en</c>.</summary>
    public static string LangHref(string path)
        => path.Length > 3 && path[0] == '/' && path[3] == '/' && char.IsAsciiLetterLower(path[1]) && char.IsAsciiLetterLower(path[2]) ? path[..3] : "/en";

    public async Task InvokeAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        if (!HttpMethods.IsPost(context.Request.Method) || !IsProfilePagePath(context.Request.Path))
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

        if (!StorefrontProfileForm.IsCurrencyChangePost(post) || !connections.IsConfigured)
        {
            await RenderAsGetAsync(context, null).ConfigureAwait(false);
            return;
        }

        var config = context.RequestServices.GetService<ICpPlatformMailer>()?.ReadConfig() ?? PhpConfig(context);
        var request = new StorefrontProfileForm.Request(
            context.Request.Cookies["session"],
            context.Request.Cookies["u_id"],
            LangHref(context.Request.Path.Value ?? string.Empty),
            config);
        var query = context.Request.Query.ToDictionary(p => p.Key, p => p.Value.Count > 0 ? p.Value[^1] ?? string.Empty : string.Empty, StringComparer.Ordinal);
        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var dispatcher = context.RequestServices.GetService<IStorefrontNotifyDispatcher>();
        StorefrontProfileForm.PostOutcome outcome;
        try
        {
            await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
            var translator = new StorefrontPhpTranslator(connection, request.LangHref.TrimStart('/'));
            var notifier = new StorefrontProfileForm.Notifier(
                (db, userId, ct) => StorefrontOrderNotificationService.CustomerProfileHtmlAsync(db, translator, (int)userId, null, config, ct),
                async (db, name, vars, userId, ct) =>
                {
                    if (dispatcher is null)
                    {
                        return;
                    }

                    var persons = await StorefrontPhpAjax.StaffPersonsWithAsync(db, config, (int)userId, [], ct).ConfigureAwait(false);
                    await dispatcher.SendAsync(db, name, vars, persons, ct).ConfigureAwait(false);
                });
            outcome = await StorefrontProfileForm.PostAsync(connection, request, post, query, notifier, context.RequestAborted).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Database connection failed", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (outcome.Body is not null)
        {
            context.Response.ContentType = "text/html; charset=UTF-8";
            await context.Response.WriteAsync(outcome.Body, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await RenderAsGetAsync(context, outcome.Alert).ConfigureAwait(false);
    }

    private async Task RenderAsGetAsync(HttpContext context, string? alert)
    {
        if (alert is not null)
        {
            context.Items[StorefrontProfileForm.AlertItemKey] = alert;
        }

        context.Request.Method = HttpMethods.Get;
        context.Request.ContentType = null;
        context.Request.ContentLength = 0;
        context.Request.Body = Stream.Null;
        await _next(context).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> PhpConfig(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        return options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : CpPhpConfig.Read(options.Value);
    }
}
