using System.Data;
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
/// Sign-in / registration e-mail code: <c>/epc-auth-send-code.php</c> and <c>/epc-auth-otp-verify-only.php</c>
/// (plus their <c>content/general_pages</c> aliases). Codes live hashed in the platform table
/// <c>epc_auth_otp_requests</c>. Verify-only answers <c>verified_email</c> for the registration form and opens no session.
/// </summary>
public static class AuthEmailOtpEndpoints
{
    public const string SendPath = "/epc-auth-send-code.php";
    public const string SendContentPath = "/content/general_pages/epc_auth_api_send_code.php";
    public const string VerifyOnlyPath = "/epc-auth-otp-verify-only.php";
    public const string VerifyOnlyContentPath = "/content/general_pages/epc_auth_api_verify_only.php";

    private const string SuperCpHost = "cp.ecomae.com";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        foreach (var path in new[] { SendPath, SendContentPath })
        {
            endpoints.MapMethods(path, ["GET", "POST"], SendAsync).DisableAntiforgery();
        }

        foreach (var path in new[] { VerifyOnlyPath, VerifyOnlyContentPath })
        {
            endpoints.MapMethods(path, ["GET", "POST"], VerifyOnlyAsync).DisableAntiforgery();
        }
    }

    internal sealed record OtpContext(bool Ok, string Message, string Kind, string TenantKey, string LoginLabel)
    {
        public static OtpContext Failed(string message) => new(false, message, string.Empty, string.Empty, string.Empty);
    }

    private static async Task SendAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IOptions<PhpReferenceOptions> php,
        CancellationToken cancellationToken)
    {
        string body;
        int status;
        try
        {
            var input = await ReadInputAsync(context.Request, cancellationToken).ConfigureAwait(false);
            var mode = AuthEmailOtp.NormalizeMode(Field(input, "context") ?? "cp");
            await using var registry = await OpenRegistryAsync(connections, cancellationToken).ConfigureAwait(false);
            var resolved = await ResolveAsync(
                context,
                connections,
                registry,
                mode,
                Field(input, "tenant_key") ?? Field(input, "site_key") ?? string.Empty,
                cancellationToken).ConfigureAwait(false);
            if (!resolved.Ok)
            {
                (status, body) = (400, AuthEmailOtp.Fail(resolved.Message));
            }
            else
            {
                var returnUrl = Field(input, "return_url");
                (status, body) = await SendCodeAsync(
                    context,
                    connections,
                    registry,
                    php.Value,
                    Field(input, "email") ?? string.Empty,
                    resolved,
                    mode,
                    AuthEmailOtp.PhpEmpty(returnUrl) ? null : returnUrl,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (status, body) = (500, AuthEmailOtp.Fail(AuthEmailOtp.MsgSendCrashed));
        }

        await WriteAsync(context, status, body).ConfigureAwait(false);
    }

    private static async Task VerifyOnlyAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        string body;
        int status;
        try
        {
            var input = await ReadInputAsync(context.Request, cancellationToken).ConfigureAwait(false);
            var email = AuthEmailOtp.NormalizeEmail(Field(input, "email"));
            var code = AuthEmailOtp.DigitsOnly(Field(input, "code"));
            var tenantKey = AuthEmailOtp.SanitizeKey(Field(input, "tenant_key") ?? Field(input, "site_key") ?? string.Empty);
            await using var registry = await OpenRegistryAsync(connections, cancellationToken).ConfigureAwait(false);
            (status, body) = await VerifyEmailOnlyAsync(context, registry, email, code, tenantKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            (status, body) = (500, AuthEmailOtp.Fail(AuthEmailOtp.MsgVerifyCrashed));
        }

        await WriteAsync(context, status, body).ConfigureAwait(false);
    }

    internal static async Task<(int Status, string Body)> SendCodeAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        DbConnection registry,
        PhpReferenceOptions php,
        string rawEmail,
        OtpContext resolved,
        string mode,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var email = AuthEmailOtp.NormalizeEmail(rawEmail);
        if (!AuthEmailOtp.IsValidEmail(email))
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgValidEmail));
        }

        if (!IsHttps(http))
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgHttpsSignIn));
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ip = RemoteIp(http);
        await ExecAsync(registry, AuthEmailOtp.OtpTableDdl, cancellationToken).ConfigureAwait(false);
        var since = now - AuthEmailOtp.RateWindowSeconds;
        var perEmail = await CountAsync(
            registry,
            "SELECT COUNT(*) FROM `epc_auth_otp_requests` WHERE `email` = @p0 AND `created_at` > @p1",
            cancellationToken,
            email,
            since).ConfigureAwait(false);
        var limited = perEmail >= AuthEmailOtp.MaxPerEmail;
        if (!limited && ip.Length > 0)
        {
            limited = await CountAsync(
                registry,
                "SELECT COUNT(*) FROM `epc_auth_otp_requests` WHERE `ip_address` = @p0 AND `created_at` > @p1",
                cancellationToken,
                ip,
                since).ConfigureAwait(false) >= AuthEmailOtp.MaxPerIp;
        }

        if (limited)
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgRateLimited));
        }

        await ExecAsync(registry, "DELETE FROM `epc_auth_otp_requests` WHERE `expires_at` < @p0", cancellationToken, now - 86400).ConfigureAwait(false);
        var code = AuthEmailOtp.NewCode();
        var hash = AuthEmailOtp.CodeHash(code, CpSocialCrypto.DeployToken());
        var stored = new List<KeyValuePair<string, string>>
        {
            new("ok", "true"),
            new("kind", OAuthStart.PhpJsonString(resolved.Kind)),
            new("tenant_key", OAuthStart.PhpJsonString(resolved.TenantKey)),
            new("login_label", OAuthStart.PhpJsonString(resolved.LoginLabel)),
            new("allow_provision", resolved.Kind is "super" or "platform_erp" ? "false" : "true"),
            new("auth_mode", OAuthStart.PhpJsonString(mode)),
        };
        if (returnUrl is not null)
        {
            stored.Add(new("return_url", OAuthStart.PhpJsonString(returnUrl)));
        }

        await ExecAsync(
            registry,
            "INSERT INTO `epc_auth_otp_requests` (`email`, `code_hash`, `tenant_key`, `context_json`, `expires_at`, `ip_address`, `created_at`) "
            + "VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
            cancellationToken,
            email,
            hash,
            resolved.TenantKey,
            JsonObject(stored),
            now + AuthEmailOtp.TtlSeconds,
            ip,
            now).ConfigureAwait(false);
        var otpId = await CountAsync(registry, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);

        var smtpFile = ReadPhpArrayFile(php, "config.epc-smtp.php");
        var send = await SendMailAsync(http, connections, php, smtpFile, email, code, resolved, mode, cancellationToken).ConfigureAwait(false);
        if (send is null)
        {
            return (200, AuthEmailOtp.SendOk());
        }

        if (AuthEmailOtp.DemoFallbackAllowed(resolved.TenantKey, smtpFile))
        {
            stored.Add(new("_operator_otp", OAuthStart.PhpJsonString(code)));
            stored.Add(new("_operator_otp_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
            await ExecAsync(
                registry,
                "UPDATE `epc_auth_otp_requests` SET `context_json` = @p0 WHERE `id` = @p1",
                cancellationToken,
                JsonObject(stored),
                otpId).ConfigureAwait(false);
            return (200, AuthEmailOtp.DemoOk(send.Value.Message));
        }

        return (400, AuthEmailOtp.SendFail(send.Value.Message, send.Value.Detail));
    }

    internal static async Task<(int Status, string Body)> VerifyEmailOnlyAsync(
        HttpContext http,
        DbConnection registry,
        string email,
        string code,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        if (email.Length == 0 || code.Length != 6)
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgCodeRequired));
        }

        if (!AuthEmailOtp.IsValidEmail(email))
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgInvalidEmail));
        }

        if (!IsHttps(http))
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgHttps));
        }

        await ExecAsync(registry, AuthEmailOtp.OtpTableDdl, cancellationToken).ConfigureAwait(false);
        var hash = AuthEmailOtp.CodeHash(code, CpSocialCrypto.DeployToken());
        var id = await CountAsync(
            registry,
            "SELECT `id` FROM `epc_auth_otp_requests` WHERE `email` = @p0 AND `code_hash` = @p1 AND `tenant_key` = @p2 AND `expires_at` >= @p3 "
            + "ORDER BY `id` DESC LIMIT 1",
            cancellationToken,
            email,
            hash,
            tenantKey,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        if (id <= 0)
        {
            return (400, AuthEmailOtp.Fail(AuthEmailOtp.MsgInvalidCode));
        }

        await ExecAsync(registry, "DELETE FROM `epc_auth_otp_requests` WHERE `id` = @p0", cancellationToken, id).ConfigureAwait(false);
        return (200, AuthEmailOtp.VerifyOk(email));
    }

    /// <summary>PHP <c>epc_auth_resolve_storefront_context()</c> / <c>epc_auth_resolve_context()</c> for these API paths.</summary>
    internal static async Task<OtpContext> ResolveAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        DbConnection registry,
        string mode,
        string rawKey,
        CancellationToken cancellationToken)
    {
        var tenantKey = AuthEmailOtp.SanitizeKey(AuthEmailOtp.PhpTrim(rawKey));
        var host = PortalHost(http);
        if (tenantKey.Length > 0)
        {
            var byKey = await TenantRowAsync(registry, "`site_key` = @p0", cancellationToken, tenantKey).ConfigureAwait(false);
            if (byKey is not null)
            {
                var fromKey = await FromRowAsync(connections, byKey, mode, cancellationToken).ConfigureAwait(false);
                if (fromKey.Ok)
                {
                    return fromKey;
                }
            }
        }

        if (mode != "storefront" && host == SuperCpHost)
        {
            return new OtpContext(true, string.Empty, "super", string.Empty, "ECOM AE Super CP");
        }

        if (host.Length > 0)
        {
            var bare = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
            var byHost = await TenantRowAsync(registry, "`hostname` = @p0 OR `hostname` = @p1", cancellationToken, host, bare).ConfigureAwait(false);
            if (byHost is not null)
            {
                return await FromRowAsync(connections, byHost, mode, cancellationToken).ConfigureAwait(false);
            }
        }

        var key = tenantKey;
        if (key.Length == 0 && http.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext tenant)
        {
            key = AuthEmailOtp.SanitizeKey(tenant.SiteKey);
        }

        return mode == "storefront"
            ? new OtpContext(true, string.Empty, "storefront_local", key, "Shop")
            : new OtpContext(true, string.Empty, "tenant_local", key, "Control Panel");
    }

    private static async Task<OtpContext> FromRowAsync(
        ITenantDbConnectionFactory connections,
        IReadOnlyDictionary<string, string?> row,
        string mode,
        CancellationToken cancellationToken)
    {
        var db = AuthEmailOtp.PhpTrim(row.GetValueOrDefault("db_name"));
        var user = AuthEmailOtp.PhpTrim(row.GetValueOrDefault("db_user"));
        var pass = row.GetValueOrDefault("db_password") ?? string.Empty;
        if (db.Length == 0 || user.Length == 0 || pass.Length == 0)
        {
            return OtpContext.Failed("Tenant database unavailable");
        }

        try
        {
            await using var tenantConnection = await connections.OpenAsync(db, user, pass, cancellationToken).ConfigureAwait(false);
            if (tenantConnection.State != ConnectionState.Open)
            {
                await tenantConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return OtpContext.Failed("Tenant database unavailable");
        }

        var key = AuthEmailOtp.SanitizeKey(row.GetValueOrDefault("site_key"));
        var trade = AuthEmailOtp.PhpTrim(row.GetValueOrDefault("trade_name") ?? key);
        var demo = !AuthEmailOtp.PhpEmpty(row.GetValueOrDefault("is_demo"));
        return mode == "storefront"
            ? new OtpContext(true, string.Empty, demo ? "storefront_demo" : "storefront_tenant", key, trade.Length > 0 ? trade : "Shop")
            : new OtpContext(true, string.Empty, demo ? "demo" : "tenant", key, trade.Length > 0 ? trade + " CP" : "Control Panel");
    }

    private static async Task<(string Message, string Detail)?> SendMailAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        PhpReferenceOptions php,
        IReadOnlyDictionary<string, string?> smtpFile,
        string email,
        string code,
        OtpContext resolved,
        string mode,
        CancellationToken cancellationToken)
    {
        var configPhp = ReadConfigPhp(php);
        var configLocal = ReadPhpArrayFile(php, "config.local.php");
        var overlay = await TenantSmtpOverlayAsync(http, connections, cancellationToken).ConfigureAwait(false);
        var cfg = AuthEmailOtp.EffectiveConfig(configPhp, configLocal, smtpFile.Count > 0 ? smtpFile : null, overlay);
        var issues = AuthEmailOtp.Diagnose(cfg);
        if (issues.Count > 0)
        {
            return (string.Join(" ", issues), "precheck");
        }

        var label = AuthEmailOtp.MailLabel(mode, resolved.LoginLabel);
        var mailer = http.RequestServices.GetService<IAuthOtpMailer>() ?? new AuthOtpMailer();
        var error = await mailer.SendAsync(
            cfg,
            email,
            AuthEmailOtp.MailSubject(label, code),
            AuthEmailOtp.MailHtml(label, code),
            AuthEmailOtp.MailText(code),
            cancellationToken).ConfigureAwait(false);
        if (error is null)
        {
            return null;
        }

        var classified = AuthEmailOtp.ClassifyError(error);
        return (classified.Message, AuthEmailOtp.PhpTrim(classified.Detail));
    }

    /// <summary>
    /// PHP <c>epc_auth_smtp_tenant_overlay()</c>: the request site's <c>integrations.smtp</c> when <c>use_tenant_smtp</c> is on.
    /// Never on the Super CP host; client storefront hosts answer the default settings (no overlay), as PHP does.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>?> TenantSmtpOverlayAsync(
        HttpContext http,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var host = PortalHost(http);
        if (host == SuperCpHost || !connections.IsConfigured)
        {
            return null;
        }

        var tenant = http.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (tenant is { Mode: TenantMode.LiveTenant or TenantMode.ErpOnlyTenant or TenantMode.DemoTenant })
        {
            return null;
        }

        var withWww = host.Length > 0 && !host.StartsWith("www.", StringComparison.Ordinal) ? "www." + host : host;
        var bare = withWww.StartsWith("www.", StringComparison.Ordinal) ? withWww[4..] : withWww;
        try
        {
            await using var connection = PlatformHostPolicy.IsSuperCpHost(host)
                ? await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false)
                : await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            }

            await using var command = Command(
                connection,
                "SELECT IFNULL(`integrations_json`, '') FROM `epc_portal_site_settings` WHERE `host` = @p0 OR `host` = @p1 LIMIT 1",
                withWww,
                bare);
            var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            var overlay = AuthEmailOtp.TenantOverlay(raw);
            return overlay.Count > 0 ? overlay : null;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, string> ReadConfigPhp(PhpReferenceOptions php)
    {
        var text = ReadDocRootFile(php, "config.php");
        return text is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : PhpConfigFile.Values(PhpConfigFile.Parse(text));
    }

    private static IReadOnlyDictionary<string, string?> ReadPhpArrayFile(PhpReferenceOptions php, string name)
        => AuthEmailOtp.ParsePhpArrayPairs(ReadDocRootFile(php, name));

    private static string? ReadDocRootFile(PhpReferenceOptions php, string name)
    {
        var root = (php.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            return null;
        }

        var path = Path.Combine(root, name);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyDictionary<string, string?>?> TenantRowAsync(
        DbConnection registry,
        string where,
        CancellationToken cancellationToken,
        params object[] args)
    {
        try
        {
            await using var command = Command(registry, "SELECT * FROM `epc_portal_tenants` WHERE " + where + " LIMIT 1", args);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            return row;
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static bool IsHttps(HttpContext http)
        => AuthEmailOtp.RequireHttps(
            http.Request.IsHttps,
            http.Request.Headers["X-Forwarded-Proto"].ToString(),
            http.Connection.LocalPort,
            http.Request.Headers.Host.ToString());

    /// <summary>PHP <c>epc_portal_host()</c>: lower-case Host header without the port.</summary>
    private static string PortalHost(HttpContext http)
    {
        var host = AuthEmailOtp.PhpLower(http.Request.Headers.Host.ToString());
        var colon = host.IndexOf(':', StringComparison.Ordinal);
        return colon >= 0 ? host[..colon] : host;
    }

    private static string RemoteIp(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        if (ip is null)
        {
            return string.Empty;
        }

        return (ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip).ToString();
    }

    /// <summary>PHP <c>epc_auth_read_json_body()</c>: a JSON array/object body, else the form fields.</summary>
    internal static async Task<IReadOnlyDictionary<string, string?>> ReadInputAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        request.EnableBuffering();
        string raw;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
        {
            raw = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        request.Body.Position = 0;
        if (AuthEmailOtp.PhpTrim(raw).Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        map[property.Name] = AuthEmailOtp.JsonScalar(property.Value);
                    }

                    return map;
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var i = 0;
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        map[(i++).ToString(CultureInfo.InvariantCulture)] = AuthEmailOtp.JsonScalar(item);
                    }

                    return map;
                }
            }
            catch (JsonException)
            {
            }
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
            foreach (var pair in form)
            {
                map[pair.Key] = pair.Value.Count > 0 ? pair.Value[^1] : string.Empty;
            }
        }

        return map;
    }

    private static string? Field(IReadOnlyDictionary<string, string?> input, string name)
        => input.TryGetValue(name, out var value) ? value : null;

    private static string JsonObject(IEnumerable<KeyValuePair<string, string>> members)
        => "{" + string.Join(",", members.Select(m => OAuthStart.PhpJsonString(m.Key) + ":" + m.Value)) + "}";

    private static async Task<DbConnection> OpenRegistryAsync(ITenantDbConnectionFactory connections, CancellationToken cancellationToken)
    {
        if (!connections.IsConfigured)
        {
            throw new InvalidOperationException("Platform database is not configured.");
        }

        var connection = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    private static DbCommand Command(DbConnection connection, string sql, params object[] args)
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

    private static async Task ExecAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CountAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        await using var command = Command(connection, sql, args);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task WriteAsync(HttpContext context, int status, string body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
    }
}
