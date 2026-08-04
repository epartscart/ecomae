using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Data;
using EcomAE.Platform.Observability;

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
        using var activity = EcomAeActivitySources.Surfaces.StartActivity("surface.erp.dashboard-summary");
        activity?.SetTag("ecomae.surface", "erp");
        activity?.SetTag("ecomae.digest", "/erp/dashboard-summary");

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
        using var activity = EcomAeActivitySources.Surfaces.StartActivity("surface.bos.fleet-summary");
        activity?.SetTag("ecomae.surface", "bos");
        activity?.SetTag("ecomae.digest", "/bos/fleet-summary");

        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var list = await ReadTenantsAsync(connection, 500, cancellationToken).ConfigureAwait(false);
            var adminSessions = await ScalarIntAsync(connection, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("ecomae.row_count", list.Count);
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

    public async Task<CpOrdersListResult> ListCpOrdersAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var emptySummary = new CpOrdersSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(emptySummary, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
            var open = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpOrdersOpen, cancellationToken).ConfigureAwait(false);
            var pendingShip = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpOrdersPendingShip, cancellationToken).ConfigureAwait(false);
            var today = 0;
            try
            {
                await using var todayCmd = connection.CreateCommand();
                todayCmd.CommandText = LegacySurfaceDashboardSql.CountCpOrdersToday;
                AddParameter(todayCmd, "@todayStart", todayStart);
                var todayVal = await todayCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                today = Convert.ToInt32(todayVal ?? 0, CultureInfo.InvariantCulture);
            }
            catch
            {
                today = 0;
            }

            var summary = new CpOrdersSummary(open, today, pendingShip, "database", string.Empty);

            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectCpShopOrders;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpShopOrderDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpShopOrderDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["status"] is DBNull ? 0 : reader["status"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["paid"] is DBNull ? 0 : reader["paid"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["paid_type"] is DBNull ? 0 : reader["paid_type"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["office_id"] is DBNull ? 0 : reader["office_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["successfully_created"] is DBNull ? 0 : reader["successfully_created"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["count_items"] is DBNull ? 0 : reader["count_items"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["order_sum"] is DBNull ? 0m : reader["order_sum"], CultureInfo.InvariantCulture)));
            }

            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(new CpOrdersSummary(0, 0, 0, "database-error", ex.Message), [], 0, "database-error", ex.Message);
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


    public async Task<ErpCashEntryListResult> ListErpCashEntriesAsync(int? accountId, int limit, CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Surfaces.StartActivity("surface.erp.cash-entries");
        activity?.SetTag("ecomae.surface", "erp");
        activity?.SetTag("ecomae.digest", "/erp/cash-entries");

        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            if (accountId is > 0)
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectErpCashEntriesForAccount;
                AddParameter(command, "@accountId", accountId.Value);
            }
            else
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectErpCashEntries;
            }

            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpCashEntryDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpCashEntryDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["account_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["account_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["account_type"] is DBNull ? string.Empty : reader["account_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["direction"] is DBNull ? 0 : reader["direction"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["amount"] is DBNull ? 0m : reader["amount"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["note"] is DBNull ? string.Empty : reader["note"], CultureInfo.InvariantCulture) ?? string.Empty));
            }

            activity?.SetTag("ecomae.row_count", rows.Count);
            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpInvoiceListResult> ListErpInvoicesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpInvoices;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpInvoiceDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpInvoiceDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["invoice_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["customer_email"] is DBNull ? string.Empty : reader["customer_email"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["issue_date"] is DBNull ? 0 : reader["issue_date"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_incl_vat"] is DBNull ? 0m : reader["total_incl_vat"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpGlJournalListResult> ListErpGlJournalsAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpGlJournals;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpGlJournalDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpGlJournalDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["journal_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["journal_date"] is DBNull ? 0 : reader["journal_date"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["source_type"] is DBNull ? string.Empty : reader["source_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["source_id"] is DBNull ? 0 : reader["source_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_debit"] is DBNull ? 0m : reader["total_debit"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpModuleListResult> ListCpModulesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpModules;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpModuleDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpModuleDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["activated"] is DBNull ? 0 : reader["activated"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["is_prototype"] is DBNull ? 0 : reader["is_prototype"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["control_available"] is DBNull ? 0 : reader["control_available"], CultureInfo.InvariantCulture) != 0));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpConfigItemMetaListResult> ListCpConfigItemsMetaAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpConfigItemsMeta;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpConfigItemMetaDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpConfigItemMetaDigest(
                    Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["type"] is DBNull ? string.Empty : reader["type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["config_group"] is DBNull ? string.Empty : reader["config_group"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["visible"] is DBNull ? 1 : reader["visible"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["order"] is DBNull ? 0 : reader["order"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<BosFleetReadinessResult> BuildBosFleetReadinessAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            var tenants = await ListPortalTenantsAsync(500, cancellationToken).ConfigureAwait(false);
            if (tenants.Source != "database")
            {
                return new(0, 0, 0, 0, 0, 0, 0, tenants.Source, tenants.Message);
            }

            var pass = 0;
            var warn = 0;
            var fail = 0;
            foreach (var tenant in tenants.Tenants)
            {
                var hasHost = !string.IsNullOrWhiteSpace(tenant.Hostname);
                if (tenant.IsActive && tenant.HasDb && hasHost)
                {
                    pass++;
                }
                else if (tenant.IsActive && (tenant.HasDb || hasHost))
                {
                    warn++;
                }
                else
                {
                    fail++;
                }
            }

            return new(
                tenants.Count,
                pass,
                warn,
                fail,
                tenants.Tenants.Count(item => item.IsActive),
                tenants.Tenants.Count(item => item.HasDb),
                tenants.Tenants.Count(item => item.ErpOnly),
                "database",
                string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpCoaAccountListResult> ListErpCoaAccountsAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpCoaAccounts;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpCoaAccountDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpCoaAccountDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["account_type"] is DBNull ? string.Empty : reader["account_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["normal_side"] is DBNull ? string.Empty : reader["normal_side"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["parent_id"] is DBNull ? 0 : reader["parent_id"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["opening_balance"] is DBNull ? 0m : reader["opening_balance"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpWarehouseListResult> ListErpWarehousesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpWarehouses;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpWarehouseDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpWarehouseDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["storage_id"] is DBNull ? 0 : reader["storage_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpSalesOrderListResult> ListErpSalesOrdersAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpSalesOrders;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpSalesOrderDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpSalesOrderDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["so_no"] is DBNull ? string.Empty : reader["so_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["customer_user_id"] is DBNull ? 0 : reader["customer_user_id"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(string.Empty, [], 0, "empty", "Enter a part number or OE code.");
        }

        if (!_connections.IsConfigured)
        {
            return new(normalized, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontPartSearch;
            AddParameter(command, "@article", normalized);
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<StorefrontPartOfferDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontPartOfferDigest(
                    Convert.ToInt32(reader["price_id"] is DBNull ? 0 : reader["price_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["price_list"] is DBNull ? string.Empty : reader["price_list"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["article_show"] is DBNull ? string.Empty : reader["article_show"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["price"] is DBNull ? 0m : reader["price"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["exist"] is DBNull ? 0 : reader["exist"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["storage"] is DBNull ? string.Empty : reader["storage"], CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return new(normalized, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(normalized, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontCartListResult> ListStorefrontCartAsync(int userId, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var emptySummary = new StorefrontCartSummary(0, 0m, "migration", "TenantRegistry DB is not configured.");
        if (userId <= 0)
        {
            return new(0, new(0, 0m, "rejected", "Valid customer user id is required."), [], 0, "rejected", "Valid customer user id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(userId, emptySummary, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var count = 0;
            var sum = 0m;
            await using (var summaryCmd = connection.CreateCommand())
            {
                summaryCmd.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCartSummary;
                AddParameter(summaryCmd, "@userId", userId);
                await using var summaryReader = await summaryCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await summaryReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    count = Convert.ToInt32(summaryReader["count"] is DBNull ? 0 : summaryReader["count"], CultureInfo.InvariantCulture);
                    sum = Convert.ToDecimal(summaryReader["sum"] is DBNull ? 0m : summaryReader["sum"], CultureInfo.InvariantCulture);
                }
            }

            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCartLines;
            AddParameter(command, "@userId", userId);
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<StorefrontCartLineDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontCartLineDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["price"] is DBNull ? 0m : reader["price"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["count_need"] is DBNull ? 0m : reader["count_need"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["checked_for_order"] is DBNull ? 0 : reader["checked_for_order"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["product_type"] is DBNull ? 0 : reader["product_type"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["time_to_exe"] is DBNull ? string.Empty : reader["time_to_exe"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["time_to_exe_guaranteed"] is DBNull ? string.Empty : reader["time_to_exe_guaranteed"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["min_order"] is DBNull ? 0m : reader["min_order"], CultureInfo.InvariantCulture)));
            }

            var summary = new StorefrontCartSummary(count, sum, "database", string.Empty);
            return new(userId, summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, new(0, 0m, "database-error", ex.Message), [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpMenuListResult> ListCpMenusAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpMenus;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpMenuDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var structureJson = Convert.ToString(
                    reader["structure"] is DBNull ? string.Empty : reader["structure"],
                    CultureInfo.InvariantCulture) ?? string.Empty;
                var summary = CpMenuStructureAnalyzer.Analyze(structureJson);
                rows.Add(new CpMenuDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToString(reader["menu_ul_class"] is DBNull ? string.Empty : reader["menu_ul_class"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["menu_ul_id"] is DBNull ? string.Empty : reader["menu_ul_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                    summary.StructurePresent,
                    summary.StructureParseOk,
                    summary.NodeCount,
                    summary.MaxDepth,
                    summary.UrlLinkCount,
                    summary.ContentLinkCount,
                    summary.UnknownLinkCount));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPageListResult> ListCpPagesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpPages;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpPageDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPageDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["url"] is DBNull ? string.Empty : reader["url"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["alias"] is DBNull ? string.Empty : reader["alias"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["published_flag"] is DBNull ? 0 : reader["published_flag"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["level"] is DBNull ? 0 : reader["level"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAdminSessionListResult> ListCpAdminSessionsAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpAdminSessions;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpAdminSessionDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpAdminSessionDigest(
                    Convert.ToInt32(reader["user_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["type"] is DBNull ? 0 : reader["type"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["session_count"] is DBNull ? 0 : reader["session_count"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpStorageListResult> ListCpStoragesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpStorages;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpStorageDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpStorageDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["short_name"] is DBNull ? string.Empty : reader["short_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["hidden"] is DBNull ? 0 : reader["hidden"], CultureInfo.InvariantCulture) != 0));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<BosAuditLogListResult> ListBosAuditLogAsync(string? area, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 1000);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            if (!string.IsNullOrWhiteSpace(area))
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectBosAuditLogForArea;
                AddParameter(command, "@area", area.Trim());
            }
            else
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectBosAuditLog;
            }

            AddParameter(command, "@limit", safeLimit);
            var rows = new List<BosAuditLogDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new BosAuditLogDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["ts"] is DBNull ? 0 : reader["ts"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["actor"] is DBNull ? string.Empty : reader["actor"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["area"] is DBNull ? string.Empty : reader["area"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["action"] is DBNull ? string.Empty : reader["action"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["target"] is DBNull ? string.Empty : reader["target"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["ip"] is DBNull ? string.Empty : reader["ip"], CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpPurchaseOrderListResult> ListErpPurchaseOrdersAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectErpPurchaseOrders;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpPurchaseOrderDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpPurchaseOrderDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["po_no"] is DBNull ? string.Empty : reader["po_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["supplier_id"] is DBNull ? 0 : reader["supplier_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpInventoryStockSummaryResult> BuildErpInventoryStockSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0m, 0m, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpInventoryStockSummary;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new(0, 0m, 0m, 0, 0, "database", string.Empty);
            }

            return new(
                Convert.ToInt64(reader["row_count"] is DBNull ? 0 : reader["row_count"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader["qty_on_hand"] is DBNull ? 0m : reader["qty_on_hand"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader["stock_value"] is DBNull ? 0m : reader["stock_value"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["warehouse_count"] is DBNull ? 0 : reader["warehouse_count"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture),
                "database",
                string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0m, 0m, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCurrencyListResult> ListCpCurrenciesAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpCurrencies;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpCurrencyDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpCurrencyDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["iso_code"] is DBNull ? string.Empty : reader["iso_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["iso_name"] is DBNull ? string.Empty : reader["iso_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["caption_short"] is DBNull ? string.Empty : reader["caption_short"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["rate"] is DBNull ? 0m : reader["rate"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["available"] is DBNull ? 0 : reader["available"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpApiClientMetaListResult> ListCpApiClientsMetaAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpApiClientsMeta;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpApiClientMetaDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpApiClientMetaDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["client_key_prefix"] is DBNull ? string.Empty : reader["client_key_prefix"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["product"] is DBNull ? string.Empty : reader["product"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["contact_email"] is DBNull ? string.Empty : reader["contact_email"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["daily_limit"] is DBNull ? 0 : reader["daily_limit"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["calls_today"] is DBNull ? 0 : reader["calls_today"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
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
