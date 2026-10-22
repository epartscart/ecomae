using System.Data.Common;
using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Auth;

/// <summary>
/// TOTP (RFC 6238, HMAC-SHA1, 30-second steps, ±1 window) second-factor for CP/ERP/BOS admin logins.
/// Per-user enrollment lives in <c>epc_totp_enrollments</c> on the same tenant DB the login used.
/// </summary>
public static class LegacyTotpService
{
    private const string Table = "epc_totp_enrollments";
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public static async Task EnsureTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"CREATE TABLE IF NOT EXISTS {Table} (" +
            "user_id BIGINT NOT NULL PRIMARY KEY," +
            "contact VARCHAR(255) NOT NULL DEFAULT ''," +
            "secret_base32 VARCHAR(64) NOT NULL," +
            "enabled TINYINT NOT NULL DEFAULT 0," +
            "created_at BIGINT NOT NULL," +
            "confirmed_at BIGINT NOT NULL DEFAULT 0" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<bool> IsEnabledAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var value = await ScalarAsync(connection,
            $"SELECT enabled FROM {Table} WHERE user_id = @u LIMIT 1",
            cancellationToken, ("@u", userId)).ConfigureAwait(false);
        return value is not null && Convert.ToInt32(value) == 1;
    }

    /// <summary>Creates (or replaces) an unconfirmed secret and returns its base32 key + otpauth URI.</summary>
    public static async Task<(string Base32, string Uri)> StartEnrollmentAsync(
        DbConnection connection, long userId, string contact, CancellationToken cancellationToken)
    {
        await EnsureTableAsync(connection, cancellationToken).ConfigureAwait(false);
        var secret = RandomNumberGenerator.GetBytes(20);
        var base32 = Base32Encode(secret);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ExecuteAsync(connection,
            $"INSERT INTO {Table} (user_id, contact, secret_base32, enabled, created_at, confirmed_at) " +
            "VALUES (@u, @c, @s, 0, @t, 0) " +
            "ON DUPLICATE KEY UPDATE contact = @c, secret_base32 = @s, enabled = 0, created_at = @t, confirmed_at = 0",
            cancellationToken, ("@u", userId), ("@c", contact), ("@s", base32), ("@t", now)).ConfigureAwait(false);
        var label = Uri.EscapeDataString("ECOMAE:" + contact);
        var uri = $"otpauth://totp/{label}?secret={base32}&issuer=ECOMAE&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
        return (base32, uri);
    }

    public static async Task<bool> ConfirmEnrollmentAsync(DbConnection connection, long userId, string code, CancellationToken cancellationToken)
    {
        var secret = await LoadSecretAsync(connection, userId, enabledOnly: false, cancellationToken).ConfigureAwait(false);
        if (secret is null || !Verify(secret, code))
        {
            return false;
        }
        await ExecuteAsync(connection,
            $"UPDATE {Table} SET enabled = 1, confirmed_at = @t WHERE user_id = @u",
            cancellationToken, ("@t", DateTimeOffset.UtcNow.ToUnixTimeSeconds()), ("@u", userId)).ConfigureAwait(false);
        return true;
    }

    public static async Task<bool> DisableAsync(DbConnection connection, long userId, string code, CancellationToken cancellationToken)
    {
        var secret = await LoadSecretAsync(connection, userId, enabledOnly: true, cancellationToken).ConfigureAwait(false);
        if (secret is null || !Verify(secret, code))
        {
            return false;
        }
        await ExecuteAsync(connection,
            $"UPDATE {Table} SET enabled = 0 WHERE user_id = @u",
            cancellationToken, ("@u", userId)).ConfigureAwait(false);
        return true;
    }

    public static async Task<bool> VerifyCodeAsync(DbConnection connection, long userId, string code, CancellationToken cancellationToken)
    {
        var secret = await LoadSecretAsync(connection, userId, enabledOnly: true, cancellationToken).ConfigureAwait(false);
        return secret is not null && Verify(secret, code);
    }

    private static async Task<string?> LoadSecretAsync(DbConnection connection, long userId, bool enabledOnly, CancellationToken cancellationToken)
    {
        var sql = $"SELECT secret_base32 FROM {Table} WHERE user_id = @u" + (enabledOnly ? " AND enabled = 1" : string.Empty) + " LIMIT 1";
        return Convert.ToString(await ScalarAsync(connection, sql, cancellationToken, ("@u", userId)).ConfigureAwait(false));
    }

    /// <summary>Verifies a 6-digit code against the current and adjacent 30-second steps.</summary>
    public static bool Verify(string secretBase32, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }
        var digits = new string(code.Where(char.IsDigit).ToArray());
        if (digits.Length != Digits)
        {
            return false;
        }
        var secret = Base32Decode(secretBase32.Trim());
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        for (var drift = -1; drift <= 1; drift++)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Compute(secret, counter + drift)),
                    Encoding.ASCII.GetBytes(digits)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Computes the TOTP code for a counter (exposed for tests).</summary>
    public static string Compute(byte[] secret, long counter)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xFF);
            counter >>= 8;
        }
        var hash = HMACSHA1.HashData(secret, counterBytes);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    public static string Base32Encode(byte[] data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Base32Alphabet[(buffer >> bits) & 0x1F]);
            }
        }
        if (bits > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        }
        return result.ToString();
    }

    public static byte[] Base32Decode(string base32)
    {
        var output = new MemoryStream();
        var buffer = 0;
        var bits = 0;
        foreach (var c in base32.ToUpperInvariant())
        {
            var idx = Array.IndexOf(Base32Alphabet, c);
            if (idx < 0)
            {
                continue;
            }
            buffer = (buffer << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.WriteByte((byte)((buffer >> bits) & 0xFF));
            }
        }
        return output.ToArray();
    }

    private static async Task<object?> ScalarAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
