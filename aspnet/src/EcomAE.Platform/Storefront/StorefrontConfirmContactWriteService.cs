using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>content/users/confirm_contact.php</c> contact confirm.
/// Marks <c>email_confirmed</c> / <c>phone_confirmed</c> when the stored code matches.
/// Does not invent a send. Column names are allowlisted to email/phone only.
/// </summary>
public interface IStorefrontConfirmContactWriteService
{
    Task<StorefrontConfirmContactWriteResult> ConfirmAsync(
        StorefrontConfirmContactWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontConfirmContactWriteRequest(
    long UserId,
    string? Type,
    string? Code);

public sealed record StorefrontConfirmContactWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    bool CanTryAgain,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = Code,
        can_try_again = CanTryAgain,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontConfirmContactWriteService : IStorefrontConfirmContactWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontConfirmContactWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string? NormalizeType(string? type)
    {
        var raw = (type ?? string.Empty).Trim().ToLowerInvariant();
        return raw is "email" or "phone" ? raw : null;
    }

    public static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim();

    public async Task<StorefrontConfirmContactWriteResult> ConfirmAsync(
        StorefrontConfirmContactWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = NormalizeType(request.Type);
        if (type is null)
        {
            return Fail("invalid", "Choose e-mail or phone.");
        }

        if (request.UserId <= 0)
        {
            return Fail("invalid", "A user id is required.");
        }

        var code = NormalizeCode(request.Code);
        if (code.Length == 0)
        {
            return Fail("invalid", "Enter the confirmation code.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Account database is not configured.");
        }

        var caption = type == "email" ? "E-mail" : "Phone";
        var col = type;
        var confirmedCol = type + "_confirmed";
        var newCol = type + "_new";
        var codeCol = type + "_code";
        var expiredCol = type + "_code_expired";
        var attemptsCol = type + "_code_attempts";

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false))
            {
                return Fail("schema", "users table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            await using var select = connection.CreateCommand();
            select.CommandText = ErpDb.Positional(
                "SELECT `" + col + "`, `" + newCol + "`, `" + codeCol + "`, `" + expiredCol + "`, `" + attemptsCol + "` FROM `users` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(select, request.UserId);
            string storedContact;
            string storedNew;
            string storedCode;
            long expired;
            int attempts;
            await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return Fail("invalid", "Confirmation code is not valid.");
                }

                storedContact = ReadString(reader, 0);
                storedNew = ReadString(reader, 1);
                storedCode = ReadString(reader, 2);
                expired = ReadLong(reader, 3);
                attempts = (int)ReadLong(reader, 4);
            }

            if (storedCode.Length == 0)
            {
                return Fail("invalid", "Confirmation code is not valid.");
            }

            if (!string.Equals(storedCode, code, StringComparison.Ordinal))
            {
                if (attempts < 3)
                {
                    var writes = await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `users` SET `" + attemptsCol + "` = `" + attemptsCol + "` + 1 WHERE `user_id` = ?"),
                        cancellationToken,
                        request.UserId).ConfigureAwait(false);
                    var left = 3 - attempts;
                    return new StorefrontConfirmContactWriteResult(
                        false,
                        "error",
                        "code",
                        "Wrong confirmation code. Tries left: " + left.ToString(CultureInfo.InvariantCulture),
                        type == "phone",
                        writes);
                }

                var locked = await ClearCodeAsync(connection, request.UserId, newCol, codeCol, expiredCol, attemptsCol, cancellationToken)
                    .ConfigureAwait(false);
                return new StorefrontConfirmContactWriteResult(
                    false,
                    "error",
                    "locked",
                    "No more tries. Ask the site administrator to confirm the contact.",
                    false,
                    locked);
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (expired > 0 && now > expired)
            {
                var cleared = await ClearCodeAsync(connection, request.UserId, newCol, codeCol, expiredCol, attemptsCol, cancellationToken)
                    .ConfigureAwait(false);
                return new StorefrontConfirmContactWriteResult(
                    false,
                    "error",
                    "expired",
                    "This confirmation code has expired.",
                    false,
                    cleared);
            }

            var contact = storedNew.Length > 0 ? storedNew : storedContact;
            var writesOk = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `users` SET `" + col + "` = ?, `" + confirmedCol + "` = 1, `" + newCol + "` = '', `" + codeCol + "` = '', `" + expiredCol + "` = 0, `" + attemptsCol + "` = 0 WHERE `user_id` = ?"),
                cancellationToken,
                contact,
                request.UserId).ConfigureAwait(false);
            if (writesOk <= 0)
            {
                return new StorefrontConfirmContactWriteResult(
                    false,
                    "error",
                    "db",
                    "Could not confirm the contact. Try again.",
                    type == "phone",
                    0);
            }

            return new StorefrontConfirmContactWriteResult(
                true,
                "ok",
                "ok",
                caption + " confirmed. This page does not invent a send.",
                false,
                writesOk);
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    private static async Task<int> ClearCodeAsync(
        System.Data.Common.DbConnection connection,
        long userId,
        string newCol,
        string codeCol,
        string expiredCol,
        string attemptsCol,
        CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `users` SET `" + newCol + "` = '', `" + codeCol + "` = '', `" + expiredCol + "` = 0, `" + attemptsCol + "` = 0 WHERE `user_id` = ?"),
            cancellationToken,
            userId).ConfigureAwait(false);

    private static string ReadString(System.Data.Common.DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static long ReadLong(System.Data.Common.DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(
        System.Data.Common.DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static StorefrontConfirmContactWriteResult Fail(string code, string message)
        => new(false, "error", code, message, false, 0);
}
