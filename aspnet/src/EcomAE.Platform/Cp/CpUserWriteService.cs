using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_set_user_comment.php</c> / <c>users/user.php</c> twins.</summary>
public interface ICpUserWriteService
{
    Task<ErpSimpleWriteResult> SetCommentAsync(long userId, string? comment, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetVinViewedAsync(IReadOnlyList<long> requestIds, int viewedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetUnlockedAsync(long userId, int unlockedFlag, long actorUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateAsync(
        string? email,
        int emailConfirmed,
        string? phone,
        int phoneConfirmed,
        string? password,
        int unlocked,
        int regVariant,
        string? fieldsJson,
        string? groupsJson,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetPasswordAsync(
        long userId,
        string? password,
        string? keepSession,
        CancellationToken cancellationToken = default);
}

public sealed class CpUserWriteService : ICpUserWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpUserWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetCommentAsync(
        long userId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var text = (comment ?? string.Empty).Trim();
        if (text.Length > 4000)
        {
            text = text[..4000];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `users` SET `comment` = ? WHERE `user_id` = ?"),
            cancellationToken,
            text, userId);
        return ErpSimpleWriteResult.Ok("Staff comment saved.", userId);
    }

    public async Task<ErpSimpleWriteResult> SetVinViewedAsync(
        IReadOnlyList<long> requestIds,
        int viewedFlag,
        CancellationToken cancellationToken = default)
    {
        var ids = (requestIds ?? []).Where(id => id > 0).Distinct().Take(80).ToArray();
        if (ids.Length == 0 || viewedFlag is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select VIN requests and a viewed flag of 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var placeholders = string.Join(",", ids.Select((_, i) => "?"));
        var args = new object?[ids.Length + 1];
        args[0] = viewedFlag;
        for (var i = 0; i < ids.Length; i++)
        {
            args[i + 1] = ids[i];
        }

        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `users_vin` SET `viewed` = ? WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            args);
        return new ErpSimpleWriteResult(true, "ok", "VIN viewed flag updated.", ids[0], Math.Max(writes, 1));
    }

    public async Task<ErpSimpleWriteResult> SetUnlockedAsync(
        long userId,
        int unlockedFlag,
        long actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || unlockedFlag is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id and unlocked flag of 0 or 1 are required.");
        }

        if (actorUserId > 0 && actorUserId == userId)
        {
            return ErpSimpleWriteResult.Fail("self", "You cannot lock or unlock your own account.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `users` SET `unlocked` = ? WHERE `user_id` = ?"),
            cancellationToken,
            unlockedFlag, userId);

        if (unlockedFlag == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ?"),
                cancellationToken,
                userId);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(unlockedFlag == 1 ? "User unlocked." : "User locked.", userId);
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? email,
        int emailConfirmed,
        string? phone,
        int phoneConfirmed,
        string? password,
        int unlocked,
        int regVariant,
        string? fieldsJson,
        string? groupsJson,
        CancellationToken cancellationToken = default)
    {
        var plain = password ?? string.Empty;
        if (plain.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is required.");
        }

        if (plain.Length > 200)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is too long.");
        }

        var fields = ParseProfileFields(fieldsJson);
        if (fields.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", fields.Error);
        }

        var groups = ParseGroups(groupsJson);
        if (groups.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", groups.Error);
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one contact (email or phone) is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var emailNorm = await NormalizeContactAsync(connection, "email", email, emailConfirmed, cancellationToken).ConfigureAwait(false);
        if (emailNorm.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", emailNorm.Error);
        }

        var phoneNorm = await NormalizeContactAsync(connection, "phone", phone, phoneConfirmed, cancellationToken).ConfigureAwait(false);
        if (phoneNorm.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", phoneNorm.Error);
        }

        if (emailNorm.Value is null && phoneNorm.Value is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one contact (email or phone) is required.");
        }

        var existing = await FindExistingUserAsync(connection, emailNorm.Value, phoneNorm.Value, cancellationToken).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("exists", "A user with this email or phone already exists.");
        }

        var hash = HashStaffPassword(plain);
        var unlockedFlag = unlocked == 1 ? 1 : 0;
        var variant = regVariant > 0 ? regVariant : 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`, `admin_created`)
                    VALUES (?,?,?,?,?,?,?,?,1)
                    """),
                cancellationToken,
                variant, emailNorm.Value, emailNorm.Confirmed, phoneNorm.Value, phoneNorm.Confirmed, hash, unlockedFlag, now).ConfigureAwait(false);
            var userId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (userId <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Could not create the account.");
            }

            var writes = 1;
            foreach (var field in fields.Fields)
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?,?,?)"),
                    cancellationToken,
                    userId, field.Name, field.Value).ConfigureAwait(false);
            }

            foreach (var groupId in groups.GroupIds)
            {
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?,?)"),
                    cancellationToken,
                    userId, groupId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "User created.", userId, Math.Max(writes, 1));
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not create the account.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetPasswordAsync(
        long userId,
        string? password,
        string? keepSession,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id is required.");
        }

        var plain = password ?? string.Empty;
        if (plain.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is required.");
        }

        if (plain.Length > 200)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is too long.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var hash = HashStaffPassword(plain);
        var keep = (keepSession ?? string.Empty).Trim();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `users` SET `password` = ? WHERE `user_id` = ?"),
            cancellationToken,
            hash, userId).ConfigureAwait(false);
        if (rows <= 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("not_found", "Account was not updated.");
        }

        if (keep.Length > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ? AND `session` != ?"),
                cancellationToken,
                userId, keep).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ?"),
                cancellationToken,
                userId).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Password updated.", userId);
    }

    /// <summary>PHP <c>epc_password_hash</c> bcrypt cost 12.</summary>
    public static string HashStaffPassword(string plain)
        => BCrypt.Net.BCrypt.HashPassword(plain ?? string.Empty, workFactor: 12);

    public static (IReadOnlyList<UserProfileFieldWrite> Fields, string? Error) ParseProfileFields(string? json)
    {
        var raw = (json ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return ([], null);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "Profile fields must be a JSON array.");
            }

            var list = new List<UserProfileFieldWrite>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (list.Count >= 40)
                {
                    break;
                }

                var name = HtmlEntities(ReadString(item, "name", "data_key", "key"));
                if (name.Length == 0)
                {
                    continue;
                }

                if (name.Length > 128)
                {
                    name = name[..128];
                }

                var value = HtmlEntities(ReadString(item, "value", "data_value"));
                if (value.Length > 2000)
                {
                    value = value[..2000];
                }

                list.Add(new UserProfileFieldWrite(name, value));
            }

            return (list, null);
        }
        catch (JsonException)
        {
            return ([], "Profile fields JSON is not valid.");
        }
    }

    public static (IReadOnlyList<int> GroupIds, string? Error) ParseGroups(string? json)
    {
        var raw = (json ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return ([], null);
        }

        if (raw[0] != '[' && raw[0] != '{')
        {
            var csv = new List<int>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    csv.Add(id);
                }
            }

            return (csv.Distinct().Take(40).ToArray(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "Groups must be a JSON array of ids.");
            }

            var ids = new List<int>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n)
                    ? n
                    : int.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        ? parsed
                        : 0;
                if (id > 0)
                {
                    ids.Add(id);
                }
            }

            return (ids.Distinct().Take(40).ToArray(), null);
        }
        catch (JsonException)
        {
            return ([], "Groups JSON is not valid.");
        }
    }

    public static bool ContactMatchesRegexp(string value, string? regexp)
    {
        if (string.IsNullOrEmpty(regexp))
        {
            return true;
        }

        try
        {
            var match = Regex.Match(value, regexp, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return match.Success && match.Value == value;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public sealed record UserProfileFieldWrite(string Name, string Value);

    private sealed record ContactNorm(string? Value, int Confirmed, string? Error);

    private static async Task<ContactNorm> NormalizeContactAsync(
        System.Data.Common.DbConnection connection,
        string fieldName,
        string? raw,
        int confirmedFlag,
        CancellationToken cancellationToken)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new ContactNorm(null, 0, null);
        }

        if (value.Length > 255)
        {
            return new ContactNorm(null, 0, "Contact format is not valid.");
        }

        string? regexp = null;
        try
        {
            regexp = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ? LIMIT 1"),
                cancellationToken,
                fieldName).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            regexp = null;
        }

        if (!string.IsNullOrEmpty(regexp))
        {
            if (!ContactMatchesRegexp(value, regexp))
            {
                return new ContactNorm(null, 0, "Contact format is not valid.");
            }
        }
        else
        {
            value = HtmlEntities(value);
        }

        return new ContactNorm(value, confirmedFlag == 1 ? 1 : 0, null);
    }

    private static async Task<long> FindExistingUserAsync(
        System.Data.Common.DbConnection connection,
        string? email,
        string? phone,
        CancellationToken cancellationToken)
    {
        if (email is null && phone is null)
        {
            return 0;
        }

        if (email is not null && phone is not null)
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email` = ? OR `phone` = ? LIMIT 1"),
                cancellationToken,
                email, phone).ConfigureAwait(false);
        }

        if (email is not null)
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1"),
                cancellationToken,
                email).ConfigureAwait(false);
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `phone` = ? LIMIT 1"),
            cancellationToken,
            phone).ConfigureAwait(false);
    }

    private static string HtmlEntities(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string ReadString(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                return (prop.GetString() ?? string.Empty).Trim();
            }

            if (prop.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return prop.ToString().Trim();
            }
        }

        return string.Empty;
    }
}
