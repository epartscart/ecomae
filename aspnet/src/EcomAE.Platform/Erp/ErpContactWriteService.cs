using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP twins of ajax_erp.php <c>save_contact</c> (<c>epc_erp_contact_save</c>),
/// <c>sync_contacts</c> (<c>epc_erp_contacts_sync_from_masters</c>) and
/// <c>customer_create</c> (<c>epc_erp_customer_provision</c>). Schema ensure mirrors
/// <c>epc_erp_phase8_ensure_schema</c> for the contacts table only.
/// </summary>
public interface IErpContactWriteService
{
    Task<long> SaveContactAsync(ErpContactInput input, CancellationToken cancellationToken = default);

    Task<int> SyncContactsAsync(CancellationToken cancellationToken = default);

    Task<long> ProvisionCustomerAsync(ErpCustomerProvisionInput input, CancellationToken cancellationToken = default);
}

public sealed record ErpContactInput
{
    public long Id { get; init; }
    public string? PartyType { get; init; }
    public string? Name { get; init; }
    public string? Company { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Trn { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? CountryCode { get; init; }
    public string? CurrencyCode { get; init; }
    public long LinkedUserId { get; init; }
    public long LinkedSupplierId { get; init; }
    public string? Notes { get; init; }
}

public sealed record ErpCustomerProvisionInput
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Company { get; init; }
    public string? Trn { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? CountryCode { get; init; }
    public string? CurrencyCode { get; init; }
}

public sealed class ErpContactWriteService : IErpContactWriteService
{
    public const string NameRequired = "Contact name is required";
    public const string CustomerRequired = "Customer name or email is required";
    public const int CustomerSyncLimit = 500;
    public static readonly IReadOnlyList<string> PartyTypes = ["customer", "supplier", "both", "staff", "other"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly TimeProvider _clock;

    public ErpContactWriteService(IErpWriteConnectionFactory connections, TimeProvider? clock = null)
    {
        _connections = connections;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>PHP: <c>in_array($party_type, [...], true) ? $party_type : 'customer'</c>.</summary>
    public static string NormalizePartyType(string? raw)
        => raw is not null && PartyTypes.Contains(raw, StringComparer.Ordinal) ? raw : "customer";

    /// <summary>PHP: <c>strtoupper(substr(trim($v ?? $default), 0, 8))</c>.</summary>
    public static string NormalizeCode(string? raw, string fallback)
    {
        var v = (raw ?? fallback).Trim();
        if (v.Length > 8)
        {
            v = v[..8];
        }

        return v.ToUpperInvariant();
    }

    /// <summary>PHP: <c>'erp-cust-' . substr(md5(uniqid('', true)), 0, 12) . '@erp.local'</c>.</summary>
    public static string SyntheticEmail()
        => "erp-cust-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant() + "@erp.local";

    public Task<long> SaveContactAsync(ErpContactInput input, CancellationToken cancellationToken = default)
        => Guarded(() => SaveContactCoreAsync(input, cancellationToken));

    public Task<int> SyncContactsAsync(CancellationToken cancellationToken = default)
        => Guarded(() => SyncContactsCoreAsync(cancellationToken));

    public Task<long> ProvisionCustomerAsync(ErpCustomerProvisionInput input, CancellationToken cancellationToken = default)
        => Guarded(() => ProvisionCustomerCoreAsync(input, cancellationToken));

    /// <summary>PHP ajax_erp.php catches Throwable and answers ok=false with the message.</summary>
    private static async Task<T> Guarded<T>(Func<Task<T>> work)
    {
        try
        {
            return await work().ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw new ErpWriteException(ex.Message);
        }
    }

    private async Task<long> SaveContactCoreAsync(ErpContactInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await SaveAsync(connection, input, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> SyncContactsCoreAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var n = 0;

        var suppliers = await RowsAsync(connection, "SELECT `id`, `name`, `contact_email`, `contact_phone`, `trn` FROM `epc_erp_suppliers` WHERE `active` = 1", cancellationToken).ConfigureAwait(false);
        foreach (var s in suppliers)
        {
            var sid = Convert.ToInt64(s["id"], CultureInfo.InvariantCulture);
            var existing = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `linked_supplier_id` = ? LIMIT 1"), cancellationToken, sid).ConfigureAwait(false);
            if (existing > 0)
            {
                continue;
            }

            await SaveAsync(connection, new ErpContactInput
            {
                PartyType = "supplier",
                Name = Str(s["name"]),
                Email = Str(s["contact_email"]),
                Phone = Str(s["contact_phone"]),
                Trn = Str(s["trn"]),
                LinkedSupplierId = sid,
            }, cancellationToken).ConfigureAwait(false);
            n++;
        }

        if (await ShopOrdersHasStatusAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            var users = await RowsAsync(
                connection,
                "SELECT DISTINCT o.`user_id`, u.`email`, u.`phone` FROM `shop_orders` o INNER JOIN `users` u ON u.`user_id` = o.`user_id` WHERE o.`user_id` > 0 LIMIT " + CustomerSyncLimit.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
            foreach (var u in users)
            {
                var uid = Convert.ToInt64(u["user_id"], CultureInfo.InvariantCulture);
                var existing = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `linked_user_id` = ? LIMIT 1"), cancellationToken, uid).ConfigureAwait(false);
                if (existing > 0)
                {
                    continue;
                }

                var email = Str(u["email"]);
                await SaveAsync(connection, new ErpContactInput
                {
                    PartyType = "customer",
                    Name = email.Length > 0 ? email : "Customer #" + uid.ToString(CultureInfo.InvariantCulture),
                    Email = email,
                    Phone = Str(u["phone"]),
                    LinkedUserId = uid,
                }, cancellationToken).ConfigureAwait(false);
                n++;
            }
        }

        return n;
    }

    private async Task<long> ProvisionCustomerCoreAsync(ErpCustomerProvisionInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var name = (input.Name ?? string.Empty).Trim();
        var email = (input.Email ?? string.Empty).Trim().ToLowerInvariant();
        var phone = (input.Phone ?? string.Empty).Trim();
        if (name.Length == 0 && email.Length == 0)
        {
            throw new ErpWriteException(CustomerRequired);
        }

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        long userId = 0;
        if (email.Length > 0)
        {
            userId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1"), cancellationToken, email).ConfigureAwait(false);
        }

        if (userId <= 0)
        {
            var regEmail = email.Length > 0 ? email : SyntheticEmail();
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`, `admin_created`) VALUES (1, ?, 1, ?, 0, ?, 1, ?, 1)"),
                cancellationToken,
                regEmail,
                phone.Length > 0 ? phone : null,
                Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                _clock.GetUtcNow().ToUnixTimeSeconds()).ConfigureAwait(false);
            userId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        var contactId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `linked_user_id` = ? LIMIT 1"), cancellationToken, userId).ConfigureAwait(false);
        await SaveAsync(connection, new ErpContactInput
        {
            Id = contactId,
            PartyType = "customer",
            Name = name.Length > 0 ? name : email,
            Company = input.Company ?? string.Empty,
            Email = email,
            Phone = phone,
            Trn = input.Trn ?? string.Empty,
            Address = input.Address ?? string.Empty,
            City = input.City ?? string.Empty,
            CountryCode = input.CountryCode ?? "AE",
            CurrencyCode = input.CurrencyCode ?? "AED",
            LinkedUserId = userId,
        }, cancellationToken).ConfigureAwait(false);

        return userId;
    }

    /// <summary>PHP <c>epc_erp_contact_save</c> body (schema already ensured by the caller).</summary>
    private async Task<long> SaveAsync(DbConnection connection, ErpContactInput input, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ErpWriteException(NameRequired);
        }

        object?[] fields =
        [
            NormalizePartyType(input.PartyType),
            name,
            (input.Company ?? string.Empty).Trim(),
            (input.Email ?? string.Empty).Trim(),
            (input.Phone ?? string.Empty).Trim(),
            (input.Trn ?? string.Empty).Trim(),
            (input.Address ?? string.Empty).Trim(),
            (input.City ?? string.Empty).Trim(),
            NormalizeCode(input.CountryCode, "AE"),
            NormalizeCode(input.CurrencyCode, "AED"),
            input.LinkedUserId,
            input.LinkedSupplierId,
            (input.Notes ?? string.Empty).Trim(),
        ];

        if (input.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_contacts` SET `party_type`=?, `name`=?, `company`=?, `email`=?, `phone`=?, `trn`=?, `address`=?, `city`=?, `country_code`=?, `currency_code`=?, `linked_user_id`=?, `linked_supplier_id`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                [.. fields, now, input.Id]).ConfigureAwait(false);
            return input.Id;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_contacts` (`party_type`, `name`, `company`, `email`, `phone`, `trn`, `address`, `city`, `country_code`, `currency_code`, `linked_user_id`, `linked_supplier_id`, `notes`, `time_created`, `time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            [.. fields, now, now]).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        return await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_phase8_ensure_schema</c> — contacts table and its <c>currency_code</c> convergence.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null,
            "CREATE TABLE IF NOT EXISTS `epc_erp_contacts` (`id` int(11) NOT NULL AUTO_INCREMENT, `party_type` enum('customer','supplier','both','staff','other') NOT NULL DEFAULT 'customer',"
            + " `name` varchar(255) NOT NULL, `company` varchar(255) DEFAULT NULL, `email` varchar(255) DEFAULT NULL, `phone` varchar(64) DEFAULT NULL, `trn` varchar(64) DEFAULT NULL,"
            + " `address` text, `city` varchar(128) DEFAULT NULL, `country_code` varchar(8) NOT NULL DEFAULT 'AE', `linked_user_id` int(11) NOT NULL DEFAULT 0, `linked_supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `notes` text, `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`),"
            + " KEY `x_party` (`party_type`,`active`), KEY `x_user` (`linked_user_id`), KEY `x_supplier` (`linked_supplier_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP unified contacts / third parties'",
            cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"), cancellationToken, "epc_erp_contacts", "currency_code").ConfigureAwait(false);
        if (exists == 0)
        {
            await ErpDb.TryExecuteAsync(connection, "ALTER TABLE `epc_erp_contacts` ADD `currency_code` varchar(8) NOT NULL DEFAULT 'AED'", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>epc_erp_shop_orders_has_status</c>: false when the table or column is missing.</summary>
    private static async Task<bool> ShopOrdersHasStatusAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"), cancellationToken, "shop_orders", "status").ConfigureAwait(false) > 0;

    private static string Str(object? value) => value is null or DBNull ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> RowsAsync(DbConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var map = new Dictionary<string, object?>(reader.FieldCount, StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                map[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(map);
        }

        return rows;
    }
}
