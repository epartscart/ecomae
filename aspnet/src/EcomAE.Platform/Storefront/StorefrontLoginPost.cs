using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The storefront half of PHP <c>plugins/authentication/plugin.php</c> for a POST to the login page: the visitor
/// session prelude (a guest session is created or touched, stale guest sessions are purged, a signed-in customer's
/// last visit is stamped), then <c>authentication</c>: the <c>content/users/stop_csrf.php</c> check against the
/// customer session, the password or 2FA code check, and on success a new session, the cookies, the guest cart move,
/// the UAE VAT type sync, the staff "customer signed in" notice and the redirect to
/// <c>epc_redirect_safe_target()</c> of <c>target</c>. A failed sign-in re-renders the page with PHP's alert script.
/// Intended deviations from PHP: failures count against the login rate limit (10 in 15 minutes per IP and contact
/// block the attempt), <c>wrong_authentication_tag</c> is JSON-quoted instead of pasted into the script, and the new
/// session row gets <c>last_activiti_time</c> at once rather than on the next request.
/// </summary>
public static class StorefrontLoginPost
{
    public const string AlertItemKey = "EcomAE.StorefrontLogin.Alert";

    public const long StaleSeconds = 2592000;

    public const long CookieSeconds = 9999999;

    public enum Kind
    {
        /// <summary>The page renders as for a GET, with <see cref="Outcome.Alert"/> appended when set.</summary>
        Render,

        /// <summary>PHP exits with <see cref="Outcome.Body"/>.</summary>
        Body,

        /// <summary>PHP sends <c>Location: </c><see cref="Outcome.Location"/>.</summary>
        Redirect,
    }

    /// <param name="Expires">Unix time; 0 is a browser-session cookie.</param>
    public sealed record CookieWrite(string Name, string Value, long Expires);

    public sealed record Outcome(Kind Kind, string Body, string? Location, IReadOnlyList<CookieWrite> Cookies, string? Alert);

    public sealed record Request(
        IReadOnlyDictionary<string, string> Post,
        IReadOnlyDictionary<string, string> Query,
        string? SessionCookie,
        string? UserCookie,
        string RemoteIp,
        string UserAgent,
        string Secret,
        string DomainPath,
        string PageUrl,
        string LangCode);

    /// <summary>The staff notice after a sign-in: connection, user id, contact.</summary>
    public delegate Task LoginNotifier(DbConnection connection, long userId, string contact, CancellationToken cancellationToken);

    private static readonly string[] BotNeedles =
    [
        "bot", "crawl", "spider", "slurp", "scrapy", "curl/", "wget", "python-requests",
        "python-urllib", "httpclient", "libwww", "httpunit", "nutch", "httrack",
        "phantomjs", "headlesschrome", "headless", "selenium", "puppeteer", "playwright",
        "axios/", "go-http-client", "java/", "okhttp", "node-fetch", "postmanruntime",
        "insomnia", "apache-httpclient", "mechanize", "beautifulsoup", "http.rb",
        "aiohttp", "facebookexternalhit", "bytespider", "gptbot", "claudebot",
        "ccbot", "anthropic", "petalbot", "semrush", "ahrefs", "mj12bot", "dotbot",
        "dataforseo", "serpstat", "screaming frog", "siteauditbot", "bingpreview",
        "yandex", "baiduspider", "duckduckbot", "applebot", "ia_archiver",
        "googlebot", "adsbot-google", "mediapartners-google", "apis-google",
        "storebot-google", "google-inspectiontool", "chrome-lighthouse",
        "pingdom", "uptimerobot", "statuscake", "monitor",
    ];

    /// <summary>PHP <c>epc_storefront_anti_crawl_is_bot()</c>: an empty or crawler user agent; the CP fetchers are not bots.</summary>
    public static bool IsBot(string? userAgent)
    {
        var ua = AuthEmailOtp.PhpLower(AuthEmailOtp.PhpTrim(userAgent ?? string.Empty));
        if (ua.Length == 0)
        {
            return true;
        }

        if (ua.Contains("epartscart cp", StringComparison.Ordinal) || ua.Contains("ecomae cp", StringComparison.Ordinal))
        {
            return false;
        }

        return BotNeedles.Any(needle => ua.Contains(needle, StringComparison.Ordinal));
    }

    /// <summary>PHP <c>epc_redirect_safe_target()</c>: a local absolute path, else <c>/</c>.</summary>
    public static string SafeTarget(string target)
    {
        target = AuthEmailOtp.PhpTrim(target);
        if (target.Length == 0 || target == "/")
        {
            return "/";
        }

        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("//", StringComparison.Ordinal)
            || target[0] != '/'
            || target.IndexOfAny(['\r', '\n', '\0']) >= 0)
        {
            return "/";
        }

        return target;
    }

    /// <summary><c>content/users/stop_csrf.php</c>'s <c>error_exit()</c> body.</summary>
    public static string CsrfError(string message)
    {
        var json = OAuthStart.PhpJsonString(message);
        return "{\"error\":" + json + ",\"message\":" + json + ",\"status\":false}";
    }

    public static async Task<Outcome> HandleAsync(DbConnection connection, Request request, LoginNotifier? notify, long now, CancellationToken cancellationToken)
    {
        var cookies = new List<CookieWrite>();
        var session = request.SessionCookie;
        var uid = request.UserCookie;

        var count = await SessionCountAsync(connection, session, uid, cancellationToken).ConfigureAwait(false);
        if (count > 1)
        {
            return new Outcome(Kind.Body, string.Empty, null, cookies, null);
        }

        var userId = count == 1 ? PhpInt(uid) : 0;
        if (count == 0 || userId == 0)
        {
            if (count == 1)
            {
                await ExecAsync(connection, "UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?", cancellationToken, now, session, uid).ConfigureAwait(false);
            }
            else
            {
                var minted = StorefrontGuestSessionService.GuestSessionToken(request.Secret, request.RemoteIp, now);
                var guestKey = LegacySessionTokenFactory.CsrfGuardKey(request.Secret, minted, request.RemoteIp, request.UserAgent);
                await ExecAsync(
                    connection,
                    "INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `last_activiti_time`, `csrf_guard_key`) VALUES (?, ?, ?, ?, ?, ?)",
                    cancellationToken,
                    minted,
                    0,
                    now,
                    string.Empty,
                    now,
                    guestKey).ConfigureAwait(false);
                cookies.Add(new CookieWrite("session", minted, now + CookieSeconds));
                cookies.Add(new CookieWrite("u_id", "0", now + CookieSeconds));
                session = minted;
                uid = "0";
            }
        }

        var stale = now - StaleSeconds;
        await ExecAsync(connection, "DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?)", cancellationToken, 0, stale).ConfigureAwait(false);
        await ExecAsync(connection, "DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?", cancellationToken, 0, stale).ConfigureAwait(false);
        if (userId > 0)
        {
            await ExecAsync(connection, "UPDATE `users` SET `time_last_visit` = ? WHERE `user_id` = ?", cancellationToken, now, userId).ConfigureAwait(false);
            await ExecAsync(connection, "UPDATE `sessions` SET `last_activiti_time` = ? WHERE `session` = ? AND `user_id` = ?", cancellationToken, now, session, userId).ConfigureAwait(false);
        }

        if (AuthEmailOtp.PhpEmpty(Post(request, "authentication")))
        {
            return new Outcome(Kind.Render, string.Empty, null, cookies, null);
        }

        var posted = request.Query.TryGetValue("csrf_guard_key", out var fromQuery) ? fromQuery : Post(request, "csrf_guard_key");
        if (posted is null)
        {
            return new Outcome(Kind.Body, CsrfError("Error! CSRF 1"), null, cookies, null);
        }

        if (AuthEmailOtp.PhpEmpty(posted))
        {
            return new Outcome(Kind.Body, CsrfError("Error! CSRF 3"), null, cookies, null);
        }

        var row = await SessionRowAsync(connection, session, uid, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return new Outcome(Kind.Body, CsrfError("Error! CSRF 3.1"), null, cookies, null);
        }

        if (!string.Equals(Col(row, "csrf_guard_key"), posted, StringComparison.Ordinal))
        {
            return new Outcome(Kind.Body, CsrfError("Error! CSRF 4"), null, cookies, null);
        }

        var contact = Post(request, "auth_contact");
        var contactType = Post(request, "auth_contact_type");
        if (AuthEmailOtp.PhpEmpty(contact) || AuthEmailOtp.PhpEmpty(contactType))
        {
            return new Outcome(Kind.Render, string.Empty, null, cookies, null);
        }

        if (contactType != "email" && contactType != "phone")
        {
            return new Outcome(Kind.Body, string.Empty, null, cookies, null);
        }

        var rateIp = request.RemoteIp;
        var limit = await LegacyLoginSecurity.CheckAsync(connection, rateIp, contact!, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string?>? user = null;
        if (!limit.Blocked)
        {
            user = request.Post.ContainsKey("code")
                ? await CodeUserAsync(connection, row, request, contactType, contact!, now, cancellationToken).ConfigureAwait(false)
                : await PasswordUserAsync(connection, request, contactType, contact!, cancellationToken).ConfigureAwait(false);
        }

        if (user is null)
        {
            if (!limit.Blocked)
            {
                await LegacyLoginSecurity.RecordAsync(connection, rateIp, contact!, false, cancellationToken).ConfigureAwait(false);
            }

            var message = await new StorefrontPhpTranslator(connection, request.LangCode).TextAsync(4787, cancellationToken).ConfigureAwait(false);
            var tag = Post(request, "wrong_authentication_tag");
            var alert = AuthEmailOtp.PhpEmpty(tag)
                ? "\n<script>alert(\"" + message + "\");</script>"
                : "\n<script>document.getElementById(" + OAuthStart.PhpJsonString(tag!) + ").innerHTML = \"" + message + "\";</script>";
            return new Outcome(Kind.Render, string.Empty, null, cookies, alert);
        }

        await LegacyLoginSecurity.ClearAsync(connection, rateIp, contact!, cancellationToken).ConfigureAwait(false);
        var signedIn = PhpInt(user.GetValueOrDefault("user_id"));
        await ExecAsync(connection, "DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?)", cancellationToken, signedIn, stale).ConfigureAwait(false);
        await ExecAsync(connection, "DELETE FROM `sessions` WHERE `user_id` = ? AND `last_activiti_time` < ?", cancellationToken, signedIn, stale).ConfigureAwait(false);

        var token = LegacySessionTokenFactory.CustomerSessionToken(contact!, (int)signedIn, now, request.Secret);
        var csrf = LegacySessionTokenFactory.CsrfGuardKey(request.Secret, token, request.RemoteIp, request.UserAgent);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `sessions` (`session`, `user_id`, `time`, `data`, `csrf_guard_key`, `last_activiti_time`) VALUES (?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            token,
            signedIn,
            now,
            string.Empty,
            csrf,
            now).ConfigureAwait(false);

        var expires = AuthEmailOtp.PhpEmpty(Post(request, "rememberme")) ? 0 : now + CookieSeconds;
        cookies.Add(new CookieWrite("session", token, expires));
        cookies.Add(new CookieWrite("u_id", signedIn.ToString(CultureInfo.InvariantCulture), expires));

        if (session is not null)
        {
            await AuthOtpVerifyLogin.MergeGuestCartAsync(connection, (int)signedIn, session, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await EpcUaeCustomerVat.SyncAsync(connection, null, signedIn, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        if (notify is not null)
        {
            try
            {
                await notify(connection, signedIn, contact!, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException or IOException or HttpRequestException)
            {
            }
        }

        var location = request.Post.TryGetValue("target", out var target)
            ? request.DomainPath.TrimEnd('/') + SafeTarget(target)
            : request.PageUrl;
        return new Outcome(Kind.Redirect, string.Empty, location, cookies, null);
    }

    private static async Task<Dictionary<string, string?>?> PasswordUserAsync(DbConnection connection, Request request, string contactType, string contact, CancellationToken cancellationToken)
    {
        var user = await UserAsync(connection, contactType, contact, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        var password = Post(request, "password") ?? string.Empty;
        var stored = user.GetValueOrDefault("password") ?? string.Empty;
        if (BcryptMatches(password, stored))
        {
            return user;
        }

        if (!string.Equals(LegacyPasswordVerifier.Md5Hex(password + request.Secret), stored, StringComparison.Ordinal))
        {
            return null;
        }

        await LegacyLoginSecurity.UpgradePasswordIfNeededAsync(connection, PhpInt(user.GetValueOrDefault("user_id")), password, stored, cancellationToken).ConfigureAwait(false);
        return user;
    }

    private static async Task<Dictionary<string, string?>?> CodeUserAsync(
        DbConnection connection,
        Dictionary<string, string?> session,
        Request request,
        string contactType,
        string contact,
        long now,
        CancellationToken cancellationToken)
    {
        var attempts = PhpInt(session.GetValueOrDefault("2fa_attempts"));
        if (attempts < 1)
        {
            return null;
        }

        var code = session.GetValueOrDefault("2fa_code");
        if (code is not null && code == Post(request, "code"))
        {
            return ExpireFaCode(session.GetValueOrDefault("data")) < now
                ? null
                : await UserAsync(connection, contactType, contact, cancellationToken).ConfigureAwait(false);
        }

        await ExecAsync(connection, "UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ?", cancellationToken, attempts - 1, session.GetValueOrDefault("session")).ConfigureAwait(false);
        return null;
    }

    /// <summary><c>json_decode($data, true)["expireFaCode"]</c> as a number; a missing value compares below any time.</summary>
    private static double ExpireFaCode(string? data)
    {
        try
        {
            using var document = JsonDocument.Parse(data ?? string.Empty);
            if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("expireFaCode", out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.Number => value.GetDouble(),
                    JsonValueKind.String => double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0,
                    JsonValueKind.True => 1,
                    _ => double.MinValue,
                };
            }
        }
        catch (JsonException)
        {
        }

        return double.MinValue;
    }

    private static bool BcryptMatches(string password, string stored)
    {
        if (!stored.StartsWith('$'))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, stored);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, string?>?> UserAsync(DbConnection connection, string contactType, string contact, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(
            connection,
            "SELECT * FROM `users` WHERE `" + contactType + "` = ? AND `" + contactType + "_confirmed` = ? AND `unlocked` = ?",
            cancellationToken,
            contact,
            1,
            1).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<long> SessionCountAsync(DbConnection connection, string? session, string? uid, CancellationToken cancellationToken)
    {
        if (session is null || uid is null)
        {
            return 0;
        }

        return await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `user_id` = ?"), cancellationToken, session, uid).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string?>?> SessionRowAsync(DbConnection connection, string? session, string? uid, CancellationToken cancellationToken)
    {
        if (session is null || uid is null)
        {
            return null;
        }

        var rows = await RowsAsync(connection, "SELECT * FROM `sessions` WHERE `session` = ? AND `user_id` = ?", cancellationToken, session, uid).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<List<Dictionary<string, string?>>> RowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i) switch
                {
                    bool b => b ? "1" : "0",
                    var v => Convert.ToString(v, CultureInfo.InvariantCulture),
                };
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task ExecAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException ex) when (EcomAE.Platform.Migration.CpMissingSchema.IsMissing(ex))
        {
        }
    }

    private static string? Post(Request request, string key) => request.Post.TryGetValue(key, out var value) ? value : null;

    private static string Col(IReadOnlyDictionary<string, string?> row, string name) => row.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;

    /// <summary>PHP <c>(int)</c> of a numeric-prefixed string.</summary>
    private static long PhpInt(string? value)
    {
        var text = AuthEmailOtp.PhpTrim(value ?? string.Empty);
        var end = 0;
        if (end < text.Length && (text[end] == '-' || text[end] == '+'))
        {
            end++;
        }

        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return long.TryParse(text.AsSpan(0, end), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }
}
