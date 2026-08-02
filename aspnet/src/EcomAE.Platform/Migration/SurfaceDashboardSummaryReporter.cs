using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Read-only CP/ERP/BOS dashboard summaries for migration shells. Performs zero writes.
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
            return new(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var cash = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumBankBalances, cancellationToken).ConfigureAwait(false);
            var ar = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumArOutstanding, cancellationToken).ConfigureAwait(false);
            var ap = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumApOutstanding, cancellationToken).ConfigureAwait(false);
            var stock = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumStockValue, cancellationToken).ConfigureAwait(false);
            return new(cash, ar, ap, stock, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var tenants = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountPortalTenants, cancellationToken).ConfigureAwait(false);
            var active = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActivePortalTenants, cancellationToken).ConfigureAwait(false);
            var adminSessions = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            return new(tenants, active, adminSessions, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, "database-error", ex.Message);
        }
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
}
