using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>epc_demo_tenants_manage.php</c> twin over <c>epc_portal_demo_list</c>, <c>epc_portal_demo_count_active</c>,
/// <c>epc_portal_demo_extend</c>, <c>epc_portal_demo_convert</c> and <c>epc_portal_demo_force_delete</c>.
/// </summary>
public interface ICpDemoTenantsService
{
    Task<CpDemoTenantsView> LoadAsync(CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ExtendAsync(string siteKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ConvertAsync(string siteKey, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string siteKey, CancellationToken cancellationToken = default);
}

public sealed record CpDemoTenantRow(
    string SiteKey,
    string TradeName,
    string IndustryCode,
    string AdminEmail,
    bool PasswordStored,
    long CreatedAt,
    long ExpiresAt,
    int DaysLeft,
    bool Expired,
    bool Suspended,
    string DemoHostname,
    string StorefrontUrl,
    string CpScopedUrl);

public sealed record CpDemoTenantsView(
    IReadOnlyList<CpDemoTenantRow> Demos,
    int Active,
    int MaxActive,
    int DemoDays,
    string Error);

public sealed class CpDemoTenantsService : ICpDemoTenantsService
{
    public const int MaxActive = 30;
    public const int DemoDays = 3;
    public const int ExtendDays = 3;
    public const string PublicBase = "https://www.ecomae.com";
    public const string CpPathPrefix = "/cp/demo/";

    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex DbNameSafe = new("^[A-Za-z0-9_]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] ProtectedDatabases = ["docpart", "ecomae", "epartscart"];

    public const string ListSql =
        "SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 ORDER BY `demo_expires_at` ASC, `created_at` DESC";

    public const string CountActiveSql =
        "SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `is_demo` = 1 AND `status` IN ('dns_pending', 'live') "
        + "AND (`demo_expires_at` = 0 OR `demo_expires_at` > @p0)";

    private static readonly string[] EnsureColumnsSql =
    [
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `is_demo` TINYINT(1) NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `demo_expires_at` INT NOT NULL DEFAULT 0",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `demo_contact_email` VARCHAR(120) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `demo_contact_phone` VARCHAR(32) NOT NULL DEFAULT ''",
        "ALTER TABLE `epc_portal_tenants` ADD COLUMN `demo_extended_count` INT NOT NULL DEFAULT 0",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpDemoTenantsService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>max(0, ceil((exp - now) / 86400))</c>, 0 when no expiry.</summary>
    public static int DaysLeft(long expiresAt, long now)
        => expiresAt > 0 ? (int)Math.Max(0, Math.Ceiling((expiresAt - now) / 86400d)) : 0;

    /// <summary>PHP <c>epc_portal_demo_row_is_erp_only</c>.</summary>
    public static bool IsErpOnly(string? introJson, string? industryCode)
    {
        var intro = (introJson ?? string.Empty).Trim();
        if (intro.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(intro);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("demo_erp_only", out var flag)
                    && flag.ValueKind is JsonValueKind.True or JsonValueKind.Number or JsonValueKind.String
                    && flag.ToString() is not ("" or "0" or "false" or "False"))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
            }
        }

        return string.Equals(industryCode, "erp_only", StringComparison.Ordinal);
    }

    public static bool IsDroppableDatabase(string? dbName)
    {
        var db = (dbName ?? string.Empty).Trim();
        return db.Length > 0
            && DbNameSafe.IsMatch(db)
            && !ProtectedDatabases.Contains(db, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<CpDemoTenantsView> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], 0, MaxActive, DemoDays, "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var active = (int)await ErpDb.LongAsync(connection, null, CountActiveSql, cancellationToken, now).ConfigureAwait(false);
            var rows = new List<CpDemoTenantRow>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ListSql;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var values = Values(reader);
                    var key = NormalizeSiteKey(Get(values, "site_key"));
                    var exp = Number(values, "demo_expires_at");
                    var suspended = values.ContainsKey("is_active") && Number(values, "is_active") == 0;
                    var erpOnly = IsErpOnly(Get(values, "intro_json"), Get(values, "industry_code"));
                    rows.Add(new CpDemoTenantRow(
                        key,
                        Get(values, "trade_name"),
                        Get(values, "industry_code"),
                        Get(values, "demo_contact_email").Trim(),
                        Get(values, "operator_temp_password").Trim().Length > 0,
                        Number(values, "created_at"),
                        exp,
                        DaysLeft(exp, now),
                        exp > 0 && exp < now,
                        suspended,
                        key.Length > 0 ? "www.ecomae.com/demo/" + key : string.Empty,
                        erpOnly || key.Length == 0 ? string.Empty : PublicBase + "/demo/" + key + "/en/",
                        PublicBase + CpPathPrefix + key + "/"));
                }
            }

            return new(rows, active, MaxActive, DemoDays, string.Empty);
        }
        catch (DbException)
        {
            return new([], 0, MaxActive, DemoDays, "Platform database unavailable.");
        }
    }

    public async Task<ErpSimpleWriteResult> ExtendAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Demo tenant not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var current = await ErpDb.ScalarAsync(
                connection, null,
                "SELECT `demo_expires_at` FROM `epc_portal_tenants` WHERE `site_key` = @p0 AND `is_demo` = 1 LIMIT 1",
                cancellationToken, key).ConfigureAwait(false);
            if (current is null || current is DBNull)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Demo tenant not found");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var baseTs = Math.Max(now, Convert.ToInt64(current, CultureInfo.InvariantCulture));
            var newExp = baseTs + (ExtendDays * 86400L);
            await ErpDb.ExecuteAsync(
                connection, null,
                "UPDATE `epc_portal_tenants` SET `demo_expires_at` = @p0, `demo_extended_count` = `demo_extended_count` + 1, `updated_at` = @p1 WHERE `site_key` = @p2",
                cancellationToken, newExp, now, key).ConfigureAwait(false);
            var until = DateTimeOffset.FromUnixTimeSeconds(newExp).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return ErpSimpleWriteResult.Ok("Extended +" + ExtendDays.ToString(CultureInfo.InvariantCulture) + " days until " + until, newExp);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public async Task<ErpSimpleWriteResult> ConvertAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Demo tenant not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            if (!await IsDemoAsync(connection, key, cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Demo tenant not found");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                "UPDATE `epc_portal_tenants` SET `is_demo` = 0, `demo_expires_at` = 0, `status` = 'dns_pending', `updated_at` = @p0 WHERE `site_key` = @p1",
                cancellationToken, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), key).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Converted to live tenant draft — assign client domain in Tenant hub", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Demo tenant not found");
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
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT `db_name`, `db_user` FROM `epc_portal_tenants` WHERE `site_key` = @p0 AND `is_demo` = 1 LIMIT 1";
                ErpDb.AddParameters(command, key);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Demo tenant not found");
                }

                var values = Values(reader);
                dbName = Get(values, "db_name").Trim();
                dbUser = Get(values, "db_user").Trim();
            }

            var dropped = await DropDatabaseAsync(connection, dbName, dbUser, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                "DELETE FROM `epc_portal_tenants` WHERE `site_key` = @p0",
                cancellationToken, key).ConfigureAwait(false);
            try
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    "UPDATE `epc_portal_demo_requests` SET `status` = 'deleted' WHERE `site_key` = @p0",
                    cancellationToken, key).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }

            return ErpSimpleWriteResult.Ok("Demo deleted: " + key + (dropped ? " (DB dropped)" : " (DB drop: skipped)"), 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
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

    private static async Task<bool> IsDemoAsync(DbConnection connection, string key, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection, null,
            "SELECT COUNT(*) FROM `epc_portal_tenants` WHERE `site_key` = @p0 AND `is_demo` = 1",
            cancellationToken, key).ConfigureAwait(false) > 0;

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
            var column = sql.Split('`')[3];
            if (!existing.Contains(column))
            {
                await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static Dictionary<string, string> Values(DbDataReader reader)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            values[reader.GetName(i)] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
        }

        return values;
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;

    private static long Number(IReadOnlyDictionary<string, string> values, string key)
        => long.TryParse(Get(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
