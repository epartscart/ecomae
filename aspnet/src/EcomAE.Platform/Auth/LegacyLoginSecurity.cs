using System.Data.Common;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP <c>content/users/epc_login_rate_limit.php</c> (failed attempts per IP and per contact in
/// <c>epc_login_attempts</c>; 10 failures in 15 minutes block the admin login) and
/// <c>content/users/epc_password_upgrade.php</c> (a legacy md5 hash is replaced by bcrypt cost 12 after a
/// successful login). Storage failures never block a login, as in PHP.
/// </summary>
public static class LegacyLoginSecurity
{
    public const int MaxAttempts = 10;
    public const int WindowSeconds = 900;

    public sealed record RateLimitResult(bool Blocked, int Remaining, int RetryAfter)
    {
        /// <summary>The CP authentication plugin message for a blocked attempt.</summary>
        public int WaitMinutes => Math.Max(1, (int)Math.Ceiling(RetryAfter / 60.0));

        public string Message => "Too many failed attempts. Please wait " + WaitMinutes + " minutes before trying again.";
    }

    /// <summary>PHP <c>epc_login_rate_limit_client_ip()</c>: CF-Connecting-IP, then the first X-Forwarded-For hop, then the remote address.</summary>
    public static string ClientIp(HttpContext? context)
    {
        if (context is null)
        {
            return "0.0.0.0";
        }

        var cloudflare = context.Request.Headers["CF-Connecting-IP"].ToString();
        if (cloudflare.Length > 0)
        {
            return cloudflare;
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        if (forwarded.Length > 0)
        {
            return forwarded.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
    }

    public static async Task EnsureTableAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_login_attempts` (
              `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
              `ip_address` VARCHAR(45) NOT NULL DEFAULT '',
              `email` VARCHAR(255) NOT NULL DEFAULT '',
              `attempted_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
              `success` TINYINT(1) NOT NULL DEFAULT 0,
              INDEX `idx_ip_email` (`ip_address`, `email`, `attempted_at`),
              INDEX `idx_cleanup` (`attempted_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);

    public static async Task RecordAsync(DbConnection connection, string ip, string contact, bool success, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_login_attempts` (`ip_address`, `email`, `attempted_at`, `success`) VALUES (?, ?, NOW(), ?)"),
                cancellationToken,
                ip,
                Key(contact),
                success ? 1 : 0).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    public static async Task<RateLimitResult> CheckAsync(DbConnection connection, string ip, string contact, CancellationToken cancellationToken, int maxAttempts = MaxAttempts, int windowSeconds = WindowSeconds)
    {
        await EnsureTableAsync(connection, cancellationToken).ConfigureAwait(false);
        var key = Key(contact);
        try
        {
            var ipFails = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_login_attempts` WHERE `ip_address` = ? AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND)"),
                cancellationToken,
                ip,
                windowSeconds).ConfigureAwait(false);
            var contactFails = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_login_attempts` WHERE `email` = ? AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND)"),
                cancellationToken,
                key,
                windowSeconds).ConfigureAwait(false);
            var fails = (int)Math.Max(ipFails, contactFails);
            var remaining = Math.Max(0, maxAttempts - fails);
            if (fails < maxAttempts)
            {
                return new RateLimitResult(false, remaining, 0);
            }

            var elapsed = await ErpDb.ScalarAsync(
                connection,
                null,
                ErpDb.Positional("SELECT TIMESTAMPDIFF(SECOND, MIN(`attempted_at`), NOW()) FROM `epc_login_attempts` WHERE (`ip_address` = ? OR `email` = ?) AND `success` = 0 AND `attempted_at` > DATE_SUB(NOW(), INTERVAL ? SECOND)"),
                cancellationToken,
                ip,
                key,
                windowSeconds).ConfigureAwait(false);
            var retryAfter = elapsed is null or DBNull ? 0 : Math.Max(0, windowSeconds - Convert.ToInt32(elapsed, System.Globalization.CultureInfo.InvariantCulture));
            return new RateLimitResult(true, remaining, retryAfter);
        }
        catch (DbException)
        {
            return new RateLimitResult(false, maxAttempts, 0);
        }
    }

    public static async Task ClearAsync(DbConnection connection, string ip, string contact, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_login_attempts` WHERE `ip_address` = ? AND `email` = ?"),
                cancellationToken,
                ip,
                Key(contact)).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP <c>epc_login_rate_limit_cleanup()</c>: attempts older than 24 hours are removed.</summary>
    public static Task CleanupAsync(DbConnection connection, CancellationToken cancellationToken)
        => ErpDb.TryExecuteAsync(connection, "DELETE FROM `epc_login_attempts` WHERE `attempted_at` < DATE_SUB(NOW(), INTERVAL 24 HOUR)", cancellationToken);

    /// <summary>PHP <c>epc_password_hash()</c>: bcrypt cost 12 with PHP's <c>$2y$</c> prefix.</summary>
    public static string HashPassword(string plain)
        => BCrypt.Net.BCrypt.HashPassword(plain, BCrypt.Net.BCrypt.GenerateSalt(12, 'y'));

    /// <summary>PHP <c>epc_password_upgrade_if_needed()</c>: <c>true</c> when a legacy md5 hash was replaced.</summary>
    public static async Task<bool> UpgradePasswordIfNeededAsync(DbConnection connection, long userId, string plain, string storedHash, CancellationToken cancellationToken)
    {
        if (!LegacyPasswordVerifier.IsLegacyMd5(storedHash))
        {
            return false;
        }

        var hash = HashPassword(plain);
        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `users` SET `password` = ? WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            hash,
            userId).ConfigureAwait(false);
        if (updated == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `users` SET `password` = ? WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                hash,
                userId).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>PHP 8.2 <c>strtolower(trim($email))</c>: only ASCII letters are lowered.</summary>
    private static string Key(string contact)
    {
        var trimmed = contact.Trim(' ', '\t', '\n', '\r', '\0', '\v');
        return string.Create(trimmed.Length, trimmed, static (span, value) =>
        {
            for (var i = 0; i < value.Length; i++)
            {
                span[i] = value[i] is >= 'A' and <= 'Z' ? (char)(value[i] + 32) : value[i];
            }
        });
    }
}
