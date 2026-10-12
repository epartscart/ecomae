using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Services;

public sealed record ClientErpTenantRecord(
    string SiteKey,
    string TradeName,
    string IndustryCode,
    string DatabaseName,
    string? DbUser,
    string? DbPassword,
    bool DedicatedDb,
    bool IsActive,
    bool IsDemo);

public interface IClientErpTenantLookup
{
    ValueTask<ClientErpTenantRecord?> FindActiveErpOnlyAsync(string siteKey, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ClientErpTenantRecord>> ListActiveErpOnlyAsync(CancellationToken cancellationToken = default);
}

public sealed class NullClientErpTenantLookup : IClientErpTenantLookup
{
    public static readonly NullClientErpTenantLookup Instance = new();

    public ValueTask<ClientErpTenantRecord?> FindActiveErpOnlyAsync(string siteKey, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<ClientErpTenantRecord?>(null);

    public ValueTask<IReadOnlyList<ClientErpTenantRecord>> ListActiveErpOnlyAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<ClientErpTenantRecord>>([]);
}

/// <summary>
/// Registry lookup by <c>site_key</c> for Super-host <c>/cp/client-erp/{key}</c>.
/// Reads <c>epc_portal_tenants</c> on the platform connection — never the request tenant DB.
/// </summary>
public sealed class DbClientErpTenantLookup : IClientErpTenantLookup
{
    private const string SelectBySiteKey = """
        SELECT
            `site_key`,
            IFNULL(`trade_name`, '') AS trade_name,
            IFNULL(`industry_code`, '') AS industry_code,
            IFNULL(`hosted_on`, '') AS hosted_on,
            IFNULL(`db_name`, '') AS db_name,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            IFNULL(`scale_policy`, '') AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `site_key` = @siteKey
        LIMIT 1
        """;

    private const string SelectActiveList = """
        SELECT
            `site_key`,
            IFNULL(`trade_name`, '') AS trade_name,
            IFNULL(`industry_code`, '') AS industry_code,
            IFNULL(`hosted_on`, '') AS hosted_on,
            IFNULL(`db_name`, '') AS db_name,
            IFNULL(`db_user`, '') AS db_user,
            IFNULL(`db_password`, '') AS db_password,
            `status`,
            `is_demo`,
            `erp_only_shared`,
            `is_active`,
            `dedicated_db`,
            IFNULL(`scale_policy`, '') AS scale_policy
        FROM `epc_portal_tenants`
        WHERE `site_key` <> ''
        ORDER BY `site_key` ASC
        """;

    private readonly ITenantDbConnectionFactory _connections;

    public DbClientErpTenantLookup(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async ValueTask<ClientErpTenantRecord?> FindActiveErpOnlyAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        var key = ClientErpPath.NormalizeSiteKey(siteKey);
        if (key.Length == 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = SelectBySiteKey;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@siteKey";
            parameter.Value = key;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var row = ReadRow(reader);
            return IsUsableErpOnly(row) ? row : null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async ValueTask<IReadOnlyList<ClientErpTenantRecord>> ListActiveErpOnlyAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = SelectActiveList;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<ClientErpTenantRecord>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = ReadRow(reader);
                if (IsUsableErpOnly(row))
                {
                    rows.Add(row);
                }
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    internal static bool IsUsableErpOnly(ClientErpTenantRecord row)
        => row.IsActive
            && !row.IsDemo
            && !string.IsNullOrWhiteSpace(row.DatabaseName);

    internal static bool IsErpOnlyType(bool erpOnlyShared, string industryCode, string hostedOn)
        => CpTenantControlCenterService.TenantType(false, erpOnlyShared, industryCode, hostedOn) == "erp_only";

    private static ClientErpTenantRecord ReadRow(DbDataReader reader)
    {
        var siteKey = ClientErpPath.NormalizeSiteKey(Text(reader, "site_key"));
        var erpOnlyShared = CpTenantControlCenterService.CoerceFlag(Value(reader, "erp_only_shared"), false);
        var industry = Text(reader, "industry_code");
        var hostedOn = Text(reader, "hosted_on");
        var isActive = CpTenantControlCenterService.CoerceFlag(Value(reader, "is_active"), true);
        var isDemo = CpTenantControlCenterService.CoerceFlag(Value(reader, "is_demo"), false);
        var dedicated = CpTenantControlCenterService.CoerceFlag(Value(reader, "dedicated_db"), false)
            || string.Equals(Text(reader, "scale_policy"), "dedicated_mysql", StringComparison.OrdinalIgnoreCase);
        var usableType = IsErpOnlyType(erpOnlyShared, industry, hostedOn);
        return new ClientErpTenantRecord(
            siteKey,
            Text(reader, "trade_name"),
            industry,
            Text(reader, "db_name"),
            EmptyToNull(Text(reader, "db_user")),
            EmptyToNull(Text(reader, "db_password")),
            dedicated,
            isActive && usableType,
            isDemo);
    }

    private static object? Value(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
    }

    private static string Text(DbDataReader reader, string column)
    {
        var value = Value(reader, column);
        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
