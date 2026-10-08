using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static EcomAE.Platform.Storefront.FreeToolsPhp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The PHP free-tools account helpers (<c>epc_free_tools_register</c>, <c>_login</c>,
/// <c>_request_reset</c>, <c>_confirm_reset</c>, <c>_account_by_token</c>, <c>_touch_account</c>, <c>_save</c>,
/// <c>_list_saves</c>, <c>_is_active</c>, <c>_request_delete</c>, <c>_confirm_delete</c>) against the platform database.
/// One instance serves one request: the connection opens on first use and is reused, like PHP's static PDO.
/// </summary>
public sealed class FreeToolsAccounts : IAsyncDisposable
{
    public delegate Task<bool> MailSender(string to, string subject, string html, CancellationToken cancellationToken);

    private const string SignInUrl = "https://www.ecomae.com/platform/free-tools";

    private static readonly ConcurrentDictionary<string, bool> SchemaReady = new(StringComparer.Ordinal);

    private readonly Func<CancellationToken, Task<DbConnection>> _open;
    private readonly MailSender _mail;
    private readonly Func<long> _clock;
    private DbConnection? _db;
    private bool _unavailable;

    public FreeToolsAccounts(Func<CancellationToken, Task<DbConnection>> open, MailSender mail, Func<long>? clock = null)
    {
        _open = open;
        _mail = mail;
        _clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static PhpArray Fail(string message) => new() { { "ok", false }, { "message", message } };

    private static string H(object? v) => StorefrontSupplierLpoNotifier.H(Str(v));

    /// <summary>PHP <c>epc_free_tools_db()</c>: null when the platform database cannot be reached.</summary>
    private async Task<DbConnection?> DbAsync(CancellationToken ct)
    {
        if (_db is not null || _unavailable)
        {
            return _db;
        }

        try
        {
            var connection = await _open(ct).ConfigureAwait(false);
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
            }

            _db = connection;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _unavailable = true;
        }

        return _db;
    }

    /// <summary>PHP <c>epc_free_tools_ensure_schema()</c>, run once per database per process.</summary>
    private static async Task EnsureSchemaAsync(DbConnection db, CancellationToken ct)
    {
        var key = db.DataSource + "/" + db.Database;
        if (SchemaReady.ContainsKey(key))
        {
            return;
        }

        await ExecAsync(db, ct,
            "CREATE TABLE IF NOT EXISTS `epc_free_tool_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `token` varchar(64) NOT NULL, "
            + "`email` varchar(190) NOT NULL, `company` varchar(190) DEFAULT NULL, `country` varchar(4) DEFAULT NULL, `time_created` int(11) DEFAULT NULL, "
            + "`time_last_seen` int(11) DEFAULT NULL, `use_count` int(11) DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `token` (`token`), KEY `email` (`email`)) "
            + "ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Free Tools tier accounts'").ConfigureAwait(false);
        await ExecAsync(db, ct,
            "CREATE TABLE IF NOT EXISTS `epc_free_tool_saves` (`id` int(11) NOT NULL AUTO_INCREMENT, `account_id` int(11) NOT NULL, `tool` varchar(32) NOT NULL, "
            + "`country` varchar(4) DEFAULT NULL, `title` varchar(190) DEFAULT NULL, `payload` mediumtext, `time_created` int(11) DEFAULT NULL, "
            + "PRIMARY KEY (`id`), KEY `account_id` (`account_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Free Tools saved results'").ConfigureAwait(false);
        try
        {
            foreach (var (probe, columns) in new[]
                     {
                         ("pass_hash", new[] { "`pass_hash` varchar(255) DEFAULT NULL" }),
                         ("time_last_login", new[] { "`time_last_login` int(11) DEFAULT NULL", "`login_count` int(11) DEFAULT 0" }),
                         ("del_code_hash", new[] { "`del_code_hash` varchar(255) DEFAULT NULL", "`del_code_expires` int(11) DEFAULT NULL" }),
                         ("reset_code_hash", new[] { "`reset_code_hash` varchar(255) DEFAULT NULL", "`reset_code_expires` int(11) DEFAULT NULL" }),
                     })
            {
                if (await RowAsync(db, ct, "SHOW COLUMNS FROM `epc_free_tool_accounts` LIKE '" + probe + "'").ConfigureAwait(false) is null)
                {
                    foreach (var column in columns)
                    {
                        await ExecAsync(db, ct, "ALTER TABLE `epc_free_tool_accounts` ADD COLUMN " + column).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        await ExecAsync(db, ct,
            "CREATE TABLE IF NOT EXISTS `epc_free_tool_settings` (`name` varchar(64) NOT NULL, `val` text, `time_updated` int(11) DEFAULT NULL, "
            + "PRIMARY KEY (`name`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Free Tools platform settings'").ConfigureAwait(false);
        SchemaReady[key] = true;
    }

    private async Task<DbConnection?> ReadyDbAsync(CancellationToken ct)
    {
        var db = await DbAsync(ct).ConfigureAwait(false);
        if (db is not null)
        {
            await EnsureSchemaAsync(db, ct).ConfigureAwait(false);
        }

        return db;
    }

    /// <summary>PHP <c>epc_free_tools_mail_shell($title, $bodyHtml)</c>.</summary>
    public static string MailShell(string title, string bodyHtml)
        => "<div style=\"font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#171717\">"
           + "<div style=\"background:#171717;color:#fff;padding:18px 22px;border-radius:12px 12px 0 0\">"
           + "<strong style=\"font-size:18px\">ECOM AE</strong> &middot; Free business tools"
           + "</div>"
           + "<div style=\"border:1px solid #e2e8f0;border-top:0;border-radius:0 0 12px 12px;padding:22px\">"
           + "<h2 style=\"margin:0 0 12px;font-size:18px\">" + H(title) + "</h2>"
           + bodyHtml
           + "<p style=\"margin-top:22px;font-size:12px;color:#64748b\">You are receiving this because you registered for ECOM AE free tools at "
           + "<a href=\"" + SignInUrl + "\" style=\"color:#0284c7\">ecomae.com</a>.</p>"
           + "</div></div>";

    private async Task<bool> SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        try
        {
            return await _mail(to, subject, html, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email) => Lower(Trim(email));

    private static bool ValidEmail(string email) => email.Length > 0 && IsEmail(email);

    private static int ByteLength(string s) => Encoding.UTF8.GetByteCount(s);

    /// <summary>PHP <c>password_hash($p, PASSWORD_DEFAULT)</c>: bcrypt <c>$2y$</c>, cost 10.</summary>
    public static string PasswordHash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(10, 'y'));

    public static bool PasswordVerify(string password, string hash)
    {
        try
        {
            return hash.Length > 0 && BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            return false;
        }
    }

    private static string SixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("000000", System.Globalization.CultureInfo.InvariantCulture);

    private static PhpArray AccountJson(string email, object? company, object? country)
        => new() { { "email", email }, { "company", company }, { "country", country } };

    public async Task<PhpArray> RegisterAsync(string email, string company, string country, string password, CancellationToken ct)
    {
        email = NormalizeEmail(email);
        if (!ValidEmail(email))
        {
            return Fail("Enter a valid email address.");
        }

        if (ByteLength(password) < 6)
        {
            return Fail("Choose a password of at least 6 characters.");
        }

        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable, please try again.");
        }

        country = Upper(Regex.Replace(country, "[^A-Za-z]", string.Empty));
        if (country.Length == 0)
        {
            country = "XX";
        }

        var now = _clock();
        var row = await RowAsync(db, ct, "SELECT * FROM `epc_free_tool_accounts` WHERE `email`=@p0 LIMIT 1", email).ConfigureAwait(false);
        var hash = PasswordHash(password);
        string token;
        if (row is not null)
        {
            if (!PhpEmpty(row.GetValueOrDefault("pass_hash")))
            {
                return new PhpArray { { "ok", false }, { "exists", true }, { "message", "This email is already registered \u2014 please log in instead." } };
            }

            token = Str(row.GetValueOrDefault("token"));
            company = company.Length > 0 ? company : Str(row.GetValueOrDefault("company"));
            country = country != "XX" ? country : Str(row.GetValueOrDefault("country"));
            await ExecAsync(db, ct,
                "UPDATE `epc_free_tool_accounts` SET `company`=@p0,`country`=@p1,`pass_hash`=@p2,`time_last_seen`=@p3,`time_last_login`=@p4,`login_count`=`login_count`+1 WHERE `id`=@p5",
                company, country, hash, now, now, Int(row.GetValueOrDefault("id"))).ConfigureAwait(false);
        }
        else
        {
            token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
            await ExecAsync(db, ct,
                "INSERT INTO `epc_free_tool_accounts` (`token`,`email`,`company`,`country`,`pass_hash`,`time_created`,`time_last_seen`,`time_last_login`,`use_count`,`login_count`) "
                + "VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,1,1)",
                token, email, company, country, hash, now, now, now).ConfigureAwait(false);
        }

        await SendAsync(
            email,
            "Your ECOM AE free tools account",
            MailShell(
                "Welcome to ECOM AE free tools",
                "<p>Hi" + (company.Length > 0 ? " " + H(company) : string.Empty) + ",</p>"
                + "<p>Your free account is ready. One login unlocks <strong>every</strong> free tool, and your results are saved so you can keep working on them daily.</p>"
                + "<p><strong>Email:</strong> " + H(email) + "<br><strong>Sign in:</strong> "
                + "<a href=\"" + SignInUrl + "\" style=\"color:#0284c7\">ecomae.com/platform/free-tools</a> &rarr; <em>Log in</em></p>"
                + "<p>Every tool localises automatically to your registered country (" + H(country) + ").</p>"),
            ct).ConfigureAwait(false);
        return new PhpArray
        {
            { "ok", true },
            { "token", token },
            { "returning", false },
            { "message", "Account created \u2014 all free tools unlocked. A confirmation email is on its way." },
            { "account", AccountJson(email, company, country) },
        };
    }

    public async Task<PhpArray> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = NormalizeEmail(email);
        if (!ValidEmail(email))
        {
            return Fail("Enter a valid email address.");
        }

        if (password.Length == 0)
        {
            return Fail("Enter your password.");
        }

        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable, please try again.");
        }

        var row = await RowAsync(db, ct, "SELECT * FROM `epc_free_tool_accounts` WHERE `email`=@p0 LIMIT 1", email).ConfigureAwait(false);
        if (row is null)
        {
            return Fail("No free account found for this email \u2014 please register.");
        }

        if (PhpEmpty(row.GetValueOrDefault("pass_hash")))
        {
            return new PhpArray { { "ok", false }, { "needs_register", true }, { "message", "This account has no password yet \u2014 please use Register to set one." } };
        }

        if (!PasswordVerify(password, Str(row.GetValueOrDefault("pass_hash"))))
        {
            return Fail("Incorrect password. Please try again.");
        }

        var now = _clock();
        await ExecAsync(db, ct,
            "UPDATE `epc_free_tool_accounts` SET `time_last_seen`=@p0,`time_last_login`=@p1,`login_count`=`login_count`+1 WHERE `id`=@p2",
            now, now, Int(row.GetValueOrDefault("id"))).ConfigureAwait(false);
        await SendAsync(
            email,
            "New sign-in to your ECOM AE free tools account",
            MailShell(
                "You just signed in",
                "<p>We noticed a sign-in to your ECOM AE free tools account (" + H(email) + ").</p>"
                + "<p>If this was you, no action is needed. If you did not sign in, please reply to this email so we can help secure your account.</p>"),
            ct).ConfigureAwait(false);
        return new PhpArray
        {
            { "ok", true },
            { "token", Str(row.GetValueOrDefault("token")) },
            { "message", "Welcome back \u2014 all your saved results are here." },
            { "account", AccountJson(email, Str(row.GetValueOrDefault("company")), Str(row.GetValueOrDefault("country"))) },
        };
    }

    public async Task<PhpArray> RequestResetAsync(string email, CancellationToken ct)
    {
        email = NormalizeEmail(email);
        var generic = new PhpArray { { "ok", true }, { "message", "If that email has an account, we have sent a reset code to it." } };
        if (!ValidEmail(email))
        {
            return Fail("Enter a valid email address.");
        }

        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable, please try again.");
        }

        var row = await RowAsync(db, ct, "SELECT * FROM `epc_free_tool_accounts` WHERE `email`=@p0 LIMIT 1", email).ConfigureAwait(false);
        if (row is null)
        {
            return generic;
        }

        var code = SixDigitCode();
        await ExecAsync(db, ct,
            "UPDATE `epc_free_tool_accounts` SET `reset_code_hash`=@p0,`reset_code_expires`=@p1 WHERE `id`=@p2",
            PasswordHash(code), _clock() + 1800, Int(row.GetValueOrDefault("id"))).ConfigureAwait(false);
        await SendAsync(
            email,
            "Reset your ECOM AE free tools password",
            MailShell(
                "Password reset code",
                "<p>We received a request to reset the password for your ECOM AE free tools account.</p>"
                + "<p>Your reset code is:</p>"
                + "<p style=\"font-size:26px;font-weight:bold;letter-spacing:4px;color:#0284c7\">" + H(code) + "</p>"
                + "<p>Enter it on the login screen with your new password. It expires in 30 minutes. "
                + "If you did not request this, ignore this email \u2014 your password stays unchanged.</p>"),
            ct).ConfigureAwait(false);
        return generic;
    }

    public async Task<PhpArray> ConfirmResetAsync(string email, string code, string password, CancellationToken ct)
    {
        email = NormalizeEmail(email);
        if (!ValidEmail(email))
        {
            return Fail("Enter a valid email address.");
        }

        if (ByteLength(password) < 6)
        {
            return Fail("Choose a new password of at least 6 characters.");
        }

        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable, please try again.");
        }

        var row = await RowAsync(db, ct, "SELECT * FROM `epc_free_tool_accounts` WHERE `email`=@p0 LIMIT 1", email).ConfigureAwait(false);
        code = Regex.Replace(code, "[^0-9]", string.Empty);
        if (row is null || PhpEmpty(row.GetValueOrDefault("reset_code_hash")) || Int(row.GetValueOrDefault("reset_code_expires")) < _clock())
        {
            return Fail("No active reset code \u2014 request a new one.");
        }

        if (!PasswordVerify(code, Str(row.GetValueOrDefault("reset_code_hash"))))
        {
            return Fail("Incorrect reset code. Please check the email and try again.");
        }

        var now = _clock();
        await ExecAsync(db, ct,
            "UPDATE `epc_free_tool_accounts` SET `pass_hash`=@p0,`reset_code_hash`=NULL,`reset_code_expires`=NULL,`time_last_seen`=@p1,`time_last_login`=@p2,`login_count`=`login_count`+1 WHERE `id`=@p3",
            PasswordHash(password), now, now, Int(row.GetValueOrDefault("id"))).ConfigureAwait(false);
        await SendAsync(
            email,
            "Your ECOM AE free tools password was changed",
            MailShell(
                "Password changed",
                "<p>The password for your ECOM AE free tools account (" + H(email) + ") was just changed.</p>"
                + "<p>If this was not you, please reply to this email right away.</p>"),
            ct).ConfigureAwait(false);
        return new PhpArray
        {
            { "ok", true },
            { "token", Str(row.GetValueOrDefault("token")) },
            { "message", "Password updated \u2014 you are now signed in." },
            { "account", AccountJson(email, Str(row.GetValueOrDefault("company")), Str(row.GetValueOrDefault("country"))) },
        };
    }

    /// <summary>PHP <c>epc_free_tools_account_by_token()</c>: the account row with native int/string values.</summary>
    public async Task<Dictionary<string, object?>?> AccountByTokenAsync(string token, CancellationToken ct)
    {
        token = Regex.Replace(Lower(token), "[^a-f0-9]", string.Empty);
        if (token.Length == 0)
        {
            return null;
        }

        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        return db is null
            ? null
            : await RowAsync(db, ct, "SELECT * FROM `epc_free_tool_accounts` WHERE `token`=@p0 LIMIT 1", token).ConfigureAwait(false);
    }

    public async Task TouchAccountAsync(long accountId, CancellationToken ct)
    {
        var db = await DbAsync(ct).ConfigureAwait(false);
        if (db is null || accountId <= 0)
        {
            return;
        }

        await EnsureSchemaAsync(db, ct).ConfigureAwait(false);
        await ExecAsync(db, ct, "UPDATE `epc_free_tool_accounts` SET `time_last_seen`=@p0,`use_count`=`use_count`+1 WHERE `id`=@p1", _clock(), accountId).ConfigureAwait(false);
    }

    public async Task<PhpArray> SaveAsync(long accountId, string tool, string country, string title, PhpArray payload, CancellationToken ct)
    {
        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable.");
        }

        await ExecAsync(db, ct,
            "INSERT INTO `epc_free_tool_saves` (`account_id`,`tool`,`country`,`title`,`payload`,`time_created`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5)",
            accountId, tool, country, title, JsonEncode(payload, escapeSlashes: true), _clock()).ConfigureAwait(false);
        var id = await ScalarAsync(db, ct, "SELECT LAST_INSERT_ID()").ConfigureAwait(false);
        return new PhpArray { { "ok", true }, { "id", Int(id) } };
    }

    public async Task<PhpArray> ListSavesAsync(long accountId, CancellationToken ct, long limit = 50)
    {
        var list = new PhpArray();
        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return list;
        }

        await using var cmd = Command(db,
            "SELECT `id`,`tool`,`country`,`title`,`time_created` FROM `epc_free_tool_saves` WHERE `account_id`=@p0 ORDER BY `id` DESC LIMIT @p1",
            accountId, limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new PhpArray();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = Native(reader.GetValue(i));
            }

            list.Append(row);
        }

        return list;
    }

    /// <summary>PHP <c>epc_free_tools_is_active($tool)</c>: false only when <c>disabled_tools</c> lists the tool.</summary>
    public async Task<bool> IsActiveAsync(string tool, CancellationToken ct)
    {
        var db = await ReadyDbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return true;
        }

        var val = await ScalarAsync(db, ct, "SELECT `val` FROM `epc_free_tool_settings` WHERE `name`='disabled_tools' LIMIT 1").ConfigureAwait(false);
        if (PhpEmpty(val))
        {
            return true;
        }

        if (PhpArray.JsonDecode(Str(val)) is not PhpArray disabled)
        {
            return true;
        }

        return !disabled.Values.Any(k => Str(k) == tool);
    }

    public async Task<PhpArray> RequestDeleteAsync(string token, CancellationToken ct)
    {
        var acc = await AccountByTokenAsync(token, ct).ConfigureAwait(false);
        if (acc is null)
        {
            return Fail("Please sign in first.");
        }

        var db = await DbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable.");
        }

        var code = SixDigitCode();
        await ExecAsync(db, ct,
            "UPDATE `epc_free_tool_accounts` SET `del_code_hash`=@p0,`del_code_expires`=@p1 WHERE `id`=@p2",
            PasswordHash(code), _clock() + 1800, Int(acc.GetValueOrDefault("id"))).ConfigureAwait(false);
        var email = Str(acc.GetValueOrDefault("email"));
        var sent = await SendAsync(
            email,
            "Confirm deletion of your ECOM AE free tools data",
            MailShell(
                "Confirm data deletion",
                "<p>We received a request to delete your ECOM AE free tools account and all saved data.</p>"
                + "<p>Your confirmation cross-code is:</p>"
                + "<p style=\"font-size:26px;font-weight:bold;letter-spacing:4px;color:#dc2626\">" + H(code) + "</p>"
                + "<p>Enter this code in the tool to permanently delete your data. It expires in 30 minutes. "
                + "<strong>If you did not request this, ignore this email \u2014 nothing will be deleted.</strong></p>"),
            ct).ConfigureAwait(false);
        return new PhpArray
        {
            { "ok", true },
            { "sent", sent },
            {
                "message", sent
                    ? "We emailed a confirmation cross-code to " + email + ". Enter it to confirm deletion."
                    : "Could not send the confirmation email. Please try again later."
            },
        };
    }

    public async Task<PhpArray> ConfirmDeleteAsync(string token, string code, CancellationToken ct)
    {
        var acc = await AccountByTokenAsync(token, ct).ConfigureAwait(false);
        if (acc is null)
        {
            return Fail("Please sign in first.");
        }

        code = Regex.Replace(code, "[^0-9]", string.Empty);
        if (PhpEmpty(acc.GetValueOrDefault("del_code_hash")) || Int(acc.GetValueOrDefault("del_code_expires")) < _clock())
        {
            return Fail("No active code \u2014 request a new confirmation code.");
        }

        if (!PasswordVerify(code, Str(acc.GetValueOrDefault("del_code_hash"))))
        {
            return Fail("Incorrect code. Please check the email and try again.");
        }

        var db = await DbAsync(ct).ConfigureAwait(false);
        if (db is null)
        {
            return Fail("Service unavailable.");
        }

        var id = Int(acc.GetValueOrDefault("id"));
        await ExecAsync(db, ct, "DELETE FROM `epc_free_tool_saves` WHERE `account_id`=@p0", id).ConfigureAwait(false);
        await ExecAsync(db, ct, "DELETE FROM `epc_free_tool_accounts` WHERE `id`=@p0", id).ConfigureAwait(false);
        await SendAsync(
            Str(acc.GetValueOrDefault("email")),
            "Your ECOM AE free tools data has been deleted",
            MailShell(
                "Data deleted",
                "<p>Your ECOM AE free tools account and all saved results have been permanently deleted, as you confirmed.</p>"
                + "<p>You are welcome back any time \u2014 just register again at "
                + "<a href=\"" + SignInUrl + "\" style=\"color:#0284c7\">ecomae.com/platform/free-tools</a>.</p>"),
            ct).ConfigureAwait(false);
        return new PhpArray { { "ok", true }, { "message", "Your account and all saved data have been permanently deleted." } };
    }

    /// <summary>PHP <c>empty($v)</c> for a database value.</summary>
    private static bool PhpEmpty(object? v) => v switch
    {
        null => true,
        string s => s.Length == 0 || s == "0",
        long l => l == 0,
        double d => d == 0,
        _ => false,
    };

    /// <summary>PDO (PHP 8.1+) values: ints as ints, everything else as strings, NULL as null.</summary>
    private static object? Native(object? v) => v switch
    {
        null or DBNull => null,
        sbyte or byte or short or ushort or int or uint or long => Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture),
        ulong u => (long)u,
        string s => s,
        byte[] b => Encoding.UTF8.GetString(b),
        _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
    };

    private static DbCommand Command(DbConnection db, string sql, params object?[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            p.Value = args[i] ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        return cmd;
    }

    private static async Task ExecAsync(DbConnection db, CancellationToken ct, string sql, params object?[] args)
    {
        await using var cmd = Command(db, sql, args);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(DbConnection db, CancellationToken ct, string sql, params object?[] args)
    {
        await using var cmd = Command(db, sql, args);
        return Native(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    private static async Task<Dictionary<string, object?>?> RowAsync(DbConnection db, CancellationToken ct, string sql, params object?[] args)
    {
        await using var cmd = Command(db, sql, args);
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = Native(reader.GetValue(i));
        }

        return row;
    }
}
