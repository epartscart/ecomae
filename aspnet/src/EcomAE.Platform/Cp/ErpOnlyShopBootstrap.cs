using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Dedicated ERP-only shop schema + operator. Registry credentials open the
/// company database — never invent a MySQL user from <c>db_user=site_key</c>
/// with an empty password. Super /cp and /erp stay on the platform DB.
/// </summary>
public interface IErpOnlyShopBootstrap
{
    Task<ErpOnlyShopEnsureResult> EnsureForSiteKeyAsync(
        string siteKey,
        string? passwordOverride = null,
        CancellationToken cancellationToken = default);

    Task<ErpOnlyShopEnsureResult> EnsureOperatorLoginAsync(
        string siteKey,
        string email,
        string password,
        bool replacePassword,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOnlyShopEnsureResult(
    bool Ok,
    bool DatabaseReady,
    bool SchemaReady,
    bool OperatorReady,
    string Message);

public sealed class ErpOnlyShopBootstrap : IErpOnlyShopBootstrap
{
    public const int BackendGroupId = 3;

    public const string CreateUsersSql = """
        CREATE TABLE IF NOT EXISTS `users` (
          `user_id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `email` VARCHAR(120) NOT NULL DEFAULT '',
          `phone` VARCHAR(32) NOT NULL DEFAULT '',
          `email_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
          `phone_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
          `password` VARCHAR(255) NOT NULL DEFAULT '',
          `unlocked` TINYINT(1) NOT NULL DEFAULT 1,
          `reg_variant` INT NOT NULL DEFAULT 1,
          `time_registered` INT NOT NULL DEFAULT 0,
          `time_last_visit` INT NOT NULL DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    public const string CreateSessionsSql = """
        CREATE TABLE IF NOT EXISTS `sessions` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `session` VARCHAR(64) NOT NULL DEFAULT '',
          `user_id` INT NOT NULL DEFAULT 0,
          `time` INT NOT NULL DEFAULT 0,
          `data` TEXT NULL,
          `type` INT NOT NULL DEFAULT 0,
          `contact_type` VARCHAR(16) NOT NULL DEFAULT '',
          `csrf_guard_key` VARCHAR(64) NOT NULL DEFAULT '',
          `last_activiti_time` INT NOT NULL DEFAULT 0,
          KEY `idx_session` (`session`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    public const string CreateCompanySettingsSql = """
        CREATE TABLE IF NOT EXISTS `epc_org_company_settings` (
          `company_id` int(11) NOT NULL,
          `setting_key` varchar(64) NOT NULL,
          `setting_value` text,
          PRIMARY KEY (`company_id`,`setting_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-company ERP settings'
        """;

    public const string CreateGroupsSql = """
        CREATE TABLE IF NOT EXISTS `groups` (
          `id` INT UNSIGNED NOT NULL PRIMARY KEY,
          `value` VARCHAR(64) NOT NULL DEFAULT '',
          `count` INT NOT NULL DEFAULT 0,
          `level` INT NOT NULL DEFAULT 0,
          `parent` INT NOT NULL DEFAULT 0,
          `unblocked` TINYINT(1) NOT NULL DEFAULT 1,
          `for_guests` TINYINT(1) NOT NULL DEFAULT 0,
          `for_registrated` TINYINT(1) NOT NULL DEFAULT 0,
          `for_backend` TINYINT(1) NOT NULL DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    public const string CreateGroupBindSql = """
        CREATE TABLE IF NOT EXISTS `users_groups_bind` (
          `user_id` INT NOT NULL,
          `group_id` INT NOT NULL,
          UNIQUE KEY `bind` (`user_id`, `group_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
        """;

    private readonly ITenantDbConnectionFactory _connections;

    public ErpOnlyShopBootstrap(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpOnlyShopEnsureResult> EnsureForSiteKeyAsync(
        string siteKey,
        string? passwordOverride = null,
        CancellationToken cancellationToken = default)
    {
        var key = CpTenantsWriteService.NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return new ErpOnlyShopEnsureResult(false, false, false, false, "Invalid site key");
        }

        if (!_connections.IsConfigured)
        {
            return new ErpOnlyShopEnsureResult(false, false, false, false, "TenantRegistry DB is not configured.");
        }

        try
        {
            string dbName;
            string email;
            string storedPassword;
            string industryCode;
            await using (var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = await LoadRegistryAsync(registry, key, cancellationToken).ConfigureAwait(false);
                if (row is null)
                {
                    return new ErpOnlyShopEnsureResult(false, false, false, false, "Tenant not in registry");
                }

                dbName = row.Value.DbName;
                email = row.Value.Email;
                storedPassword = row.Value.TempPassword;
                industryCode = row.Value.IndustryCode;
                if (!CpTenantsWriteService.IsDroppableDatabase(dbName))
                {
                    return new ErpOnlyShopEnsureResult(false, false, false, false, "Database name is reserved.");
                }

                await CreateDatabaseAsync(registry, dbName, cancellationToken).ConfigureAwait(false);
            }

            DbConnection shop;
            try
            {
                shop = await _connections
                    .OpenAsync(dbName, userName: null, password: null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new ErpOnlyShopEnsureResult(
                    false,
                    false,
                    false,
                    false,
                    "Could not open `" + dbName + "` with the platform registry credentials. CloudPanel must CREATE DATABASE `" + dbName + "` and grant the registry user access — do not remap TenantRegistry to PHP.");
            }

            await using (shop)
            {
                await EnsureSchemaAsync(shop, cancellationToken).ConfigureAwait(false);
                await EnsureIndustryProfileAsync(
                    shop,
                    RouteTenantResolver.ResolveIndustryCode(key, industryCode, host: null),
                    cancellationToken).ConfigureAwait(false);
                var replace = !string.IsNullOrWhiteSpace(passwordOverride);
                var password = replace ? passwordOverride! : storedPassword;
                var operatorReady = false;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    operatorReady = await EnsureOperatorAsync(
                        shop, email, password, cancellationToken, replacePassword: replace).ConfigureAwait(false);
                }

                return new ErpOnlyShopEnsureResult(
                    true,
                    true,
                    true,
                    operatorReady,
                    operatorReady
                        ? "Shop login ready for " + email + " on `" + dbName + "`."
                        : "Shop tables ready on `" + dbName + "` — Super CP must reset the operator password.");
            }
        }
        catch (DbException ex)
        {
            return new ErpOnlyShopEnsureResult(
                false,
                false,
                false,
                false,
                "Shop ensure failed: " + ex.GetType().Name);
        }
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection, CreateUsersSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, CreateSessionsSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, CreateGroupsSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, CreateGroupBindSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "INSERT IGNORE INTO `groups` (`id`,`value`,`for_backend`,`unblocked`) VALUES ("
            + BackendGroupId.ToString(CultureInfo.InvariantCulture)
            + ",'Admins',1,1)",
            cancellationToken).ConfigureAwait(false);
        await LegacyLoginSecurity.EnsureTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpUaeTaxComplianceSchema.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpProcurementSchema.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpRfqSchema.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PHP <c>epc_erp_company_industry_pack_set</c> + <c>erp_industry_profile</c> for
    /// ERP-only shops. Jewellery / fit-out / auto-parts / real-estate packs follow the
    /// bound tenant so industry updates apply even when the Super host is not jewellery.
    /// Existing operator-chosen values are left in place (INSERT IGNORE).
    /// </summary>
    public static async Task EnsureIndustryProfileAsync(
        DbConnection connection,
        string? industryCode,
        CancellationToken cancellationToken)
    {
        var industry = (industryCode ?? string.Empty).Trim();
        if (industry.Length == 0)
        {
            return;
        }

        var pack = PackForIndustry(industry);
        var profile = ProfileForIndustry(industry);
        if (pack.Length == 0)
        {
            return;
        }

        try
        {
            await ErpIndustryApplyWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(connection, CreateCompanySettingsSql, cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureCompanyContextAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureTenantOrganizationAsync(connection, cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT IGNORE INTO `epc_price_settings` (`setting_key`,`setting_value`) VALUES (?,?)"),
                cancellationToken,
                "erp_industry_profile",
                profile).ConfigureAwait(false);

            var companyId = await ErpDb.LongAsync(
                connection,
                null,
                "SELECT MIN(`id`) FROM `epc_erp_pm_legal_entities` WHERE `active`=1",
                cancellationToken).ConfigureAwait(false);
            if (companyId > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "INSERT IGNORE INTO `epc_org_company_settings` (`company_id`,`setting_key`,`setting_value`) VALUES (?,?,?)"),
                    cancellationToken,
                    companyId,
                    "industry_pack",
                    pack).ConfigureAwait(false);
            }

            if (IndustrySpecificRoutePolicy.Matches("jewellery", industry))
            {
                await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            // Best-effort: shop login must still succeed when a lean tenant cannot store the pack.
        }
    }

    public static string PackForIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0)
        {
            return string.Empty;
        }

        if (IndustrySpecificRoutePolicy.Matches("jewellery", code))
        {
            return "jewellery_diamond";
        }

        if (IndustrySpecificRoutePolicy.Matches("fitout", code))
        {
            return "fitout_contracting";
        }

        if (code is "auto_parts" or "automotive")
        {
            return "auto_parts";
        }

        return code;
    }

    public static string ProfileForIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        if (code.Length == 0)
        {
            return string.Empty;
        }

        if (IndustrySpecificRoutePolicy.Matches("jewellery", code))
        {
            return "jewellery";
        }

        if (IndustrySpecificRoutePolicy.Matches("fitout", code))
        {
            return "building_construction";
        }

        if (code is "auto_parts" or "automotive")
        {
            return "auto_parts";
        }

        return code;
    }

    public async Task<ErpOnlyShopEnsureResult> EnsureOperatorLoginAsync(
        string siteKey,
        string email,
        string password,
        bool replacePassword,
        CancellationToken cancellationToken = default)
    {
        var prepared = await EnsureForSiteKeyAsync(siteKey, passwordOverride: null, cancellationToken).ConfigureAwait(false);
        if (!prepared.DatabaseReady || !prepared.SchemaReady)
        {
            return prepared;
        }

        var key = CpTenantsWriteService.NormalizeSiteKey(siteKey);
        string dbName;
        await using (var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = await LoadRegistryAsync(registry, key, cancellationToken).ConfigureAwait(false);
            if (row is null)
            {
                return new ErpOnlyShopEnsureResult(false, false, false, false, "Tenant not in registry");
            }

            dbName = row.Value.DbName;
        }

        try
        {
            await using var shop = await _connections
                .OpenAsync(dbName, userName: null, password: null, cancellationToken)
                .ConfigureAwait(false);
            var ready = await EnsureOperatorAsync(shop, email, password, cancellationToken, replacePassword)
                .ConfigureAwait(false);
            return new ErpOnlyShopEnsureResult(
                ready,
                true,
                true,
                ready,
                ready
                    ? "Login ready for " + email.Trim().ToLowerInvariant() + " on `" + dbName + "`."
                    : "Could not write the shop login for " + email + ".");
        }
        catch (DbException)
        {
            return new ErpOnlyShopEnsureResult(false, true, true, false, "Could not open the company shop database.");
        }
    }

    public static async Task<bool> EnsureOperatorAsync(
        DbConnection connection,
        string email,
        string password,
        CancellationToken cancellationToken,
        bool replacePassword = true)
    {
        var contact = email.Trim().ToLowerInvariant();
        if (contact.Length == 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE LOWER(`email`) = LOWER(?) LIMIT 1"),
            cancellationToken,
            contact).ConfigureAwait(false);
        if (existing > 0 && !replacePassword)
        {
            await ErpDb.TryExecuteAsync(
                connection,
                "UPDATE `users` SET `email_confirmed`=1, `unlocked`=1 WHERE `user_id`="
                + existing.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
        }
        else if (existing > 0)
        {
            if (password.Length == 0)
            {
                return false;
            }

            var hash = LegacyLoginSecurity.HashPassword(password);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `users` SET `password`=?, `email_confirmed`=1, `unlocked`=1 WHERE `user_id`=?"),
                cancellationToken,
                hash,
                existing).ConfigureAwait(false);
        }
        else
        {
            if (password.Length == 0)
            {
                return false;
            }

            var hash = LegacyLoginSecurity.HashPassword(password);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `users` (`email`,`email_confirmed`,`phone_confirmed`,`password`,`unlocked`,`time_registered`) "
                    + "VALUES (?,1,0,?,1,?)"),
                cancellationToken,
                contact,
                hash,
                now).ConfigureAwait(false);
            existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE LOWER(`email`) = LOWER(?) LIMIT 1"),
                cancellationToken,
                contact).ConfigureAwait(false);
        }

        if (existing <= 0)
        {
            return false;
        }

        await ErpDb.TryExecuteAsync(
            connection,
            "INSERT IGNORE INTO `users_groups_bind` (`user_id`,`group_id`) VALUES ("
            + existing.ToString(CultureInfo.InvariantCulture) + ","
            + BackendGroupId.ToString(CultureInfo.InvariantCulture) + ")",
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static async Task<(string DbName, string Email, string TempPassword, string IndustryCode)?> LoadRegistryAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        var full =
            "SELECT IFNULL(`db_name`,'') AS db_name, IFNULL(`from_email`,'') AS from_email, "
            + "IFNULL(`operator_temp_password`,'') AS operator_temp_password, "
            + "IFNULL(`industry_code`,'') AS industry_code "
            + "FROM `epc_portal_tenants` WHERE `site_key`=@p0 LIMIT 1";
        var noIndustry =
            "SELECT IFNULL(`db_name`,'') AS db_name, IFNULL(`from_email`,'') AS from_email, "
            + "IFNULL(`operator_temp_password`,'') AS operator_temp_password, "
            + "'' AS industry_code "
            + "FROM `epc_portal_tenants` WHERE `site_key`=@p0 LIMIT 1";
        var minimal =
            "SELECT IFNULL(`db_name`,'') AS db_name, IFNULL(`from_email`,'') AS from_email, "
            + "'' AS operator_temp_password, '' AS industry_code "
            + "FROM `epc_portal_tenants` WHERE `site_key`=@p0 LIMIT 1";
        var row = await ReadRegistryRowAsync(connection, full, siteKey, cancellationToken).ConfigureAwait(false)
            ?? await ReadRegistryRowAsync(connection, noIndustry, siteKey, cancellationToken).ConfigureAwait(false)
            ?? await ReadRegistryRowAsync(connection, minimal, siteKey, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var dbName = string.IsNullOrWhiteSpace(row.Value.DbName) ? siteKey : row.Value.DbName;
        var email = string.IsNullOrWhiteSpace(row.Value.Email)
            ? CpTenantsWriteService.OperatorEmailFor(siteKey)
            : row.Value.Email;
        return (dbName, email, row.Value.TempPassword, row.Value.IndustryCode);
    }

    private static async Task<(string DbName, string Email, string TempPassword, string IndustryCode)?> ReadRegistryRowAsync(
        DbConnection connection,
        string sql,
        string siteKey,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            ErpDb.AddParameters(command, siteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return (
                reader["db_name"]?.ToString()?.Trim() ?? string.Empty,
                reader["from_email"]?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty,
                reader["operator_temp_password"]?.ToString() ?? string.Empty,
                reader["industry_code"]?.ToString()?.Trim() ?? string.Empty);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<bool> CreateDatabaseAsync(
        DbConnection connection,
        string dbName,
        CancellationToken cancellationToken)
    {
        if (!CpTenantsWriteService.IsDroppableDatabase(dbName))
        {
            return false;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                "CREATE DATABASE IF NOT EXISTS `" + dbName + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
