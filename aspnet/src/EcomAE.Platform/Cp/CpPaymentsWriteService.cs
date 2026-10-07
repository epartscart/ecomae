using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Input of the PHP <c>save_account</c> action (<c>epc_pay_accounts_save</c>).</summary>
public sealed record CpPaymentAccountInput(
    long Id,
    string OwnerType,
    long OwnerId,
    string Title,
    string Handler,
    string Mode,
    string CredentialsJson,
    string ConnectedAccountId,
    string PayoutIban,
    string PayoutBank,
    string PayoutName,
    decimal PlatformFeePct,
    string Status,
    bool DemoMode,
    bool IsDefault);

/// <summary>
/// Live twin of PHP <c>cp/content/shop/payments/ajax_payments.php</c>: gateway seeding, default-rail
/// activation, credential saving, individual payment accounts and settlement status
/// (<c>epc_payment_helpers.php</c> + <c>epc_payment_accounts.php</c>).
/// </summary>
public interface ICpPaymentsWriteService
{
    Task<ErpSimpleWriteResult> ActivateAsync(string? handler, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> MarkSettlementAsync(long settlementId, string? status, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SeedGatewaysAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveConfigAsync(long systemId, string? parametersValuesJson, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAccountAsync(CpPaymentAccountInput input, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DisableAccountAsync(long accountId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SeedPlatformAccountAsync(CancellationToken cancellationToken = default);
}

public sealed class CpPaymentsWriteService : ICpPaymentsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPaymentsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>activate</c> → <c>epc_payment_set_active</c>: exactly one default rail.</summary>
    public async Task<ErpSimpleWriteResult> ActivateAsync(
        string? handler,
        CancellationToken cancellationToken = default)
    {
        var key = SanitizeHandler(handler);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Handler required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            key);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Gateway not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            "UPDATE `shop_payment_systems` SET `active` = 0",
            cancellationToken);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_payment_systems` SET `active` = 1 WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            key);
        return ErpSimpleWriteResult.Ok("Activated: " + HandlerTitle(key), id);
    }

    /// <summary>PHP <c>mark_settlement</c> → <c>epc_pay_accounts_mark_settlement</c>.</summary>
    public async Task<ErpSimpleWriteResult> MarkSettlementAsync(
        long settlementId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (settlementId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Settlement id required");
        }

        var key = SanitizeSettlementStatus(status);
        if (key.Length == 0)
        {
            key = "paid_out";
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_payment_settlements` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                key, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), settlementId);
            return ErpSimpleWriteResult.Ok("Settlement updated", settlementId);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Settlement update failed: " + ex.Message);
        }
    }

    /// <summary>PHP <c>seed_dummy</c>: upsert the catalogue, enable legacy handlers, ensure the platform account.</summary>
    public async Task<ErpSimpleWriteResult> SeedGatewaysAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var seeded = 0;
            foreach (var definition in PhpPaymentGatewayCatalog.Definitions.Values)
            {
                await UpsertGatewayAsync(connection, definition, cancellationToken).ConfigureAwait(false);
                seeded++;
            }

            // PHP epc_payment_enable_legacy(): every configured handler stays selectable.
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "UPDATE `shop_payment_systems` SET `anable` = 1 WHERE `handler` <> '' AND `handler` IS NOT NULL",
                cancellationToken).ConfigureAwait(false);

            await EnsureAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await SeedPlatformAccountAsync(connection, cancellationToken).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok(
                "Gateways + platform payment account seeded",
                seeded);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Gateway seeding failed: " + ex.Message);
        }
    }

    /// <summary>PHP <c>save_config</c>: store the credential JSON and make that gateway the only active rail.</summary>
    public async Task<ErpSimpleWriteResult> SaveConfigAsync(
        long systemId,
        string? parametersValuesJson,
        CancellationToken cancellationToken = default)
    {
        var json = string.IsNullOrWhiteSpace(parametersValuesJson) ? "{}" : parametersValuesJson.Trim();
        if (!IsValidJson(json))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid parameters JSON");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "UPDATE `shop_payment_systems` SET `active` = 0",
                cancellationToken).ConfigureAwait(false);
            if (systemId <= 0)
            {
                return ErpSimpleWriteResult.Ok("All payment gateways disabled", 0);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_payment_systems` SET `active` = 1, `parameters_values` = ? WHERE `id` = ?"),
                cancellationToken,
                json,
                systemId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Payment gateway saved and activated", systemId);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Gateway save failed: " + ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAccountAsync(
        CpPaymentAccountInput input,
        CancellationToken cancellationToken = default)
    {
        var credentials = string.IsNullOrWhiteSpace(input.CredentialsJson) ? "{}" : input.CredentialsJson.Trim();
        if (!IsValidJson(credentials))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid credentials JSON");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var id = await SaveAccountAsync(
                connection,
                input with { CredentialsJson = credentials },
                cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Payment account saved", id);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Account save failed: " + ex.Message);
        }
    }

    /// <summary>PHP <c>disable_account</c>: re-save the row with <c>status = disabled</c> and no default flag.</summary>
    public async Task<ErpSimpleWriteResult> DisableAccountAsync(
        long accountId,
        CancellationToken cancellationToken = default)
    {
        if (accountId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Account id required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_payment_accounts` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                accountId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Account not found");
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_payment_accounts` SET `status` = 'disabled', `is_default` = 0, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                accountId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Account disabled", accountId);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Account disable failed: " + ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> SeedPlatformAccountAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var id = await SeedPlatformAccountAsync(connection, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Platform payment account ready", id);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform account seeding failed: " + ex.Message);
        }
    }

    private static async Task UpsertGatewayAsync(
        DbConnection connection,
        PhpPaymentGatewayDefinition definition,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Serialize(definition.Parameters.Select(p => new
        {
            name = p.Name,
            type = p.Type,
            caption = p.Caption
        }));
        var demo = JsonSerializer.Serialize(definition.Demo);
        var nameKey = PhpPaymentGatewayCatalog.NameKey(definition.Handler);
        var descriptionKey = PhpPaymentGatewayCatalog.DescriptionKey(definition.Handler);

        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            definition.Handler).ConfigureAwait(false);
        if (id > 0)
        {
            var saved = (await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `parameters_values` FROM `shop_payment_systems` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                id).ConfigureAwait(false) ?? "").Trim();
            if (saved is "" or "[]" or "null")
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_payment_systems` SET `parameters_values` = ?, `anable` = 1 WHERE `id` = ?"),
                    cancellationToken,
                    demo,
                    id).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `shop_payment_systems` SET `name` = ?, `description` = ?, `parameters` = ?, `anable` = 1 WHERE `id` = ?"),
                cancellationToken,
                nameKey,
                descriptionKey,
                parameters,
                id).ConfigureAwait(false);
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_payment_systems` (`name`, `parameters`, `parameters_values`, `anable`, `description`, `active`, `handler`) "
                + "VALUES (?, ?, ?, 1, ?, 0, ?)"),
            cancellationToken,
            nameKey,
            parameters,
            demo,
            descriptionKey,
            definition.Handler).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_pay_accounts_seed_platform</c>: default account for the active (else first enabled) rail.</summary>
    private static async Task<long> SeedPlatformAccountAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `epc_payment_accounts` WHERE `owner_type` = 'platform' AND `owner_id` = 0 "
            + "AND `status` = 'active' ORDER BY `is_default` DESC, `id` DESC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        long systemId = 0;
        var handler = "";
        var credentials = "{}";
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`handler`,''), IFNULL(`parameters_values`,'') FROM `shop_payment_systems` "
                              + "ORDER BY `active` DESC, `anable` DESC, `id` ASC LIMIT 1";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                systemId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                handler = reader.GetString(1);
                var raw = reader.GetString(2).Trim();
                credentials = IsValidJsonObject(raw) ? raw : "{}";
            }
        }

        if (systemId <= 0)
        {
            return 0;
        }

        return await SaveAccountAsync(
            connection,
            new CpPaymentAccountInput(
                0,
                "platform",
                0,
                "Platform default",
                handler,
                "direct",
                credentials,
                "",
                "",
                "",
                "",
                0m,
                "active",
                true,
                true),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_pay_accounts_save</c>, including the legacy <c>shop_offices</c> sync.</summary>
    private static async Task<long> SaveAccountAsync(
        DbConnection connection,
        CpPaymentAccountInput input,
        CancellationToken cancellationToken)
    {
        var ownerType = NormaliseOwnerType(input.OwnerType);
        var handler = SanitizeHandler(input.Handler);
        var mode = NormaliseMode(input.Mode);
        var status = NormaliseStatus(input.Status);
        var ownerId = Math.Max(0, input.OwnerId);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var fee = Math.Round(input.PlatformFeePct, 2, MidpointRounding.AwayFromZero);
        var title = input.Title.Trim();
        if (title.Length == 0)
        {
            title = ownerType + " #" + ownerId.ToString(CultureInfo.InvariantCulture);
        }

        var systemId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_payment_systems` WHERE `handler` = ? LIMIT 1"),
            cancellationToken,
            handler).ConfigureAwait(false);

        if (input.IsDefault)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_payment_accounts` SET `is_default` = 0 WHERE `owner_type` = ? AND `owner_id` = ?"),
                cancellationToken,
                ownerType,
                ownerId).ConfigureAwait(false);
        }

        long id;
        if (input.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_payment_accounts` SET `owner_type`=?, `owner_id`=?, `title`=?, `handler`=?, `pay_system_id`=?, "
                    + "`mode`=?, `credentials`=?, `connected_account_id`=?, `payout_iban`=?, `payout_bank`=?, `payout_name`=?, "
                    + "`platform_fee_pct`=?, `status`=?, `demo_mode`=?, `is_default`=?, `updated_at`=? WHERE `id`=?"),
                cancellationToken,
                ownerType, ownerId, title, handler, systemId, mode, input.CredentialsJson,
                input.ConnectedAccountId.Trim(), input.PayoutIban.Trim(), input.PayoutBank.Trim(), input.PayoutName.Trim(),
                fee, status, input.DemoMode ? 1 : 0, input.IsDefault ? 1 : 0, now, input.Id).ConfigureAwait(false);
            id = input.Id;
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_payment_accounts` (`owner_type`,`owner_id`,`title`,`handler`,`pay_system_id`,`mode`,"
                    + "`credentials`,`connected_account_id`,`payout_iban`,`payout_bank`,`payout_name`,`platform_fee_pct`,"
                    + "`status`,`demo_mode`,`is_default`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                ownerType, ownerId, title, handler, systemId, mode, input.CredentialsJson,
                input.ConnectedAccountId.Trim(), input.PayoutIban.Trim(), input.PayoutBank.Trim(), input.PayoutName.Trim(),
                fee, status, input.DemoMode ? 1 : 0, input.IsDefault ? 1 : 0, now, now).ConfigureAwait(false);
            id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        if (ownerType == "office" && ownerId > 0 && status == "active")
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_offices` SET `pay_system_id` = ?, `pay_system_parameters` = ? WHERE `id` = ?"),
                    cancellationToken,
                    systemId,
                    input.CredentialsJson,
                    ownerId).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP swallows the legacy office sync when those columns are absent.
            }
        }

        return id;
    }

    /// <summary>PHP <c>epc_pay_accounts_ensure_schema</c>.</summary>
    internal static async Task EnsureAccountSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_payment_accounts` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + "`owner_type` VARCHAR(16) NOT NULL DEFAULT 'platform',"
            + "`owner_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`title` VARCHAR(190) NOT NULL DEFAULT '',"
            + "`handler` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`pay_system_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`mode` VARCHAR(16) NOT NULL DEFAULT 'direct',"
            + "`credentials` MEDIUMTEXT NULL,"
            + "`connected_account_id` VARCHAR(190) NOT NULL DEFAULT '',"
            + "`payout_iban` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`payout_bank` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`payout_name` VARCHAR(190) NOT NULL DEFAULT '',"
            + "`platform_fee_pct` DECIMAL(5,2) NOT NULL DEFAULT 0.00,"
            + "`status` VARCHAR(16) NOT NULL DEFAULT 'active',"
            + "`demo_mode` TINYINT(1) NOT NULL DEFAULT 1,"
            + "`is_default` TINYINT(1) NOT NULL DEFAULT 0,"
            + "`created_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `idx_owner` (`owner_type`, `owner_id`),"
            + "KEY `idx_handler` (`handler`), KEY `idx_status` (`status`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_payment_settlements` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + "`operation_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`order_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`account_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`owner_type` VARCHAR(16) NOT NULL DEFAULT '',"
            + "`owner_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`handler` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`gross_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,"
            + "`fee_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,"
            + "`net_amount` DECIMAL(12,2) NOT NULL DEFAULT 0.00,"
            + "`currency` VARCHAR(8) NOT NULL DEFAULT 'AED',"
            + "`status` VARCHAR(24) NOT NULL DEFAULT 'pending',"
            + "`note` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`created_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `idx_op` (`operation_id`), KEY `idx_order` (`order_id`),"
            + "KEY `idx_account` (`account_id`), KEY `idx_status` (`status`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `shop_users_accounting` ADD COLUMN `epc_payment_account_id` INT UNSIGNED NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP owner whitelist of <c>epc_pay_accounts_owner_types()</c>.</summary>
    public static string NormaliseOwnerType(string? raw)
    {
        var key = SanitizeLower(raw);
        return key is "office" or "vendor" or "platform" ? key : "platform";
    }

    /// <summary>PHP account mode whitelist.</summary>
    public static string NormaliseMode(string? raw)
    {
        var key = SanitizeLower(raw);
        return key is "direct" or "connected" or "payout" ? key : "direct";
    }

    /// <summary>PHP account status whitelist.</summary>
    public static string NormaliseStatus(string? raw)
    {
        var key = SanitizeLower(raw);
        return key is "active" or "pending" or "disabled" ? key : "active";
    }

    public static bool IsValidJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(raw);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string SanitizeLower(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if ((ch is >= 'a' and <= 'z') || ch == '_')
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString();
    }

    /// <summary>PHP <c>preg_replace('/[^a-z_]/', '', $status)</c> on mark_settlement.</summary>
    public static string SanitizeSettlementStatus(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if ((ch is >= 'a' and <= 'z') || ch == '_')
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString();
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', $handler)</c>.</summary>
    public static string SanitizeHandler(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if ((ch is >= 'a' and <= 'z') || (ch is >= '0' and <= '9') || ch == '_')
            {
                buffer.Append(ch);
            }
        }

        return buffer.ToString();
    }

    /// <summary>PHP <c>epc_payment_handler_title</c>: catalogue title, else the humanised handler.</summary>
    public static string HandlerTitle(string handler)
        => PhpPaymentGatewayCatalog.Title(handler);
}
