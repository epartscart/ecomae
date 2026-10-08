using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// E-mail code sign-in (PHP <c>epc_auth_otp_verify</c>): <c>/epc-auth-verify-code.php</c> and <c>/content/general_pages/epc_auth_api_verify_code.php</c>.
/// The code row is matched and deleted, the storefront customer or CP user is found (or created where allowed), a session
/// row is written, and the answer carries the redirect: the cookies are set here, or a signed handoff link goes to the
/// tenant host.
/// </summary>
/// <remarks>
/// CP sign-in never adds backend groups to an existing user, and a new e-mail only gets a CP account on a demo sandbox.
/// Live tenants and Super CP need an existing user with backend access.
/// </remarks>
public static class AuthOtpVerifyLogin
{
    public const string VerifyPath = "/epc-auth-verify-code.php";
    public const string VerifyContentPath = "/content/general_pages/epc_auth_api_verify_code.php";

    public const string MsgSignedIn = "Signed in";
    public const string MsgStorefrontRefused = "Could not sign in — account may be locked";
    public const string MsgCpRefused = "No CP access for this email on this workspace";
    public const string MsgNoSession = "Could not create session";
    public const string MsgHttps = "HTTPS is required for sign-in";
    public const string MsgInvalidCode = "Invalid or expired code";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        foreach (var path in new[] { VerifyPath, VerifyContentPath })
        {
            endpoints.MapMethods(path, ["GET", "POST"], VerifyAsync).DisableAntiforgery();
        }
    }

    /// <summary>PHP <c>epc_portal_demo_row_is_erp_only</c>.</summary>
    public static bool DemoRowIsErpOnly(string? introJson, string? industryCode)
    {
        if (!string.IsNullOrEmpty(introJson) && introJson != "0")
        {
            try
            {
                using var doc = JsonDocument.Parse(introJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("demo_erp_only", out var flag)
                    && AuthEmailOtp.JsonScalar(flag) is { } value
                    && !AuthEmailOtp.PhpEmpty(value))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
            }
        }

        return industryCode is "erp_only" or "erp_standalone";
    }

    /// <summary>PHP <c>epc_portal_demo_cp_post_login_url</c> on <c>epc_portal_demo_cp_tenant_base</c> (<c>epc_portal_demo_cp_path_prefix</c>) and <c>epc_portal_demo_erp_shell_url</c>.</summary>
    public static string DemoCpPostLoginUrl(string siteKey, bool erpOnly)
    {
        var key = AuthEmailOtp.SanitizeKey(siteKey);
        return "/cp/demo/" + key + "/" + (erpOnly ? "shop/finance/erp?epc_erp_shell=1" : "shop/orders");
    }

    public static string DemoCpReturnPath(string siteKey, bool erpOnly)
    {
        var url = DemoCpPostLoginUrl(siteKey, erpOnly);
        var query = url.IndexOf('?', StringComparison.Ordinal);
        return query >= 0 ? url[..query] : url;
    }

    /// <summary>PHP <c>epc_auth_normalize_host</c>: lower case, trimmed, without a leading <c>www.</c> (the port stays).</summary>
    public static string NormalizeHost(string? host)
    {
        var value = AuthEmailOtp.PhpLower(AuthEmailOtp.PhpTrim(host));
        return value.StartsWith("www.", StringComparison.Ordinal) ? value[4..] : value;
    }

    /// <summary>PHP <c>epc_auth_should_handoff</c>.</summary>
    public static bool ShouldHandoff(string? currentHost, string? returnHost)
    {
        var target = NormalizeHost(returnHost);
        return target.Length > 0 && target != NormalizeHost(currentHost);
    }

    /// <summary>PHP <c>epc_auth_storefront_post_login_redirect</c>.</summary>
    public static string StorefrontRedirect(string returnHost, string returnPath, string? returnUrl)
    {
        var url = AuthEmailOtp.PhpTrim(returnUrl);
        if (url.Length > 0 && url[0] == '/' && !url.Contains("//", StringComparison.Ordinal))
        {
            return returnHost.Length > 0 ? "https://" + returnHost + url : url;
        }

        var path = returnPath.Length > 0 && returnPath[0] != '/' ? "/" + returnPath : returnPath;
        return returnHost.Length > 0 ? "https://" + returnHost + path : path;
    }

    /// <summary>PHP <c>epc_auth_post_login_redirect</c>.</summary>
    public static string CpRedirect(string kind, string tenantKey, string returnHost, string returnPath, bool erpOnly)
    {
        switch (kind)
        {
            case "demo" when tenantKey.Length > 0:
                return DemoCpPostLoginUrl(tenantKey, erpOnly);
            case "tenant" or "tenant_local":
                return returnHost.Length > 0 ? "https://" + returnHost + "/cp/control" : "/cp/control";
            case "super":
                return "https://" + (returnHost.Length > 0 ? returnHost : "www.ecomae.com") + "/cp/control";
        }

        var path = returnPath.Length > 0 && returnPath[0] != '/' ? "/" + returnPath : returnPath;
        return returnHost.Length > 0 ? "https://" + returnHost + path : path;
    }

    /// <summary>PHP <c>epc_auth_handoff_build</c> for a context with a return host.</summary>
    public static string HandoffUrl(int userId, string sessionToken, string returnHost, string returnPath, string tenantKey, string mode, long expiresUnix, string signingSecret)
    {
        var json = "{\"uid\":" + userId.ToString(CultureInfo.InvariantCulture)
            + ",\"sess\":" + OAuthStart.PhpJsonString(sessionToken)
            + ",\"host\":" + OAuthStart.PhpJsonString(returnHost)
            + ",\"path\":" + OAuthStart.PhpJsonString(returnPath)
            + ",\"exp\":" + expiresUnix.ToString(CultureInfo.InvariantCulture)
            + ",\"tk\":" + OAuthStart.PhpJsonString(tenantKey)
            + ",\"mode\":" + OAuthStart.PhpJsonString(mode)
            + "}";
        var payload = OAuthStart.Base64UrlEncode(Encoding.UTF8.GetBytes(json));
        var sig = OAuthStart.HmacHex(payload, signingSecret);
        return "https://" + returnHost + "/epc-auth-handoff.php?p=" + Uri.EscapeDataString(payload) + "&s=" + Uri.EscapeDataString(sig);
    }

    public static string Ok(string redirect)
        => "{\"ok\":true,\"message\":" + OAuthStart.PhpJsonString(MsgSignedIn) + ",\"redirect\":" + OAuthStart.PhpJsonString(redirect) + "}";

    private static async Task VerifyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IOptions<EcomAeOptions> options,
        CancellationToken cancellationToken)
    {
        int status;
        string body;
        try
        {
            (status, body) = await VerifyCoreAsync(context, connections, options.Value.SecretSuccession ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            (status, body) = (503, AuthEmailOtp.Fail("Platform database unavailable"));
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(body), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PHP <c>epc_auth_otp_verify</c> through <c>epc_auth_finish_login</c>: the session row of
    /// <c>epc_auth_create_storefront_session_record</c> or <c>epc_auth_create_cp_session_record</c>, then the cookies of
    /// <c>epc_auth_establish_storefront_session</c> / <c>epc_auth_set_storefront_session_cookies</c> or
    /// <c>epc_auth_establish_cp_session</c> / <c>epc_auth_set_cp_session_cookies</c>, or the handoff link.
    /// </summary>
    private static async Task<(int Status, string Body)> VerifyCoreAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        string secret,
        CancellationToken cancellationToken)
    {
        var input = await AuthEmailOtpEndpoints.ReadInputAsync(http.Request, cancellationToken).ConfigureAwait(false);
        var mode = AuthEmailOtp.NormalizeMode(Field(input, "context") ?? "cp");
        var storefront = mode == "storefront";
        await using var registry = await AuthEmailOtpEndpoints.OpenRegistryAsync(connections, cancellationToken).ConfigureAwait(false);
        var resolved = await AuthEmailOtpEndpoints.ResolveAsync(
            http,
            connections,
            registry,
            mode,
            Field(input, "tenant_key") ?? Field(input, "site_key") ?? string.Empty,
            cancellationToken).ConfigureAwait(false);
        if (!resolved.Ok)
        {
            return (400, AuthEmailOtp.Fail(resolved.Message));
        }

        var rawReturnUrl = Field(input, "return_url");
        var returnUrl = AuthEmailOtp.PhpEmpty(rawReturnUrl) ? string.Empty : rawReturnUrl!;
        var email = AuthEmailOtp.NormalizeEmail(Field(input, "email"));
        var code = AuthEmailOtp.DigitsOnly(Field(input, "code"));
        if (email.Length == 0 || code.Length != 6)
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgCodeRequired));
        }

        if (!AuthEmailOtp.RequireHttps(
                http.Request.IsHttps,
                http.Request.Headers["X-Forwarded-Proto"].ToString(),
                http.Connection.LocalPort,
                http.Request.Headers.Host.ToString()))
        {
            return (400, AuthEmailOtp.Fail(MsgHttps));
        }

        await Exec(registry, AuthEmailOtp.OtpTableDdl, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var otpId = await Scalar(
            registry,
            "SELECT `id` FROM `epc_auth_otp_requests` WHERE `email` = @p0 AND `code_hash` = @p1 AND `tenant_key` = @p2 AND `expires_at` >= @p3 ORDER BY `id` DESC LIMIT 1",
            cancellationToken,
            email,
            AuthEmailOtp.CodeHash(code, CpSocialCrypto.DeployToken()),
            resolved.TenantKey,
            now).ConfigureAwait(false);
        if (otpId <= 0)
        {
            return (400, AuthEmailOtp.Fail(MsgInvalidCode));
        }

        await Exec(registry, "DELETE FROM `epc_auth_otp_requests` WHERE `id` = @p0", cancellationToken, otpId).ConfigureAwait(false);

        await using var tenant = await OpenTenantAsync(http, connections, resolved, cancellationToken).ConfigureAwait(false);
        var account = await OAuthAccountProvision.FindOrProvisionAsync(
            tenant,
            email,
            string.Empty,
            storefront,
            storefront || resolved.Kind == "demo",
            secret,
            cancellationToken).ConfigureAwait(false);
        if (account.UserId <= 0)
        {
            return (400, AuthEmailOtp.Fail(account.Message ?? (storefront ? MsgStorefrontRefused : MsgCpRefused)));
        }

        var userId = account.UserId;
        var contact = await StringScalar(tenant, "SELECT `email` FROM `users` WHERE `user_id` = @p0 LIMIT 1", cancellationToken, userId).ConfigureAwait(false);
        if (contact is null)
        {
            return (400, AuthEmailOtp.Fail(MsgNoSession));
        }

        now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var session = storefront
            ? LegacySessionTokenFactory.CustomerSessionToken(contact, userId, now, secret)
            : LegacySessionTokenFactory.AdminSessionToken(contact, now, secret);
        var csrf = LegacySessionTokenFactory.CsrfGuardKey(
            secret,
            session,
            LegacySessionTokenFactory.ResolveClientIp(http.Request),
            http.Request.Headers.UserAgent.ToString());
        if (storefront)
        {
            await Exec(
                tenant,
                "INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`) VALUES (@p0, @p1, @p2, '', @p2, @p3)",
                cancellationToken,
                session,
                userId,
                now,
                csrf).ConfigureAwait(false);
            await TryExec(tenant, "UPDATE `users` SET `time_last_visit` = @p0 WHERE `user_id` = @p1", cancellationToken, now, userId).ConfigureAwait(false);
        }
        else
        {
            await Exec(
                tenant,
                "INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `type`, `contact_type`, `csrf_guard_key`) VALUES (@p0, @p1, @p2, '', 1, 'email', @p3)",
                cancellationToken,
                session,
                userId,
                now,
                csrf).ConfigureAwait(false);
        }

        if (ShouldHandoff(http.Request.Headers.Host.ToString(), resolved.ReturnHost))
        {
            return (200, Ok(HandoffUrl(userId, session, resolved.ReturnHost, resolved.ReturnPath, resolved.TenantKey, mode, now + 120, secret)));
        }

        if (storefront)
        {
            var guest = http.Request.Cookies["session"] ?? string.Empty;
            LegacyLoginCookieWriter.Apply(http.Response, new LegacyLoginSuccess(userId, contact, session, csrf, false, "/"), rememberMe: false);
            if (guest.Length > 0)
            {
                await MergeGuestCartAsync(tenant, userId, guest, cancellationToken).ConfigureAwait(false);
            }

            return (200, Ok(StorefrontRedirect(resolved.ReturnHost, resolved.ReturnPath, returnUrl)));
        }

        LegacyLoginCookieWriter.Apply(http.Response, new LegacyLoginSuccess(userId, contact, session, csrf, true, "/"), rememberMe: false);
        http.Response.Cookies.Append("epc_platform_erp", "deleted", new CookieOptions { Path = "/", Expires = DateTimeOffset.FromUnixTimeSeconds(1) });
        return (200, Ok(CpRedirect(resolved.Kind, resolved.TenantKey, resolved.ReturnHost, resolved.ReturnPath, resolved.ErpOnly)));
    }

    /// <summary>PHP <c>epc_auth_merge_guest_cart</c>: the guest session's carts move to the signed-in customer.</summary>
    public static async Task MergeGuestCartAsync(DbConnection connection, int userId, string guestSession, CancellationToken cancellationToken)
    {
        if (userId <= 0 || guestSession.Length == 0)
        {
            return;
        }

        await TryExec(
            connection,
            "UPDATE `shop_carts` SET `user_id` = @p0, `session_id` = 0 WHERE `user_id` = 0 AND `session_id` = (SELECT `id` FROM `sessions` WHERE `session` = @p1 LIMIT 1)",
            cancellationToken,
            userId,
            guestSession.Replace(" ", string.Empty, StringComparison.Ordinal)).ConfigureAwait(false);
    }

    private static async Task<DbConnection> OpenTenantAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        AuthEmailOtpEndpoints.OtpContext resolved,
        CancellationToken cancellationToken)
    {
        DbConnection connection;
        if (resolved.Db is { } db)
        {
            connection = await connections.OpenAsync(db.Database, db.User, db.Password, cancellationToken).ConfigureAwait(false);
        }
        else if (resolved.Kind == "super")
        {
            connection = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            connection = await connections.OpenForTenantAsync(
                http.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext,
                cancellationToken).ConfigureAwait(false);
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    private static string? Field(IReadOnlyDictionary<string, string?> input, string name)
        => input.TryGetValue(name, out var value) ? value : null;

    private static DbCommand Command(DbConnection connection, string sql, object[] args)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@p" + i.ToString(CultureInfo.InvariantCulture);
            parameter.Value = args[i];
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static async Task Exec(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task TryExec(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        try
        {
            await Exec(connection, sql, cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task<long> Scalar(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> StringScalar(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
