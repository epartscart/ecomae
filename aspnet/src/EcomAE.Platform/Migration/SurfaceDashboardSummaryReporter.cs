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

    public async Task<CpPowerBiDigestResult> BuildCpPowerBiDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var emptySummary = new CpPowerBiConfigSummary(
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, "none", string.Empty, false, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(emptySummary, [], 0, "migration", emptySummary.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);

            string siteKey = string.Empty, workspaceId = string.Empty, azureTenantId = string.Empty;
            string defaultReportId = string.Empty, defaultDatasetId = string.Empty, embedUrl = string.Empty;
            string embedMode = "none", notes = string.Empty;
            var active = false;

            await using (var configCmd = connection.CreateCommand())
            {
                configCmd.CommandText = LegacySurfaceDashboardSql.SelectCpPowerBiConfig;
                await using var configReader = await configCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await configReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    siteKey = Convert.ToString(configReader["site_key"] is DBNull ? string.Empty : configReader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                    workspaceId = Convert.ToString(configReader["workspace_id"] is DBNull ? string.Empty : configReader["workspace_id"], CultureInfo.InvariantCulture) ?? string.Empty;
                    azureTenantId = Convert.ToString(configReader["azure_tenant_id"] is DBNull ? string.Empty : configReader["azure_tenant_id"], CultureInfo.InvariantCulture) ?? string.Empty;
                    defaultReportId = Convert.ToString(configReader["default_report_id"] is DBNull ? string.Empty : configReader["default_report_id"], CultureInfo.InvariantCulture) ?? string.Empty;
                    defaultDatasetId = Convert.ToString(configReader["default_dataset_id"] is DBNull ? string.Empty : configReader["default_dataset_id"], CultureInfo.InvariantCulture) ?? string.Empty;
                    embedUrl = Convert.ToString(configReader["embed_url"] is DBNull ? string.Empty : configReader["embed_url"], CultureInfo.InvariantCulture) ?? string.Empty;
                    embedMode = Convert.ToString(configReader["embed_mode"] is DBNull ? "none" : configReader["embed_mode"], CultureInfo.InvariantCulture) ?? "none";
                    notes = Convert.ToString(configReader["notes"] is DBNull ? string.Empty : configReader["notes"], CultureInfo.InvariantCulture) ?? string.Empty;
                    active = Convert.ToInt32(configReader["active"] is DBNull ? 0 : configReader["active"], CultureInfo.InvariantCulture) != 0;
                }
            }

            var reports = new List<CpPowerBiReportDigest>();
            await using (var reportCmd = connection.CreateCommand())
            {
                reportCmd.CommandText = LegacySurfaceDashboardSql.SelectCpPowerBiReports;
                AddParameter(reportCmd, "@limit", safeLimit);
                await using var reportReader = await reportCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reportReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    reports.Add(new CpPowerBiReportDigest(
                        Convert.ToInt64(reportReader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reportReader["site_key"] is DBNull ? string.Empty : reportReader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reportReader["report_id"] is DBNull ? string.Empty : reportReader["report_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reportReader["report_name"] is DBNull ? string.Empty : reportReader["report_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reportReader["dataset_id"] is DBNull ? string.Empty : reportReader["dataset_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reportReader["category"] is DBNull ? string.Empty : reportReader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reportReader["embed_url"] is DBNull ? string.Empty : reportReader["embed_url"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reportReader["active"] is DBNull ? 0 : reportReader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpPowerBiConfigSummary(
                siteKey, workspaceId, azureTenantId, defaultReportId, defaultDatasetId,
                embedUrl, embedMode, notes, active, reports.Count, "database", string.Empty);
            return new(summary, reports, reports.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = emptySummary with { Source = "database-error", Message = ex.Message, ReportCount = 0 };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpMobileAppsDigestResult> BuildCpMobileAppsDigestAsync(CancellationToken cancellationToken = default)
    {
        var empty = new CpMobileAppsSummary(
            false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, true, string.Empty, false,
            "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectCpMobileAppsIntegrationsJson;
            var raw = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) ?? string.Empty;
            var summary = ParseMobileAppsSummary(raw, "database", string.Empty);
            return new(summary, summary.Source, summary.Message);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, "database-error", ex.Message);
        }
    }

    public async Task<CpMetabaseDigestResult> BuildCpMetabaseDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpMetabaseConfigSummary(string.Empty, string.Empty, false, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var siteKey = string.Empty;
            var metabaseUrl = string.Empty;
            var active = false;
            await using (var configCmd = connection.CreateCommand())
            {
                configCmd.CommandText = LegacySurfaceDashboardSql.SelectCpMetabaseConfig;
                await using var reader = await configCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    siteKey = Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                    metabaseUrl = Convert.ToString(reader["metabase_url"] is DBNull ? string.Empty : reader["metabase_url"], CultureInfo.InvariantCulture) ?? string.Empty;
                    active = Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0;
                }
            }

            var dashboards = new List<CpMetabaseDashboardDigest>();
            await using (var dashCmd = connection.CreateCommand())
            {
                dashCmd.CommandText = LegacySurfaceDashboardSql.SelectCpMetabaseDashboards;
                AddParameter(dashCmd, "@limit", safeLimit);
                await using var reader = await dashCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    dashboards.Add(new CpMetabaseDashboardDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["dashboard_id"] is DBNull ? 0 : reader["dashboard_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["dashboard_name"] is DBNull ? string.Empty : reader["dashboard_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpMetabaseConfigSummary(siteKey, metabaseUrl, active, dashboards.Count, "database", string.Empty);
            return new(summary, dashboards, dashboards.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpNlReportingDigestResult> ListCpNlReportDefinitionsAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpNlReportDefinitions;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpNlReportDefinitionDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpNlReportDefinitionDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["description"] is DBNull ? string.Empty : reader["description"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["report_type"] is DBNull ? string.Empty : reader["report_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["schedule"] is DBNull ? string.Empty : reader["schedule"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["format"] is DBNull ? string.Empty : reader["format"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt64(reader["created_by"] is DBNull ? 0 : reader["created_by"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpMarketingBroadcastDigestResult> BuildCpMarketingBroadcastDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var emptySummary = new CpMarketingBroadcastSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(emptySummary, [], 0, "migration", emptySummary.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var campaignsCount = 0;
            var emailsSent = 0;
            var whatsappSent = 0;
            await using (var statsCmd = connection.CreateCommand())
            {
                statsCmd.CommandText = LegacySurfaceDashboardSql.SelectCpMarketingBroadcastStats;
                await using var reader = await statsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    campaignsCount = Convert.ToInt32(reader["campaigns"] is DBNull ? 0 : reader["campaigns"], CultureInfo.InvariantCulture);
                    emailsSent = Convert.ToInt32(reader["emails_sent"] is DBNull ? 0 : reader["emails_sent"], CultureInfo.InvariantCulture);
                    whatsappSent = Convert.ToInt32(reader["whatsapp_sent"] is DBNull ? 0 : reader["whatsapp_sent"], CultureInfo.InvariantCulture);
                }
            }

            var campaigns = new List<CpMarketingBroadcastCampaignDigest>();
            await using (var listCmd = connection.CreateCommand())
            {
                listCmd.CommandText = LegacySurfaceDashboardSql.SelectCpMarketingBroadcastCampaigns;
                AddParameter(listCmd, "@limit", safeLimit);
                await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    campaigns.Add(new CpMarketingBroadcastCampaignDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["created_at"] is DBNull ? 0 : reader["created_at"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["channel"] is DBNull ? string.Empty : reader["channel"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["template_key"] is DBNull ? string.Empty : reader["template_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["subject"] is DBNull ? string.Empty : reader["subject"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["preview"] is DBNull ? string.Empty : reader["preview"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["audience_mode"] is DBNull ? string.Empty : reader["audience_mode"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["audience_meta"] is DBNull ? string.Empty : reader["audience_meta"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["total_targets"] is DBNull ? 0 : reader["total_targets"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sent_ok"] is DBNull ? 0 : reader["sent_ok"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sent_fail"] is DBNull ? 0 : reader["sent_fail"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["operator_id"] is DBNull ? 0 : reader["operator_id"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpMarketingBroadcastSummary(campaignsCount, emailsSent, whatsappSent, "database", string.Empty);
            return new(summary, campaigns, campaigns.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = emptySummary with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDemoTenantsDigestResult> ListCpDemoTenantsAsync(int limit, CancellationToken cancellationToken = default)
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
            command.CommandText = LegacySurfaceDashboardSql.SelectCpDemoTenants;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<CpDemoTenantDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpDemoTenantDigest(
                    Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["hostname"] is DBNull ? string.Empty : reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["industry_code"] is DBNull ? string.Empty : reader["industry_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["trade_name"] is DBNull ? string.Empty : reader["trade_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["hub_name"] is DBNull ? string.Empty : reader["hub_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["hosted_on"] is DBNull ? string.Empty : reader["hosted_on"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["erp_only_shared"] is DBNull ? 0 : reader["erp_only_shared"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["is_active"] is DBNull ? 0 : reader["is_active"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt64(reader["demo_expires_at"] is DBNull ? 0 : reader["demo_expires_at"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["demo_contact_email"] is DBNull ? string.Empty : reader["demo_contact_email"], CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPartsAgentDigestResult> BuildCpPartsAgentDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPartsAgentSummary(0, 0, 0, 0, 0, false, string.Empty, string.Empty, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var enabled = false;
            var agentName = string.Empty;
            var domain = string.Empty;
            await using (var cfg = connection.CreateCommand())
            {
                cfg.CommandText = LegacySurfaceDashboardSql.SelectCpPartsAgentConfig;
                await using var reader = await cfg.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    enabled = Convert.ToInt32(reader["enabled"] is DBNull ? 0 : reader["enabled"], CultureInfo.InvariantCulture) != 0;
                    agentName = Convert.ToString(reader["agent_name"] is DBNull ? string.Empty : reader["agent_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    domain = Convert.ToString(reader["domain"] is DBNull ? string.Empty : reader["domain"], CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            var total = 0;
            var sessionsToday = 0;
            var messagesToday = 0;
            var loggedIn = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPartsAgentStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    total = Convert.ToInt32(reader["total_sessions"] is DBNull ? 0 : reader["total_sessions"], CultureInfo.InvariantCulture);
                    sessionsToday = Convert.ToInt32(reader["sessions_today"] is DBNull ? 0 : reader["sessions_today"], CultureInfo.InvariantCulture);
                    messagesToday = Convert.ToInt32(reader["messages_today"] is DBNull ? 0 : reader["messages_today"], CultureInfo.InvariantCulture);
                    loggedIn = Convert.ToInt32(reader["logged_in_sessions"] is DBNull ? 0 : reader["logged_in_sessions"], CultureInfo.InvariantCulture);
                }
            }

            var sessions = new List<CpPartsAgentSessionDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPartsAgentSessions;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    sessions.Add(new CpPartsAgentSessionDigest(
                        Convert.ToString(reader["session_id"] is DBNull ? string.Empty : reader["session_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["message_count"] is DBNull ? 0 : reader["message_count"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["country_code"] is DBNull ? string.Empty : reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["country_name"] is DBNull ? string.Empty : reader["country_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["ip_hash"] is DBNull ? string.Empty : reader["ip_hash"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["last_user_text"] is DBNull ? string.Empty : reader["last_user_text"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["last_agent_text"] is DBNull ? string.Empty : reader["last_agent_text"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpPartsAgentSummary(total, sessionsToday, messagesToday, loggedIn, Math.Max(0, total - loggedIn), enabled, agentName, domain, "database", string.Empty);
            return new(summary, sessions, sessions.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPosOverviewDigestResult> BuildCpPosOverviewDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPosOverviewSummary(false, string.Empty, 0, 0, 0m, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var posEnabled = false;
            var registerName = string.Empty;
            await using (var cfg = connection.CreateCommand())
            {
                cfg.CommandText = LegacySurfaceDashboardSql.SelectCpPosSettings;
                await using var reader = await cfg.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    posEnabled = Convert.ToInt32(reader["pos_enabled"] is DBNull ? 0 : reader["pos_enabled"], CultureInfo.InvariantCulture) != 0;
                    registerName = Convert.ToString(reader["register_name"] is DBNull ? string.Empty : reader["register_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            var openSessions = 0;
            var salesToday = 0;
            var salesTotalToday = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPosStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    openSessions = Convert.ToInt32(reader["open_sessions"] is DBNull ? 0 : reader["open_sessions"], CultureInfo.InvariantCulture);
                    salesToday = Convert.ToInt32(reader["sales_today"] is DBNull ? 0 : reader["sales_today"], CultureInfo.InvariantCulture);
                    salesTotalToday = Convert.ToDecimal(reader["sales_total_today"] is DBNull ? 0 : reader["sales_total_today"], CultureInfo.InvariantCulture);
                }
            }

            var sales = new List<CpPosSaleDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPosSales;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    sales.Add(new CpPosSaleDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["sale_no"] is DBNull ? string.Empty : reader["sale_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["session_id"] is DBNull ? 0 : reader["session_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["customer_label"] is DBNull ? string.Empty : reader["customer_label"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["subtotal_ex"] is DBNull ? 0 : reader["subtotal_ex"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["vat_amount"] is DBNull ? 0 : reader["vat_amount"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["total_amount"] is DBNull ? 0 : reader["total_amount"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["payment_method"] is DBNull ? string.Empty : reader["payment_method"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tax_kit_code"] is DBNull ? string.Empty : reader["tax_kit_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPosOverviewSummary(posEnabled, registerName, openSessions, salesToday, salesTotalToday, "database", string.Empty);
            return new(summary, sales, sales.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpTaxToolkitsDigestResult> BuildCpTaxToolkitsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpTaxToolkitsSummary(0, 0, string.Empty, string.Empty, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var toolkitCount = 0;
            var installCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpTaxToolkitStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    toolkitCount = Convert.ToInt32(reader["toolkit_count"] is DBNull ? 0 : reader["toolkit_count"], CultureInfo.InvariantCulture);
                    installCount = Convert.ToInt32(reader["install_count"] is DBNull ? 0 : reader["install_count"], CultureInfo.InvariantCulture);
                }
            }

            var tenantCountry = string.Empty;
            var tenantKit = string.Empty;
            await using (var profile = connection.CreateCommand())
            {
                profile.CommandText = LegacySurfaceDashboardSql.SelectCpTaxTenantProfile;
                await using var reader = await profile.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tenantCountry = Convert.ToString(reader["country_code"] is DBNull ? string.Empty : reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty;
                    tenantKit = Convert.ToString(reader["kit_code"] is DBNull ? string.Empty : reader["kit_code"], CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            var toolkits = new List<CpTaxToolkitDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpTaxToolkits;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    toolkits.Add(new CpTaxToolkitDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["kit_code"] is DBNull ? string.Empty : reader["kit_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["jurisdiction"] is DBNull ? string.Empty : reader["jurisdiction"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tax_type"] is DBNull ? string.Empty : reader["tax_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["is_system"] is DBNull ? 0 : reader["is_system"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpTaxToolkitsSummary(toolkitCount, installCount, tenantCountry, tenantKit, "database", string.Empty);
            return new(summary, toolkits, toolkits.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSmsWhatsappDigestResult> BuildCpSmsWhatsappDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSmsWhatsappSummary(0, string.Empty, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var operators = new List<CpSmsOperatorDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSmsOperators;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    operators.Add(new CpSmsOperatorDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["handler"] is DBNull ? string.Empty : reader["handler"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["description"] is DBNull ? string.Empty : reader["description"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["control_available"] is DBNull ? 0 : reader["control_available"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var whatsappSent = 0;
            var whatsappFailed = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpWhatsappLogStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    whatsappSent = Convert.ToInt32(reader["whatsapp_sent"] is DBNull ? 0 : reader["whatsapp_sent"], CultureInfo.InvariantCulture);
                    whatsappFailed = Convert.ToInt32(reader["whatsapp_failed"] is DBNull ? 0 : reader["whatsapp_failed"], CultureInfo.InvariantCulture);
                }
            }

            var waLog = new List<CpWhatsappLogDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpWhatsappNotifyLog;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    waLog.Add(new CpWhatsappLogDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["created_at"] is DBNull ? 0 : reader["created_at"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["notify_name"] is DBNull ? string.Empty : reader["notify_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["phone_masked"] is DBNull ? string.Empty : reader["phone_masked"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["status"] is DBNull ? 0 : reader["status"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["message_preview"] is DBNull ? string.Empty : reader["message_preview"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var active = operators.FirstOrDefault(o => o.Active)?.Name ?? string.Empty;
            var summary = new CpSmsWhatsappSummary(operators.Count, active, whatsappSent, whatsappFailed, "database", string.Empty);
            return new(summary, operators, waLog, operators.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpCrmBoardDigestResult> BuildCpCrmBoardDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCrmBoardSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var leads = 0; var opps = 0; var acts = 0; var tickets = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCrmStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    leads = Convert.ToInt32(reader["leads"] is DBNull ? 0 : reader["leads"], CultureInfo.InvariantCulture);
                    opps = Convert.ToInt32(reader["opportunities"] is DBNull ? 0 : reader["opportunities"], CultureInfo.InvariantCulture);
                    acts = Convert.ToInt32(reader["activities"] is DBNull ? 0 : reader["activities"], CultureInfo.InvariantCulture);
                    tickets = Convert.ToInt32(reader["tickets_open"] is DBNull ? 0 : reader["tickets_open"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCrmLeadDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCrmLeads;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCrmLeadDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["source"] is DBNull ? string.Empty : reader["source"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["owner_id"] is DBNull ? 0 : reader["owner_id"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["amount"] is DBNull ? 0 : reader["amount"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpCrmBoardSummary(leads, opps, acts, tickets, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDocumentControlDigestResult> BuildCpDocumentControlDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDocumentControlSummary(string.Empty, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var company = string.Empty;
            await using (var cfg = connection.CreateCommand())
            {
                cfg.CommandText = LegacySurfaceDashboardSql.SelectCpDocumentCompanyName;
                await using var reader = await cfg.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    company = Convert.ToString(reader["company_name"] is DBNull ? string.Empty : reader["company_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                }
            }

            var templateCount = 0; var attachmentCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpDocumentStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    templateCount = Convert.ToInt32(reader["template_count"] is DBNull ? 0 : reader["template_count"], CultureInfo.InvariantCulture);
                    attachmentCount = Convert.ToInt32(reader["attachment_count"] is DBNull ? 0 : reader["attachment_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpDocumentTemplateDigest>();
            var n = 0;
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDocumentTemplates;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    n++;
                    rows.Add(new CpDocumentTemplateDigest(
                        n,
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpDocumentControlSummary(company, templateCount, attachmentCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDeliveryMethodsDigestResult> BuildCpDeliveryMethodsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDeliveryMethodsSummary(0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var methods = 0; var available = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpDeliveryStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    methods = Convert.ToInt32(reader["methods"] is DBNull ? 0 : reader["methods"], CultureInfo.InvariantCulture);
                    available = Convert.ToInt32(reader["available"] is DBNull ? 0 : reader["available"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpDeliveryMethodDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDeliveryModes;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpDeliveryMethodDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["handler"] is DBNull ? string.Empty : reader["handler"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["available"] is DBNull ? 0 : reader["available"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["control_available"] is DBNull ? 0 : reader["control_available"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpDeliveryMethodsSummary(methods, available, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCrossesDigestResult> BuildCpCrossesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCrossesSummary(0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var total = 0; var brands = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCrossesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    total = Convert.ToInt32(reader["total_pairs"] is DBNull ? 0 : reader["total_pairs"], CultureInfo.InvariantCulture);
                    brands = Convert.ToInt32(reader["brands"] is DBNull ? 0 : reader["brands"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCrossPairDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCrossPairs;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCrossPairDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["cross_manufacturer"] is DBNull ? string.Empty : reader["cross_manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["cross_article"] is DBNull ? string.Empty : reader["cross_article"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpCrossesSummary(total, brands, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpHrOverviewDigestResult> BuildCpHrOverviewDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpHrOverviewSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var active = 0; var leave = 0; var payroll = 0; var attendance = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpHrStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    active = Convert.ToInt32(reader["active_employees"] is DBNull ? 0 : reader["active_employees"], CultureInfo.InvariantCulture);
                    leave = Convert.ToInt32(reader["pending_leave"] is DBNull ? 0 : reader["pending_leave"], CultureInfo.InvariantCulture);
                    payroll = Convert.ToInt32(reader["payroll_runs"] is DBNull ? 0 : reader["payroll_runs"], CultureInfo.InvariantCulture);
                    attendance = Convert.ToInt32(reader["attendance_rows"] is DBNull ? 0 : reader["attendance_rows"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpHrEmployeeDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpHrEmployees;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpHrEmployeeDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["department"] is DBNull ? string.Empty : reader["department"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["join_date"] is DBNull ? 0 : reader["join_date"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpHrOverviewSummary(active, leave, payroll, attendance, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpProductionOverviewDigestResult> BuildCpProductionOverviewDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpProductionOverviewSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var bom = 0; var open = 0; var completed = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpProductionStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    bom = Convert.ToInt32(reader["bom_count"] is DBNull ? 0 : reader["bom_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_work_orders"] is DBNull ? 0 : reader["open_work_orders"], CultureInfo.InvariantCulture);
                    completed = Convert.ToInt32(reader["completed_work_orders"] is DBNull ? 0 : reader["completed_work_orders"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpProductionWorkOrderDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpProductionWorkOrders;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpProductionWorkOrderDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["wo_no"] is DBNull ? string.Empty : reader["wo_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["qty_planned"] is DBNull ? 0 : reader["qty_planned"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["qty_produced"] is DBNull ? 0 : reader["qty_produced"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpProductionOverviewSummary(bom, open, completed, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpProjectsOverviewDigestResult> BuildCpProjectsOverviewDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpProjectsOverviewSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var open = 0; var tasks = 0; var contracts = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpProjectsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    open = Convert.ToInt32(reader["open_projects"] is DBNull ? 0 : reader["open_projects"], CultureInfo.InvariantCulture);
                    tasks = Convert.ToInt32(reader["task_count"] is DBNull ? 0 : reader["task_count"], CultureInfo.InvariantCulture);
                    contracts = Convert.ToInt32(reader["contract_count"] is DBNull ? 0 : reader["contract_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpProjectDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpProjects;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpProjectDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["billing_type"] is DBNull ? string.Empty : reader["billing_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["contract_value"] is DBNull ? 0 : reader["contract_value"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpProjectsOverviewSummary(open, tasks, contracts, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpIndustryPacksDigestResult> BuildCpIndustryPacksDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpIndustryPacksSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var packCount = 0; var activePacks = 0; var assignments = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpIndustryPackStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    packCount = Convert.ToInt32(reader["pack_count"] is DBNull ? 0 : reader["pack_count"], CultureInfo.InvariantCulture);
                    activePacks = Convert.ToInt32(reader["active_packs"] is DBNull ? 0 : reader["active_packs"], CultureInfo.InvariantCulture);
                    assignments = Convert.ToInt32(reader["assignments"] is DBNull ? 0 : reader["assignments"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpIndustryPackDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpIndustryPacks;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpIndustryPackDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["pack_key"] is DBNull ? string.Empty : reader["pack_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["description"] is DBNull ? string.Empty : reader["description"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["icon"] is DBNull ? string.Empty : reader["icon"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpIndustryPacksSummary(packCount, activePacks, assignments, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpJewelleryRetailDigestResult> BuildCpJewelleryRetailDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpJewelleryRetailSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var vouchers = 0; var open = 0; var tags = 0; var metal = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    vouchers = Convert.ToInt32(reader["voucher_count"] is DBNull ? 0 : reader["voucher_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_vouchers"] is DBNull ? 0 : reader["open_vouchers"], CultureInfo.InvariantCulture);
                    tags = Convert.ToInt32(reader["tag_count"] is DBNull ? 0 : reader["tag_count"], CultureInfo.InvariantCulture);
                    metal = Convert.ToInt32(reader["metal_stock_rows"] is DBNull ? 0 : reader["metal_stock_rows"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpJewelleryVoucherDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryVouchers;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpJewelleryVoucherDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["voc_type"] is DBNull ? string.Empty : reader["voc_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["voc_date"] is DBNull ? string.Empty : reader["voc_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["voc_no"] is DBNull ? 0 : reader["voc_no"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["party_name"] is DBNull ? string.Empty : reader["party_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["net_amount"] is DBNull ? 0 : reader["net_amount"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["vat_amount"] is DBNull ? 0 : reader["vat_amount"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["total_with_vat"] is DBNull ? 0 : reader["total_with_vat"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpJewelleryRetailSummary(vouchers, open, tags, metal, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPriceListsDigestResult> BuildCpPriceListsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPriceListsSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var activeLists = 0; var priceRows = 0; var uploads = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPriceListStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    activeLists = Convert.ToInt32(reader["active_lists"] is DBNull ? 0 : reader["active_lists"], CultureInfo.InvariantCulture);
                    priceRows = Convert.ToInt32(reader["price_rows"] is DBNull ? 0 : reader["price_rows"], CultureInfo.InvariantCulture);
                    uploads = Convert.ToInt32(reader["upload_count"] is DBNull ? 0 : reader["upload_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPriceListDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPriceLists;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPriceListDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["customer_id"] is DBNull ? 0 : reader["customer_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["priority"] is DBNull ? 0 : reader["priority"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpPriceListsSummary(activeLists, priceRows, uploads, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAutoPriceDigestResult> BuildCpAutoPriceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAutoPriceSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var rules = 0; var sources = 0; var compareRuns = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAutoPriceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rules = Convert.ToInt32(reader["active_rules"] is DBNull ? 0 : reader["active_rules"], CultureInfo.InvariantCulture);
                    sources = Convert.ToInt32(reader["active_sources"] is DBNull ? 0 : reader["active_sources"], CultureInfo.InvariantCulture);
                    compareRuns = Convert.ToInt32(reader["compare_runs"] is DBNull ? 0 : reader["compare_runs"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAutoPriceRuleDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAutoPriceRules;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAutoPriceRuleDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["rule_key"] is DBNull ? string.Empty : reader["rule_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["min_margin_percent"] is DBNull ? 0 : reader["min_margin_percent"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["auto_update_prices"] is DBNull ? 0 : reader["auto_update_prices"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["schedule_hours"] is DBNull ? 0 : reader["schedule_hours"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpAutoPriceSummary(rules, sources, compareRuns, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpUaeTaxComplianceDigestResult> BuildCpUaeTaxComplianceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpUaeTaxComplianceSummary(0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var legislation = 0; var advance = 0; var refunds = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpUaeTaxStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    legislation = Convert.ToInt32(reader["legislation_count"] is DBNull ? 0 : reader["legislation_count"], CultureInfo.InvariantCulture);
                    advance = Convert.ToInt32(reader["vat_advance_rows"] is DBNull ? 0 : reader["vat_advance_rows"], CultureInfo.InvariantCulture);
                    refunds = Convert.ToInt32(reader["vat_refund_rows"] is DBNull ? 0 : reader["vat_refund_rows"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpUaeTaxItemDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpUaeTaxItems;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpUaeTaxItemDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["slug"] is DBNull ? string.Empty : reader["slug"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["issue_date"] is DBNull ? string.Empty : reader["issue_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tax_category"] is DBNull ? string.Empty : reader["tax_category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["is_new"] is DBNull ? 0 : reader["is_new"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["is_updated"] is DBNull ? 0 : reader["is_updated"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["time_synced"] is DBNull ? 0 : reader["time_synced"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpUaeTaxComplianceSummary(legislation, advance, refunds, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpBudgetsDigestResult> BuildCpBudgetsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpBudgetsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var budgets = 0; var active = 0; var lines = 0; var dims = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpBudgetStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    budgets = Convert.ToInt32(reader["budget_count"] is DBNull ? 0 : reader["budget_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_budgets"] is DBNull ? 0 : reader["active_budgets"], CultureInfo.InvariantCulture);
                    lines = Convert.ToInt32(reader["budget_line_count"] is DBNull ? 0 : reader["budget_line_count"], CultureInfo.InvariantCulture);
                    dims = Convert.ToInt32(reader["dimension_count"] is DBNull ? 0 : reader["dimension_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpBudgetDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpBudgets;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpBudgetDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["fiscal_year"] is DBNull ? string.Empty : reader["fiscal_year"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["business_unit_id"] is DBNull ? 0 : reader["business_unit_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_master"] is DBNull ? 0 : reader["is_master"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpBudgetsSummary(budgets, active, lines, dims, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCarriersDigestResult> BuildCpCarriersDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCarriersSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var carriers = 0; var active = 0; var rates = 0; var open = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCarrierStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    carriers = Convert.ToInt32(reader["carrier_count"] is DBNull ? 0 : reader["carrier_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_carriers"] is DBNull ? 0 : reader["active_carriers"], CultureInfo.InvariantCulture);
                    rates = Convert.ToInt32(reader["rate_count"] is DBNull ? 0 : reader["rate_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_shipments"] is DBNull ? 0 : reader["open_shipments"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCarrierDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCarriers;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCarrierDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["mode"] is DBNull ? string.Empty : reader["mode"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["rating"] is DBNull ? 0 : reader["rating"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpCarriersSummary(carriers, active, rates, open, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPaymentGatewaysDigestResult> BuildCpPaymentGatewaysDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPaymentGatewaysSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var gateways = 0; var active = 0; var selectable = 0; var accounts = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPaymentGatewayStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    gateways = Convert.ToInt32(reader["gateway_count"] is DBNull ? 0 : reader["gateway_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_gateways"] is DBNull ? 0 : reader["active_gateways"], CultureInfo.InvariantCulture);
                    selectable = Convert.ToInt32(reader["selectable_gateways"] is DBNull ? 0 : reader["selectable_gateways"], CultureInfo.InvariantCulture);
                    accounts = Convert.ToInt32(reader["account_count"] is DBNull ? 0 : reader["account_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPaymentGatewayDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPaymentGateways;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPaymentGatewayDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["handler"] is DBNull ? string.Empty : reader["handler"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["is_selectable"] is DBNull ? 0 : reader["is_selectable"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpPaymentGatewaysSummary(gateways, active, selectable, accounts, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpWorkflowsDigestResult> BuildCpWorkflowsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpWorkflowsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
            var workflows = 0; var active = 0; var runs = 0; var failed = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpWorkflowStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    workflows = Convert.ToInt32(reader["workflow_count"] is DBNull ? 0 : reader["workflow_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_workflows"] is DBNull ? 0 : reader["active_workflows"], CultureInfo.InvariantCulture);
                    runs = Convert.ToInt32(reader["run_count"] is DBNull ? 0 : reader["run_count"], CultureInfo.InvariantCulture);
                    failed = Convert.ToInt32(reader["failed_runs"] is DBNull ? 0 : reader["failed_runs"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpWorkflowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpWorkflows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpWorkflowDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["trigger_type"] is DBNull ? string.Empty : reader["trigger_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["version"] is DBNull ? 0 : reader["version"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["run_count"] is DBNull ? 0 : reader["run_count"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["last_run_status"] is DBNull ? string.Empty : reader["last_run_status"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpWorkflowsSummary(workflows, active, runs, failed, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    private static CpMobileAppsSummary ParseMobileAppsSummary(string integrationsJson, string source, string message)
    {
        var enabled = false;
        var appName = string.Empty;
        var bundleId = string.Empty;
        var deepLinkScheme = string.Empty;
        var deepLinkDomain = string.Empty;
        var apiBaseUrl = string.Empty;
        var playStoreUrl = string.Empty;
        var appStoreUrl = string.Empty;
        var pwaEnabled = true;
        var firebaseProjectId = string.Empty;
        var pushEnabled = false;

        if (!string.IsNullOrWhiteSpace(integrationsJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(integrationsJson);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("mobile", out var mobile)
                    && mobile.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    enabled = ReadJsonBool(mobile, "enabled");
                    appName = ReadJsonString(mobile, "app_name");
                    bundleId = ReadJsonString(mobile, "bundle_id");
                    deepLinkScheme = ReadJsonString(mobile, "deep_link_scheme");
                    deepLinkDomain = ReadJsonString(mobile, "deep_link_domain");
                    apiBaseUrl = ReadJsonString(mobile, "api_base_url");
                    playStoreUrl = ReadJsonString(mobile, "play_store_url");
                    appStoreUrl = ReadJsonString(mobile, "app_store_url");
                    pwaEnabled = mobile.TryGetProperty("pwa_enabled", out _) ? ReadJsonBool(mobile, "pwa_enabled") : true;
                    firebaseProjectId = ReadJsonString(mobile, "firebase_project_id");
                    pushEnabled = ReadJsonBool(mobile, "push_enabled");
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                return new(
                    false, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, true, string.Empty, false,
                    "database-error", $"integrations_json.mobile parse failed: {ex.Message}");
            }
        }

        return new(
            enabled, appName, bundleId, deepLinkScheme, deepLinkDomain, apiBaseUrl,
            playStoreUrl, appStoreUrl, pwaEnabled, firebaseProjectId, pushEnabled,
            source, message);
    }

    private static string ReadJsonString(System.Text.Json.JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop))
        {
            return string.Empty;
        }

        return prop.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => prop.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => prop.ToString(),
            System.Text.Json.JsonValueKind.True => "true",
            System.Text.Json.JsonValueKind.False => "false",
            _ => string.Empty
        };
    }

    private static bool ReadJsonBool(System.Text.Json.JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop))
        {
            return false;
        }

        return prop.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Number => prop.TryGetInt32(out var n) && n != 0,
            System.Text.Json.JsonValueKind.String => bool.TryParse(prop.GetString(), out var b) && b,
            _ => false
        };
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
