using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Runs <see cref="StorefrontLoginPost"/> for an <c>authentication</c> form POST to the storefront login page
/// (<c>/users/login</c>, <c>/{lang}/users/login</c>, <c>/storefront/login</c>) before routing, as the PHP plugin runs
/// before the page. A CSRF refusal or exit writes PHP's body, a sign-in sets the cookies and redirects, anything else
/// renders the page as a GET (with the failure alert in <see cref="StorefrontLoginPost.AlertItemKey"/>). Crawlers are
/// left alone, as in PHP.
/// </summary>
public sealed class StorefrontLoginPostMiddleware
{
    private readonly RequestDelegate _next;

    public StorefrontLoginPostMiddleware(RequestDelegate next) => _next = next;

    public static bool IsLoginPagePath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        return value is "/users/login" or EcomAE.Platform.Routing.EcomAeRoutes.StorefrontLogin
            || (value.Length == 15 && value[0] == '/' && char.IsAsciiLetterLower(value[1]) && char.IsAsciiLetterLower(value[2]) && value.EndsWith("/users/login", StringComparison.Ordinal));
    }

    /// <summary><c>/shop/checkout/login_offer</c> or <c>/{lang}/shop/checkout/login_offer</c>.</summary>
    public static bool IsLoginOfferPath(PathString path)
    {
        const string Offer = "/shop/checkout/login_offer";
        var value = path.Value ?? string.Empty;
        if (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        return value == Offer
            || (value.Length == Offer.Length + 3 && value[0] == '/' && char.IsAsciiLetterLower(value[1]) && char.IsAsciiLetterLower(value[2]) && value.EndsWith(Offer, StringComparison.Ordinal));
    }

    /// <summary>
    /// The login offer posts PHP's relative <c>target</c> (<see cref="StorefrontCheckoutLoginOffer.Target"/>), which
    /// <see cref="StorefrontLoginPost.SafeTarget"/> turns into <c>/</c> as PHP does, sending a customer who signs in mid-checkout
    /// to the home page. Here that exact value continues to the delivery step instead (an intended deviation).
    /// </summary>
    public static string LoginOfferTarget(string lang, bool langInPath)
        => (langInPath ? "/" + lang : string.Empty) + "/" + StorefrontCheckoutLoginOffer.Target;

    public async Task InvokeAsync(HttpContext context, ITenantDbConnectionFactory connections)
    {
        var offer = IsLoginOfferPath(context.Request.Path);
        if (!HttpMethods.IsPost(context.Request.Method) || !(offer || IsLoginPagePath(context.Request.Path)) || !context.Request.HasFormContentType)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        context.Request.EnableBuffering();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        context.Request.Body.Position = 0;
        if (!form.TryGetValue("authentication", out var flag) || AuthEmailOtp.PhpEmpty(flag.ToString()))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (StorefrontLoginPost.IsBot(userAgent) || !connections.IsConfigured)
        {
            await RenderAsGetAsync(context, null).ConfigureAwait(false);
            return;
        }

        var config = context.RequestServices.GetService<ICpPlatformMailer>()?.ReadConfig() ?? PhpConfig(context);
        var path = context.Request.Path.Value ?? string.Empty;
        var langInPath = path.Length >= 15 && path[3] == '/'
            && (path.AsSpan(3).StartsWith("/users/login", StringComparison.Ordinal) || (offer && path.AsSpan(3).StartsWith("/shop/", StringComparison.Ordinal)));
        var lang = langInPath ? path.Substring(1, 2) : "en";
        var post = form.ToDictionary(p => p.Key, p => p.Value.Count > 0 ? p.Value[^1] ?? string.Empty : string.Empty, StringComparer.Ordinal);
        if (offer && post.TryGetValue("target", out var target) && target == StorefrontCheckoutLoginOffer.Target)
        {
            post["target"] = LoginOfferTarget(lang, langInPath);
        }

        var request = new StorefrontLoginPost.Request(
            post,
            context.Request.Query.ToDictionary(p => p.Key, p => p.Value.Count > 0 ? p.Value[^1] ?? string.Empty : string.Empty, StringComparer.Ordinal),
            context.Request.Cookies["session"],
            context.Request.Cookies["u_id"],
            LegacyLoginSecurity.ClientIp(context),
            userAgent,
            config.TryGetValue("secret_succession", out var secret) ? secret : string.Empty,
            context.Request.Scheme + "://" + context.Request.Host.Value + "/",
            context.Request.Scheme + "://" + context.Request.Host.Value + context.Request.PathBase + context.Request.Path + context.Request.QueryString,
            lang);

        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        StorefrontLoginPost.Outcome outcome;
        try
        {
            await using var connection = await connections.OpenForTenantAsync(tenant, context.RequestAborted).ConfigureAwait(false);
            var dispatcher = context.RequestServices.GetService<IStorefrontNotifyDispatcher>();
            outcome = await StorefrontLoginPost.HandleAsync(
                connection,
                request,
                dispatcher is null ? null : (db, userId, contact, ct) => NotifyAsync(db, dispatcher, config, request, userId, contact, ct),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                context.RequestAborted).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("Database connection failed", context.RequestAborted).ConfigureAwait(false);
            return;
        }

        foreach (var cookie in outcome.Cookies)
        {
            context.Response.Cookies.Append(cookie.Name, cookie.Value, new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                IsEssential = true,
                Expires = cookie.Expires > 0 ? DateTimeOffset.FromUnixTimeSeconds(cookie.Expires) : null,
            });
        }

        switch (outcome.Kind)
        {
            case StorefrontLoginPost.Kind.Redirect:
                context.Response.Redirect(outcome.Location!);
                return;
            case StorefrontLoginPost.Kind.Body:
                context.Response.ContentType = "text/html; charset=UTF-8";
                await context.Response.WriteAsync(outcome.Body, context.RequestAborted).ConfigureAwait(false);
                return;
            default:
                if (outcome.Cookies.Count > 0)
                {
                    var jar = context.Request.Cookies
                        .Where(c => outcome.Cookies.All(w => w.Name != c.Key))
                        .Select(c => c.Key + "=" + c.Value)
                        .Concat(outcome.Cookies.Select(w => w.Name + "=" + w.Value));
                    context.Request.Headers.Cookie = string.Join("; ", jar);
                }

                await RenderAsGetAsync(context, outcome.Alert).ConfigureAwait(false);
                return;
        }
    }

    private async Task RenderAsGetAsync(HttpContext context, string? alert)
    {
        if (alert is not null)
        {
            context.Items[StorefrontLoginPost.AlertItemKey] = alert;
        }

        context.Request.Method = HttpMethods.Get;
        context.Request.ContentType = null;
        context.Request.ContentLength = 0;
        context.Request.Body = Stream.Null;
        await _next(context).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_staff_send_notify('epc_customer_login', …)</c> with <c>epc_build_auth_event_html('Customer signed in', …)</c>.</summary>
    private static async Task NotifyAsync(
        System.Data.Common.DbConnection connection,
        IStorefrontNotifyDispatcher dispatcher,
        IReadOnlyDictionary<string, string> config,
        StorefrontLoginPost.Request request,
        long userId,
        string contact,
        CancellationToken cancellationToken)
    {
        var translator = new StorefrontPhpTranslator(connection, request.LangCode);
        var eventHtml = await StorefrontPhpAjax.AuthEventHtmlAsync(connection, translator, config, "Customer signed in", userId, contact, string.Empty, request.RemoteIp, request.UserAgent, cancellationToken).ConfigureAwait(false);
        var persons = await StorefrontPhpAjax.StaffPersonsWithAsync(connection, config, (int)userId, [], cancellationToken).ConfigureAwait(false);
        await dispatcher.SendAsync(
            connection,
            "epc_customer_login",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["user_id"] = userId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["login_contact"] = contact,
                ["event_html"] = eventHtml,
            },
            persons,
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, string> PhpConfig(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        return options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : CpPhpConfig.Read(options.Value);
    }
}
