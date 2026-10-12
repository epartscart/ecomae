using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Super CP tenant control writes: PHP <c>ajax_portal.php</c> / <c>epc_portal_tenant_control_set_active</c>
/// plus native ERP-only onboard / standalone delete / operator temp-password reset.
/// This service does not invent a send — passwords are shown once in the write result.
/// </summary>
public interface ICpTenantsWriteService
{
    Task<ErpSimpleWriteResult> SetActiveAsync(
        CpTenantsSetActiveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> OnboardErpOnlyAsync(
        CpTenantsOnboardErpOnlyRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteStandaloneErpAsync(
        CpTenantsDeleteStandaloneRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ResetOperatorPasswordAsync(
        string? siteKey,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> OnboardFourLaunchClientsAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RepairErpOnlyShopAsync(
        string? siteKey,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RepairFourLaunchClientsAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetOperatorPasswordAsync(
        string? siteKey,
        string? email,
        string? password,
        string? confirmPassword,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddOperatorLoginAsync(
        string? siteKey,
        string? email,
        string? password,
        string? confirmPassword,
        CancellationToken cancellationToken = default);
}

public sealed record CpTenantsSetActiveRequest(string? SiteKey, bool Active);

public sealed record CpTenantsOnboardErpOnlyRequest(
    string? SiteKey,
    string? TradeName,
    string? IndustryCode,
    string? AdminEmail);

public sealed record CpTenantsDeleteStandaloneRequest(string? SiteKey, string? ConfirmSiteKey);

public sealed class CpTenantsWriteService : ICpTenantsWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbNameSafe = new("^[A-Za-z0-9_]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EmailSafe = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Commerce / platform registry rows that must never be hard-deleted here.</summary>
    public static readonly HashSet<string> ProtectedSiteKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ecomae", "ecomae_corp", "platform", "epartscart", "taxofinca",
        "thejewellerytrend", "stylelookok", "wheelspartspk", "industries",
    };

    public static readonly HashSet<string> ProtectedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "docpart", "ecomae", "epartscart",
    };

    private static readonly string[] EnsureColumnsSql =
    [
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `hosted_on` VARCHAR(16) NOT NULL DEFAULT 'client'",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `dedicated_db` TINYINT(1) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `scale_policy` VARCHAR(32) NOT NULL DEFAULT 'shared_docpart'",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 1",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `is_demo` TINYINT(1) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `operator_temp_password` VARCHAR(120) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `intro_json` MEDIUMTEXT NULL",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `from_email` VARCHAR(120) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `blockchain_mode` VARCHAR(24) NOT NULL DEFAULT 'anchor'",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `hub_name` VARCHAR(120) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `notes` VARCHAR(500) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `db_user` VARCHAR(64) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `db_password` VARCHAR(255) NOT NULL DEFAULT ''",
    ];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpOnlyShopBootstrap? _shopBootstrap;

    public CpTenantsWriteService(
        IErpWriteConnectionFactory connections,
        IErpOnlyShopBootstrap? shopBootstrap = null)
    {
        _connections = connections;
        _shopBootstrap = shopBootstrap;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($site_key))</c>.</summary>
    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string ToggleMessage(bool active)
        => active ? "Tenant enabled" : "Tenant disabled — storefront and CP blocked";

    public static bool IsDroppableDatabase(string? dbName)
    {
        var db = (dbName ?? string.Empty).Trim();
        return db.Length > 0
            && DbNameSafe.IsMatch(db)
            && !ProtectedDatabases.Contains(db);
    }

    public static bool IsProtectedSiteKey(string? siteKey)
        => ProtectedSiteKeys.Contains(NormalizeSiteKey(siteKey));

    public static bool IsAllowedIndustry(string? industryCode)
        => PhpPortalIndustryCatalog.Knows(industryCode)
           && !string.Equals(industryCode, "platform_host", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeIndustry(string? industryCode)
    {
        var code = (industryCode ?? string.Empty).Trim().ToLowerInvariant();
        return IsAllowedIndustry(code) ? code : "auto_parts";
    }

    /// <summary>ERP-only operator login is always the four-letter company key at ecomae.com.</summary>
    public static string OperatorEmailFor(string? siteKey)
    {
        var key = NormalizeSiteKey(siteKey);
        return key.Length == 0 ? string.Empty : key + "@ecomae.com";
    }

    public static bool IsUsableLoginEmail(string? email)
        => EmailSafe.IsMatch((email ?? string.Empty).Trim().ToLowerInvariant());

    public static bool IsUsableOperatorPassword(string? password)
    {
        var value = password ?? string.Empty;
        return value.Length is >= 8 and <= 72;
    }

    public async Task<ErpSimpleWriteResult> SetActiveAsync(
        CpTenantsSetActiveRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var active = request.Active ? 1 : 0;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Tenant not in registry");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_portal_tenants` SET `is_active`=?, `updated_at`=? WHERE `site_key`=?"),
                cancellationToken, active, now, siteKey).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok(ToggleMessage(request.Active), id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant registry table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> OnboardErpOnlyAsync(
        CpTenantsOnboardErpOnlyRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length is < 2 or > 32)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key must be 2–32 letters, numbers, or underscore.");
        }

        if (IsProtectedSiteKey(siteKey))
        {
            return ErpSimpleWriteResult.Fail("protected", "That site key is reserved for an existing commerce or platform tenant.");
        }

        var trade = (request.TradeName ?? string.Empty).Trim();
        if (trade.Length == 0)
        {
            trade = siteKey.ToUpperInvariant();
        }

        var industry = NormalizeIndustry(request.IndustryCode);
        var email = OperatorEmailFor(siteKey);
        var supplied = (request.AdminEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (EmailSafe.IsMatch(supplied) && supplied.EndsWith("@ecomae.com", StringComparison.Ordinal))
        {
            email = supplied;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var password = NewTempPassword();
        var intro = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["admin_cp_email"] = email,
            ["operator_login_email"] = email,
            ["erp_only_shared"] = 1,
            ["erp_only"] = true,
            ["industry_code"] = industry,
        });

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var existing = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);
            if (existing > 0)
            {
                return ErpSimpleWriteResult.Fail("exists", "Site key already exists. Delete the old standalone row first, or pick another key.");
            }

            var dbTaken = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `db_name`=? AND (`dedicated_db`=1 OR `erp_only_shared`=1) LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);
            if (dbTaken > 0 || !IsDroppableDatabase(siteKey))
            {
                return ErpSimpleWriteResult.Fail("db_taken", "Database name is reserved or already assigned.");
            }

            var dbProvisioned = await CreateDatabaseAsync(connection, siteKey, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_portal_tenants` "
                    + "(`site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`hub_name`,`from_email`,"
                    + "`db_name`,`db_user`,`db_password`,`notes`,`intro_json`,`hosted_on`,`erp_only_shared`,"
                    + "`dedicated_db`,`scale_policy`,`blockchain_mode`,`is_active`,`is_demo`,"
                    + "`operator_temp_password`,`created_at`,`updated_at`) "
                    + "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                siteKey,
                "www.ecomae.com",
                industry,
                "live",
                trade.Length > 120 ? trade[..120] : trade,
                "",
                email.Length > 120 ? email[..120] : email,
                siteKey,
                "",
                "",
                "ERP-only. No public storefront.",
                intro,
                "platform",
                1,
                1,
                "dedicated_mysql",
                "anchor",
                1,
                0,
                password,
                now,
                now).ConfigureAwait(false);

            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);

            var shop = await EnsureShopAsync(siteKey, password, cancellationToken).ConfigureAwait(false);
            var login = "https://www.ecomae.com/cp/client-erp/" + siteKey + "/";
            var dbNote = dbProvisioned ? "dedicated DB created" : "registry saved — create the MySQL database on CloudPanel if CREATE was refused";
            return ErpSimpleWriteResult.Ok(
                "ERP-only tenant " + siteKey + " created (" + industry + "). Login " + login
                + " · email " + email + " · temporary password " + password + " · " + dbNote
                + (shop.Ok ? " · " + shop.Message : "")
                + ". Copy the password now — this page does not invent a send.",
                id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Could not register the ERP-only tenant.");
        }
    }

    public async Task<ErpSimpleWriteResult> OnboardFourLaunchClientsAsync(CancellationToken cancellationToken = default)
    {
        var created = new List<string>();
        var existed = new List<string>();
        var failed = new List<string>();
        foreach (var client in ErpOnlyLaunchClients.All)
        {
            var result = await OnboardErpOnlyAsync(
                new CpTenantsOnboardErpOnlyRequest(client.SiteKey, client.TradeName, client.IndustryCode, client.Email),
                cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                created.Add(client.SiteKey + " · " + result.Message);
            }
            else if (string.Equals(result.Code, "exists", StringComparison.Ordinal))
            {
                existed.Add(client.SiteKey);
            }
            else
            {
                failed.Add(client.SiteKey + ":" + result.Code);
            }
        }

        if (created.Count == 0 && existed.Count == 0)
        {
            return ErpSimpleWriteResult.Fail(
                failed.Count == 0 ? "db" : failed[0].Split(':').Last(),
                "Could not create ASAP/INDJ/FITO/REAL. " + string.Join("; ", failed));
        }

        var parts = new List<string>();
        if (created.Count > 0)
        {
            parts.Add("Created " + created.Count + ": " + string.Join(" | ", created));
        }

        if (existed.Count > 0)
        {
            parts.Add("Already present: " + string.Join(", ", existed));
        }

        if (failed.Count > 0)
        {
            parts.Add("Failed: " + string.Join("; ", failed));
        }

        parts.Add("Copy passwords now — this page does not invent a send.");
        return ErpSimpleWriteResult.Ok(string.Join(" ", parts), created.Count);
    }

    public async Task<ErpSimpleWriteResult> RepairErpOnlyShopAsync(
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site key");
        }

        if (IsProtectedSiteKey(key))
        {
            return ErpSimpleWriteResult.Fail("protected", "Commerce and platform tenants are not repaired here.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        string password;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var stored = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(`operator_temp_password`,'') FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, key).ConfigureAwait(false);
            if (stored is null)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Tenant not in registry");
            }

            password = stored.Trim();
            if (password.Length == 0)
            {
                password = NewTempPassword();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_portal_tenants` SET `operator_temp_password`=?, `updated_at`=? WHERE `site_key`=?"),
                    cancellationToken, password, now, key).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Could not read the ERP-only tenant.");
        }

        var shop = await EnsureShopAsync(key, password, cancellationToken).ConfigureAwait(false);
        if (!shop.Ok)
        {
            return ErpSimpleWriteResult.Fail("shop", shop.Message);
        }

        return ErpSimpleWriteResult.Ok(
            "Shop login repaired for " + key + " · " + shop.Message
            + " · temporary password " + password
            + ". Copy it now — this page does not invent a send.",
            1);
    }

    public async Task<ErpSimpleWriteResult> RepairFourLaunchClientsAsync(CancellationToken cancellationToken = default)
    {
        var ok = new List<string>();
        var failed = new List<string>();
        foreach (var client in ErpOnlyLaunchClients.All)
        {
            var result = await RepairErpOnlyShopAsync(client.SiteKey, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                ok.Add(client.SiteKey + " · " + result.Message);
            }
            else
            {
                failed.Add(client.SiteKey + ":" + result.Code);
            }
        }

        if (ok.Count == 0)
        {
            return ErpSimpleWriteResult.Fail(
                "shop",
                "Could not repair ASAP/INDJ/FITO/REAL shop login. " + string.Join("; ", failed));
        }

        var parts = new List<string> { "Repaired " + ok.Count + ": " + string.Join(" | ", ok) };
        if (failed.Count > 0)
        {
            parts.Add("Failed: " + string.Join("; ", failed));
        }

        parts.Add("Copy passwords now — this page does not invent a send.");
        return ErpSimpleWriteResult.Ok(string.Join(" ", parts), ok.Count);
    }

    public async Task<ErpSimpleWriteResult> SetOperatorPasswordAsync(
        string? siteKey,
        string? email,
        string? password,
        string? confirmPassword,
        CancellationToken cancellationToken = default)
    {
        var check = ValidateLoginWrite(siteKey, email, password, confirmPassword, emailRequired: false);
        if (check.Fail is not null)
        {
            return check.Fail;
        }

        var shop = await EnsureOperatorLoginAsync(
            check.SiteKey, check.Email, check.Password, cancellationToken).ConfigureAwait(false);
        if (!shop.Ok)
        {
            return ErpSimpleWriteResult.Fail("shop", shop.Message);
        }

        if (IsPrimaryOperatorEmail(check.Email, check.SiteKey, check.PrimaryEmail))
        {
            await StoreTempPasswordAsync(check.SiteKey, check.Password, cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            "Password set for " + check.Email + " on " + check.SiteKey
            + ". Sign in at /cp/client-erp/" + check.SiteKey
            + "/ — this page does not invent a send.",
            1);
    }

    public async Task<ErpSimpleWriteResult> AddOperatorLoginAsync(
        string? siteKey,
        string? email,
        string? password,
        string? confirmPassword,
        CancellationToken cancellationToken = default)
    {
        var check = ValidateLoginWrite(siteKey, email, password, confirmPassword, emailRequired: true);
        if (check.Fail is not null)
        {
            return check.Fail;
        }

        if (IsPrimaryOperatorEmail(check.Email, check.SiteKey, check.PrimaryEmail))
        {
            return await SetOperatorPasswordAsync(
                check.SiteKey, check.Email, check.Password, check.Password, cancellationToken).ConfigureAwait(false);
        }

        var shop = await EnsureOperatorLoginAsync(
            check.SiteKey, check.Email, check.Password, cancellationToken).ConfigureAwait(false);
        if (!shop.Ok)
        {
            return ErpSimpleWriteResult.Fail("shop", shop.Message);
        }

        await AppendExtraLoginAsync(check.SiteKey, check.Email, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Extra login " + check.Email + " added for " + check.SiteKey
            + ". Same company URL /cp/client-erp/" + check.SiteKey
            + "/ — copy the password now; this page does not invent a send.",
            1);
    }

    public async Task<ErpSimpleWriteResult> DeleteStandaloneErpAsync(
        CpTenantsDeleteStandaloneRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        var confirm = NormalizeSiteKey(request.ConfirmSiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site key");
        }

        if (!string.Equals(siteKey, confirm, StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("confirm", "Type the site key again to confirm complete delete.");
        }

        if (IsProtectedSiteKey(siteKey))
        {
            return ErpSimpleWriteResult.Fail("protected", "Commerce and platform tenants cannot be deleted here.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            string dbName;
            string dbUser;
            int erpOnly;
            int isDemo;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT `db_name`, `db_user`, IFNULL(`erp_only_shared`,0) AS erp_only_shared, "
                    + "IFNULL(`is_demo`,0) AS is_demo, `hosted_on`, `industry_code` "
                    + "FROM `epc_portal_tenants` WHERE `site_key`=@p0 LIMIT 1";
                ErpDb.AddParameters(command, siteKey);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Tenant not in registry");
                }

                dbName = reader["db_name"]?.ToString()?.Trim() ?? string.Empty;
                dbUser = reader["db_user"]?.ToString()?.Trim() ?? string.Empty;
                erpOnly = Convert.ToInt32(reader["erp_only_shared"] is DBNull ? 0 : reader["erp_only_shared"]);
                isDemo = Convert.ToInt32(reader["is_demo"] is DBNull ? 0 : reader["is_demo"]);
                var hostedOn = reader["hosted_on"]?.ToString() ?? string.Empty;
                var industry = reader["industry_code"]?.ToString() ?? string.Empty;
                var type = CpTenantControlCenterService.TenantType(isDemo != 0, erpOnly != 0, industry, hostedOn);
                if (type is not "erp_only")
                {
                    return ErpSimpleWriteResult.Fail("not_standalone", "Only ERP-only (no storefront) tenants can be deleted here.");
                }
            }

            if (isDemo != 0)
            {
                return ErpSimpleWriteResult.Fail("demo", "Demo tenants delete from Demo tenants, not this form.");
            }

            var dropped = await DropDatabaseAsync(connection, dbName, dbUser, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_portal_tenants` WHERE `site_key`=?"),
                cancellationToken, siteKey).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok(
                "Standalone ERP tenant " + siteKey + " deleted"
                + (dropped ? " (database dropped)" : " (registry removed; database drop skipped)") + ".",
                0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Could not delete the standalone ERP tenant.");
        }
    }

    public async Task<ErpSimpleWriteResult> ResetOperatorPasswordAsync(
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var password = NewTempPassword();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var id = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, key).ConfigureAwait(false);
            if (id <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Tenant not in registry");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_portal_tenants` SET `operator_temp_password`=?, `updated_at`=? WHERE `site_key`=?"),
                cancellationToken, password, now, key).ConfigureAwait(false);

            var shop = await EnsureShopAsync(key, password, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Temporary password for " + key + " is " + password
                + (shop.Ok ? " · " + shop.Message : " · shop seed skipped — login will retry ensure")
                + ". Copy it now — this page does not invent a send.",
                id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Could not reset the operator password.");
        }
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SHOW COLUMNS FROM `epc_portal_tenants`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                existing.Add(reader.GetValue(0)?.ToString() ?? string.Empty);
            }
        }

        foreach (var sql in EnsureColumnsSql)
        {
            var parts = sql.Split('`');
            if (parts.Length < 4)
            {
                continue;
            }

            if (!existing.Contains(parts[3]))
            {
                await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<bool> CreateDatabaseAsync(
        DbConnection connection,
        string dbName,
        CancellationToken cancellationToken)
    {
        if (!IsDroppableDatabase(dbName))
        {
            return false;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                "CREATE DATABASE IF NOT EXISTS `" + dbName + "` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci",
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task<bool> DropDatabaseAsync(
        DbConnection connection,
        string dbName,
        string dbUser,
        CancellationToken cancellationToken)
    {
        if (!IsDroppableDatabase(dbName))
        {
            return false;
        }

        try
        {
            await ErpDb.ExecuteAsync(connection, null, "DROP DATABASE IF EXISTS `" + dbName + "`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return false;
        }

        if (dbUser.Length > 0 && DbNameSafe.IsMatch(dbUser) && !string.Equals(dbUser, dbName, StringComparison.Ordinal))
        {
            await ErpDb.TryExecuteAsync(connection, "DROP USER IF EXISTS '" + dbUser + "'@'localhost'", cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private async Task<ErpOnlyShopEnsureResult> EnsureShopAsync(
        string siteKey,
        string? password,
        CancellationToken cancellationToken)
    {
        if (_shopBootstrap is null)
        {
            return new ErpOnlyShopEnsureResult(false, false, false, false, "shop bootstrap not registered");
        }

        return await _shopBootstrap.EnsureForSiteKeyAsync(siteKey, password, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ErpOnlyShopEnsureResult> EnsureOperatorLoginAsync(
        string siteKey,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        if (_shopBootstrap is null)
        {
            return new ErpOnlyShopEnsureResult(false, false, false, false, "shop bootstrap not registered");
        }

        return await _shopBootstrap
            .EnsureOperatorLoginAsync(siteKey, email, password, replacePassword: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private sealed record LoginWriteCheck(
        ErpSimpleWriteResult? Fail,
        string SiteKey,
        string Email,
        string Password,
        string PrimaryEmail);

    private LoginWriteCheck ValidateLoginWrite(
        string? siteKey,
        string? email,
        string? password,
        string? confirmPassword,
        bool emailRequired)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return new LoginWriteCheck(ErpSimpleWriteResult.Fail("invalid", "Invalid site key"), "", "", "", "");
        }

        if (IsProtectedSiteKey(key))
        {
            return new LoginWriteCheck(
                ErpSimpleWriteResult.Fail("protected", "Commerce and platform tenants are not edited here."),
                key, "", "", "");
        }

        if (!IsUsableOperatorPassword(password))
        {
            return new LoginWriteCheck(
                ErpSimpleWriteResult.Fail("password", "Password must be 8–72 characters."),
                key, "", "", "");
        }

        if (!string.Equals(password, confirmPassword ?? string.Empty, StringComparison.Ordinal))
        {
            return new LoginWriteCheck(
                ErpSimpleWriteResult.Fail("confirm", "Password and confirmation do not match."),
                key, "", "", "");
        }

        var primary = OperatorEmailFor(key);
        var contact = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (contact.Length == 0)
        {
            if (emailRequired)
            {
                return new LoginWriteCheck(
                    ErpSimpleWriteResult.Fail("email", "Enter the extra login email."),
                    key, "", "", primary);
            }

            contact = primary;
        }

        if (!IsUsableLoginEmail(contact))
        {
            return new LoginWriteCheck(
                ErpSimpleWriteResult.Fail("email", "Enter a valid login email."),
                key, "", "", primary);
        }

        return new LoginWriteCheck(null, key, contact, password!, primary);
    }

    private static bool IsPrimaryOperatorEmail(string email, string siteKey, string primaryEmail)
        => string.Equals(email, primaryEmail, StringComparison.OrdinalIgnoreCase)
           || string.Equals(email, OperatorEmailFor(siteKey), StringComparison.OrdinalIgnoreCase);

    private async Task StoreTempPasswordAsync(string siteKey, string password, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_portal_tenants` SET `operator_temp_password`=?, `updated_at`=? WHERE `site_key`=?"),
                cancellationToken, password, now, siteKey).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private async Task AppendExtraLoginAsync(string siteKey, string email, CancellationToken cancellationToken)
    {
        if (!_connections.IsConfigured)
        {
            return;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var raw = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `intro_json` FROM `epc_portal_tenants` WHERE `site_key`=? LIMIT 1"),
                cancellationToken, siteKey).ConfigureAwait(false);
            var next = CpTenantControlCenterService.MergeExtraLoginEmail(raw, email);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_portal_tenants` SET `intro_json`=?, `updated_at`=? WHERE `site_key`=?"),
                cancellationToken, next, now, siteKey).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static string NewTempPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var chars = new char[12];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }
}
