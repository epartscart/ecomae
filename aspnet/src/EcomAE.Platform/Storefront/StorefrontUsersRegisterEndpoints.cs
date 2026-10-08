using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Cp.PriceImport;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// <c>/users/register</c> and <c>/{lang}/users/register</c>: a GET goes to the registration form, a POST runs
/// <see cref="StorefrontPhpAjax.UsersRegisterAsync"/>. A refusal redirects to <c>{lang}/?error_message=</c>; the page text
/// after a registration is carried to <see cref="ResultPath"/> in a short-lived protected token. The form's captcha image,
/// check and refresh button (<see cref="StorefrontCaptcha"/>) are served here too.
/// </summary>
public static class StorefrontUsersRegisterEndpoints
{
    public const string ResultPath = "/users/registered";

    private const string Purpose = "EcomAE.Storefront.UsersRegister.Result";

    private static readonly TimeSpan ResultLifetime = TimeSpan.FromMinutes(10);

    private const long MaxUploadBytes = 20L * 1024 * 1024;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods(StorefrontPhpAjax.UsersRegisterPath, ["GET", "POST"], (HttpContext context, ITenantDbConnectionFactory connections, CancellationToken ct)
                => RegisterAsync(context, connections, string.Empty, ct))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods("/{lang:regex(^[a-z]{{2}}$)}" + StorefrontPhpAjax.UsersRegisterPath, ["GET", "POST"], (HttpContext context, ITenantDbConnectionFactory connections, string lang, CancellationToken ct)
                => RegisterAsync(context, connections, "/" + lang, ct))
            .DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontCaptcha.ImagePath, ["GET", "POST"], CaptchaImage).DisableAntiforgery().AllowAnonymous();
        endpoints.MapMethods(StorefrontCaptcha.CheckPath, ["GET", "POST"], CaptchaCheckAsync).DisableAntiforgery().AllowAnonymous();
        endpoints.MapGet(StorefrontCaptcha.RefreshPath, () => Results.Bytes(StorefrontCaptcha.RefreshImage(), "image/png")).AllowAnonymous();
    }

    private static IResult CaptchaImage(HttpContext context)
    {
        var code = StorefrontCaptcha.GenerateCode(Random.Shared);
        context.Response.Cookies.Append(StorefrontCaptcha.CookieName, StorefrontCaptcha.CookieValue(code), new CookieOptions
        {
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddSeconds(StorefrontCaptcha.CookieSeconds),
            MaxAge = TimeSpan.FromSeconds(StorefrontCaptcha.CookieSeconds),
        });
        foreach (var (name, value) in StorefrontCaptcha.ImageHeaders)
        {
            context.Response.Headers.Append(name, value);
        }

        return Results.Bytes(StorefrontCaptcha.RenderPng(code, Random.Shared), "image/png");
    }

    private static async Task<IResult> CaptchaCheckAsync(HttpContext context, CancellationToken cancellationToken)
    {
        string? posted = context.Request.Query.TryGetValue("captcha_check", out var query) ? query.ToString() : null;
        if (HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            if (form.TryGetValue("captcha_check", out var value))
            {
                posted = value.ToString();
            }
        }

        var correct = StorefrontCaptcha.Check(posted, context.Request.Cookies[StorefrontCaptcha.CookieName]);
        return Results.Bytes(System.Text.Encoding.ASCII.GetBytes(correct is null ? string.Empty : correct.Value ? "true" : "false"), "text/html; charset=UTF-8");
    }

    /// <summary>The page text a <see cref="ResultPath"/> token carries, or <c>null</c> when it is missing, forged or expired.</summary>
    public static string? ReadResult(IDataProtectionProvider? provider, string? token)
    {
        if (provider is null || string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return provider.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }

    private static async Task<IResult> RegisterAsync(HttpContext context, ITenantDbConnectionFactory connections, string langHref, CancellationToken cancellationToken)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return Results.Redirect((langHref.Length > 0 ? langHref : "/en") + "/users/registration");
        }

        if (!connections.IsConfigured)
        {
            return Results.Text("Database connection failed", "text/plain; charset=utf-8", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var post = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = new Dictionary<string, EpcRegistrationUpload>(StringComparer.Ordinal);
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pair in form)
            {
                post[pair.Key] = pair.Value.Count > 0 ? pair.Value[^1] ?? string.Empty : string.Empty;
            }

            foreach (var file in form.Files)
            {
                if (string.IsNullOrEmpty(file.FileName) || file.Length > MaxUploadBytes)
                {
                    continue;
                }

                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                files[file.Name] = new EpcRegistrationUpload(file.FileName, buffer.ToArray());
            }
        }

        var cookies = context.Request.Cookies.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal);
        var config = context.RequestServices.GetService<ICpPlatformMailer>()?.ReadConfig() ?? PhpConfig(context);
        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        StorefrontPhpAjax.UsersRegisterOutcome outcome;
        try
        {
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            outcome = await StorefrontPhpAjax.UsersRegisterAsync(
                connection,
                token => connections.OpenForTenantAsync(tenant, token),
                context.RequestServices.GetService<IStorefrontNotifyDispatcher>(),
                config,
                new StorefrontPhpAjax.UsersRegisterRequest(
                    post,
                    files,
                    cookies,
                    context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    context.Request.Headers.UserAgent.ToString(),
                    langHref,
                    WebRoot(context)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            return Results.Text("Database connection failed", "text/plain; charset=utf-8", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (outcome.Location is not null)
        {
            return Results.Redirect(outcome.Location);
        }

        var provider = context.RequestServices.GetService<IDataProtectionProvider>();
        if (provider is null)
        {
            return Results.Redirect(langHref + "/");
        }

        var token = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector().Protect(outcome.Html, ResultLifetime);
        return Results.Redirect(langHref + ResultPath + "?r=" + Uri.EscapeDataString(token));
    }

    private static string WebRoot(HttpContext context)
    {
        var root = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>()?.Value.PhpDocRoot;
        return string.IsNullOrWhiteSpace(root)
            ? context.RequestServices.GetService<IWebHostEnvironment>()?.ContentRootPath ?? AppContext.BaseDirectory
            : root;
    }

    private static IReadOnlyDictionary<string, string> PhpConfig(HttpContext context)
    {
        var options = context.RequestServices.GetService<IOptions<PhpReferenceOptions>>();
        return options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : CpPhpConfig.Read(options.Value);
    }
}
