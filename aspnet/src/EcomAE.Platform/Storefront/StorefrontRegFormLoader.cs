using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Reads what <see cref="StorefrontRegForm"/> prints from the tenant database, the registry and the request: the
/// <c>reg_fields</c> and <c>reg_variants</c> rows, translations, the available contact channels, the guest session's
/// <c>csrf_guard_key</c>, the storefront login context, the site trade name and the configured sign-in providers.
/// </summary>
public static class StorefrontRegFormLoader
{
    public sealed record Request(
        bool LoggedIn,
        string LangHref,
        string? SessionCookie,
        string? UserCookie,
        string? LastGoogleEmailCookie,
        IReadOnlyDictionary<string, string> Config,
        TenantContext? Tenant);

    public static async Task<string> RenderAsync(
        HttpContext http,
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        Request request,
        CancellationToken cancellationToken)
    {
        var translator = new StorefrontPhpTranslator(connection, LangCode(request.LangHref));
        var strings = new Dictionary<int, string>();
        foreach (var id in StorefrontRegForm.StringIds)
        {
            strings[id] = await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        }

        string T(int id) => strings.TryGetValue(id, out var text) ? text : string.Empty;
        if (request.LoggedIn)
        {
            return StorefrontRegForm.Render(new StorefrontRegForm.Input { LoggedIn = true }, T);
        }

        var fields = new List<StorefrontRegForm.AdditionalField>();
        foreach (var row in await RowsAsync(connection, "SELECT * FROM `reg_fields` WHERE `main_flag` = ? ORDER BY `order` ASC", cancellationToken, 0).ConfigureAwait(false))
        {
            fields.Add(new StorefrontRegForm.AdditionalField(
                Col(row, "main_flag"),
                Col(row, "name"),
                await translator.TextAsync(Col(row, "caption"), cancellationToken).ConfigureAwait(false),
                Col(row, "show_for"),
                Col(row, "required_for"),
                Col(row, "maxlen"),
                Col(row, "regexp"),
                Col(row, "widget_type"),
                Col(row, "widget_options"),
                await translator.TextAsync(Col(row, "example"), cancellationToken).ConfigureAwait(false)));
        }

        var variantRows = await RowsAsync(connection, "SELECT * FROM `reg_variants` ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var variants = new List<StorefrontRegForm.Variant>();
        foreach (var row in variantRows)
        {
            var caption = Col(row, "caption");
            variants.Add(new StorefrontRegForm.Variant(
                Col(row, "id"),
                variantRows.Count == 1 ? caption : await translator.TextAsync(caption, cancellationToken).ConfigureAwait(false)));
        }

        var csrf = string.Empty;
        if (!string.IsNullOrEmpty(request.SessionCookie) && request.UserCookie is not null)
        {
            csrf = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                request.SessionCookie,
                request.UserCookie).ConfigureAwait(false) ?? string.Empty;
        }

        var siteKey = request.Tenant?.SiteKey;
        return StorefrontRegForm.Render(
            new StorefrontRegForm.Input
            {
                AdditionalFields = fields,
                Variants = variants,
                Communications = await StorefrontPhpAjax.AvailableCommunicationsAsync(connection, request.Config, cancellationToken).ConfigureAwait(false),
                LangHref = request.LangHref,
                CsrfGuardKey = csrf,
                SocialBlock = await SocialBlockAsync(http, connection, connections, php, env, request, cancellationToken).ConfigureAwait(false),
                Enhanced = true,
                UsersAgreement = await StorefrontAuthPartials.UsersAgreementModuleAsync(translator, request.LangHref, cancellationToken).ConfigureAwait(false),
                OtpModal = new StorefrontOtpModal().Render(StorefrontRegForm.OtpModalOptions(StorefrontRegForm.OtpTenantKey(siteKey), string.Empty)),
                MinPasswordLen = request.Config.TryGetValue("min_password_len", out var min) ? min : string.Empty,
                DomainPath = request.Config.TryGetValue("domain_path", out var domain) ? domain : string.Empty,
            },
            T);
    }

    /// <summary>The <c>{lang}</c> of <c>/{lang}</c>, else <c>en</c>.</summary>
    public static string LangCode(string langHref)
        => langHref.Length == 3 && langHref[0] == '/' ? langHref[1..] : "en";

    /// <summary>
    /// The site trade name: the site settings contact <c>trade_name</c>, then <c>hub_name</c>, then the host without
    /// <c>www.</c>, <c>.com</c> dropped and dots as spaces, first letter upper-cased.
    /// </summary>
    public static async Task<string> SiteTradeNameAsync(DbConnection connection, string host, CancellationToken cancellationToken)
    {
        try
        {
            var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
            if (aliases.Count > 0)
            {
                var rows = await RowsAsync(
                    connection,
                    "SELECT * FROM `epc_portal_site_settings` WHERE `host` IN (" + string.Join(", ", aliases.Select(_ => "?")) + ") ORDER BY `id` ASC LIMIT 1",
                    cancellationToken,
                    aliases.Cast<object>().ToArray()).ConfigureAwait(false);
                if (rows.Count > 0)
                {
                    var contact = CpIndustrySettingsService.ParseContact(Col(rows[0], "contact_json"), null);
                    if (contact.TradeName.Length > 0)
                    {
                        return contact.TradeName;
                    }

                    var hub = Col(rows[0], "hub_name");
                    if (hub.Length > 0)
                    {
                        return hub;
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        return HostTradeName(host);
    }

    public static string HostTradeName(string host)
    {
        var trade = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        trade = trade.Replace(".com", string.Empty, StringComparison.Ordinal).Replace('.', ' ');
        return trade.Length > 0 && trade[0] is >= 'a' and <= 'z' ? char.ToUpperInvariant(trade[0]) + trade[1..] : trade;
    }

    private static async Task<string> SocialBlockAsync(
        HttpContext http,
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IWebHostEnvironment env,
        Request request,
        CancellationToken cancellationToken)
    {
        var (tenantKey, loginLabel) = await LoginContextAsync(http, connections, cancellationToken).ConfigureAwait(false);
        var enabled = await EnabledProvidersAsync(connections, php, env, cancellationToken).ConfigureAwait(false);
        var returnUrl = request.LangHref.TrimEnd('/') + "/";
        var buttons = new StorefrontOAuthButtons().Render(
            enabled,
            new StorefrontOAuthButtons.Options { Context = "storefront", ReturnUrl = returnUrl, RequireTerms = true, Divider = false },
            request.LastGoogleEmailCookie,
            StorefrontOAuthButtons.NewUidSuffix());
        var tradeName = await SiteTradeNameAsync(connection, http.Request.Host.Host.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
        return EpcRegistrationEnhancedRender.SocialBlock(true, request.LangHref, tenantKey, tradeName, loginLabel, buttons, null, null);
    }

    /// <summary>The storefront login context's <c>tenant_key</c> and <c>login_label</c> for the sign-in widgets.</summary>
    public static async Task<(string TenantKey, string LoginLabel)> LoginContextAsync(HttpContext http, ITenantDbConnectionFactory connections, CancellationToken cancellationToken)
    {
        try
        {
            await using var registry = await AuthEmailOtpEndpoints.OpenRegistryAsync(connections, cancellationToken).ConfigureAwait(false);
            var context = await AuthEmailOtpEndpoints.ResolveAsync(http, connections, registry, "storefront", string.Empty, cancellationToken).ConfigureAwait(false);
            if (context.Ok)
            {
                return (context.TenantKey, context.LoginLabel);
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
        }

        return (string.Empty, "Shop");
    }

    /// <summary>The sign-in providers with credentials, in <see cref="StorefrontOAuthButtons.ProviderIds"/> order.</summary>
    public static async Task<IReadOnlyList<string>> EnabledProvidersAsync(ITenantDbConnectionFactory connections, PhpReferenceOptions php, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var enabled = new List<string>();
        foreach (var provider in StorefrontOAuthButtons.ProviderIds)
        {
            try
            {
                if (OAuthStart.IsConfigured(provider, await OAuthStartEndpoint.LoadCredentialsAsync(provider, connections, php, env, cancellationToken).ConfigureAwait(false)))
                {
                    enabled.Add(provider);
                }
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
            }
        }

        return enabled;
    }

    private static async Task<List<Dictionary<string, string>>> RowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        var rows = new List<Dictionary<string, string>>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, args.Cast<object?>().ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                }

                rows.Add(row);
            }
        }
        catch (DbException)
        {
        }

        return rows;
    }

    private static string Col(IReadOnlyDictionary<string, string> row, string name) => row.TryGetValue(name, out var value) ? value : string.Empty;
}
