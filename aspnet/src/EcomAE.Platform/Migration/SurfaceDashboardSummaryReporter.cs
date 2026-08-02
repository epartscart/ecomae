using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Read-only CP/ERP/BOS/storefront digests for migration shells. Performs zero writes.
/// Missing tables degrade to zeros with a database-error/migration source.
/// </summary>
public sealed class SurfaceDashboardSummaryReporter : ISurfaceDashboardSummaryReporter
{
    private readonly ITenantDbConnectionFactory _connections;

    public SurfaceDashboardSummaryReporter(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var users = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountUsers, cancellationToken).ConfigureAwait(false);
            var adminSessions = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            var tenants = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountPortalTenants, cancellationToken).ConfigureAwait(false);
            var active = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActivePortalTenants, cancellationToken).ConfigureAwait(false);
            return new(users, adminSessions, tenants, active, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpDashboardSummary> BuildErpAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var cash = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumCashBankTotal, cancellationToken).ConfigureAwait(false);
            var credit = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumSupplierCredit, cancellationToken).ConfigureAwait(false);
            var debit = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumSupplierDebit, cancellationToken).ConfigureAwait(false);
            var cashAccounts = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCashAccounts, cancellationToken).ConfigureAwait(false);
            var suppliers = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActiveSuppliers, cancellationToken).ConfigureAwait(false);
            var purchases = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActivePurchases, cancellationToken).ConfigureAwait(false);
            return new(cash, credit, debit, credit - debit, cashAccounts, suppliers, purchases, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var list = await ReadTenantsAsync(connection, 500, cancellationToken).ConfigureAwait(false);
            var adminSessions = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            return SummarizeFleet(list, adminSessions, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontAccountSummary> BuildStorefrontAccountAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(userId, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        if (userId <= 0)
        {
            return new(0, 0, 0, "rejected", "Valid customer user id is required.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var orders = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerOrders, userId, cancellationToken).ConfigureAwait(false);
            var sessions = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerSessionsForUser, userId, cancellationToken).ConfigureAwait(false);
            return new(userId, orders, sessions, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<PortalTenantListResult> ListPortalTenantsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var tenants = await ReadTenantsAsync(connection, safeLimit, cancellationToken).ConfigureAwait(false);
            return new(tenants, tenants.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<BosFleetHealthResult> BuildBosFleetHealthAsync(int sampleLimit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(sampleLimit, 1, 100);
        if (!_connections.IsConfigured)
        {
            return new(new(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured."), [], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var tenants = await ReadTenantsAsync(connection, 500, cancellationToken).ConfigureAwait(false);
            var adminSessions = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            var summary = SummarizeFleet(tenants, adminSessions, "database", string.Empty);
            var sample = tenants.Take(safeLimit).ToArray();
            return new(summary, sample, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(new(0, 0, 0, 0, 0, "database-error", ex.Message), [], "database-error", ex.Message);
        }
    }

    public async Task<ErpAccountsSummaryResult> BuildErpAccountsAsync(CancellationToken cancellationToken = default)
    {
        var summary = await BuildErpAsync(cancellationToken).ConfigureAwait(false);
        return new(summary, summary.Source, summary.Message);
    }

    public async Task<StorefrontOrdersResult> ListStorefrontOrdersAsync(int userId, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        if (!_connections.IsConfigured)
        {
            return new(userId, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        if (userId <= 0)
        {
            return new(0, [], 0, "rejected", "Valid customer user id is required.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectCustomerOrders;
            AddParameter(command, "@userId", userId);
            AddParameter(command, "@limit", safeLimit);

            var rows = new List<StorefrontOrderDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontOrderDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["paid"] is DBNull ? 0 : reader["paid"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["successfully_created"] is DBNull ? 0 : reader["successfully_created"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["status"] is DBNull ? 0 : reader["status"], CultureInfo.InvariantCulture)));
            }

            return new(userId, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, [], 0, "database-error", ex.Message);
        }
    }

    private static BosFleetSummary SummarizeFleet(IReadOnlyList<PortalTenantDigest> tenants, int adminSessions, string source, string message)
        => new(
            tenants.Count,
            tenants.Count(item => item.IsActive),
            adminSessions,
            tenants.Count(item => item.HasDb),
            tenants.Count(item => item.ErpOnly),
            source,
            message);

    private static async Task<IReadOnlyList<PortalTenantDigest>> ReadTenantsAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectPortalTenants;
        AddParameter(command, "@limit", limit);

        var rows = new List<PortalTenantDigest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var dbName = Convert.ToString(reader["db_name"], CultureInfo.InvariantCulture) ?? string.Empty;
            rows.Add(new PortalTenantDigest(
                Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["industry_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["trade_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["hub_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["hosted_on"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt32(reader["erp_only_shared"] is DBNull ? 0 : reader["erp_only_shared"], CultureInfo.InvariantCulture) != 0,
                Convert.ToInt32(reader["is_active"] is DBNull ? 1 : reader["is_active"], CultureInfo.InvariantCulture) != 0,
                !string.IsNullOrWhiteSpace(dbName)));
        }

        return rows;
    }

    private static async Task<int> ScalarIntAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
    }

    private static async Task<int> ScalarIntSafeAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            return await ScalarIntAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<int> ScalarIntSafeAsync(DbConnection connection, string sql, int userId, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@userId", userId);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<decimal> ScalarDecimalSafeAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToDecimal(value ?? 0m, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0m;
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
