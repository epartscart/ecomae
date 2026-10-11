using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-helm auth SMTP. PHP identifiers kept for the inventory:
/// <c>epc_auth_smtp_file_config</c>, <c>epc_auth_smtp_tenant_overlay</c>,
/// <c>epc_auth_smtp_effective_config</c>, <c>epc_auth_smtp_recommended_presets</c>,
/// <c>epc_auth_smtp_validate_input</c>, <c>epc_auth_smtp_write_file_config</c>,
/// <c>epc_auth_smtp_apply_to_mailer</c>, <c>epc_auth_smtp_diagnose</c>,
/// <c>epc_auth_smtp_classify_error</c>, <c>epc_auth_smtp_send_html</c>,
/// <c>epc_auth_otp_demo_fallback_allowed</c>, <c>epc_auth_otp_store_operator_code</c>,
/// <c>epc_auth_otp_operator_lookup</c>.
/// GET never mints a session cookie. Tenant overlay stays on that tenant.
/// </summary>
public static class PhpPlanQ1Helm
{
    public const string AuthSmtpPath = "content/general_pages/epc_auth_smtp.php";

    public sealed class DpConfig
    {
        public string smtp_mode { get; set; } = "0";
        public string smtp_host { get; set; } = "";
        public string smtp_port { get; set; } = "";
        public string smtp_encryption { get; set; } = "";
        public string smtp_username { get; set; } = "";
        public string smtp_password { get; set; } = "";
        public string from_email { get; set; } = "";
        public string from_name { get; set; } = "";
    }

    public sealed class MailerBag
    {
        public string Host { get; set; } = "";
        public int Port { get; set; }
        public string SMTPSecure { get; set; } = "";
        public bool SMTPAuth { get; set; }
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Sender { get; set; } = "";
        public string ErrorInfo { get; set; } = "";
    }

    public sealed class OtpRow
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string TenantKey { get; set; } = "";
        public string ContextJson { get; set; } = "";
        public int CreatedAt { get; set; }
    }

    public static DpConfig Config { get; set; } = new();
    public static Dictionary<string, string> SmtpFile { get; set; } = new(StringComparer.Ordinal);
    public static bool SmtpFileExists { get; set; }
    public static Dictionary<string, string> LocalFile { get; set; } = new(StringComparer.Ordinal);
    public static bool LocalFileExists { get; set; }
    public static bool IsSuperCp { get; set; }
    public static Dictionary<string, object?> SiteSettings { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> Clock { get; set; } = () => 1_760_083_200;
    public static bool MailerExists { get; set; }
    public static bool MailOk { get; set; }
    public static string SmtpFail { get; set; } = "";
    public static List<OtpRow> Otps { get; } = [];
    public static int NextOtpId { get; set; } = 1;

    public static void Reset()
    {
        Config = new();
        SmtpFile = new(StringComparer.Ordinal);
        SmtpFileExists = false;
        LocalFile = new(StringComparer.Ordinal);
        LocalFileExists = false;
        IsSuperCp = false;
        SiteSettings = new(StringComparer.Ordinal);
        Clock = () => 1_760_083_200;
        MailerExists = false;
        MailOk = false;
        SmtpFail = "";
        Otps.Clear();
        NextOtpId = 1;
    }

    public static Dictionary<string, object?> EpcAuthSmtpRecommendedPresets()
        => new(StringComparer.Ordinal)
        {
            ["gmail_tls"] = Preset("Gmail (TLS 587)", "smtp.gmail.com", "587", "tls",
                "username must equal from_email; password = 16-char App Password."),
            ["gmail_ssl"] = Preset("Gmail (SSL 465)", "smtp.gmail.com", "465", "ssl",
                "username must equal from_email; password = 16-char App Password."),
            ["hostinger_ssl"] = Preset("Hostinger mailbox (SSL 465)", "smtp.hostinger.com", "465", "ssl",
                "Use the VPS / CloudPanel mailbox password (e.g. hello@ecomae.com)."),
            ["hostinger_tls"] = Preset("Hostinger mailbox (TLS 587)", "smtp.hostinger.com", "587", "tls",
                "Use the VPS / CloudPanel mailbox password (e.g. hello@ecomae.com)."),
        };

    public static Dictionary<string, object?> EpcAuthSmtpFileConfig()
        => SmtpFileExists ? Copy(SmtpFile) : new(StringComparer.Ordinal);

    public static Dictionary<string, object?> EpcAuthSmtpTenantOverlay()
    {
        if (IsSuperCp)
        {
            return new(StringComparer.Ordinal);
        }

        var settings = SiteSettings;
        var integrations = settings.TryGetValue("integrations", out var integ) ? integ : null;
        var smtp = Field(AsDict(integrations), "smtp");
        var smtpMap = AsDict(smtp);
        if (smtpMap.Count == 0 || PhpEmpty(Field(smtpMap, "use_tenant_smtp")))
        {
            return new(StringComparer.Ordinal);
        }

        var outMap = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in new[] { "smtp_host", "smtp_port", "smtp_encryption", "smtp_username", "smtp_password", "from_name", "from_email" })
        {
            var value = Field(smtpMap, key);
            if (!PhpEmpty(value))
            {
                outMap[key] = PhpString(value);
            }
        }

        return outMap;
    }

    public static Dictionary<string, object?> EpcAuthSmtpEffectiveConfig()
    {
        var dp = Config;
        var baseMap = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["smtp_mode"] = PhpString(dp.smtp_mode),
            ["smtp_host"] = PhpString(dp.smtp_host),
            ["smtp_port"] = PhpString(dp.smtp_port),
            ["smtp_encryption"] = PhpString(dp.smtp_encryption),
            ["smtp_username"] = PhpString(dp.smtp_username),
            ["smtp_password"] = PhpString(dp.smtp_password),
            ["from_email"] = PhpString(dp.from_email),
            ["from_name"] = PhpString(dp.from_name),
            ["allow_mail_fallback"] = false,
        };
        if (LocalFileExists)
        {
            foreach (var pair in LocalFile)
            {
                if (baseMap.ContainsKey(pair.Key))
                {
                    baseMap[pair.Key] = pair.Value;
                }
            }
        }

        var source = "config.php";
        if (LocalFileExists)
        {
            source = "config.php + config.local.php";
        }

        var overlay = EpcAuthSmtpFileConfig();
        if (overlay.Count > 0)
        {
            source = "config.epc-smtp.php";
            ApplyOverlay(baseMap, overlay, useEmpty: false);
        }

        var tenant = EpcAuthSmtpTenantOverlay();
        if (tenant.Count > 0)
        {
            source = "tenant integrations (site_settings)";
            ApplyOverlay(baseMap, tenant, useEmpty: false);
        }

        baseMap["_source"] = source;
        baseMap["_epc_smtp_file"] = SmtpFileExists;
        return baseMap;
    }

    public static Dictionary<string, object?> EpcAuthSmtpValidateInput(Dictionary<string, object?> input)
    {
        var errors = new List<object?>();
        var warnings = new List<object?>();
        var modeOn = !PhpEmpty(Field(input, "smtp_mode"));
        var host = PhpLower(PhpTrim(Field(input, "smtp_host")));
        var port = PhpTrim(Field(input, "smtp_port"));
        var user = PhpLower(PhpTrim(Field(input, "smtp_username")));
        var from = PhpLower(PhpTrim(Field(input, "from_email")));
        var enc = PhpLower(PhpTrim(Field(input, "smtp_encryption")));
        var pass = PhpString(Field(input, "smtp_password"));
        var passLen = Encoding.UTF8.GetByteCount(pass);
        if (from.Length == 0 || !PhpValidEmail(from))
        {
            errors.Add("from_email must be a valid email address.");
        }

        if (modeOn)
        {
            if (host.Length == 0)
            {
                errors.Add("smtp_host is required when SMTP mode is on.");
            }

            if (port.Length == 0 || !PhpCtypeDigit(port))
            {
                errors.Add("smtp_port must be a number (587 for TLS, 465 for SSL).");
            }

            if (enc is not ("tls" or "ssl" or ""))
            {
                errors.Add("smtp_encryption must be tls, ssl, or empty.");
            }

            if (pass.Length > 0 && passLen < 8)
            {
                errors.Add("smtp_password looks too short (" + passLen.ToString(CultureInfo.InvariantCulture) + " chars).");
            }
        }

        var isGmail = host.Contains("gmail", StringComparison.Ordinal) || host.Contains("googlemail", StringComparison.Ordinal);
        if (isGmail)
        {
            if (pass.Length > 0 && passLen != 16)
            {
                warnings.Add("Gmail App Passwords are normally 16 characters (you entered " + passLen.ToString(CultureInfo.InvariantCulture)
                    + "). Generate one at myaccount.google.com → Security → 2-Step Verification → App passwords.");
            }

            if (user.Length > 0 && from.Length > 0 && user != from)
            {
                warnings.Add("For Gmail, smtp_username should equal from_email (" + from + ").");
            }

            if ((enc == "tls" && port != "587") || (enc == "ssl" && port != "465"))
            {
                warnings.Add("Gmail expects TLS on port 587 or SSL on port 465.");
            }
        }

        if (user.Length == 0 && modeOn)
        {
            warnings.Add("smtp_username is empty — most providers require authentication.");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["errors"] = errors,
            ["warnings"] = warnings
        };
    }

    public static Dictionary<string, object?> EpcAuthSmtpWriteFileConfig(Dictionary<string, object?> input)
    {
        var valid = EpcAuthSmtpValidateInput(input);
        var errors = (List<object?>)valid["errors"]!;
        var warnings = (List<object?>)valid["warnings"]!;
        if (errors.Count > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = string.Join(" ", errors.Select(e => Convert.ToString(e, CultureInfo.InvariantCulture))),
                ["path"] = "SMTP_FILE",
                ["warnings"] = warnings
            };
        }

        var existing = EpcAuthSmtpFileConfig();
        var pass = PhpString(Field(input, "smtp_password"));
        if (pass.Length == 0 && existing.TryGetValue("smtp_password", out var kept) && PhpString(kept).Length > 0)
        {
            pass = PhpString(kept);
        }

        var written = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smtp_mode"] = !PhpEmpty(Field(input, "smtp_mode")) ? "1" : "0",
            ["smtp_host"] = PhpTrim(Field(input, "smtp_host")),
            ["smtp_port"] = PhpTrim(Field(input, "smtp_port")),
            ["smtp_encryption"] = PhpLower(PhpTrim(Field(input, "smtp_encryption"))),
            ["smtp_username"] = PhpTrim(Field(input, "smtp_username")),
            ["smtp_password"] = pass,
            ["from_email"] = PhpTrim(Field(input, "from_email")),
            ["from_name"] = PhpTrim(Field(input, "from_name")),
            ["allow_mail_fallback"] = !PhpEmpty(Field(input, "allow_mail_fallback")) ? "1" : "0",
            ["disable_demo_otp_fallback"] = !PhpEmpty(Field(input, "disable_demo_otp_fallback")) ? "1" : "0",
        };
        SmtpFile = written.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        SmtpFileExists = true;
        var msg = "SMTP settings saved to config.epc-smtp.php.";
        if (pass.Length == 0)
        {
            msg += " Note: no password is stored — set one (Gmail App Password or mailbox password) unless using mail() fallback.";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg,
            ["path"] = "SMTP_FILE",
            ["warnings"] = warnings,
            ["mode"] = 416,
            ["content"] = BuildSmtpFile("ISO")
        };
    }

    public static void EpcAuthSmtpApplyToMailer(MailerBag mail, Dictionary<string, object?> cfg)
    {
        var modeOn = SmtpModeOn(Field(cfg, "smtp_mode"));
        if (modeOn && PhpTrim(Field(cfg, "smtp_host")).Length > 0)
        {
            mail.Host = PhpString(Field(cfg, "smtp_host"));
            mail.Port = PhpInt(Field(cfg, "smtp_port"));
            var enc = PhpLower(PhpTrim(Field(cfg, "smtp_encryption")));
            mail.SMTPSecure = enc is "ssl" or "tls" ? enc : "";
            var user = PhpTrim(Field(cfg, "smtp_username"));
            var pass = PhpString(Field(cfg, "smtp_password"));
            if (user.Length > 0)
            {
                mail.SMTPAuth = true;
                mail.Username = user;
                mail.Password = pass;
            }
        }

        var from = PhpTrim(Field(cfg, "from_email"));
        if (from.Length > 0)
        {
            mail.Sender = from;
        }
    }

    public static Dictionary<string, object?> EpcAuthSmtpDiagnose()
    {
        var cfg = EpcAuthSmtpEffectiveConfig();
        var issues = new List<object?>();
        var modeOn = SmtpModeOn(Field(cfg, "smtp_mode"));
        var passLen = Encoding.UTF8.GetByteCount(PhpString(Field(cfg, "smtp_password")));
        if (!modeOn)
        {
            issues.Add("SMTP mode is off in site config — enable smtp_mode=1 or deploy config.epc-smtp.php.");
        }

        if (PhpTrim(Field(cfg, "smtp_host")).Length == 0)
        {
            issues.Add("smtp_host is empty.");
        }

        if (passLen < 8 && modeOn)
        {
            issues.Add("smtp_password is missing or too short (" + passLen.ToString(CultureInfo.InvariantCulture)
                + " chars) — use a Gmail App Password (16 chars) or Hostinger mailbox password.");
        }

        if (PhpTrim(Field(cfg, "from_email")).Length == 0)
        {
            issues.Add("from_email is empty.");
        }

        var user = PhpLower(PhpTrim(Field(cfg, "smtp_username")));
        var from = PhpLower(PhpTrim(Field(cfg, "from_email")));
        if (user.Length > 0 && from.Length > 0 && user != from
            && PhpString(Field(cfg, "smtp_host")).Contains("gmail", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("smtp_username and from_email differ — Gmail often requires them to match.");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = issues.Count == 0,
            ["issues"] = issues,
            ["source"] = PhpString(Field(cfg, "_source")),
            ["smtp_mode"] = modeOn,
            ["password_length"] = passLen,
            ["host"] = PhpString(Field(cfg, "smtp_host")),
            ["port"] = PhpString(Field(cfg, "smtp_port")),
            ["encryption"] = PhpString(Field(cfg, "smtp_encryption")),
            ["username"] = PhpString(Field(cfg, "smtp_username")),
            ["from_email"] = PhpString(Field(cfg, "from_email")),
            ["from_name"] = PhpString(Field(cfg, "from_name")),
            ["epc_smtp_file"] = !PhpEmpty(Field(cfg, "_epc_smtp_file")),
            ["allow_mail_fallback"] = !PhpEmpty(Field(cfg, "allow_mail_fallback")),
        };
    }

    public static Dictionary<string, object?> EpcAuthSmtpClassifyError(string errorInfo, IReadOnlyList<string>? debugLines = null)
    {
        var blob = PhpLower(errorInfo + "\n" + string.Join("\n", debugLines ?? []));
        if (blob.Contains("could not connect", StringComparison.Ordinal) || blob.Contains("connect() failed", StringComparison.Ordinal)
            || blob.Contains("connection refused", StringComparison.Ordinal) || blob.Contains("connection timed out", StringComparison.Ordinal))
        {
            return Fail("SMTP connection failed — check host, port, and firewall (try ssl/465 or tls/587).", errorInfo);
        }

        if (blob.Contains("535", StringComparison.Ordinal) || blob.Contains("authentication", StringComparison.Ordinal)
            || blob.Contains("username and password not accepted", StringComparison.Ordinal) || blob.Contains("auth", StringComparison.Ordinal))
        {
            return Fail("SMTP authentication failed — use an app password (Gmail) or correct mailbox password (Hostinger).", errorInfo);
        }

        if (blob.Contains("password", StringComparison.Ordinal) && blob.Contains("empty", StringComparison.Ordinal))
        {
            return Fail("SMTP password not configured — deploy config.epc-smtp.php or update CP mail settings.", errorInfo);
        }

        return Fail("Could not send email — check SMTP settings in Control Panel", errorInfo);
    }

    public static Dictionary<string, object?> EpcAuthSmtpSendHtml(string to, string subject, string html, string altBody = "")
    {
        var diag = EpcAuthSmtpDiagnose();
        if (!(bool)diag["ok"]!)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = string.Join(" ", ((List<object?>)diag["issues"]!).Select(i => Convert.ToString(i, CultureInfo.InvariantCulture))),
                ["detail"] = "precheck",
                ["transport"] = "none"
            };
        }

        var cfg = EpcAuthSmtpEffectiveConfig();
        if (!MailerExists)
        {
            if (!PhpEmpty(Field(cfg, "allow_mail_fallback")) && MailOk)
            {
                return OkSend("Sent via PHP mail()", "", "mail");
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Mailer library missing on server",
                ["detail"] = "MAILER",
                ["transport"] = "none"
            };
        }

        if (SmtpFail.Length == 0)
        {
            return OkSend("Sent via SMTP", "", "smtp");
        }

        var classified = EpcAuthSmtpClassifyError(SmtpFail);
        if (!PhpEmpty(Field(cfg, "allow_mail_fallback")) && MailOk)
        {
            return OkSend("Sent via PHP mail() after SMTP failed", SmtpFail, "mail");
        }

        return classified;
    }

    public static bool EpcAuthOtpDemoFallbackAllowed(string tenantKey)
    {
        tenantKey = tenantKey.Trim();
        if (tenantKey.Length == 0 || !tenantKey.StartsWith("demo_", StringComparison.Ordinal))
        {
            return false;
        }

        var overlay = EpcAuthSmtpFileConfig();
        return PhpEmpty(Field(overlay, "disable_demo_otp_fallback"));
    }

    public static void EpcAuthOtpStoreOperatorCode(int otpId, string code)
    {
        if (otpId <= 0)
        {
            return;
        }

        var row = Otps.FirstOrDefault(r => r.Id == otpId);
        if (row is null)
        {
            return;
        }

        var ctx = DecodeObject(row.ContextJson);
        ctx["_operator_otp"] = code;
        ctx["_operator_otp_at"] = (int)Clock();
        row.ContextJson = PhpJsonEncode(ctx);
    }

    public static Dictionary<string, object?> EpcAuthOtpOperatorLookup(string email)
    {
        email = PhpLower(email.Trim());
        if (email.Length == 0 || !PhpValidEmail(email))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Valid email required"
            };
        }

        foreach (var row in Otps.Where(r => r.Email == email).OrderByDescending(r => r.Id).Take(5))
        {
            var ctx = DecodeObject(row.ContextJson);
            if (PhpEmpty(Field(ctx, "_operator_otp")))
            {
                continue;
            }

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["email"] = email,
                ["code"] = PhpString(Field(ctx, "_operator_otp")),
                ["logged_at"] = PhpInt(Field(ctx, "_operator_otp_at") ?? row.CreatedAt),
                ["tenant_key"] = row.TenantKey
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = "No operator-logged OTP for this email (SMTP may have succeeded or address not used on a demo tenant)."
        };
    }

    public static string BuildSmtpFile(string saved)
    {
        var sb = new StringBuilder();
        sb.Append("<?php\n/**\n * Platform SMTP for auth OTP — written by Super CP → Modern auth settings.\n");
        sb.Append(" * Do NOT commit to git. Overrides config.php / config.local.php for OTP mail.\n");
        sb.Append(" * Last saved: ").Append(saved).Append("\n */\nreturn array(\n");
        foreach (var pair in SmtpFile)
        {
            sb.Append('\t').Append(PhpVarExport(pair.Key)).Append(" => ").Append(PhpVarExport(pair.Value)).Append(",\n");
        }

        sb.Append(");\n");
        return sb.ToString();
    }

    private static Dictionary<string, object?> Preset(string label, string host, string port, string enc, string note)
        => new(StringComparer.Ordinal)
        {
            ["label"] = label,
            ["smtp_host"] = host,
            ["smtp_port"] = port,
            ["smtp_encryption"] = enc,
            ["note"] = note
        };

    private static void ApplyOverlay(Dictionary<string, object?> dest, Dictionary<string, object?> overlay, bool useEmpty)
    {
        foreach (var pair in overlay)
        {
            if (pair.Key == "allow_mail_fallback")
            {
                dest["allow_mail_fallback"] = !PhpEmpty(pair.Value);
                continue;
            }

            if (dest.ContainsKey(pair.Key) && pair.Value is not null && PhpString(pair.Value) != "")
            {
                dest[pair.Key] = PhpString(pair.Value);
            }
        }
    }

    private static Dictionary<string, object?> Fail(string message, string detail)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = message,
            ["detail"] = detail,
            ["transport"] = "smtp"
        };

    private static Dictionary<string, object?> OkSend(string message, string detail, string transport)
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = message,
            ["detail"] = detail,
            ["transport"] = transport
        };

    private static Dictionary<string, object?> Copy(Dictionary<string, string> src)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in src)
        {
            map[pair.Key] = pair.Value;
        }

        return map;
    }

    private static Dictionary<string, object?> AsDict(object? value)
    {
        if (value is Dictionary<string, object?> map)
        {
            return map;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string PhpTrim(object? value)
        => PhpString(value).Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    private static string PhpLower(string value)
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

    private static bool PhpCtypeDigit(string value)
        => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static bool PhpValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email && email.Contains('@') && email.IndexOf('.', email.IndexOf('@')) > email.IndexOf('@');
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool SmtpModeOn(object? mode)
        => PhpInt(mode) == 1 || mode is true || PhpString(mode) == "1";

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
        }

        var text = PhpString(value);
        if (text.Length == 0)
        {
            return 0;
        }

        var n2 = 0;
        var sign = 1;
        var p = 0;
        while (p < text.Length && char.IsWhiteSpace(text[p]))
        {
            p++;
        }

        if (p < text.Length && (text[p] == '+' || text[p] == '-'))
        {
            sign = text[p] == '-' ? -1 : 1;
            p++;
        }

        var any = false;
        while (p < text.Length && char.IsAsciiDigit(text[p]))
        {
            any = true;
            n2 = (n2 * 10) + (text[p] - '0');
            p++;
        }

        return any ? sign * n2 : 0;
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            "" => true,
            "0" => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpVarExport(string value)
        => "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal) + "'";

    private static string PhpJsonEncode(object? value)
        => JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }).Replace("/", "\\/", StringComparison.Ordinal);

    private static Dictionary<string, object?> DecodeObject(string json)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                map[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number when prop.Value.TryGetInt32(out var n) => n,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }
        }
        catch (JsonException)
        {
        }

        return map;
    }
}
