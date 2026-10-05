using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Auth;

/// <summary>
/// PHP <c>epc_auth_find_or_provision_storefront_customer</c> and
/// <c>epc_auth_find_or_provision_cp_user</c>. An existing unlocked account is reused.
/// A new account is inserted only when the context allows it. Super CP and the
/// industries directory do not create CP accounts.
/// </summary>
public static class OAuthAccountProvision
{
    public sealed record Result(int UserId, bool Created, string? Message);

    public static bool AllowNewAccount(string? authMode, string? returnHost)
    {
        if (OAuthStart.NormalizeMode(authMode) == "storefront")
        {
            return true;
        }

        var host = OAuthCallback.NormalizeHost(returnHost);
        if (PlatformHostPolicy.IsSuperCpHost(host)
            || EcomaeIndustryShowcaseSnapshots.IsIndustriesDirectoryHost(host)
            || EcomaeIndustryShowcaseSnapshots.TryResolveHostSlug(host, out _))
        {
            return false;
        }

        return true;
    }

    public static async Task<Result> FindOrProvisionAsync(
        DbConnection connection,
        string? email,
        string? displayName,
        bool storefront,
        bool allowProvision,
        string secretSuccession,
        CancellationToken cancellationToken = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0 || !normalized.Contains('@', StringComparison.Ordinal))
        {
            return new Result(0, false, null);
        }

        try
        {
            var existing = await LoadUserAsync(connection, normalized, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (existing.Unlocked != 1)
                {
                    return new Result(0, false, null);
                }

                if (storefront && existing.EmailConfirmed != 1)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `users` SET `email_confirmed` = 1 WHERE `user_id` = ?"),
                        cancellationToken,
                        existing.UserId).ConfigureAwait(false);
                }

                if (storefront || await HasBackendAccessAsync(connection, existing.UserId, cancellationToken).ConfigureAwait(false))
                {
                    return new Result(existing.UserId, false, null);
                }

                return new Result(0, false, null);
            }

            if (!allowProvision)
            {
                return new Result(0, false, null);
            }

            var name = (displayName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                var at = normalized.IndexOf('@');
                name = at > 0 ? normalized[..at] : normalized;
            }

            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant() + "Aa1!";
            var hash = LegacyPasswordVerifier.Md5Hex(password + (secretSuccession ?? string.Empty));
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var userId = storefront
                ? await InsertStorefrontUserAsync(connection, normalized, hash, now, cancellationToken).ConfigureAwait(false)
                : await InsertCpUserAsync(connection, normalized, hash, now, cancellationToken).ConfigureAwait(false);
            if (userId <= 0)
            {
                return new Result(0, false, "Accounts are not in this database.");
            }

            await TryProfileAsync(connection, userId, name, cancellationToken).ConfigureAwait(false);
            if (storefront)
            {
                await TryStorefrontGroupAsync(connection, userId, cancellationToken).ConfigureAwait(false);
                return new Result(userId, true, null);
            }

            await TryBackendGroupsAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            if (!await HasBackendAccessAsync(connection, userId, cancellationToken).ConfigureAwait(false))
            {
                return new Result(0, true, null);
            }

            return new Result(userId, true, null);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new Result(0, false, "Accounts are not in this database.");
        }
    }

    private static async Task<int> InsertStorefrontUserAsync(
        DbConnection connection,
        string email,
        string hash,
        string now,
        CancellationToken cancellationToken)
    {
        var variant = await DefaultRegVariantAsync(connection, cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `ip_address`) VALUES (?, 1, ?, 1, ?, ?, ?)"),
                cancellationToken,
                email,
                hash,
                variant,
                now,
                string.Empty).ConfigureAwait(false);
        }
        catch (DbException ex) when (IsUnknownColumn(ex))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`) VALUES (?, 1, ?, 1)"),
                cancellationToken,
                email,
                hash).ConfigureAwait(false);
        }

        return await LastIdAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> InsertCpUserAsync(
        DbConnection connection,
        string email,
        string hash,
        string now,
        CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`) VALUES (?, 1, ?, 1, 1, ?, 1)"),
                cancellationToken,
                email,
                hash,
                now).ConfigureAwait(false);
        }
        catch (DbException ex) when (IsUnknownColumn(ex))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`) VALUES (?, 1, ?, 1)"),
                cancellationToken,
                email,
                hash).ConfigureAwait(false);
        }

        return await LastIdAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> DefaultRegVariantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var id = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `reg_variants` ORDER BY `order`, `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            return id > 0 ? (int)id : 1;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 1;
        }
    }

    private static async Task TryProfileAsync(DbConnection connection, int userId, string name, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
                cancellationToken,
                userId,
                "name",
                name).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
        }
    }

    private static async Task TryStorefrontGroupAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        try
        {
            var groupId = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (groupId <= 0)
            {
                return;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                cancellationToken,
                userId,
                groupId).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
        }
    }

    private static async Task TryBackendGroupsAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id` FROM `groups` WHERE `for_backend` = 1 ORDER BY `id` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            ids.Add(3);
        }

        if (ids.Count == 0)
        {
            ids.Add(3);
        }

        foreach (var groupId in ids)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                    cancellationToken,
                    userId,
                    groupId).ConfigureAwait(false);
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                return;
            }
        }
    }

    private static async Task<bool> HasBackendAccessAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `users_groups_bind` b INNER JOIN `groups` g ON g.`id` = b.`group_id` WHERE b.`user_id` = ? AND g.`for_backend` = 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        return count > 0;
    }

    private static async Task<ExistingUser?> LoadUserAsync(DbConnection connection, string email, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `user_id`, `unlocked`, `email_confirmed` FROM `users` WHERE `email` = ? LIMIT 1");
        ErpDb.AddParameters(command, email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new ExistingUser(
            Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture));
    }

    private static async Task<int> LastIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return id > int.MaxValue ? 0 : (int)id;
    }

    private static bool IsUnknownColumn(DbException exception)
        => exception.Message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);

    private sealed record ExistingUser(int UserId, int Unlocked, int EmailConfirmed);
}
