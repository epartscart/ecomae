using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP sign-in / registration e-mail code rules (send, verify-only, SMTP precheck and transport):
/// input normalisation, the HTTPS gate, the SMTP effective config and its precheck, error classification,
/// the mail content and the JSON answers (PHP <c>json_encode</c> escaping).
/// </summary>
public static class AuthEmailOtp
{
    public const int TtlSeconds = 600;
    public const int RateWindowSeconds = 3600;
    public const int MaxPerEmail = 5;
    public const int MaxPerIp = 20;

    public const string OtpTableDdl =
        "CREATE TABLE IF NOT EXISTS `epc_auth_otp_requests` (\n"
        + "\t\t\t`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,\n"
        + "\t\t\t`email` VARCHAR(120) NOT NULL,\n"
        + "\t\t\t`code_hash` VARCHAR(64) NOT NULL,\n"
        + "\t\t\t`tenant_key` VARCHAR(64) NOT NULL DEFAULT '',\n"
        + "\t\t\t`context_json` TEXT NULL,\n"
        + "\t\t\t`expires_at` INT NOT NULL,\n"
        + "\t\t\t`ip_address` VARCHAR(45) NOT NULL DEFAULT '',\n"
        + "\t\t\t`created_at` INT NOT NULL DEFAULT 0,\n"
        + "\t\t\tINDEX `email_created` (`email`, `created_at`),\n"
        + "\t\t\tINDEX `expires_at` (`expires_at`)\n"
        + "\t\t) ENGINE=InnoDB DEFAULT CHARSET=utf8";

    public const string MsgValidEmail = "Valid email is required";
    public const string MsgHttpsSignIn = "HTTPS is required for sign-in";
    public const string MsgHttps = "HTTPS is required";
    public const string MsgRateLimited = "Too many sign-in attempts — try again in an hour (max 5 codes per email, 20 per IP)";
    public const string MsgSent = "Sign-in code sent — check your inbox";
    public const string MsgDemoReady =
        "Sign-in code ready — email could not be sent (SMTP). Super CP operators can view the code under Modern auth settings.";
    public const string MsgSendFallback = "Could not send email — check SMTP settings in Control Panel";
    public const string MsgSendCrashed = "Could not send code — please retry";
    public const string MsgCodeRequired = "Email and 6-digit code are required";
    public const string MsgInvalidEmail = "Invalid email address";
    public const string MsgInvalidCode = "Invalid or expired code — please try again";
    public const string MsgVerified = "Email verified";
    public const string MsgVerifyCrashed = "Verification temporarily unavailable — please retry";

    public static readonly IReadOnlyList<string> SmtpKeys =
    [
        "smtp_mode", "smtp_host", "smtp_port", "smtp_encryption", "smtp_username", "smtp_password", "from_email", "from_name",
    ];

    private static readonly string[] TenantOverlayKeys =
    [
        "smtp_host", "smtp_port", "smtp_encryption", "smtp_username", "smtp_password", "from_name", "from_email",
    ];

    private static readonly Regex KeyUnsafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PhpArrayPair = new(
        @"'(?<key>(?:\\.|[^'\\])*)'\s*=>\s*(?:'(?<str>(?:\\.|[^'\\])*)'|""(?<dq>(?:\\.|[^""\\])*)""|(?<raw>-?[0-9]+(?:\.[0-9]+)?|true|false|null|TRUE|FALSE|NULL))",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>PHP <c>trim()</c> default character list.</summary>
    public static string PhpTrim(string? value) => (value ?? string.Empty).Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    /// <summary>PHP <c>strtolower()</c> (ASCII only).</summary>
    public static string PhpLower(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= 'A' and <= 'Z')
            {
                chars[i] = (char)(chars[i] + 32);
            }
        }

        return new string(chars);
    }

    public static string NormalizeEmail(string? raw) => PhpLower(PhpTrim(raw));

    public static string SanitizeKey(string? raw) => KeyUnsafe.Replace(PhpLower(raw ?? string.Empty), string.Empty);

    /// <summary><c>preg_replace('/\D/', '', trim($code))</c>.</summary>
    public static string DigitsOnly(string? raw)
    {
        var sb = new StringBuilder();
        foreach (var c in PhpTrim(raw))
        {
            if (c is >= '0' and <= '9')
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    public static bool IsValidEmail(string email) => email.Length > 0 && StorefrontSupplierLpoNotifier.IsPlainEmail(email);

    public static string NormalizeMode(string? mode) => PhpLower(PhpTrim(mode)) == "storefront" ? "storefront" : "cp";

    /// <summary>PHP <c>epc_auth_require_https()</c>.</summary>
    public static bool RequireHttps(bool isHttps, string? forwardedProto, int localPort, string? hostHeader)
    {
        if (isHttps)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(forwardedProto) && forwardedProto != "0" && PhpLower(forwardedProto) == "https")
        {
            return true;
        }

        if (localPort == 443)
        {
            return true;
        }

        var host = PhpLower(hostHeader ?? string.Empty);
        return host.Length > 0 && (host.Contains("localhost", StringComparison.Ordinal) || host == "127.0.0.1");
    }

    public static string CodeHash(string code, string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code + "|" + secret))).ToLowerInvariant();

    public static string NewCode() => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);

    /// <summary>PHP <c>(int)</c> on a string: optional whitespace, sign, leading digits.</summary>
    public static long PhpInt(string? value)
    {
        var s = (value ?? string.Empty).TrimStart(' ', '\t', '\n', '\r', '\v', '\f');
        var i = 0;
        var negative = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            negative = s[i] == '-';
            i++;
        }

        long n = 0;
        while (i < s.Length && s[i] is >= '0' and <= '9' && n < long.MaxValue / 10)
        {
            n = (n * 10) + (s[i] - '0');
            i++;
        }

        return negative ? -n : n;
    }

    /// <summary>
    /// Single-quoted / double-quoted / scalar <c>'key' =&gt; value</c> pairs of a PHP array literal
    /// (<c>config.epc-smtp.php</c>, <c>$epc_config_local</c>). Values are PHP <c>(string)</c> casts; <c>null</c> maps to null.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ParsePhpArrayPairs(string? php)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(php))
        {
            return map;
        }

        foreach (Match m in PhpArrayPair.Matches(php))
        {
            var key = UnescapeSingle(m.Groups["key"].Value);
            string? value;
            if (m.Groups["str"].Success)
            {
                value = UnescapeSingle(m.Groups["str"].Value);
            }
            else if (m.Groups["dq"].Success)
            {
                value = m.Groups["dq"].Value.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            }
            else
            {
                var raw = m.Groups["raw"].Value.ToLowerInvariant();
                value = raw switch
                {
                    "true" => "1",
                    "false" => string.Empty,
                    "null" => null,
                    _ => m.Groups["raw"].Value,
                };
            }

            map[key] = value;
        }

        return map;
    }

    private static string UnescapeSingle(string value)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length && (value[i + 1] == '\'' || value[i + 1] == '\\'))
            {
                sb.Append(value[i + 1]);
                i++;
            }
            else
            {
                sb.Append(value[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// PHP <c>epc_auth_smtp_effective_config()</c>: <c>config.php</c> (+ <c>$epc_config_local</c>), then non-empty
    /// <c>config.epc-smtp.php</c> values, then the tenant <c>integrations.smtp</c> overlay.
    /// </summary>
    public static SmtpEffectiveConfig EffectiveConfig(
        IReadOnlyDictionary<string, string> configPhp,
        IReadOnlyDictionary<string, string?>? configLocal,
        IReadOnlyDictionary<string, string?>? smtpFile,
        IReadOnlyDictionary<string, string>? tenantOverlay)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in SmtpKeys)
        {
            values[key] = configPhp.TryGetValue(key, out var v) ? v : string.Empty;
        }

        if (configLocal is not null)
        {
            foreach (var key in SmtpKeys)
            {
                if (configLocal.TryGetValue(key, out var v))
                {
                    values[key] = v ?? string.Empty;
                }
            }
        }

        var allowMailFallback = false;
        foreach (var overlay in new[] { smtpFile, tenantOverlay?.ToDictionary(p => p.Key, p => (string?)p.Value, StringComparer.Ordinal) })
        {
            if (overlay is null)
            {
                continue;
            }

            foreach (var pair in overlay)
            {
                if (pair.Key == "allow_mail_fallback")
                {
                    allowMailFallback = !PhpEmpty(pair.Value);
                    continue;
                }

                if (values.ContainsKey(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                {
                    values[pair.Key] = pair.Value;
                }
            }
        }

        return new SmtpEffectiveConfig(values, allowMailFallback);
    }

    /// <summary>PHP <c>epc_auth_smtp_tenant_overlay()</c> from a decoded <c>integrations_json</c>.</summary>
    public static IReadOnlyDictionary<string, string> TenantOverlay(string? integrationsJson)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(integrationsJson))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(integrationsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("smtp", out var smtp)
                || smtp.ValueKind != JsonValueKind.Object
                || !smtp.TryGetProperty("use_tenant_smtp", out var use)
                || PhpEmpty(JsonScalar(use)))
            {
                return map;
            }

            foreach (var key in TenantOverlayKeys)
            {
                if (smtp.TryGetProperty(key, out var el))
                {
                    var value = JsonScalar(el);
                    if (!PhpEmpty(value))
                    {
                        map[key] = value!;
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return map;
    }

    /// <summary>PHP <c>(string)</c> of a decoded JSON value; null for JSON null.</summary>
    public static string? JsonScalar(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "1",
        JsonValueKind.False => string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => "Array",
    };

    /// <summary>PHP <c>empty()</c> for a string-cast value.</summary>
    public static bool PhpEmpty(string? value) => string.IsNullOrEmpty(value) || value == "0";

    public static bool SmtpModeOn(string? mode) => PhpInt(mode) == 1 || mode == "1";

    /// <summary>PHP <c>epc_auth_smtp_diagnose()</c> issue list (empty = precheck passes).</summary>
    public static IReadOnlyList<string> Diagnose(SmtpEffectiveConfig cfg)
    {
        var issues = new List<string>();
        var modeOn = SmtpModeOn(cfg["smtp_mode"]);
        var passLen = Encoding.UTF8.GetByteCount(cfg["smtp_password"]);
        if (!modeOn)
        {
            issues.Add("SMTP mode is off in site config — enable smtp_mode=1 or deploy config.epc-smtp.php.");
        }

        if (PhpTrim(cfg["smtp_host"]).Length == 0)
        {
            issues.Add("smtp_host is empty.");
        }

        if (passLen < 8 && modeOn)
        {
            issues.Add("smtp_password is missing or too short (" + passLen.ToString(CultureInfo.InvariantCulture)
                + " chars) — use a Gmail App Password (16 chars) or Hostinger mailbox password.");
        }

        if (PhpTrim(cfg["from_email"]).Length == 0)
        {
            issues.Add("from_email is empty.");
        }

        var user = PhpLower(PhpTrim(cfg["smtp_username"]));
        var from = PhpLower(PhpTrim(cfg["from_email"]));
        if (user.Length > 0 && from.Length > 0 && user != from
            && cfg["smtp_host"].Contains("gmail", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("smtp_username and from_email differ — Gmail often requires them to match.");
        }

        return issues;
    }

    /// <summary>PHP <c>epc_auth_smtp_classify_error()</c>: (message, detail).</summary>
    public static (string Message, string Detail) ClassifyError(string errorInfo)
    {
        var blob = PhpLower(errorInfo + "\n");
        if (blob.Contains("could not connect", StringComparison.Ordinal) || blob.Contains("connect() failed", StringComparison.Ordinal)
            || blob.Contains("connection refused", StringComparison.Ordinal) || blob.Contains("connection timed out", StringComparison.Ordinal))
        {
            return ("SMTP connection failed — check host, port, and firewall (try ssl/465 or tls/587).", errorInfo);
        }

        if (blob.Contains("535", StringComparison.Ordinal) || blob.Contains("authentication", StringComparison.Ordinal)
            || blob.Contains("username and password not accepted", StringComparison.Ordinal) || blob.Contains("auth", StringComparison.Ordinal))
        {
            return ("SMTP authentication failed — use an app password (Gmail) or correct mailbox password (Hostinger).", errorInfo);
        }

        if (blob.Contains("password", StringComparison.Ordinal) && blob.Contains("empty", StringComparison.Ordinal))
        {
            return ("SMTP password not configured — deploy config.epc-smtp.php or update CP mail settings.", errorInfo);
        }

        return (MsgSendFallback, errorInfo);
    }

    /// <summary>PHP <c>epc_auth_otp_demo_fallback_allowed()</c>.</summary>
    public static bool DemoFallbackAllowed(string tenantKey, IReadOnlyDictionary<string, string?>? smtpFile)
    {
        var key = PhpTrim(tenantKey);
        if (key.Length == 0 || !key.StartsWith("demo_", StringComparison.Ordinal))
        {
            return false;
        }

        return smtpFile is null || !smtpFile.TryGetValue("disable_demo_otp_fallback", out var v) || PhpEmpty(v);
    }

    /// <summary>PHP <c>epc_auth_send_otp_email()</c> label rule.</summary>
    public static string MailLabel(string authMode, string? loginLabel)
        => loginLabel ?? (authMode == "storefront" ? "Shop" : "Sign in");

    public static string MailSubject(string label, string code) => label + " — sign-in code " + code;

    public static string MailHtml(string label, string code)
        => "<p>Your sign-in code for <strong>" + StorefrontSupplierLpoNotifier.H(label)
            + "</strong> is:</p><p style=\"font-size:28px;letter-spacing:6px;font-weight:700;\">"
            + code + "</p><p>Expires in 10 minutes.</p>";

    public static string MailText(string code)
        => "Your sign-in code is: " + code + "\n\nIt expires in 10 minutes.\n\nIf you did not request this, ignore this email.\n";

    public static string Fail(string message) => "{\"ok\":false,\"message\":" + OAuthStart.PhpJsonString(message) + "}";

    public static string SendFail(string message, string detail)
        => "{\"ok\":false,\"message\":" + OAuthStart.PhpJsonString(message) + ",\"smtp_detail\":" + OAuthStart.PhpJsonString(detail) + "}";

    public static string SendOk()
        => "{\"ok\":true,\"message\":" + OAuthStart.PhpJsonString(MsgSent) + ",\"expires_in\":600}";

    public static string DemoOk(string smtpHint)
        => "{\"ok\":true,\"message\":" + OAuthStart.PhpJsonString(MsgDemoReady)
            + ",\"expires_in\":600,\"demo_otp_logged\":true,\"smtp_hint\":" + OAuthStart.PhpJsonString(smtpHint) + "}";

    public static string VerifyOk(string email)
        => "{\"ok\":true,\"message\":" + OAuthStart.PhpJsonString(MsgVerified) + ",\"verified_email\":" + OAuthStart.PhpJsonString(email) + "}";
}

/// <summary>The merged SMTP settings PHP calls the effective config.</summary>
public sealed class SmtpEffectiveConfig(IReadOnlyDictionary<string, string> values, bool allowMailFallback)
{
    public IReadOnlyDictionary<string, string> Values { get; } = values;

    public bool AllowMailFallback { get; } = allowMailFallback;

    public string this[string key] => Values.TryGetValue(key, out var v) ? v : string.Empty;
}
