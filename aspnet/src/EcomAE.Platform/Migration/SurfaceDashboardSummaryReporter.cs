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
            return new(userId, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        if (userId <= 0)
        {
            return new(0, 0, 0, 0, "rejected", "Valid customer user id is required.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var orders = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerOrders, userId, cancellationToken).ConfigureAwait(false);
            var sessions = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerSessionsForUser, userId, cancellationToken).ConfigureAwait(false);
            var garage = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerGarage, userId, cancellationToken).ConfigureAwait(false);
            return new(userId, orders, sessions, garage, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, 0, 0, 0, "database-error", ex.Message);
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

    public async Task<CpUserListResult> ListCpUsersAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectCpUsers;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpUserDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpUserDigest(
                    Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["phone"] is DBNull ? string.Empty : reader["phone"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["unlocked"] is DBNull ? 0 : reader["unlocked"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_registered"] is DBNull ? 0 : reader["time_registered"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_last_visit"] is DBNull ? 0 : reader["time_last_visit"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpGroupListResult> ListCpGroupsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectCpGroups;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpGroupDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpGroupDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["value"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["for_backend"] is DBNull ? 0 : reader["for_backend"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["for_guests"] is DBNull ? 0 : reader["for_guests"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["for_registrated"] is DBNull ? 0 : reader["for_registrated"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["unblocked"] is DBNull ? 1 : reader["unblocked"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["parent"] is DBNull ? 0 : reader["parent"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["level"] is DBNull ? 0 : reader["level"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpSupplierListResult> ListErpSuppliersAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpSuppliers;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpSupplierDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpSupplierDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["storage_id"] is DBNull ? 0 : reader["storage_id"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["balance"] is DBNull ? 0m : reader["balance"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpPurchaseListResult> ListErpPurchasesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpPurchases;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpPurchaseDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpPurchaseDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["supplier_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["supplier_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["purchase_date"] is DBNull ? 0 : reader["purchase_date"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["invoice_number"] is DBNull ? string.Empty : reader["invoice_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontGarageResult> ListStorefrontGarageAsync(int userId, int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCustomerGarage;
            AddParameter(command, "@userId", userId);
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<StorefrontGarageVehicleDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontGarageVehicleDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["marka"] is DBNull ? string.Empty : reader["marka"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["model"] is DBNull ? string.Empty : reader["model"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["year"] is DBNull ? string.Empty : reader["year"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["vin"] is DBNull ? string.Empty : reader["vin"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture)));
            }

            return new(userId, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpCashAccountListResult> ListErpCashAccountsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpCashAccounts;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpCashAccountDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpCashAccountDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["account_type"] is DBNull ? string.Empty : reader["account_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["currency_code"] is DBNull ? string.Empty : reader["currency_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["opening_balance"] is DBNull ? 0m : reader["opening_balance"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["balance"] is DBNull ? 0m : reader["balance"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontProfileResult> BuildStorefrontProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(userId, string.Empty, 0, string.Empty, 0, 0, new Dictionary<string, string>(), "migration", "TenantRegistry DB is not configured.");
        }

        if (userId <= 0)
        {
            return new(0, string.Empty, 0, string.Empty, 0, 0, new Dictionary<string, string>(), "rejected", "Valid customer user id is required.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            string email = string.Empty;
            var emailConfirmed = 0;
            string phone = string.Empty;
            var phoneConfirmed = 0;
            var regVariant = 0;

            await using (var userCommand = connection.CreateCommand())
            {
                userCommand.CommandText = LegacySurfaceDashboardSql.SelectStorefrontUserCore;
                AddParameter(userCommand, "@userId", userId);
                await using var userReader = await userCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await userReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new(userId, string.Empty, 0, string.Empty, 0, 0, new Dictionary<string, string>(), "not-found", "User row not found.");
                }

                email = Convert.ToString(userReader["email"] is DBNull ? string.Empty : userReader["email"], CultureInfo.InvariantCulture) ?? string.Empty;
                emailConfirmed = Convert.ToInt32(userReader["email_confirmed"] is DBNull ? 0 : userReader["email_confirmed"], CultureInfo.InvariantCulture);
                phone = Convert.ToString(userReader["phone"] is DBNull ? string.Empty : userReader["phone"], CultureInfo.InvariantCulture) ?? string.Empty;
                phoneConfirmed = Convert.ToInt32(userReader["phone_confirmed"] is DBNull ? 0 : userReader["phone_confirmed"], CultureInfo.InvariantCulture);
                regVariant = Convert.ToInt32(userReader["reg_variant"] is DBNull ? 0 : userReader["reg_variant"], CultureInfo.InvariantCulture);
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var profileCommand = connection.CreateCommand();
                profileCommand.CommandText = LegacySurfaceDashboardSql.SelectStorefrontUserProfiles;
                AddParameter(profileCommand, "@userId", userId);
                await using var profileReader = await profileCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await profileReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var key = Convert.ToString(profileReader["data_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (key.Length == 0)
                    {
                        continue;
                    }

                    fields[key] = Convert.ToString(profileReader["data_value"] is DBNull ? string.Empty : profileReader["data_value"], CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }
            catch
            {
                // users_profiles may be missing on some tenants; core user fields still return.
            }

            return new(userId, email, emailConfirmed, phone, phoneConfirmed, regVariant, fields, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, string.Empty, 0, string.Empty, 0, 0, new Dictionary<string, string>(), "database-error", ex.Message);
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
