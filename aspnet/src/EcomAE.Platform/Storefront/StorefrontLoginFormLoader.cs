using System.Data.Common;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Reads what <see cref="StorefrontLoginForm"/> prints from the tenant database, the registry and the request:
/// translations, whether SMS is available, the visitor session's <c>csrf_guard_key</c>, the current front template,
/// the storefront login context and the configured sign-in providers.
/// </summary>
public static class StorefrontLoginFormLoader
{
    public sealed record Request(
        long UserId,
        string LangHref,
        string? SessionCookie,
        string? UserCookie,
        string? LastGoogleEmailCookie,
        IReadOnlyDictionary<string, string> Config);

    public static async Task<string> RenderAsync(
        HttpContext http,
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        Request request,
        CancellationToken cancellationToken)
    {
        var (input, t) = await LoadAsync(http, connection, connections, php, env, request, [], cancellationToken).ConfigureAwait(false);
        return StorefrontLoginForm.Render(input, t);
    }

    /// <summary>The <c>/shop/checkout/login_offer</c> page (<see cref="StorefrontCheckoutLoginOffer"/>).</summary>
    public static async Task<string> RenderCheckoutLoginOfferAsync(
        HttpContext http,
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        Request request,
        CancellationToken cancellationToken)
    {
        var (input, t) = await LoadAsync(http, connection, connections, php, env, request, [StorefrontCheckoutLoginOffer.GuestButtonStringId], cancellationToken)
            .ConfigureAwait(false);
        return StorefrontCheckoutLoginOffer.Render(input, t, request.Config);
    }

    private static async Task<(StorefrontLoginForm.Input Input, Func<int, string> T)> LoadAsync(
        HttpContext http,
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        Request request,
        IReadOnlyList<int> extraStrings,
        CancellationToken cancellationToken)
    {
        var translator = new StorefrontPhpTranslator(connection, StorefrontRegFormLoader.LangCode(request.LangHref.TrimEnd('/')));
        var strings = new Dictionary<int, string>();
        foreach (var id in StorefrontLoginForm.StringIds.Concat(extraStrings))
        {
            strings[id] = await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        }

        string T(int id) => strings.TryGetValue(id, out var text) ? text : string.Empty;
        if (request.UserId > 0)
        {
            return (new StorefrontLoginForm.Input { UserId = request.UserId, LangHref = request.LangHref }, T);
        }

        var csrf = string.Empty;
        if (request.SessionCookie is not null && request.UserCookie is not null)
        {
            csrf = await TryStringAsync(
                connection,
                "SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1",
                cancellationToken,
                request.SessionCookie,
                request.UserCookie).ConfigureAwait(false);
        }

        var templateId = await TryStringAsync(connection, "SELECT `id` FROM `templates` WHERE `current` = ? AND `is_frontend` = ? LIMIT 1", cancellationToken, 1, 1).ConfigureAwait(false);
        var (tenantKey, loginLabel) = await StorefrontRegFormLoader.LoginContextAsync(http, connections, cancellationToken).ConfigureAwait(false);
        var enabled = await StorefrontRegFormLoader.EnabledProvidersAsync(connections, php, env, cancellationToken).ConfigureAwait(false);
        var buttons = new StorefrontOAuthButtons().Render(
            enabled,
            new StorefrontOAuthButtons.Options { Context = "storefront", ReturnUrl = request.LangHref.TrimEnd('/') + "/", Divider = false },
            request.LastGoogleEmailCookie,
            StorefrontOAuthButtons.NewUidSuffix());

        return (new StorefrontLoginForm.Input
        {
            LangHref = request.LangHref,
            CsrfGuardKey = csrf,
            Sms = (await StorefrontPhpAjax.AvailableCommunicationsAsync(connection, request.Config, cancellationToken).ConfigureAwait(false)).Sms,
            SocialButtons = buttons,
            TemplateId = long.TryParse(templateId, out var template) ? template : 0,
            TenantKey = tenantKey,
            LoginLabel = loginLabel,
        }, T);
    }

    private static async Task<string> TryStringAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            return await ErpDb.StringAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }
}
