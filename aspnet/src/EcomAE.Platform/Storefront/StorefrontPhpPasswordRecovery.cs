using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/users/forgot_password.php</c> (the reset request: code, lock and the <c>forgot_password_by_*</c>
/// notification through <c>send_notify.php</c>), <c>content/users/new_password.php</c> (the reset link or SMS code:
/// a wrong or late code is discarded, a right one replaces the password and shows it) and
/// <c>DP_User::available_communications()</c>.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string ForgotPasswordSendPath = "/storefront/forgot-password-app/send";

    /// <summary>The <c>translate_str_by_id()</c> ids the reset request can end with; the page shows only these.</summary>
    public static readonly IReadOnlySet<string> ForgotPasswordResults = new HashSet<string>(StringComparer.Ordinal)
    {
        "4719", "4720", "4694", "2122", "4722", "4723", "4724", "4725",
    };

    /// <summary>English text for the reset ids when the tenant has no translation for them.</summary>
    public static readonly IReadOnlyDictionary<string, string> PasswordResetFallbacks = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["4719"] = "Choose how to receive the reset code.",
        ["4720"] = "No account has this confirmed contact.",
        ["4694"] = "A code was sent recently. Try again in 5 minutes.",
        ["2122"] = "The reset request could not be saved.",
        ["4722"] = "The reset message could not be sent.",
        ["4723"] = "The reset message could not be delivered to this contact.",
        ["4724"] = "A link to reset the password has been sent to your e-mail.",
        ["4729"] = "The code is wrong. Request a new password reset.",
        ["4730"] = "The code has expired. Request a new password reset.",
        ["4731"] = "The new password could not be saved.",
        ["4732"] = "Your new password",
    };

    public const string ForgotEmailSent = "4724";
    public const string ForgotPhoneSent = "4725";

    public sealed record Communications(bool Smtp, bool Sms)
    {
        public bool All => Smtp && Sms;
    }

    /// <summary>PHP <c>DP_User::available_communications()</c>: SMTP when every SMTP setting is filled, SMS when exactly one operator is active.</summary>
    public static async Task<Communications> AvailableCommunicationsAsync(DbConnection connection, IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken)
    {
        var smtp = new[] { "from_name", "from_email", "smtp_mode", "smtp_encryption", "smtp_host", "smtp_port", "smtp_username", "smtp_password" }
            .All(key => config.TryGetValue(key, out var value) && value.Length > 0 && value != "0");
        long active;
        try
        {
            active = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `sms_api` WHERE `active` = ?"), cancellationToken, 1).ConfigureAwait(false);
        }
        catch (DbException)
        {
            active = 0;
        }

        return new Communications(smtp, active == 1);
    }

    /// <summary>The reset request answer: a <see cref="ForgotPasswordResults"/> id, or <c>null</c> when PHP shows the form again.</summary>
    public sealed record ForgotPasswordOutcome(string? MessageId, bool LoggedIn);

    public static async Task<ForgotPasswordOutcome> ForgotPasswordSendAsync(
        DbConnection connection,
        string? session,
        string? userCookie,
        IReadOnlyDictionary<string, string> post,
        StorefrontNotifyDispatcher? dispatcher,
        IReadOnlyDictionary<string, string> config,
        string langHrefSlashAfter,
        CancellationToken cancellationToken)
    {
        if (await CookieUserIdAsync(connection, session, userCookie, cancellationToken).ConfigureAwait(false) != 0)
        {
            return new ForgotPasswordOutcome(null, true);
        }

        if (!post.TryGetValue("forgot_password_contact", out var contact))
        {
            return new ForgotPasswordOutcome(null, false);
        }

        if (!post.TryGetValue("forgot_password_contact_type", out var type) || type is not ("email" or "phone"))
        {
            return new ForgotPasswordOutcome("4719", false);
        }

        long userId;
        long lockUntil;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `user_id`, `" + type + "_code_send_lock_expired` FROM `users` WHERE `" + type + "` = ? AND `" + type + "_confirmed` = ?");
            ErpDb.AddParameters(command, contact, 1);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ForgotPasswordOutcome("4720", false);
            }

            userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            lockUntil = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        }

        var now = UnixNow();
        if (lockUntil > now)
        {
            return new ForgotPasswordOutcome("4694", false);
        }

        var translator = new StorefrontPhpTranslator(connection, string.IsNullOrEmpty(langHrefSlashAfter) ? "en" : langHrefSlashAfter.TrimEnd('/'));
        string code;
        string name;
        string varsJson;
        var domainPath = config.TryGetValue("domain_path", out var d) ? d : string.Empty;
        if (type == "email")
        {
            code = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            name = "forgot_password_by_email";
            var siteKey = config.TryGetValue("site_name", out var s) ? s : string.Empty;
            var href = "<a target='_blank' href='" + domainPath + langHrefSlashAfter + "users/new_password?code=" + code + "&type=email&contact="
                + Uri.EscapeDataString(contact) + "'>" + await translator.TextAsync(4721, cancellationToken).ConfigureAwait(false) + "</a>";
            varsJson = "{\"site_name\":" + PhpJsonOrNull(await translator.RawAsync(siteKey, cancellationToken).ConfigureAwait(false))
                + ",\"forgot_password_code_href\":" + OAuthStart.PhpJsonString(href) + "}";
        }
        else
        {
            code = RandomNumberGenerator.GetInt32(10000, 100000).ToString(CultureInfo.InvariantCulture);
            name = "forgot_password_by_phone";
            varsJson = "{\"forgot_password_code\":" + code + "}";
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users` SET `forgot_password_time` = ?, `forgot_password_code` = ?, `" + type + "_code_send_lock_expired` = ? WHERE `user_id` = ?"),
                cancellationToken,
                now,
                code,
                now + 300,
                userId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new ForgotPasswordOutcome("2122", false);
        }

        if (dispatcher is null)
        {
            return new ForgotPasswordOutcome("4722", false);
        }

        var answer = await dispatcher.SendNotifyHttpAsync(
            connection,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["check"] = config.TryGetValue("secret_succession", out var secret) ? secret : string.Empty,
                ["name"] = name,
                ["vars"] = varsJson,
                ["persons"] = "[{\"type\":\"user_id\",\"user_id\":" + userId.ToString(CultureInfo.InvariantCulture) + "}]",
            },
            config,
            cancellationToken).ConfigureAwait(false);
        return new ForgotPasswordOutcome(PersonStatus(answer, type) switch
        {
            null => "4722",
            false => "4723",
            true => type == "email" ? ForgotEmailSent : ForgotPhoneSent,
        }, false);
    }

    /// <summary><c>null</c> for <c>$curl_result["status"] == false</c>, else <c>persons[0].contacts[$type].status</c>.</summary>
    private static bool? PersonStatus(string? answer, string type)
    {
        if (string.IsNullOrEmpty(answer))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(answer);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.True)
            {
                return null;
            }

            return root.TryGetProperty("persons", out var persons)
                && persons.ValueKind == JsonValueKind.Array
                && persons.GetArrayLength() > 0
                && persons[0].TryGetProperty("contacts", out var contacts)
                && contacts.ValueKind == JsonValueKind.Object
                && contacts.TryGetProperty(type, out var channel)
                && channel.ValueKind == JsonValueKind.Object
                && channel.TryGetProperty("status", out var sent)
                && sent.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>What <c>new_password.php</c> shows: a redirect home, a translated message id, or the new password.</summary>
    public sealed record NewPasswordOutcome(bool RedirectHome, string? MessageId, string? Password);

    public static async Task<NewPasswordOutcome> NewPasswordAsync(
        DbConnection connection,
        string? code,
        string? type,
        string? contact,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        var home = new NewPasswordOutcome(true, null, null);
        if ((code is null && type is null && contact is null) || type is not ("email" or "phone"))
        {
            return home;
        }

        long userId;
        string stored;
        long requestedAt;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `user_id`, `forgot_password_code`, `forgot_password_time` FROM `users` WHERE `" + type + "` = ? AND `" + type + "_confirmed` = ?");
            ErpDb.AddParameters(command, HtmlEntitiesQuotes(contact ?? string.Empty), 1);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return home;
            }

            userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            stored = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            requestedAt = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
        }

        if (stored.Length == 0)
        {
            return home;
        }

        if (!StorefrontSmsHandlers.LooseEquals(code, stored) || requestedAt < UnixNow() - 1800)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users` SET `forgot_password_time` = ?, `forgot_password_code` = ? WHERE `user_id` = ?"),
                cancellationToken,
                0,
                string.Empty,
                userId).ConfigureAwait(false);
            return new NewPasswordOutcome(false, StorefrontSmsHandlers.LooseEquals(code, stored) ? "4730" : "4729", null);
        }

        var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8))[..10];
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users` SET `password` = ?, `forgot_password_time` = ?, `forgot_password_code` = ? WHERE `user_id` = ?"),
                cancellationToken,
                Md5Hex(password + (config.TryGetValue("secret_succession", out var secret) ? secret : string.Empty)),
                0,
                string.Empty,
                userId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new NewPasswordOutcome(false, "4731", null);
        }

        return new NewPasswordOutcome(false, "4732", password);
    }

    /// <summary>PHP 8.1 <c>htmlentities()</c> for the ASCII characters it changes (<c>ENT_QUOTES | ENT_SUBSTITUTE</c>).</summary>
    public static string HtmlEntitiesQuotes(string value)
        => value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    /// <summary>The phone branch: <c>T4725</c> and the GET form to <c>new_password</c> with the code field.</summary>
    public static async Task<string> ForgotPhoneCodeFormAsync(StorefrontPhpTranslator translator, string langHref, string contact, CancellationToken cancellationToken)
    {
        async Task<string> T(int id, string fallback)
        {
            var text = await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
            return WebUtility.HtmlEncode(text.Length > 0 ? text : fallback);
        }

        var label = await T(4726, "Code from SMS");
        return "<div class=\"col-lg-12\">" + await T(4725, "A code has been sent to your phone. Enter it to get a new password.") + "</div>"
            + "<form method=\"GET\" action=\"" + WebUtility.HtmlEncode(langHref) + "/users/new_password\">"
            + "<input type=\"hidden\" name=\"contact\" value=\"" + WebUtility.HtmlEncode(contact) + "\" />"
            + "<input type=\"hidden\" name=\"type\" value=\"phone\" />"
            + "<div class=\"form-group\"><label for=\"\" class=\"col-sm-2 control-label\">" + label + "</label>"
            + "<div class=\"col-sm-6\" style=\"padding:5px;\"><input type=\"text\" class=\"form-control\" name=\"code\" id=\"code\" value=\"\" placeholder=\"" + label + "\"></div>"
            + "<div class=\"col-sm-4\" style=\"padding:5px;\"><button type=\"submit\">" + await T(4521, "Send") + "</button></div>"
            + "</div></form>";
    }
}
