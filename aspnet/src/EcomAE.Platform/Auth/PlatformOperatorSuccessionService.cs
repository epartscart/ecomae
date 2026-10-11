using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Auth;

public sealed record OperatorSuccessionResult(
    bool Succeeded,
    int Writes,
    string Code,
    string Message,
    string Email,
    bool PasswordUnchanged);

/// <summary>
/// Moves the Super CP / Super BOS / Super ERP operator login from
/// <see cref="PlatformOperatorIdentity.LegacyEmail"/> to
/// <see cref="PlatformOperatorIdentity.CanonicalEmail"/> and keeps the stored password hash.
/// Never echoes the hash.
/// </summary>
public static class PlatformOperatorSuccessionService
{
    public static async Task<OperatorSuccessionResult> SucceedOnConnectionAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var canonical = await LoadUserAsync(connection, PlatformOperatorIdentity.CanonicalEmail, cancellationToken).ConfigureAwait(false);
        var legacy = await LoadUserAsync(connection, PlatformOperatorIdentity.LegacyEmail, cancellationToken).ConfigureAwait(false);

        if (canonical is not null && legacy is not null && canonical.UserId != legacy.UserId)
        {
            return new OperatorSuccessionResult(
                false,
                0,
                "collision",
                "Both " + PlatformOperatorIdentity.LegacyEmail + " and " + PlatformOperatorIdentity.CanonicalEmail + " exist as different users. Resolve the collision before remapping.",
                PlatformOperatorIdentity.CanonicalEmail,
                true);
        }

        var target = canonical ?? legacy;
        if (target is null)
        {
            return new OperatorSuccessionResult(
                false,
                0,
                "missing",
                "No Super operator row found for " + PlatformOperatorIdentity.LegacyEmail + " or " + PlatformOperatorIdentity.CanonicalEmail + ".",
                PlatformOperatorIdentity.CanonicalEmail,
                true);
        }

        var writes = 0;
        if (!string.Equals(target.Email, PlatformOperatorIdentity.CanonicalEmail, StringComparison.OrdinalIgnoreCase))
        {
            writes += await ExecuteAsync(
                connection,
                "UPDATE `users` SET `email` = @email, `email_confirmed` = 1, `unlocked` = 1 WHERE `user_id` = @id AND `password` = @hash",
                cancellationToken,
                ("@email", PlatformOperatorIdentity.CanonicalEmail),
                ("@id", target.UserId),
                ("@hash", target.PasswordHash)).ConfigureAwait(false);
        }
        else
        {
            writes += await ExecuteAsync(
                connection,
                "UPDATE `users` SET `email_confirmed` = 1, `unlocked` = 1 WHERE `user_id` = @id AND `password` = @hash",
                cancellationToken,
                ("@id", target.UserId),
                ("@hash", target.PasswordHash)).ConfigureAwait(false);
        }

        writes += await BindBackendGroupsAsync(connection, target.UserId, cancellationToken).ConfigureAwait(false);
        var kept = await PasswordStillMatchesAsync(connection, target.UserId, target.PasswordHash, cancellationToken).ConfigureAwait(false);
        return new OperatorSuccessionResult(
            kept,
            Math.Max(writes, 1),
            kept ? "ok" : "hash_changed",
            kept
                ? "Super CP / Super BOS / Super ERP admin is " + PlatformOperatorIdentity.CanonicalEmail + ". Password hash unchanged."
                : "Operator email updated but the stored password hash changed — refuse and inspect.",
            PlatformOperatorIdentity.CanonicalEmail,
            kept);
    }

    private static async Task<int> BindBackendGroupsAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var writes = 0;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id` FROM `groups` WHERE `for_backend` = 1";
        var ids = new List<int>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
            ids.Add(3);
        }

        if (ids.Count == 0)
        {
            ids.Add(3);
        }

        foreach (var id in ids)
        {
            try
            {
                writes += await ExecuteAsync(
                    connection,
                    "INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (@userId, @groupId)",
                    cancellationToken,
                    ("@userId", userId),
                    ("@groupId", id)).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }
        }

        return writes;
    }

    private static async Task<UserRow?> LoadUserAsync(DbConnection connection, string email, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `user_id`, IFNULL(`email`, '') AS email, IFNULL(`password`, '') AS password
            FROM `users`
            WHERE LOWER(`email`) = LOWER(@email)
            LIMIT 1
            """;
        Add(command, "@email", email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new UserRow(
            Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["password"], CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static async Task<bool> PasswordStillMatchesAsync(DbConnection connection, int userId, string hash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT IFNULL(`password`, '') FROM `users` WHERE `user_id` = @id LIMIT 1";
        Add(command, "@id", userId);
        var stored = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Equals(stored, hash, StringComparison.Ordinal);
    }

    private static async Task<int> ExecuteAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] values)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            Add(command, name, value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record UserRow(int UserId, string Email, string PasswordHash);
}
