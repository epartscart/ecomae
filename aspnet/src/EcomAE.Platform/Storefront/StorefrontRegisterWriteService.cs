using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>content/users/register.php</c> account INSERT (quick register).
/// Activation e-mail / SMS and manager notify stay Classic — this does not invent a send.
/// Captcha, KYC file upload, and OTP <c>simple_register</c> stay Classic.
/// </summary>
public interface IStorefrontRegisterWriteService
{
    Task<StorefrontRegisterWriteResult> RegisterAsync(
        StorefrontRegisterWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontRegisterWriteRequest(
    string? ContactType,
    string? Contact,
    string? Password,
    string? PasswordRepeat,
    bool UsersAgreement,
    int RegVariant = 1,
    string? IpAddress = null,
    IReadOnlyDictionary<string, string>? ProfileFields = null);

public sealed record StorefrontRegisterWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    long UserId,
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
        cutoverAllowed = true,
        phpAuthoritative = false,
        validation_code = Code,
        would_write = Ok && Writes > 0,
        user_id = UserId,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontRegisterWriteService : IStorefrontRegisterWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly EcomAeOptions _options;

    public StorefrontRegisterWriteService(
        IErpWriteConnectionFactory connections,
        IOptions<EcomAeOptions> options)
    {
        _connections = connections;
        _options = options.Value;
    }

    public static string? NormalizeContactType(string? type)
    {
        var raw = (type ?? string.Empty).Trim().ToLowerInvariant();
        return raw is "email" or "phone" ? raw : null;
    }

    public static string NormalizeContact(string? contact)
        => (contact ?? string.Empty).Trim();

    public static bool LooksLikeEmail(string contact)
        => contact.Contains('@', StringComparison.Ordinal)
           && contact.IndexOf('@') > 0
           && contact.IndexOf('@') < contact.Length - 1;

    public static bool LooksLikePhone(string contact)
    {
        var digits = new string(contact.Where(char.IsDigit).ToArray());
        return digits.Length is >= 7 and <= 15;
    }

    public async Task<StorefrontRegisterWriteResult> RegisterAsync(
        StorefrontRegisterWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = NormalizeContactType(request.ContactType);
        if (type is null)
        {
            return Fail("invalid", "Choose e-mail or phone.");
        }

        var contact = NormalizeContact(request.Contact);
        if (contact.Length == 0)
        {
            return Fail("invalid", "Enter your e-mail or phone.");
        }

        if (type == "email" && !LooksLikeEmail(contact))
        {
            return Fail("invalid", "Enter a valid e-mail.");
        }

        if (type == "phone" && !LooksLikePhone(contact))
        {
            return Fail("invalid", "Enter a valid phone number.");
        }

        if (!request.UsersAgreement)
        {
            return Fail("agreement", "Accept the user agreement to register.");
        }

        var password = request.Password ?? string.Empty;
        var repeat = request.PasswordRepeat ?? string.Empty;
        if (password.Length < 6)
        {
            return Fail("password", "Password must be at least 6 characters.");
        }

        if (!string.Equals(password, repeat, StringComparison.Ordinal))
        {
            return Fail("password", "Passwords do not match.");
        }

        if (string.IsNullOrWhiteSpace(_options.SecretSuccession))
        {
            return Fail("config", "Registration is not configured.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Account database is not configured.");
        }

        var storedContact = StorefrontGuestSessionService.HtmlEntities(contact);
        var hash = LegacyPasswordVerifier.Md5Hex(password + _options.SecretSuccession);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = type == "email"
            ? LegacyPasswordVerifier.Md5Hex(
                LegacyPasswordVerifier.Md5Hex(contact) + LegacyPasswordVerifier.Md5Hex(_options.SecretSuccession))
            : Random.Shared.Next(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var contactCol = type == "email" ? "email" : "phone";
        var codeCol = type == "email" ? "email_code" : "phone_code";
        var expiredCol = type == "email" ? "email_code_expired" : "phone_code_expired";

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "users", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "users_groups_bind", cancellationToken).ConfigureAwait(false)
                || !await TableExistsAsync(connection, "groups", cancellationToken).ConfigureAwait(false))
            {
                return Fail("schema", "users tables are not provisioned. Schema ensure stays on the Classic twin.");
            }

            if (!await MatchesRegFieldAsync(connection, type, contact, cancellationToken).ConfigureAwait(false))
            {
                return Fail("invalid", "Enter a valid " + type + ".");
            }

            var taken = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `" + contactCol + "` = ?"),
                cancellationToken,
                storedContact).ConfigureAwait(false);
            if (taken > 0)
            {
                return Fail("exists", "That e-mail or phone is already registered. Sign in instead.");
            }

            var ip = (request.IpAddress ?? string.Empty).Trim();
            if (ip.Length > 0)
            {
                try
                {
                    var recent = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional(
                            """
                            SELECT COUNT(*) FROM `users`
                            WHERE `ip_address` = ? AND `time_registered` > ?
                              AND IFNULL(`email_confirmed`,0) = 0 AND IFNULL(`phone_confirmed`,0) = 0
                            """),
                        cancellationToken,
                        ip,
                        now - 86400).ConfigureAwait(false);
                    if (recent > 0)
                    {
                        return Fail("rate", "An unconfirmed account was already created from this network today.");
                    }
                }
                catch
                {
                    // ip_address may be missing on throwaway DBs.
                }
            }

            var variant = request.RegVariant > 0 ? request.RegVariant : 1;
            var variantOk = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `reg_variants` WHERE `id` = ?"),
                cancellationToken,
                variant).ConfigureAwait(false);
            if (variantOk != 1)
            {
                return Fail("invalid", "Registration variant is not available.");
            }

            var groupId = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            if (groupId <= 0)
            {
                return Fail("group", "Registered-customer group is not configured.");
            }

            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var writes = await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        "INSERT INTO `users` (`" + contactCol + "`, `reg_variant`, `password`, `" + codeCol + "`, `time_registered`, `" + expiredCol + "`, `unlocked`) VALUES (?, ?, ?, ?, ?, ?, 1)"),
                    cancellationToken,
                    storedContact,
                    variant,
                    hash,
                    code,
                    now,
                    now + 1800).ConfigureAwait(false);
                var userId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                if (writes <= 0 || userId <= 0)
                {
                    throw new ErpWriteException("Could not create the account.");
                }

                writes += await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)"),
                    cancellationToken,
                    userId,
                    groupId).ConfigureAwait(false);

                if (await TableExistsAsync(connection, "users_profiles", cancellationToken).ConfigureAwait(false))
                {
                    writes += await UpsertProfileAsync(connection, tx, userId, "epc_customer_type", "retail", cancellationToken)
                        .ConfigureAwait(false);
                    writes += await UpsertProfileAsync(connection, tx, userId, "epc_trade_registered_at", now.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken)
                        .ConfigureAwait(false);
                    writes += await UpsertProfileAsync(connection, tx, userId, "epc_trade_approval_status", "approved", cancellationToken)
                        .ConfigureAwait(false);
                    foreach (var field in StorefrontCustomerWriteService.NormalizeProfileFields(request.ProfileFields))
                    {
                        writes += await UpsertProfileAsync(connection, tx, userId, field.Key, field.Value, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (ip.Length > 0)
                {
                    try
                    {
                        writes += await ErpDb.ExecuteAsync(
                            connection,
                            tx,
                            ErpDb.Positional("UPDATE `users` SET `ip_address` = ? WHERE `user_id` = ?"),
                            cancellationToken,
                            ip.Length > 45 ? ip[..45] : ip,
                            userId).ConfigureAwait(false);
                    }
                    catch
                    {
                        // optional column
                    }
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new StorefrontRegisterWriteResult(
                    true,
                    "ok",
                    "ok",
                    "Account created. Sign in to continue. This page does not invent a send — confirm your e-mail or phone from your profile after you sign in.",
                    userId,
                    writes);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception ex)
        {
            return Fail("db", ex.Message);
        }
    }

    private static async Task<bool> MatchesRegFieldAsync(
        System.Data.Common.DbConnection connection,
        string type,
        string contact,
        CancellationToken cancellationToken)
    {
        string? regexp;
        try
        {
            regexp = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `regexp` FROM `reg_fields` WHERE `name` = ? LIMIT 1"),
                cancellationToken,
                type).ConfigureAwait(false);
        }
        catch
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(regexp))
        {
            return true;
        }

        try
        {
            var match = Regex.Match(contact, regexp, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return match.Success && string.Equals(match.Value, contact, StringComparison.Ordinal);
        }
        catch (RegexParseException)
        {
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static async Task<int> UpsertProfileAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction tx,
        long userId,
        string key,
        string value,
        CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT COUNT(*) FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ?"),
            cancellationToken,
            userId,
            key).ConfigureAwait(false);
        if (exists > 0)
        {
            return await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `users_profiles` SET `data_value` = ? WHERE `user_id` = ? AND `data_key` = ?"),
                cancellationToken,
                value,
                userId,
                key).ConfigureAwait(false);
        }

        return await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)"),
            cancellationToken,
            userId,
            key,
            value).ConfigureAwait(false);
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

    private static StorefrontRegisterWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0, 0);
}
