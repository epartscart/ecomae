using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Observability;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Read-only CP/ERP/BOS/storefront digests for migration shells. Performs zero writes.
/// Missing tables degrade to zeros with a database-error/migration source.
/// </summary>
public sealed class SurfaceDashboardSummaryReporter : ISurfaceDashboardSummaryReporter
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly PhpWarehouseSearchBridge? _phpWarehouseBridge;

    public SurfaceDashboardSummaryReporter(
        ITenantDbConnectionFactory connections,
        IHttpContextAccessor? httpContextAccessor = null,
        PhpWarehouseSearchBridge? phpWarehouseBridge = null)
    {
        _connections = connections;
        _httpContextAccessor = httpContextAccessor;
        _phpWarehouseBridge = phpWarehouseBridge;
    }

    /// <summary>
    /// Product paths must not call PHP files (deletion-ready). Opt-in only for emergency parity probes.
    /// </summary>
    private static bool AllowPhpWarehouseBridge =>
        string.Equals(
            Environment.GetEnvironmentVariable("ECOMAE_ALLOW_PHP_WAREHOUSE_BRIDGE"),
            "YES",
            StringComparison.OrdinalIgnoreCase);

    public async Task<ControlPanelDashboardSummary> BuildControlPanelAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            // Shop KPIs (users/sessions) from tenant shop DB — ePartsCart forces docpart when unbound.
            // Portal tenant counts live only on the platform registry DB (never on docpart).
            int users;
            int adminSessions;
            await using (var shop = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false))
            {
                users = await ScalarIntAsync(shop, LegacySurfaceDashboardSql.CountUsers, cancellationToken).ConfigureAwait(false);
                adminSessions = await ScalarIntSafeAsync(shop, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            }

            int tenants;
            int active;
            await using (var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false))
            {
                tenants = await ScalarIntSafeAsync(registry, LegacySurfaceDashboardSql.CountPortalTenants, cancellationToken).ConfigureAwait(false);
                active = await ScalarIntSafeAsync(registry, LegacySurfaceDashboardSql.CountActivePortalTenants, cancellationToken).ConfigureAwait(false);
            }

            return new(users, adminSessions, tenants, active, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(0, 0, 0, 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpDashboardDigestResult> BuildErpAsync(CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Surfaces.StartActivity("surface.erp.dashboard-summary");
        activity?.SetTag("ecomae.surface", "erp");
        activity?.SetTag("ecomae.digest", "/erp/dashboard-summary");

        if (!_connections.IsConfigured)
        {
            var mig = EmptyErpSummary("migration", "TenantRegistry DB is not configured.");
            return new(mig, [], 0, mig.Source, mig.Message);
        }

        try
        {
            // Uses TenantContext DB/credentials when present (per-tenant isolation).
            // ePartsCart shop fallback = docpart (same as storefront OpenStorefrontShopAsync).
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var monthStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero)
                .ToUnixTimeSeconds();
            var periodKey = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var overdueBefore = now - (86400L * 30);

            ErpDashboardSummary summary;
            try
            {
                summary = await ReadErpDashboardBatchAsync(
                    connection, monthStart, now, periodKey, overdueBefore, cancellationToken).ConfigureAwait(false);
                activity?.SetTag("ecomae.erp_kpi_path", "batch");
            }
            catch
            {
                // Missing tables make the single-statement batch fail; degrade per-scalar like PHP digests.
                summary = await ReadErpDashboardScalarsAsync(
                    connection, monthStart, now, periodKey, overdueBefore, cancellationToken).ConfigureAwait(false);
                activity?.SetTag("ecomae.erp_kpi_path", "scalar-fallback");
            }

            var queue = BuildErpApprovalQueue(summary);
            return new(summary, queue, queue.Count, summary.Source, summary.Message);
        }
        catch (Exception ex)
        {
            var err = EmptyErpSummary("database-error", ex.Message);
            return new(err, [], 0, err.Source, err.Message);
        }
    }

    private static async Task<ErpDashboardSummary> ReadErpDashboardBatchAsync(
        DbConnection connection,
        long monthStart,
        long now,
        string periodKey,
        long overdueBefore,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectErpDashboardSummaryBatch;
        AddParameter(command, "@dateFrom", monthStart);
        AddParameter(command, "@dateTo", now);
        AddParameter(command, "@periodKey", periodKey);
        AddParameter(command, "@overdueBefore", overdueBefore);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return EmptyErpSummary("database", string.Empty);
        }

        var cash = ReadDecimal(reader, "cash_position");
        var credit = ReadDecimal(reader, "supplier_credit");
        var debit = ReadDecimal(reader, "supplier_debit");
        var cashAccounts = ReadInt(reader, "cash_accounts");
        var suppliers = ReadInt(reader, "active_suppliers");
        var purchases = ReadInt(reader, "active_purchases");
        var receivables = ReadDecimal(reader, "receivables");
        var payables = ReadDecimal(reader, "payables");
        var stockValue = ReadDecimal(reader, "stock_value");
        var revenue = ReadDecimal(reader, "revenue_ex_vat");
        var orders = ReadInt(reader, "orders_count");
        var arBalance = ReadDecimal(reader, "ar_balance");
        var apBalance = ReadDecimal(reader, "ap_balance");
        var vatOut = ReadDecimal(reader, "vat_out");
        var vatIn = ReadDecimal(reader, "vat_in");
        var inventoryItems = ReadInt(reader, "inventory_items");
        var periodStatus = Convert.ToString(reader["period_status"], CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(periodStatus))
        {
            periodStatus = "open";
        }

        var draftSo = ReadInt(reader, "draft_sales_orders");
        var pendingPo = ReadInt(reader, "pending_purchase_orders");
        var unpostedGl = ReadInt(reader, "unposted_gl_journals");
        var overdueInv = ReadInt(reader, "overdue_invoices");
        var lowStock = ReadInt(reader, "low_stock_items");
        var pendingEinv = ReadInt(reader, "pending_einvoices");
        var processOpen = ReadInt(reader, "process_open");
        var processDone = ReadInt(reader, "process_done");
        var processOverdue = ReadInt(reader, "process_overdue");

        return new ErpDashboardSummary(
            cash, credit, debit, credit - debit, cashAccounts, suppliers, purchases,
            receivables, payables, stockValue,
            revenue, orders, arBalance, apBalance, vatOut - vatIn, periodStatus, inventoryItems,
            draftSo, pendingPo, unpostedGl, overdueInv, lowStock, pendingEinv,
            processOpen, processDone, processOverdue,
            "database", string.Empty);
    }

    private async Task<ErpDashboardSummary> ReadErpDashboardScalarsAsync(
        DbConnection connection,
        long monthStart,
        long now,
        string periodKey,
        long overdueBefore,
        CancellationToken cancellationToken)
    {
        var cash = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumCashBankTotal, cancellationToken).ConfigureAwait(false);
        var credit = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumSupplierCredit, cancellationToken).ConfigureAwait(false);
        var debit = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumSupplierDebit, cancellationToken).ConfigureAwait(false);
        var cashAccounts = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCashAccounts, cancellationToken).ConfigureAwait(false);
        var suppliers = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActiveSuppliers, cancellationToken).ConfigureAwait(false);
        var purchases = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountActivePurchases, cancellationToken).ConfigureAwait(false);
        var receivables = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumErpDashboardReceivables, cancellationToken).ConfigureAwait(false);
        var payables = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumErpDashboardPayables, cancellationToken).ConfigureAwait(false);
        var stockValue = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumErpDashboardStockValue, cancellationToken).ConfigureAwait(false);
        var revenue = await ScalarDecimalParamSafeAsync(
            connection, LegacySurfaceDashboardSql.SumErpCcRevenueExVat, cancellationToken,
            ("@dateFrom", monthStart), ("@dateTo", now)).ConfigureAwait(false);
        var orders = await ScalarIntParamSafeAsync(
            connection, LegacySurfaceDashboardSql.CountErpCcOrders, cancellationToken,
            ("@dateFrom", monthStart), ("@dateTo", now)).ConfigureAwait(false);
        var arBalance = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumErpCcArBalance, cancellationToken).ConfigureAwait(false);
        var apBalance = await ScalarDecimalSafeAsync(connection, LegacySurfaceDashboardSql.SumErpCcApBalance, cancellationToken).ConfigureAwait(false);
        var vatOut = await ScalarDecimalParamSafeAsync(
            connection, LegacySurfaceDashboardSql.SumErpCcVatOut, cancellationToken,
            ("@dateFrom", monthStart), ("@dateTo", now)).ConfigureAwait(false);
        var vatIn = await ScalarDecimalParamSafeAsync(
            connection, LegacySurfaceDashboardSql.SumErpCcVatIn, cancellationToken,
            ("@dateFrom", monthStart), ("@dateTo", now)).ConfigureAwait(false);
        var inventoryItems = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcInventoryItems, cancellationToken).ConfigureAwait(false);
        var periodStatus = await ScalarStringParamSafeAsync(
            connection, LegacySurfaceDashboardSql.SelectErpCcPeriodStatus, "open", cancellationToken,
            ("@periodKey", periodKey)).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(periodStatus))
        {
            periodStatus = "open";
        }

        var draftSo = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcDraftSalesOrders, cancellationToken).ConfigureAwait(false);
        var pendingPo = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcPendingPurchaseOrders, cancellationToken).ConfigureAwait(false);
        var unpostedGl = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcUnpostedGlJournals, cancellationToken).ConfigureAwait(false);
        var overdueInv = await ScalarIntParamSafeAsync(
            connection, LegacySurfaceDashboardSql.CountErpCcOverdueInvoices, cancellationToken,
            ("@overdueBefore", overdueBefore)).ConfigureAwait(false);
        var lowStock = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcLowStockItems, cancellationToken).ConfigureAwait(false);
        var pendingEinv = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcPendingEinvoices, cancellationToken).ConfigureAwait(false);
        var processOpen = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcProcessOpen, cancellationToken).ConfigureAwait(false);
        var processDone = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcProcessDone, cancellationToken).ConfigureAwait(false);
        var processOverdue = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountErpCcProcessOverdue, cancellationToken).ConfigureAwait(false);

        return new ErpDashboardSummary(
            cash, credit, debit, credit - debit, cashAccounts, suppliers, purchases,
            receivables, payables, stockValue,
            revenue, orders, arBalance, apBalance, vatOut - vatIn, periodStatus, inventoryItems,
            draftSo, pendingPo, unpostedGl, overdueInv, lowStock, pendingEinv,
            processOpen, processDone, processOverdue,
            "database", string.Empty);
    }

    public async Task<BosFleetSummary> BuildBosAsync(CancellationToken cancellationToken = default)
    {
        using var activity = EcomAeActivitySources.Surfaces.StartActivity("surface.bos.fleet-summary");
        activity?.SetTag("ecomae.surface", "bos");
        activity?.SetTag("ecomae.digest", "/bos/fleet-summary");

        if (!_connections.IsConfigured)
        {
            return EmptyBosSummary("migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            IReadOnlyList<PortalTenantDigest> list;
            await using (var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false))
            {
                list = await ReadTenantsAsync(registry, 500, cancellationToken).ConfigureAwait(false);
            }

            int adminSessions;
            await using (var shop = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false))
            {
                adminSessions = await ScalarIntSafeAsync(shop, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            }

            activity?.SetTag("ecomae.row_count", list.Count);
            return SummarizeFleet(list, adminSessions, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return EmptyBosSummary("database-error", ex.Message);
        }
    }

    public async Task<StorefrontAccountDigestResult> BuildStorefrontAccountAsync(int userId, int recentLimit = 10, CancellationToken cancellationToken = default)
    {
        var safeRecent = Math.Clamp(recentLimit, 1, 50);
        if (!_connections.IsConfigured)
        {
            var mig = new StorefrontAccountSummary(userId, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
            return new(mig, [], 0, "migration", mig.Message);
        }

        if (userId <= 0)
        {
            var rejected = new StorefrontAccountSummary(0, 0, 0, 0, "rejected", "Valid customer user id is required.");
            return new(rejected, [], 0, "rejected", rejected.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var orders = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerOrders, userId, cancellationToken).ConfigureAwait(false);
            var sessions = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerSessionsForUser, userId, cancellationToken).ConfigureAwait(false);
            var garage = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCustomerGarage, userId, cancellationToken).ConfigureAwait(false);
            var recent = new List<StorefrontOrderDigest>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectCustomerOrders;
                AddParameter(command, "@userId", userId);
                AddParameter(command, "@limit", safeRecent);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    recent.Add(new StorefrontOrderDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["paid"] is DBNull ? 0 : reader["paid"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["successfully_created"] is DBNull ? 0 : reader["successfully_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["status"] is DBNull ? 0 : reader["status"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new StorefrontAccountSummary(userId, orders, sessions, garage, "database", string.Empty);
            return new(summary, recent, recent.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = new StorefrontAccountSummary(userId, 0, 0, 0, "database-error", ex.Message);
            return new(err, [], 0, "database-error", ex.Message);
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
            // epc_portal_tenants is platform-registry only — OpenAsync(null) on epartscart
            // opens docpart and yields empty / database-error ("Nothing to show").
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
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
            var empty = EmptyBosSummary("migration", "TenantRegistry DB is not configured.");
            return new(empty, [], empty.Source, empty.Message);
        }

        try
        {
            // Fleet directory = registry; admin session count = shop (or platform on Super-CP).
            IReadOnlyList<PortalTenantDigest> tenants;
            await using (var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false))
            {
                tenants = await ReadTenantsAsync(registry, 500, cancellationToken).ConfigureAwait(false);
            }

            int adminSessions;
            await using (var shop = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false))
            {
                adminSessions = await ScalarIntSafeAsync(shop, LegacySurfaceDashboardSql.CountAdminSessions, cancellationToken).ConfigureAwait(false);
            }

            var summary = SummarizeFleet(tenants, adminSessions, "database", string.Empty);
            var sample = tenants.Take(safeLimit).ToArray();
            return new(summary, sample, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = EmptyBosSummary("database-error", ex.Message);
            return new(err, [], err.Source, err.Message);
        }
    }

    public async Task<ErpAccountsSummaryResult> BuildErpAccountsAsync(CancellationToken cancellationToken = default)
    {
        var digest = await BuildErpAsync(cancellationToken).ConfigureAwait(false);
        return new(digest.Summary, digest.Source, digest.Message);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
            var open = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpOrdersOpen, cancellationToken).ConfigureAwait(false);
            var pendingShip = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpOrdersPendingShip, cancellationToken).ConfigureAwait(false);
            var completed = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpOrdersCompleted, cancellationToken).ConfigureAwait(false);
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

            var summary = new CpOrdersSummary(open, today, pendingShip, "database", string.Empty, completed);

            List<CpShopOrderDigest> rows;
            try
            {
                rows = await ReadCpShopOrdersAsync(connection, LegacySurfaceDashboardSql.SelectCpShopOrders, safeLimit, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Tenant schemas may lack viewed/logs/obtaining joins — fall back to core columns.
                rows = await ReadCpShopOrdersAsync(connection, LegacySurfaceDashboardSql.SelectCpShopOrdersCore, safeLimit, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(new CpOrdersSummary(0, 0, 0, "database-error", ex.Message), [], 0, "database-error", ex.Message);
        }
    }

    private async Task<List<CpShopOrderDigest>> ReadCpShopOrdersAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        int safeLimit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@limit", safeLimit);
        var rows = new List<CpShopOrderDigest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadCpShopOrderDigest(reader));
        }

        return rows;
    }

    public async Task<CpOrderDetailDigest?> GetCpOrderDetailAsync(long orderId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var orderCmd = connection.CreateCommand();
            orderCmd.CommandText = LegacySurfaceDashboardSql.SelectCpShopOrderById;
            AddParameter(orderCmd, "@orderId", orderId);
            await using var orderReader = await orderCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await orderReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var order = ReadCpShopOrderDigest(orderReader);
            var paidSum = Convert.ToDecimal(orderReader["paid_sum"] is DBNull ? 0m : orderReader["paid_sum"], CultureInfo.InvariantCulture);
            var customerPhone = Convert.ToString(orderReader["customer_phone"] is DBNull ? "" : orderReader["customer_phone"], CultureInfo.InvariantCulture) ?? "";
            await orderReader.DisposeAsync().ConfigureAwait(false);

            var items = new List<CpOrderItemDigest>();
            try
            {
                await using var itemsCmd = connection.CreateCommand();
                itemsCmd.CommandText = LegacySurfaceDashboardSql.SelectCpShopOrderItems;
                AddParameter(itemsCmd, "@orderId", orderId);
                await using var itemsReader = await itemsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await itemsReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    items.Add(new CpOrderItemDigest(
                        Convert.ToInt64(itemsReader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(itemsReader["order_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(itemsReader["brand"] is DBNull ? "" : itemsReader["brand"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToString(itemsReader["article"] is DBNull ? "" : itemsReader["article"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToString(itemsReader["name"] is DBNull ? "" : itemsReader["name"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToDecimal(itemsReader["price"] is DBNull ? 0m : itemsReader["price"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(itemsReader["count_need"] is DBNull ? 0m : itemsReader["count_need"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(itemsReader["purchase"] is DBNull ? 0m : itemsReader["purchase"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(itemsReader["status"] is DBNull ? 0 : itemsReader["status"], CultureInfo.InvariantCulture),
                        Convert.ToString(itemsReader["status_name"] is DBNull ? "" : itemsReader["status_name"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToString(itemsReader["storage_label"] is DBNull ? "" : itemsReader["storage_label"], CultureInfo.InvariantCulture) ?? ""));
                }
            }
            catch
            {
                // Item detail columns vary by tenant schema — list shell still works.
            }

            var logs = new List<CpOrderLogDigest>();
            try
            {
                await using var logsCmd = connection.CreateCommand();
                logsCmd.CommandText = LegacySurfaceDashboardSql.SelectCpShopOrderLogs;
                AddParameter(logsCmd, "@orderId", orderId);
                await using var logsReader = await logsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await logsReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    logs.Add(new CpOrderLogDigest(
                        Convert.ToInt64(logsReader["time"] is DBNull ? 0 : logsReader["time"], CultureInfo.InvariantCulture),
                        Convert.ToString(logsReader["text"] is DBNull ? "" : logsReader["text"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToInt32(logsReader["is_manager"] is DBNull ? 0 : logsReader["is_manager"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(logsReader["is_robot"] is DBNull ? 0 : logsReader["is_robot"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
            }

            var messages = new List<CpOrderMessageDigest>();
            try
            {
                await using var msgCmd = connection.CreateCommand();
                msgCmd.CommandText = LegacySurfaceDashboardSql.SelectCpShopOrderMessages;
                AddParameter(msgCmd, "@orderId", orderId);
                await using var msgReader = await msgCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await msgReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    messages.Add(new CpOrderMessageDigest(
                        Convert.ToInt64(msgReader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(msgReader["time"] is DBNull ? 0 : msgReader["time"], CultureInfo.InvariantCulture),
                        Convert.ToString(msgReader["text"] is DBNull ? "" : msgReader["text"], CultureInfo.InvariantCulture) ?? "",
                        Convert.ToInt32(msgReader["is_customer"] is DBNull ? 0 : msgReader["is_customer"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
            }

            var priceSum = order.OrderSum;
            var purchaseSum = order.PurchaseSum;
            var paidLeft = Math.Max(0m, priceSum - Math.Abs(paidSum));
            return new CpOrderDetailDigest(
                order,
                priceSum,
                purchaseSum,
                Math.Abs(paidSum),
                paidLeft,
                priceSum - purchaseSum,
                order.CustomerLabel,
                order.CustomerLabel,
                customerPhone,
                items,
                logs,
                messages,
                "database",
                string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private static CpShopOrderDigest ReadCpShopOrderDigest(System.Data.Common.DbDataReader reader)
    {
        var forInverse = HasColumn(reader, "status_for_inverse")
            ? Convert.ToInt32(reader["status_for_inverse"] is DBNull ? 0 : reader["status_for_inverse"], CultureInfo.InvariantCulture)
            : 0;
        var forFinish = HasColumn(reader, "status_for_finish")
            ? Convert.ToInt32(reader["status_for_finish"] is DBNull ? 0 : reader["status_for_finish"], CultureInfo.InvariantCulture)
            : 0;
        var forCreated = HasColumn(reader, "status_for_created")
            ? Convert.ToInt32(reader["status_for_created"] is DBNull ? 0 : reader["status_for_created"], CultureInfo.InvariantCulture)
            : 0;
        var badge = forInverse == 1
            ? "epc-scp-badge--urgent"
            : forFinish == 1
                ? "epc-scp-badge--tenant"
                : forCreated == 1
                    ? "epc-scp-badge--high"
                    : "epc-scp-badge--normal";

        return new CpShopOrderDigest(
            Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["status"] is DBNull ? 0 : reader["status"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["paid"] is DBNull ? 0 : reader["paid"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["paid_type"] is DBNull ? 0 : reader["paid_type"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["office_id"] is DBNull ? 0 : reader["office_id"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["successfully_created"] is DBNull ? 0 : reader["successfully_created"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["count_items"] is DBNull ? 0 : reader["count_items"], CultureInfo.InvariantCulture),
            Convert.ToDecimal(reader["order_sum"] is DBNull ? 0m : reader["order_sum"], CultureInfo.InvariantCulture),
            HasColumn(reader, "purchase_sum")
                ? Convert.ToDecimal(reader["purchase_sum"] is DBNull ? 0m : reader["purchase_sum"], CultureInfo.InvariantCulture)
                : 0m,
            HasColumn(reader, "last_modified")
                ? Convert.ToInt64(reader["last_modified"] is DBNull ? 0 : reader["last_modified"], CultureInfo.InvariantCulture)
                : 0,
            HasColumn(reader, "viewed_flag")
                ? Convert.ToInt32(reader["viewed_flag"] is DBNull ? 1 : reader["viewed_flag"], CultureInfo.InvariantCulture)
                : 1,
            HasColumn(reader, "customer_label")
                ? Convert.ToString(reader["customer_label"] is DBNull ? "" : reader["customer_label"], CultureInfo.InvariantCulture) ?? ""
                : "",
            HasColumn(reader, "status_name")
                ? Convert.ToString(reader["status_name"] is DBNull ? "" : reader["status_name"], CultureInfo.InvariantCulture) ?? ""
                : "",
            badge,
            HasColumn(reader, "obtain_caption")
                ? Convert.ToString(reader["obtain_caption"] is DBNull ? "" : reader["obtain_caption"], CultureInfo.InvariantCulture) ?? ""
                : "");
    }

    private static bool HasColumn(System.Data.Common.DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
                    Convert.ToDecimal(reader["balance"] is DBNull ? 0m : reader["balance"], CultureInfo.InvariantCulture),
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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

    public Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, int limit, CancellationToken cancellationToken = default)
        => SearchStorefrontPartsAsync(article, null, limit, cancellationToken);

    public async Task<StorefrontPartSearchResult> SearchStorefrontPartsAsync(string article, string? brand, int limit, CancellationToken cancellationToken = default)
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

        var brandTrim = (brand ?? string.Empty).Trim();
        var brandUpper = brandTrim.ToUpperInvariant();
        var brandCompact = CompactStorefrontBrand(brandTrim);
        var unbound = TryGetUnboundTenantShopMessage(out var unboundMessage);
        // Brand+article (CHPU / protocol-3): prefer indexed article_search on the primary article only.
        // Skip 80-cross candidate expansion + REPLACE() cascade — PHP prices_enclosure is ~30–50ms.
        var brandedFastPath = brandTrim.Length > 0;

        if (!unbound)
        {
            try
            {
                await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
                var hasSearchCol = await ProbePriceArticleSearchColumnAsync(connection, cancellationToken).ConfigureAwait(false);
                IReadOnlyList<string> candidates = brandedFastPath
                    ? [normalized]
                    : await CollectStorefrontArticleCandidatesAsync(connection, normalized, cancellationToken).ConfigureAwait(false);

                // PHP CHPU: article-only warehouse SQL, then brand/synonym filter in UI.
                // Keep branded SQL first for index friendliness; resolve aliases before heavy miss path.
                var rows = brandedFastPath
                    ? await QueryStorefrontPartOffersBrandedFastAsync(
                        connection,
                        candidates,
                        brandUpper,
                        brandCompact,
                        safeLimit,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false)
                    : await QueryStorefrontPartOffersCascadeAsync(
                        connection,
                        candidates,
                        brandUpper,
                        brandCompact,
                        safeLimit,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false);

                if (rows.Count == 0 && brandTrim.Length > 0)
                {
                    // 1) Manufacturer synonym aliases (JA ASHIKA → JS ASAKASHI etc.) — ArticleSearch only.
                    var aliases = await LoadManufacturerBrandAliasesAsync(connection, brandTrim, cancellationToken)
                        .ConfigureAwait(false);
                    rows = await QueryStorefrontPartOffersForBrandAliasesAsync(
                        connection,
                        candidates,
                        brandTrim,
                        brandCompact,
                        aliases,
                        safeLimit,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false);

                    // 2) Fuzzy pick from article warehouse brands (spoken "ja ashika" vs "JS ASAKASHI").
                    if (rows.Count == 0)
                    {
                        var resolved = await ResolveWarehouseBrandForArticleAsync(
                            connection,
                            candidates,
                            brandTrim,
                            brandCompact,
                            hasSearchCol,
                            cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(resolved)
                            && !string.Equals(resolved, brandUpper, StringComparison.OrdinalIgnoreCase))
                        {
                            rows = await QueryStorefrontPartOffersBrandedFastAsync(
                                connection,
                                candidates,
                                resolved.ToUpperInvariant(),
                                CompactStorefrontBrand(resolved),
                                safeLimit,
                                hasSearchCol,
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                if (rows.Count > 0)
                {
                    return new(normalized, rows, rows.Count, "database", string.Empty);
                }
            }
            catch (Exception ex)
            {
                if (_phpWarehouseBridge is null || !AllowPhpWarehouseBridge)
                {
                    return new(normalized, [], 0, "database-error", ex.Message);
                }
            }
        }

        // Default: ASP.NET SQL only — no product .php HTTP twin (PHP deletion-ready).
        if (AllowPhpWarehouseBridge && _phpWarehouseBridge is not null)
        {
            var phpRows = await _phpWarehouseBridge
                .TryLoadOffersAsync(normalized, brandTrim, safeLimit, cancellationToken)
                .ConfigureAwait(false);
            if (phpRows.Count > 0)
            {
                return new(normalized, phpRows, phpRows.Count, "php-chpu", string.Empty);
            }
        }

        if (unbound)
        {
            return new(normalized, [], 0, "migration", unboundMessage);
        }

        return new(normalized, [], 0, "database", string.Empty);
    }

    public async Task<StorefrontPartStockProbeResult> ProbeStorefrontPartStockAsync(
        string article,
        string? brand,
        CancellationToken cancellationToken = default)
    {
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(string.Empty, false, string.Empty, 0m, "empty", "Enter a part number or OE code.");
        }

        if (!_connections.IsConfigured)
        {
            return new(normalized, false, string.Empty, 0m, "migration", "TenantRegistry DB is not configured.");
        }

        var brandTrim = (brand ?? string.Empty).Trim();
        var brandUpper = brandTrim.ToUpperInvariant();
        if (TryGetUnboundTenantShopMessage(out var unboundMessage))
        {
            return new(normalized, false, string.Empty, 0m, "migration", unboundMessage);
        }

        try
        {
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var hasSearchCol = await ProbePriceArticleSearchColumnAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            // Mirror PHP max_statement_time≈2s stock probe — never block first paint.
            command.CommandTimeout = 2;
            if (hasSearchCol && brandUpper.Length > 0)
            {
                command.CommandText = """
                    SELECT IFNULL(`exist`, 0) AS exist,
                           IFNULL(`name`, '') AS name,
                           IFNULL(`price`, 0) AS price
                    FROM `shop_docpart_prices_data`
                    WHERE `article_search` = @article
                      AND UPPER(TRIM(`manufacturer`)) = @brand
                      AND IFNULL(`exist`, 0) > 0
                    LIMIT 1
                    """;
            }
            else if (hasSearchCol)
            {
                command.CommandText = """
                    SELECT IFNULL(`exist`, 0) AS exist,
                           IFNULL(`name`, '') AS name,
                           IFNULL(`price`, 0) AS price
                    FROM `shop_docpart_prices_data`
                    WHERE `article_search` = @article
                      AND IFNULL(`exist`, 0) > 0
                    LIMIT 1
                    """;
            }
            else if (brandUpper.Length > 0)
            {
                command.CommandText = """
                    SELECT IFNULL(`exist`, 0) AS exist,
                           IFNULL(`name`, '') AS name,
                           IFNULL(`price`, 0) AS price
                    FROM `shop_docpart_prices_data`
                    WHERE UPPER(TRIM(IFNULL(`article`, ''))) = @article
                      AND UPPER(TRIM(`manufacturer`)) = @brand
                      AND IFNULL(`exist`, 0) > 0
                    LIMIT 1
                    """;
            }
            else
            {
                command.CommandText = """
                    SELECT IFNULL(`exist`, 0) AS exist,
                           IFNULL(`name`, '') AS name,
                           IFNULL(`price`, 0) AS price
                    FROM `shop_docpart_prices_data`
                    WHERE UPPER(TRIM(IFNULL(`article`, ''))) = @article
                      AND IFNULL(`exist`, 0) > 0
                    LIMIT 1
                    """;
            }

            AddParameter(command, "@article", normalized);
            if (brandUpper.Length > 0)
            {
                AddParameter(command, "@brand", brandUpper);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var exist = Convert.ToInt32(reader["exist"] is DBNull ? 0 : reader["exist"], CultureInfo.InvariantCulture);
                var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                var price = Convert.ToDecimal(reader["price"] is DBNull ? 0m : reader["price"], CultureInfo.InvariantCulture);
                return new(normalized, exist > 0, name, price, "database", string.Empty);
            }

            return new(normalized, false, string.Empty, 0m, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(normalized, false, string.Empty, 0m, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontArticleBrandsResult> ListStorefrontArticleBrandsAsync(string article, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(string.Empty, [], 0, "empty", "Enter a part number or OE code.");
        }

        if (!_connections.IsConfigured)
        {
            return new(normalized, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        var unbound = TryGetUnboundTenantShopMessage(out var unboundMessage);
        if (!unbound)
        {
            try
            {
                await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
                var hasSearchCol = await ProbePriceArticleSearchColumnAsync(connection, cancellationToken).ConfigureAwait(false);
                var hasAnalogsSearch = await ProbeAnalogsSearchColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
                var candidates = await CollectStorefrontArticleCandidatesAsync(connection, normalized, cancellationToken).ConfigureAwait(false);

                var byBrand = new Dictionary<string, StorefrontArticleBrandDigest>(StringComparer.OrdinalIgnoreCase);
                var warehouseBrands = await QueryStorefrontWarehouseBrandsAsync(
                    connection,
                    candidates,
                    safeLimit,
                    StorefrontArticleMatchMode.SimpleEquality,
                    hasSearchCol,
                    cancellationToken).ConfigureAwait(false);

                if (warehouseBrands.Count == 0)
                {
                    warehouseBrands = await QueryStorefrontWarehouseBrandsAsync(
                        connection,
                        candidates,
                        safeLimit,
                        StorefrontArticleMatchMode.ArticleSearch,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false);
                }

                if (warehouseBrands.Count == 0)
                {
                    warehouseBrands = await QueryStorefrontWarehouseBrandsAsync(
                        connection,
                        candidates,
                        safeLimit,
                        StorefrontArticleMatchMode.ExactTrim,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false);
                }

                if (warehouseBrands.Count == 0)
                {
                    warehouseBrands = await QueryStorefrontWarehouseBrandsAsync(
                        connection,
                        candidates,
                        safeLimit,
                        StorefrontArticleMatchMode.ReplaceNormalize,
                        hasSearchCol,
                        cancellationToken).ConfigureAwait(false);
                }

                foreach (var brand in warehouseBrands)
                {
                    byBrand[brand.Brand.ToUpperInvariant()] = brand;
                }

                // PHP epc_collect_article_catalog_brands always merges CP crosses into the picker.
                await MergeCpCrossBrandsAsync(connection, normalized, byBrand, hasAnalogsSearch, cancellationToken)
                    .ConfigureAwait(false);

                // Warehouse stock brands first (PHP SSR), then cross-only siblings.
                var brands = byBrand.Values
                    .OrderByDescending(b => b.Exist > 0)
                    .ThenBy(b => b.Brand, StringComparer.OrdinalIgnoreCase)
                    .Take(safeLimit)
                    .ToList();

                if (brands.Count > 0)
                {
                    return new(normalized, brands, brands.Count, "database", string.Empty);
                }
            }
            catch (Exception ex)
            {
                if (_phpWarehouseBridge is null || !AllowPhpWarehouseBridge)
                {
                    return new(normalized, [], 0, "database-error", ex.Message);
                }
            }
        }

        if (AllowPhpWarehouseBridge && _phpWarehouseBridge is not null)
        {
            var phpBrands = await _phpWarehouseBridge
                .TryLoadBrandsAsync(normalized, safeLimit, cancellationToken)
                .ConfigureAwait(false);
            if (phpBrands.Count > 0)
            {
                return new(normalized, phpBrands, phpBrands.Count, "php-chpu", string.Empty);
            }
        }

        if (unbound)
        {
            return new(normalized, [], 0, "migration", unboundMessage);
        }

        return new(normalized, [], 0, "database", string.Empty);
    }

    public Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, int limit, CancellationToken cancellationToken = default)
        => ListStorefrontCrossRefsAsync(article, brand: null, limit, cancellationToken);

    public async Task<StorefrontCrossRefsResult> ListStorefrontCrossRefsAsync(string article, string? brand, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        var brandNorm = (brand ?? string.Empty).Trim().ToUpperInvariant();
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
            // Shop DB (docpart on ePartsCart) — not registry OpenAsync(null).
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var hasAnalogsSearch = await ProbeAnalogsSearchColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs.Replace(
                "{CROSS_MATCH}",
                LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearch),
                StringComparison.Ordinal);
            AddParameter(command, "@article", normalized);
            AddParameter(command, "@limit", Math.Min(safeLimit * 20, 3000));
            var rows = new List<StorefrontCrossRefDigest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && rows.Count < safeLimit)
            {
                var sourceBrand = Convert.ToString(reader["source_brand"] is DBNull ? string.Empty : reader["source_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
                var sourceArticle = Convert.ToString(reader["source_article"] is DBNull ? string.Empty : reader["source_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                var crossBrand = Convert.ToString(reader["cross_brand"] is DBNull ? string.Empty : reader["cross_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
                var crossArticle = Convert.ToString(reader["cross_article"] is DBNull ? string.Empty : reader["cross_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                var sourceNorm = PriceLookupRequest.NormalizeArticle(sourceArticle);
                var crossNorm = PriceLookupRequest.NormalizeArticle(crossArticle);
                string partnerBrand;
                string partnerArticle;
                string matchedBrand;
                if (sourceNorm == normalized && crossNorm != string.Empty)
                {
                    matchedBrand = sourceBrand;
                    partnerBrand = crossBrand;
                    partnerArticle = crossArticle;
                }
                else if (crossNorm == normalized && sourceNorm != string.Empty)
                {
                    matchedBrand = crossBrand;
                    partnerBrand = sourceBrand;
                    partnerArticle = sourceArticle;
                }
                else
                {
                    continue;
                }

                // PHP ajax_epc_cross_search filters by selected manufacturer when brand is set.
                if (brandNorm.Length > 0
                    && !string.Equals(matchedBrand.Trim(), brandNorm, StringComparison.OrdinalIgnoreCase)
                    && CompactStorefrontBrand(matchedBrand) != CompactStorefrontBrand(brandNorm))
                {
                    continue;
                }

                var key = partnerBrand.Trim().ToUpperInvariant() + "|" + PriceLookupRequest.NormalizeArticle(partnerArticle);
                if (!seen.Add(key))
                {
                    continue;
                }

                rows.Add(new StorefrontCrossRefDigest(partnerBrand.Trim(), partnerArticle.Trim(), false));
            }

            await EnrichStorefrontCrossStockAsync(connection, rows, cancellationToken).ConfigureAwait(false);

            // Product path is ASP.NET-only. PHP ajax_epc_cross_search is never called unless both
            // ECOMAE_ALLOW_PHP_WAREHOUSE_BRIDGE=YES and ECOMAE_SSR_PHP_CROSS=YES (emergency probe).
            if (rows.Count < 8
                && AllowPhpWarehouseBridge
                && _phpWarehouseBridge is not null
                && string.Equals(
                    Environment.GetEnvironmentVariable("ECOMAE_SSR_PHP_CROSS"),
                    "YES",
                    StringComparison.OrdinalIgnoreCase))
            {
                var phpRows = await _phpWarehouseBridge
                    .TryLoadCrossSearchAsync(normalized, brandNorm.Length > 0 ? brandNorm : null, safeLimit, cancellationToken)
                    .ConfigureAwait(false);
                if (phpRows.Count > rows.Count)
                {
                    return new(normalized, phpRows, phpRows.Count, "php-cross-search", string.Empty);
                }
            }

            return new(normalized, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(normalized, [], 0, "database-error", ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<StorefrontCrossSearchResult> BuildStorefrontCrossSearchAsync(
        string article,
        string? brand,
        int limit,
        CancellationToken cancellationToken = default,
        bool includeCrossbase = false)
    {
        // PHP local path loads hundreds of CP analogs in one indexed query — keep that shape for ~1s paint.
        var safeLimit = Math.Clamp(limit, 1, 600);
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        var brandNorm = (brand ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(string.Empty, brandNorm, [], [], 0, 0, 0, "empty", "Enter a part number or OE code.");
        }

        if (!_connections.IsConfigured)
        {
            return new(normalized, brandNorm, [], [], 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var hasAnalogsSearch = await ProbeAnalogsSearchColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 2;
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs.Replace(
                "{CROSS_MATCH}",
                LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearch),
                StringComparison.Ordinal);
            AddParameter(command, "@article", normalized);
            AddParameter(command, "@limit", Math.Min(safeLimit * 8, 5000));
            var rows = new List<StorefrontCrossRefDigest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var selfBrandCompact = CompactStorefrontBrand(brandNorm);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && rows.Count < safeLimit)
            {
                var sourceBrand = Convert.ToString(reader["source_brand"] is DBNull ? string.Empty : reader["source_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
                var sourceArticle = Convert.ToString(reader["source_article"] is DBNull ? string.Empty : reader["source_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                var crossBrand = Convert.ToString(reader["cross_brand"] is DBNull ? string.Empty : reader["cross_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
                var crossArticle = Convert.ToString(reader["cross_article"] is DBNull ? string.Empty : reader["cross_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                var sourceNorm = PriceLookupRequest.NormalizeArticle(sourceArticle);
                var crossNorm = PriceLookupRequest.NormalizeArticle(crossArticle);
                string partnerBrand;
                string partnerArticle;
                if (sourceNorm == normalized && crossNorm != string.Empty)
                {
                    partnerBrand = crossBrand;
                    partnerArticle = crossArticle;
                }
                else if (crossNorm == normalized && sourceNorm != string.Empty)
                {
                    partnerBrand = sourceBrand;
                    partnerArticle = sourceArticle;
                }
                else
                {
                    continue;
                }

                var partnerNorm = PriceLookupRequest.NormalizeArticle(partnerArticle);
                // Skip the searched brand+article itself.
                if (partnerNorm == normalized
                    && (selfBrandCompact.Length == 0 || CompactStorefrontBrand(partnerBrand) == selfBrandCompact))
                {
                    continue;
                }

                var key = partnerBrand.Trim().ToUpperInvariant() + "|" + partnerNorm;
                if (!seen.Add(key))
                {
                    continue;
                }

                rows.Add(new StorefrontCrossRefDigest(partnerBrand.Trim(), partnerArticle.Trim(), false, "cp"));
            }

            var localCount = rows.Count;
            var crossbaseCount = 0;
            var source = "aspnet-cross-local";

            // PHP ajax_epc_cross_search merges crossbase.ru after local CP — opt-in so first paint stays fast.
            if (includeCrossbase && rows.Count < safeLimit)
            {
                var (crossbaseRefs, reported) = await CrossbaseReferenceLoader
                    .LoadAsync(normalized, safeLimit - rows.Count, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var xref in crossbaseRefs)
                {
                    var partnerNorm = PriceLookupRequest.NormalizeArticle(xref.Article);
                    var key = xref.Brand.Trim().ToUpperInvariant() + "|" + partnerNorm;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    rows.Add(xref);
                    crossbaseCount++;
                    if (rows.Count >= safeLimit)
                    {
                        break;
                    }
                }

                if (crossbaseCount > 0)
                {
                    source = "aspnet-cross-local+crossbase";
                }

                if (reported > crossbaseCount)
                {
                    // Prefer provider total when HTML reports more than we parsed into the window.
                    crossbaseCount = Math.Max(crossbaseCount, reported);
                }
            }

            // No N+1 stock probes here — that is what made PHP-shaped cross feel "asleep".
            return new(
                normalized,
                brandNorm,
                rows,
                [],
                localCount,
                crossbaseCount,
                rows.Count,
                source,
                string.Empty);
        }
        catch (Exception ex)
        {
            return new(normalized, brandNorm, [], [], 0, 0, 0, "database-error", ex.Message);
        }
    }

    /// <summary>
    /// When storefront Live/ErpOnly tenant resolved without a shop DB name, OpenAsync(null) hits the
    /// registry/platform schema (no shop_docpart_prices_data). Surface a clear hostname/www hint.
    /// ePartsCart recovers via Model C <c>docpart</c> (same as <see cref="RouteTenantResolver"/>).
    /// </summary>
    private bool TryGetUnboundTenantShopMessage(out string message)
    {
        message = string.Empty;
        // ePartsCart always recovers via shared Model C docpart (PHP portal parity) —
        // never surface the unbound migration gate on www.epartscart.com search digests.
        if (IsEpartsCartRequest())
        {
            return false;
        }

        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (tenant is null
            || tenant.Surface != TenantSurface.Storefront
            || tenant.Mode is not (TenantMode.LiveTenant or TenantMode.ErpOnlyTenant)
            || tenant.HasTenantDatabase)
        {
            return false;
        }

        message = "Tenant shop database is not bound for this host — check epc_portal_tenants.hostname (www alias).";
        return true;
    }

    /// <summary>
    /// Open tenant shop DB. When context has no db_name on ePartsCart hosts, force <c>docpart</c>
    /// so warehouse search / brands / bunches do not false-fail as unbound on www.
    /// </summary>
    private Task<DbConnection> OpenStorefrontShopAsync(CancellationToken cancellationToken)
        => OpenTenantShopAsync(cancellationToken);

    /// <summary>
    /// Open the tenant shop schema for CP/ERP/storefront digests.
    /// ePartsCart always uses shared Model C <c>docpart</c> with registry/base credentials
    /// (PHP <c>epc_portal_resolve_tenant_db</c>) — never platform registry tables and never
    /// portal <c>db_user</c> override (that override emptied every CP/ERP module on www).
    /// </summary>
    private Task<DbConnection> OpenTenantShopAsync(CancellationToken cancellationToken)
    {
        if (IsEpartsCartRequest())
        {
            return _connections.OpenAsync("docpart", cancellationToken);
        }

        return _connections.OpenAsync(null, cancellationToken);
    }

    private bool IsEpartsCartRequest()
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (tenant is not null && RouteTenantResolver.IsEpartsCartHost(tenant.Host, tenant.SiteKey))
        {
            return true;
        }

        var host = _httpContextAccessor?.HttpContext?.Request.Host.Host ?? string.Empty;
        return RouteTenantResolver.IsEpartsCartHost(host, siteKey: null);
    }

    private enum StorefrontArticleMatchMode
    {
        SimpleEquality,
        ArticleSearch,
        ExactTrim,
        ReplaceNormalize
    }

    private static string CompactStorefrontBrand(string brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return string.Empty;
        }

        return brand.Trim().ToUpperInvariant()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
    }

    private int? _priceArticleSearchColumnState; // null=unknown, 1=yes, -1=no
    private int? _analogsSearchColumnState;

    private async Task<bool> ProbePriceArticleSearchColumnAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (_priceArticleSearchColumnState is 1)
        {
            return true;
        }

        if (_priceArticleSearchColumnState is -1)
        {
            return false;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            _priceArticleSearchColumnState = 1;
            return true;
        }
        catch
        {
            _priceArticleSearchColumnState = -1;
            return false;
        }
    }

    private async Task<bool> ProbeAnalogsSearchColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (_analogsSearchColumnState is 1)
        {
            return true;
        }

        if (_analogsSearchColumnState is -1)
        {
            return false;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `article_search`, `analog_search` FROM `shop_docpart_articles_analogs_list` LIMIT 1";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            _analogsSearchColumnState = 1;
            return true;
        }
        catch
        {
            _analogsSearchColumnState = -1;
            return false;
        }
    }

    /// <summary>PHP <c>docpart_collect_article_candidates</c> — primary article + both sides of CP crosses (cap ~80).</summary>
    private async Task<List<string>> CollectStorefrontArticleCandidatesAsync(
        DbConnection connection,
        string normalizedArticle,
        CancellationToken cancellationToken)
    {
        var candidates = new List<string> { normalizedArticle };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedArticle };
        try
        {
            var hasAnalogsSearch = await ProbeAnalogsSearchColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs.Replace(
                "{CROSS_MATCH}",
                LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearch),
                StringComparison.Ordinal);
            AddParameter(command, "@article", normalizedArticle);
            AddParameter(command, "@limit", 80);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && candidates.Count < 80)
            {
                var sourceArticle = Convert.ToString(reader["source_article"] is DBNull ? string.Empty : reader["source_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                var crossArticle = Convert.ToString(reader["cross_article"] is DBNull ? string.Empty : reader["cross_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                AddCandidate(candidates, seen, PriceLookupRequest.NormalizeArticle(sourceArticle));
                AddCandidate(candidates, seen, PriceLookupRequest.NormalizeArticle(crossArticle));
            }
        }
        catch
        {
            // Cross expansion is best-effort; primary article still works.
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, HashSet<string> seen, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !seen.Add(value) || candidates.Count >= 80)
        {
            return;
        }

        candidates.Add(value);
    }

    private async Task<List<StorefrontPartOfferDigest>> QueryStorefrontPartOffersCascadeAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        string brandUpper,
        string brandCompact,
        int safeLimit,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        foreach (var mode in new[]
                 {
                     StorefrontArticleMatchMode.SimpleEquality,
                     StorefrontArticleMatchMode.ArticleSearch,
                     StorefrontArticleMatchMode.ExactTrim,
                     StorefrontArticleMatchMode.ReplaceNormalize
                 })
        {
            var rows = await QueryStorefrontPartOffersAsync(
                connection,
                candidates,
                brandUpper,
                brandCompact,
                safeLimit,
                mode,
                hasSearchCol,
                cancellationToken).ConfigureAwait(false);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        return [];
    }

    /// <summary>
    /// Brand+article / protocol-3 AJAX path: indexed <c>article_search</c> first.
    /// When the search column exists, skip SimpleEquality/ExactTrim miss cascades (they burn 200–600ms
    /// on wrong brands like JA ASHIKA). Fall back to light exacts only when article_search is absent.
    /// Never runs REPLACE()-normalize (PHP CHPU stock/prices path avoids that CPU spike).
    /// </summary>
    private async Task<List<StorefrontPartOfferDigest>> QueryStorefrontPartOffersBrandedFastAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        string brandUpper,
        string brandCompact,
        int safeLimit,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        IEnumerable<StorefrontArticleMatchMode> modes = hasSearchCol
            ? [StorefrontArticleMatchMode.ArticleSearch]
            :
            [
                StorefrontArticleMatchMode.SimpleEquality,
                StorefrontArticleMatchMode.ExactTrim
            ];

        foreach (var mode in modes)
        {
            var rows = await QueryStorefrontPartOffersAsync(
                connection,
                candidates,
                brandUpper,
                brandCompact,
                safeLimit,
                mode,
                hasSearchCol,
                cancellationToken).ConfigureAwait(false);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        return [];
    }

    /// <summary>
    /// Retry branded ArticleSearch for manufacturer synonym aliases (stops on first hit).
    /// </summary>
    private async Task<List<StorefrontPartOfferDigest>> QueryStorefrontPartOffersForBrandAliasesAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        string brandTrim,
        string brandCompact,
        IReadOnlySet<string> aliases,
        int safeLimit,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        var tried = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { brandTrim };
        var attempts = 0;
        foreach (var alias in aliases)
        {
            var a = (alias ?? string.Empty).Trim();
            if (a.Length == 0 || !tried.Add(a))
            {
                continue;
            }

            attempts++;
            if (attempts > 6)
            {
                break;
            }

            var rows = await QueryStorefrontPartOffersBrandedFastAsync(
                connection,
                candidates,
                a.ToUpperInvariant(),
                CompactStorefrontBrand(a),
                safeLimit,
                hasSearchCol,
                cancellationToken).ConfigureAwait(false);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        return [];
    }

    /// <summary>
    /// When the requested brand is a spoken/near miss (JA ASHIKA vs JS ASAKASHI), pick the best
    /// warehouse manufacturer for this article via compact / substring score — one brand list query.
    /// </summary>
    private async Task<string?> ResolveWarehouseBrandForArticleAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        string brandTrim,
        string brandCompact,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        if (brandCompact.Length < 3)
        {
            return null;
        }

        var brands = await QueryStorefrontWarehouseBrandsAsync(
            connection,
            candidates,
            40,
            hasSearchCol ? StorefrontArticleMatchMode.ArticleSearch : StorefrontArticleMatchMode.SimpleEquality,
            hasSearchCol,
            cancellationToken).ConfigureAwait(false);
        if (brands.Count == 0)
        {
            return null;
        }

        string? best = null;
        var bestScore = 0;
        StorefrontArticleBrandDigest? dominantStocked = null;
        foreach (var b in brands)
        {
            var name = (b.Brand ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (b.Exist > 0
                && (dominantStocked is null || b.Exist > dominantStocked.Exist))
            {
                dominantStocked = b;
            }

            var compact = CompactStorefrontBrand(name);
            var score = ScoreWarehouseBrandMatch(brandCompact, compact, b.Exist);
            if (score > bestScore)
            {
                bestScore = score;
                best = name;
            }
        }

        // Spoken miss (JA ASHIKA → JS ASAKASHI): LCS is often 4 ("ASHI"). Accept ≥4.
        if (bestScore >= 180)
        {
            return best;
        }

        // Single dominant in-stock manufacturer for this article — accept when request shares a stem.
        if (dominantStocked is not null
            && dominantStocked.Exist > 0
            && brands.Count(static x => x.Exist > 0) == 1)
        {
            var domName = (dominantStocked.Brand ?? string.Empty).Trim();
            if (domName.Length > 0)
            {
                var domCompact = CompactStorefrontBrand(domName);
                if (ScoreWarehouseBrandMatch(brandCompact, domCompact, dominantStocked.Exist) >= 100)
                {
                    return domName;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Score spoken / near-miss brands. JAASHIKA vs JSASAKASHI shares "ASHI" (len 4).
    /// </summary>
    private static int ScoreWarehouseBrandMatch(string brandCompact, string warehouseCompact, int exist)
    {
        if (warehouseCompact.Length == 0 || brandCompact.Length == 0)
        {
            return 0;
        }

        if (warehouseCompact == brandCompact)
        {
            return 1000 + exist;
        }

        if (warehouseCompact.Contains(brandCompact, StringComparison.Ordinal)
            || brandCompact.Contains(warehouseCompact, StringComparison.Ordinal))
        {
            return 500 + Math.Min(warehouseCompact.Length, brandCompact.Length) + Math.Min(exist, 100);
        }

        // Drop 2-letter prefix (JA/JS/OE…) and compare stems (ASHIKA vs ASAKASHI).
        var reqStem = brandCompact.Length > 4 ? brandCompact[2..] : brandCompact;
        var whStem = warehouseCompact.Length > 4 ? warehouseCompact[2..] : warehouseCompact;
        if (reqStem.Length >= 4
            && (whStem.Contains(reqStem, StringComparison.Ordinal)
                || reqStem.Contains(whStem, StringComparison.Ordinal)
                || warehouseCompact.Contains(reqStem, StringComparison.Ordinal)
                || brandCompact.Contains(whStem, StringComparison.Ordinal)))
        {
            return 300 + Math.Min(reqStem.Length, whStem.Length) + Math.Min(exist, 80);
        }

        var shared = LongestCommonSubstringLength(warehouseCompact, brandCompact);
        if (shared >= 4)
        {
            return 180 + shared + Math.Min(exist, 50);
        }

        return 0;
    }

    private static int LongestCommonSubstringLength(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return 0;
        }

        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        var max = 0;
        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    cur[j] = prev[j - 1] + 1;
                    if (cur[j] > max)
                    {
                        max = cur[j];
                    }
                }
                else
                {
                    cur[j] = 0;
                }
            }

            (prev, cur) = (cur, prev);
            Array.Clear(cur);
        }

        return max;
    }

    private static bool ManufacturerMatchesBrand(
        string manufacturer,
        string brandTrim,
        string brandCompact,
        IReadOnlySet<string> aliases)
    {
        var mfr = (manufacturer ?? string.Empty).Trim();
        if (mfr.Length == 0 || brandTrim.Length == 0)
        {
            return false;
        }

        if (aliases.Contains(mfr))
        {
            return true;
        }

        if (string.Equals(mfr, brandTrim, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var compact = CompactStorefrontBrand(mfr);
        return brandCompact.Length > 0 && compact == brandCompact;
    }

    private async Task<HashSet<string>> LoadManufacturerBrandAliasesAsync(
        DbConnection connection,
        string brand,
        CancellationToken cancellationToken)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            brand.Trim(),
            brand.Trim().ToUpperInvariant()
        };
        if (string.IsNullOrWhiteSpace(brand))
        {
            return aliases;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TRIM(m.`name`) AS name
                FROM `shop_docpart_manufacturers` m
                WHERE UPPER(TRIM(m.`name`)) = @brand
                UNION
                SELECT TRIM(s.`synonym`) AS name
                FROM `shop_docpart_manufacturers_synonyms` s
                INNER JOIN `shop_docpart_manufacturers` m ON m.`id` = s.`manufacturer_id`
                WHERE UPPER(TRIM(m.`name`)) = @brand OR UPPER(TRIM(s.`synonym`)) = @brand
                UNION
                SELECT TRIM(m2.`name`) AS name
                FROM `shop_docpart_manufacturers_synonyms` s2
                INNER JOIN `shop_docpart_manufacturers` m2 ON m2.`id` = s2.`manufacturer_id`
                WHERE UPPER(TRIM(s2.`synonym`)) = @brand
                LIMIT 80
                """;
            AddParameter(command, "@brand", brand.Trim().ToUpperInvariant());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    aliases.Add(name.Trim());
                }
            }
        }
        catch
        {
            // Synonym tables are optional on some tenants.
        }

        return aliases;
    }

    private static string ResolveStorefrontArticleMatchSql(
        IReadOnlyList<string> candidates,
        StorefrontArticleMatchMode mode,
        bool hasSearchCol,
        out bool bindIndexed)
    {
        bindIndexed = mode is not StorefrontArticleMatchMode.SimpleEquality;
        return mode switch
        {
            StorefrontArticleMatchMode.SimpleEquality
                => LegacySurfaceDashboardSql.StorefrontPriceArticleSimpleEqualitySql,
            StorefrontArticleMatchMode.ArticleSearch when hasSearchCol
                => LegacySurfaceDashboardSql.StorefrontPriceArticleSearchInSql(candidates.Count),
            StorefrontArticleMatchMode.ArticleSearch
                => LegacySurfaceDashboardSql.StorefrontPriceArticleExactInSql(candidates.Count),
            StorefrontArticleMatchMode.ExactTrim
                => LegacySurfaceDashboardSql.StorefrontPriceArticleExactInSql(candidates.Count),
            _
                => LegacySurfaceDashboardSql.StorefrontPriceArticleReplaceInSql(candidates.Count)
        };
    }

    private static async Task<List<StorefrontPartOfferDigest>> QueryStorefrontPartOffersAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        string brandUpper,
        string brandCompact,
        int safeLimit,
        StorefrontArticleMatchMode mode,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        if (mode == StorefrontArticleMatchMode.ArticleSearch && !hasSearchCol)
        {
            return [];
        }

        var articleMatch = ResolveStorefrontArticleMatchSql(candidates, mode, hasSearchCol, out var bindIndexed);
        await using var command = connection.CreateCommand();
        // Protocol-3 / CHPU first paint budget is 1–3s — never wait on the 30s pool default.
        command.CommandTimeout = 2;
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontPartSearch.Replace(
            "{ARTICLE_MATCH}",
            articleMatch,
            StringComparison.Ordinal);
        if (bindIndexed)
        {
            BindArticleCandidates(command, candidates);
        }

        AddParameter(command, "@article", candidates[0]);
        AddParameter(command, "@brand", brandUpper);
        AddParameter(command, "@brandCompact", brandCompact);
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
                Convert.ToString(reader["storage"] is DBNull ? string.Empty : reader["storage"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["time_to_exe"] is DBNull ? string.Empty : reader["time_to_exe"], CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return rows;
    }

    private static async Task<List<StorefrontArticleBrandDigest>> QueryStorefrontWarehouseBrandsAsync(
        DbConnection connection,
        IReadOnlyList<string> candidates,
        int safeLimit,
        StorefrontArticleMatchMode mode,
        bool hasSearchCol,
        CancellationToken cancellationToken)
    {
        if (mode == StorefrontArticleMatchMode.ArticleSearch && !hasSearchCol)
        {
            return [];
        }

        var articleMatch = ResolveStorefrontArticleMatchSql(candidates, mode, hasSearchCol, out var bindIndexed);
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands.Replace(
            "{ARTICLE_MATCH}",
            articleMatch,
            StringComparison.Ordinal);
        if (bindIndexed)
        {
            BindArticleCandidates(command, candidates);
        }

        AddParameter(command, "@article", candidates[0]);
        AddParameter(command, "@limit", safeLimit);

        var brands = new List<StorefrontArticleBrandDigest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = Convert.ToString(reader["brand_name"] is DBNull ? string.Empty : reader["brand_name"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var partName = Convert.ToString(reader["part_name"] is DBNull ? string.Empty : reader["part_name"], CultureInfo.InvariantCulture) ?? string.Empty;
            var exist = Convert.ToInt32(reader["exist_sum"] is DBNull ? 0 : reader["exist_sum"], CultureInfo.InvariantCulture);
            decimal? minPrice = null;
            if (reader["min_price"] is not DBNull and not null)
            {
                minPrice = Convert.ToDecimal(reader["min_price"], CultureInfo.InvariantCulture);
            }

            var warehouse = Convert.ToString(reader["warehouse"] is DBNull ? string.Empty : reader["warehouse"], CultureInfo.InvariantCulture) ?? string.Empty;
            brands.Add(new StorefrontArticleBrandDigest(
                name.Trim(),
                partName.Trim(),
                exist,
                minPrice,
                warehouse.Trim()));
        }

        return brands;
    }

    private static async Task MergeCpCrossBrandsAsync(
        DbConnection connection,
        string normalizedArticle,
        Dictionary<string, StorefrontArticleBrandDigest> byBrand,
        bool hasAnalogsSearch,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs.Replace(
            "{CROSS_MATCH}",
            LegacySurfaceDashboardSql.StorefrontCrossArticleMatchSql(hasAnalogsSearch),
            StringComparison.Ordinal);
        AddParameter(command, "@article", normalizedArticle);
        AddParameter(command, "@limit", 3000);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sourceBrand = Convert.ToString(reader["source_brand"] is DBNull ? string.Empty : reader["source_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
            var sourceArticle = Convert.ToString(reader["source_article"] is DBNull ? string.Empty : reader["source_article"], CultureInfo.InvariantCulture) ?? string.Empty;
            var crossBrand = Convert.ToString(reader["cross_brand"] is DBNull ? string.Empty : reader["cross_brand"], CultureInfo.InvariantCulture) ?? string.Empty;
            var crossArticle = Convert.ToString(reader["cross_article"] is DBNull ? string.Empty : reader["cross_article"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (PriceLookupRequest.NormalizeArticle(sourceArticle) == normalizedArticle)
            {
                AddCrossBrand(byBrand, sourceBrand);
            }

            if (PriceLookupRequest.NormalizeArticle(crossArticle) == normalizedArticle)
            {
                AddCrossBrand(byBrand, crossBrand);
            }
        }
    }

    private static void AddCrossBrand(Dictionary<string, StorefrontArticleBrandDigest> byBrand, string manufacturer)
    {
        var mfr = (manufacturer ?? string.Empty).Trim();
        if (mfr.Length == 0)
        {
            return;
        }

        var key = mfr.ToUpperInvariant();
        if (!byBrand.ContainsKey(key))
        {
            byBrand[key] = new StorefrontArticleBrandDigest(mfr);
        }
    }

    private static void BindArticleCandidates(DbCommand command, IReadOnlyList<string> candidates)
    {
        // Exact/Replace SQL use distinct @aN / @bN / @cN prefixes for each IN clause copy.
        foreach (var prefix in new[] { "a", "b", "c" })
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                AddParameter(command, "@" + prefix + i.ToString(CultureInfo.InvariantCulture), candidates[i]);
            }
        }
    }

    private static async Task EnrichStorefrontCrossStockAsync(
        DbConnection connection,
        List<StorefrontCrossRefDigest> rows,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (string.IsNullOrWhiteSpace(row.Article))
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            // Prefer light exact match for stock peek (avoids heavy REPLACE scans).
            var articleMatch = LegacySurfaceDashboardSql.StorefrontPriceArticleExactInSql(1);
            command.CommandText = """
                SELECT IFNULL(MAX(d.`exist`), 0) AS max_exist
                FROM `shop_docpart_prices_data` d
                WHERE {ARTICLE_MATCH}
                  AND (@brand = '' OR UPPER(TRIM(d.`manufacturer`)) = @brand
                       OR REPLACE(REPLACE(REPLACE(UPPER(TRIM(d.`manufacturer`)), ' ', ''), '-', ''), '.', '') = @brandCompact)
                """.Replace("{ARTICLE_MATCH}", articleMatch, StringComparison.Ordinal);
            var articleNorm = PriceLookupRequest.NormalizeArticle(row.Article);
            var brandUpper = row.Brand.Trim().ToUpperInvariant();
            BindArticleCandidates(command, [articleNorm]);
            AddParameter(command, "@article", articleNorm);
            AddParameter(command, "@brand", brandUpper);
            AddParameter(command, "@brandCompact", CompactStorefrontBrand(row.Brand));
            var maxExist = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var inStock = Convert.ToInt32(maxExist is DBNull ? 0 : maxExist, CultureInfo.InvariantCulture) > 0;
            if (inStock)
            {
                rows[i] = row with { InStock = true };
            }
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
                    Convert.ToDecimal(reader["min_order"] is DBNull ? 0m : reader["min_order"], CultureInfo.InvariantCulture),
                    Convert.ToDecimal(reader["t2_exist"] is DBNull ? 0m : reader["t2_exist"], CultureInfo.InvariantCulture)));
            }

            var summary = new StorefrontCartSummary(count, sum, "database", string.Empty);
            return new(userId, summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, new(0, 0m, "database-error", ex.Message), [], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontQuoteListResult> ListStorefrontQuotesAsync(int userId, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        if (userId <= 0)
        {
            return new(0, [], 0, "rejected", "Valid customer user id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(userId, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCustomerQuotes;
            AddParameter(command, "@userId", userId);
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<StorefrontQuoteDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontQuoteDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0L : reader["time_created"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_updated"] is DBNull ? 0L : reader["time_updated"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture)));
            }

            return new(userId, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontQuoteDetailDigest?> GetStorefrontQuoteAsync(int userId, int quoteId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || quoteId <= 0)
        {
            return null;
        }

        if (!_connections.IsConfigured)
        {
            return new(quoteId, string.Empty, [], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            string status;
            await using (var header = connection.CreateCommand())
            {
                header.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCustomerQuoteHeader;
                AddParameter(header, "@quoteId", quoteId);
                AddParameter(header, "@userId", userId);
                await using var headerReader = await header.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await headerReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                status = Convert.ToString(headerReader["status"] is DBNull ? string.Empty : headerReader["status"], CultureInfo.InvariantCulture) ?? string.Empty;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCustomerQuoteItems;
            AddParameter(command, "@quoteId", quoteId);
            var items = new List<StorefrontQuoteItemDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var json = Convert.ToString(reader["product_object_json"] is DBNull ? string.Empty : reader["product_object_json"], CultureInfo.InvariantCulture) ?? string.Empty;
                var (mfr, article, name, priceFromJson) = ParseQuoteProductObject(json);
                var useAlt = Convert.ToInt32(reader["offer_alternative"] is DBNull ? 0 : reader["offer_alternative"], CultureInfo.InvariantCulture) == 1;
                if (useAlt)
                {
                    var altMfr = Convert.ToString(reader["alt_manufacturer"] is DBNull ? string.Empty : reader["alt_manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var altArticle = Convert.ToString(reader["alt_article"] is DBNull ? string.Empty : reader["alt_article"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var altName = Convert.ToString(reader["alt_name"] is DBNull ? string.Empty : reader["alt_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(altMfr) && !string.IsNullOrWhiteSpace(altArticle))
                    {
                        mfr = altMfr;
                        article = altArticle;
                        name = altName;
                    }
                }

                var qty = useAlt
                    ? Convert.ToDecimal(reader["alt_count_need"] is DBNull ? 0m : reader["alt_count_need"], CultureInfo.InvariantCulture)
                    : Convert.ToDecimal(reader["count_need"] is DBNull ? 0m : reader["count_need"], CultureInfo.InvariantCulture);
                if (qty <= 0)
                {
                    qty = Convert.ToDecimal(reader["count_need"] is DBNull ? 1m : reader["count_need"], CultureInfo.InvariantCulture);
                }

                var price = useAlt
                    ? Convert.ToDecimal(reader["alt_quoted_price"] is DBNull ? 0m : reader["alt_quoted_price"], CultureInfo.InvariantCulture)
                    : Convert.ToDecimal(reader["quoted_price"] is DBNull ? 0m : reader["quoted_price"], CultureInfo.InvariantCulture);
                if (price <= 0 && priceFromJson > 0)
                {
                    price = priceFromJson;
                }

                items.Add(new StorefrontQuoteItemDigest(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    mfr,
                    article,
                    name,
                    qty,
                    price));
            }

            return new(quoteId, status, items, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(quoteId, string.Empty, [], "database-error", ex.Message);
        }
    }

    public async Task<StorefrontProductResult> GetStorefrontProductAsync(int productId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0)
        {
            return new(null, "empty", "Product id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(null, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontProductById;
            AddParameter(command, "@productId", productId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new(null, "database", "Product not found.");
            }

            var product = ReadStorefrontProduct(reader);
            var profileId = 0;
            try
            {
                profileId = Convert.ToInt32(
                    reader["sku_profile_id"] is DBNull ? 0 : reader["sku_profile_id"],
                    CultureInfo.InvariantCulture);
            }
            catch (IndexOutOfRangeException)
            {
                profileId = 0;
            }

            // Close product reader before issuing follow-up image/spec queries on the same connection.
            await reader.DisposeAsync().ConfigureAwait(false);

            var images = await LoadStorefrontProductImagesAsync(connection, productId, profileId, cancellationToken)
                .ConfigureAwait(false);
            var specs = profileId > 0
                ? await LoadStorefrontSkuSpecsAsync(connection, profileId, cancellationToken).ConfigureAwait(false)
                : [];
            product = product with { Images = images, Specs = specs };
            return new(product, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(null, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontProductListResult> ListStorefrontProductsByIdsAsync(IReadOnlyList<int> productIds, CancellationToken cancellationToken = default)
    {
        var ids = (productIds ?? [])
            .Where(id => id > 0)
            .Distinct()
            .Take(80)
            .ToList();
        if (ids.Count == 0)
        {
            return new([], 0, "empty", "No product ids.");
        }

        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            var placeholders = new string[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                placeholders[i] = "@p" + i.ToString(CultureInfo.InvariantCulture);
                AddParameter(command, placeholders[i], ids[i]);
            }

            var idList = string.Join(",", placeholders);
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontProductsByIds
                .Replace("{IDS}", idList, StringComparison.Ordinal);
            var rows = new List<StorefrontProductDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadStorefrontProduct(reader));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontCatalogueTreeResult> ListStorefrontCatalogueTreeAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var flat = await ReadCatalogueCategoryRowsAsync(
                connection,
                LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategoriesTranslated,
                cancellationToken).ConfigureAwait(false);
            if (flat.Count == 0)
            {
                // Lang table missing / empty — still humanize aliases (never show bare "1324").
                flat = await ReadCatalogueCategoryRowsAsync(
                    connection,
                    LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategories,
                    cancellationToken).ConfigureAwait(false);
            }

            var tree = StorefrontOwnCatalogueTreeBuilder.Build(flat, filterApai: true);
            return new(tree, CountTreeNodes(tree), "database", string.Empty);
        }
        catch (Exception ex)
        {
            try
            {
                await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
                var flat = await ReadCatalogueCategoryRowsAsync(
                    connection,
                    LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategories,
                    cancellationToken).ConfigureAwait(false);
                var tree = StorefrontOwnCatalogueTreeBuilder.Build(flat, filterApai: true);
                return new(tree, CountTreeNodes(tree), "database", ex.Message);
            }
            catch (Exception fallbackEx)
            {
                return new([], 0, "database-error", fallbackEx.Message);
            }
        }
    }

    private static async Task<List<StorefrontCatalogueCategoryRow>> ReadCatalogueCategoryRowsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var flat = new List<StorefrontCatalogueCategoryRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = Convert.ToInt32(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture);
            var alias = Convert.ToString(reader["alias"] is DBNull ? string.Empty : reader["alias"], CultureInfo.InvariantCulture) ?? string.Empty;
            var url = Convert.ToString(reader["url"] is DBNull ? string.Empty : reader["url"], CultureInfo.InvariantCulture) ?? string.Empty;
            var translated = ReadOptionalString(reader, "value_translated");
            var valueRaw = Convert.ToString(reader["value_lang_id"] is DBNull ? string.Empty : reader["value_lang_id"], CultureInfo.InvariantCulture) ?? string.Empty;
            var value = StorefrontOwnCatalogueTreeBuilder.LabelFor(
                alias,
                string.IsNullOrWhiteSpace(translated) ? valueRaw : translated,
                id);
            flat.Add(new StorefrontCatalogueCategoryRow(
                id,
                alias,
                url,
                Convert.ToInt32(reader["parent"] is DBNull ? 0 : reader["parent"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["level"] is DBNull ? 0 : reader["level"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["child_count"] is DBNull ? 0 : reader["child_count"], CultureInfo.InvariantCulture),
                Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                Convert.ToString(reader["image"] is DBNull ? string.Empty : reader["image"], CultureInfo.InvariantCulture) ?? string.Empty,
                value));
        }

        return flat;
    }

    private static string ReadOptionalString(DbDataReader reader, string column)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (!string.Equals(reader.GetName(i), column, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (reader.IsDBNull(i))
            {
                return string.Empty;
            }

            return Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task<StorefrontCatalogueProductsResult> ListStorefrontCatalogueProductsAsync(
        int categoryId,
        string? categoryUrl,
        string? searchString,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var search = (searchString ?? string.Empty).Trim().ToLowerInvariant();
        if (search.Length > 80)
        {
            search = search[..80];
        }

        limit = Math.Clamp(limit <= 0 ? 48 : limit, 1, 200);
        var url = (categoryUrl ?? string.Empty).Trim().TrimStart('/');
        var categoryValue = string.Empty;

        if (!_connections.IsConfigured)
        {
            return new(categoryId, url, categoryValue, search, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);

            if (categoryId <= 0 && url.Length > 0)
            {
                var cats = await ReadCatalogueCategoryRowsAsync(
                    connection,
                    LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategories,
                    cancellationToken).ConfigureAwait(false);
                foreach (var cat in cats)
                {
                    if (!string.Equals(cat.Url.Trim().TrimStart('/'), url, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    categoryId = cat.Id;
                    categoryValue = StorefrontOwnCatalogueTreeBuilder.LabelFor(cat.Alias, cat.Value, cat.Id);
                    break;
                }
            }
            else if (categoryId > 0)
            {
                var cats = await ReadCatalogueCategoryRowsAsync(
                    connection,
                    LegacySurfaceDashboardSql.SelectStorefrontCatalogueCategories,
                    cancellationToken).ConfigureAwait(false);
                foreach (var cat in cats)
                {
                    if (cat.Id != categoryId)
                    {
                        continue;
                    }

                    url = string.IsNullOrWhiteSpace(cat.Url) ? url : cat.Url;
                    categoryValue = StorefrontOwnCatalogueTreeBuilder.LabelFor(cat.Alias, cat.Value, cat.Id);
                    break;
                }
            }

            await using var command = connection.CreateCommand();
            if (categoryId > 0)
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCatalogueProductsByCategory;
                AddParameter(command, "@categoryId", categoryId);
                AddParameter(command, "@search", search);
                AddParameter(command, "@limit", limit);
            }
            else if (search.Length > 0)
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontCatalogueProductsByName;
                AddParameter(command, "@search", search);
                AddParameter(command, "@limit", limit);
            }
            else
            {
                return new(0, url, categoryValue, search, [], 0, "empty", "Pick a category or enter a name search.");
            }

            var rows = new List<StorefrontProductDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(ReadStorefrontProduct(reader));
            }

            return new(categoryId, url, categoryValue, search, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(categoryId, url, categoryValue, search, [], 0, "database-error", ex.Message);
        }
    }

    private static int CountTreeNodes(IReadOnlyList<StorefrontCatalogueCategoryNode> tree)
    {
        var n = 0;
        foreach (var node in tree)
        {
            n += 1 + CountTreeNodes(node.Data);
        }

        return n;
    }

    public async Task<StorefrontGenuineBrandsResult> ListStorefrontGenuineBrandsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            // UMAPI manufacturer cache lives in the shop DB (docpart on ePartsCart).
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontGenuineManufacturerNames;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    AddGenuineBrandKeys(keys, name);
                }
            }

            try
            {
                await using var synonymCmd = connection.CreateCommand();
                synonymCmd.CommandText = LegacySurfaceDashboardSql.SelectStorefrontManufacturerSynonyms;
                await using var synonymReader = await synonymCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await synonymReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var name = Convert.ToString(synonymReader["name"] is DBNull ? string.Empty : synonymReader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var synonym = Convert.ToString(synonymReader["synonym"] is DBNull ? string.Empty : synonymReader["synonym"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (keys.Contains(NormalizeGenuineBrandKey(name)))
                    {
                        AddGenuineBrandKeys(keys, synonym);
                    }
                }
            }
            catch
            {
                // Synonym tables optional on some tenants.
            }

            var list = keys.Where(k => k.Length > 0).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
            return new(list, list.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<StorefrontOfficeStorageBunchesResult> ListStorefrontOfficeStorageBunchesAsync(
        string article,
        string? brand,
        CancellationToken cancellationToken = default)
    {
        var normalized = PriceLookupRequest.NormalizeArticle(article ?? string.Empty);
        var brandTrim = (brand ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new(string.Empty, brandTrim, [], 0, "empty", "Article required.");
        }

        var unbound = TryGetUnboundTenantShopMessage(out _);
        if (_connections.IsConfigured && !unbound)
        {
            try
            {
                await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
                var bunches = await LoadOfficeStorageBunchesFromSqlAsync(connection, cancellationToken)
                    .ConfigureAwait(false);
                if (bunches.Count > 0)
                {
                    return new(normalized, brandTrim, bunches, bunches.Count, "database", string.Empty);
                }
            }
            catch
            {
                // ASP.NET-only: do not fall through to PHP bunches twin.
            }
        }

        if (AllowPhpWarehouseBridge && _phpWarehouseBridge is not null)
        {
            var phpBunches = await _phpWarehouseBridge
                .TryLoadBunchesAsync(normalized, brandTrim, cancellationToken)
                .ConfigureAwait(false);
            if (phpBunches.Count > 0)
            {
                return new(normalized, brandTrim, phpBunches, phpBunches.Count, "php-chpu", string.Empty);
            }
        }

        if (unbound)
        {
            return new(normalized, brandTrim, [], 0, "migration",
                "Tenant shop database is not bound for this host — check epc_portal_tenants.hostname (www alias).");
        }

        return new(normalized, brandTrim, [], 0, "database", string.Empty);
    }

    private async Task<List<StorefrontOfficeStorageBunchDigest>> LoadOfficeStorageBunchesFromSqlAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontOfficeStorageBunches;
        var apiBunches = new List<StorefrontOfficeStorageBunchDigest>();
        var priceNested = new List<StorefrontOfficeStorageBunchDigest>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var officeId = Convert.ToInt32(reader["office_id"] is DBNull ? 0 : reader["office_id"], CultureInfo.InvariantCulture);
                var storageId = Convert.ToInt32(reader["storage_id"] is DBNull ? 0 : reader["storage_id"], CultureInfo.InvariantCulture);
                var handler = Convert.ToString(reader["handler_folder"] is DBNull ? string.Empty : reader["handler_folder"], CultureInfo.InvariantCulture) ?? string.Empty;
                var isPrices = string.Equals(handler, "prices", StringComparison.OrdinalIgnoreCase);
                var isTreelax = string.Equals(handler, "treelax_catalogue", StringComparison.OrdinalIgnoreCase);
                if (isPrices)
                {
                    priceNested.Add(new(officeId, storageId, 1, handler, false));
                }
                else
                {
                    apiBunches.Add(new(officeId, storageId, isTreelax ? 1 : 2, handler, isTreelax));
                }
            }
        }

        // PHP part_search: when office maps omit price warehouses, still poll all active price lists.
        if (priceNested.Count == 0)
        {
            await using var fallback = connection.CreateCommand();
            fallback.CommandText = LegacySurfaceDashboardSql.SelectStorefrontPriceStorageFallback;
            await using var fallbackReader = await fallback.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await fallbackReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var officeId = Convert.ToInt32(fallbackReader["office_id"] is DBNull ? 1 : fallbackReader["office_id"], CultureInfo.InvariantCulture);
                var storageId = Convert.ToInt32(fallbackReader["storage_id"] is DBNull ? 0 : fallbackReader["storage_id"], CultureInfo.InvariantCulture);
                priceNested.Add(new(officeId, storageId, 1, "prices", false));
            }
        }

        var bunches = new List<StorefrontOfficeStorageBunchDigest>();
        if (priceNested.Count > 0)
        {
            bunches.Add(new(0, 0, 3, "prices", false, priceNested));
        }

        bunches.AddRange(apiBunches);
        return bunches;
    }

    public async Task<StorefrontProductsOfBunchResult> PollStorefrontProductsOfBunchAsync(
        string article,
        string? brand,
        int officeId,
        int storageId,
        string? queryJson,
        int geoId = 0,
        CancellationToken cancellationToken = default)
    {
        // Protocol-3 price aggregate (office_id=0, storage_id=0): ASP.NET SQL under a hard 2.5s budget.
        // No product .php twin by default (PHP deletion-ready).
        if (officeId == 0 && storageId == 0)
        {
            using var budgetCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budgetCts.CancelAfter(TimeSpan.FromMilliseconds(2500));

            // Cap at 80 for first paint — full 200-row ORDER BY is slower and not needed for CHPU shell.
            var searchTask = SearchStorefrontPartsAsync(article, brand, 80, budgetCts.Token);
            Task<StorefrontProductsOfBunchResult>? phpTask =
                AllowPhpWarehouseBridge && _phpWarehouseBridge is not null
                    ? _phpWarehouseBridge.TryLoadProductsOfBunchAsync(
                        article, brand, officeId, storageId, queryJson, geoId, budgetCts.Token, timeoutSeconds: 2)
                    : null;

            StorefrontPartSearchResult? search = null;
            try
            {
                search = await searchTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // budget elapsed
            }

            if (search is not null
                && search.Rows.Count > 0
                && (string.Equals(search.Source, "database", StringComparison.Ordinal)
                    || string.Equals(search.Source, "php-chpu", StringComparison.Ordinal))
                && search.Rows.Any(static r => r.Price > 0m || r.Exist > 0))
            {
                var source = string.Equals(search.Source, "database", StringComparison.Ordinal)
                    ? "aspnet-warehouse"
                    : "php-chpu";
                // PricesVisible left true here; StorefrontModule applies guest/wholesale gate.
                return new(1, 0, 0, search.Rows, true, source, string.Empty);
            }

            if (phpTask is not null)
            {
                try
                {
                    var php = await phpTask.ConfigureAwait(false);
                    if (php.Products.Count > 0)
                    {
                        return php;
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // budget elapsed
                }
            }

            // Catalog-only SQL rows (price 0) still beat an empty table for first paint.
            if (search is not null && search.Rows.Count > 0)
            {
                var source = string.Equals(search.Source, "database", StringComparison.Ordinal)
                    ? "aspnet-warehouse"
                    : search.Source;
                return new(1, 0, 0, search.Rows, true, source, string.Empty);
            }

            return new(0, 0, 0, [], false, "aspnet-warehouse-empty",
                "No ASP.NET warehouse offers for this brand/article yet.");
        }

        // Nested API-supplier bunches: ASP.NET has no native handlers yet — return empty unless
        // emergency PHP bridge is explicitly re-enabled.
        if (!AllowPhpWarehouseBridge || _phpWarehouseBridge is null)
        {
            return new(0, officeId, storageId, [], false, "aspnet-only",
                "Nested supplier bunches await native ASP.NET handlers (PHP product URLs disabled).");
        }

        return await _phpWarehouseBridge
            .TryLoadProductsOfBunchAsync(article, brand, officeId, storageId, queryJson, geoId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StorefrontBulkUploadHistoryResult> ListStorefrontBulkUploadHistoryAsync(
        int userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return new(0, [], 0, "empty", "Customer session required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(userId, [], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontBulkUploadHistory;
            AddParameter(command, "@userId", userId);
            AddParameter(command, "@limit", Math.Clamp(limit, 1, 50));
            var rows = new List<StorefrontBulkUploadHistoryDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["file_name"] is DBNull ? string.Empty : reader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["uploaded_count"] is DBNull ? 0 : reader["uploaded_count"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["available_count"] is DBNull ? 0 : reader["available_count"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["cross_count"] is DBNull ? 0 : reader["cross_count"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["short_count"] is DBNull ? 0 : reader["short_count"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["notfound_count"] is DBNull ? 0 : reader["notfound_count"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["updated_at"] is DBNull ? string.Empty : reader["updated_at"], CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return new(userId, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(userId, [], 0, "database-error", ex.Message);
        }
    }

    private static StorefrontProductDigest ReadStorefrontProduct(DbDataReader reader)
    {
        string Col(string name)
        {
            try
            {
                var ordinal = reader.GetOrdinal(name);
                return reader.IsDBNull(ordinal)
                    ? string.Empty
                    : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch (IndexOutOfRangeException)
            {
                return string.Empty;
            }
        }

        return new(
            Convert.ToInt32(reader["id"], CultureInfo.InvariantCulture),
            Col("caption"),
            Col("alias"),
            Convert.ToInt32(reader["category_id"] is DBNull ? 0 : reader["category_id"], CultureInfo.InvariantCulture),
            Col("manufacturer"),
            Col("article"),
            Convert.ToInt32(reader["published"] is DBNull ? 0 : reader["published"], CultureInfo.InvariantCulture) != 0,
            Col("description"));
    }

    private static async Task<IReadOnlyList<StorefrontProductImageDigest>> LoadStorefrontProductImagesAsync(
        DbConnection connection,
        int productId,
        int profileId,
        CancellationToken cancellationToken)
    {
        var images = new List<StorefrontProductImageDigest>();
        if (profileId > 0)
        {
            try
            {
                await using var skuCmd = connection.CreateCommand();
                skuCmd.CommandText = LegacySurfaceDashboardSql.SelectStorefrontSkuPhotos;
                AddParameter(skuCmd, "@profileId", profileId);
                await using var skuReader = await skuCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await skuReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var file = Convert.ToString(skuReader["file_name"] is DBNull ? string.Empty : skuReader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (file.Length == 0)
                    {
                        continue;
                    }

                    images.Add(new(
                        Convert.ToInt32(skuReader["id"], CultureInfo.InvariantCulture),
                        "/content/files/images/sku_media/" + file.TrimStart('/'),
                        Convert.ToString(skuReader["alt"] is DBNull ? string.Empty : skuReader["alt"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(skuReader["is_primary"] is DBNull ? 0 : skuReader["is_primary"], CultureInfo.InvariantCulture) != 0));
                }
            }
            catch
            {
                // SKU media tables optional.
            }
        }

        if (images.Count == 0)
        {
            try
            {
                await using var imgCmd = connection.CreateCommand();
                imgCmd.CommandText = LegacySurfaceDashboardSql.SelectStorefrontProductImages;
                AddParameter(imgCmd, "@productId", productId);
                await using var imgReader = await imgCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await imgReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var file = Convert.ToString(imgReader["file_name"] is DBNull ? string.Empty : imgReader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (file.Length == 0)
                    {
                        continue;
                    }

                    var url = file.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
                        ? file
                        : "/content/files/images/products_images/" + file.TrimStart('/');
                    images.Add(new(
                        Convert.ToInt32(imgReader["id"], CultureInfo.InvariantCulture),
                        url,
                        string.Empty,
                        images.Count == 0));
                }
            }
            catch
            {
                // Legacy gallery optional.
            }
        }

        return images;
    }

    private static async Task<IReadOnlyList<StorefrontProductSpecDigest>> LoadStorefrontSkuSpecsAsync(
        DbConnection connection,
        int profileId,
        CancellationToken cancellationToken)
    {
        var specs = new List<StorefrontProductSpecDigest>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontSkuSpecs;
            AddParameter(command, "@profileId", profileId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var label = Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty;
                var value = Convert.ToString(reader["value"] is DBNull ? string.Empty : reader["value"], CultureInfo.InvariantCulture) ?? string.Empty;
                if (label.Length == 0 && value.Length == 0)
                {
                    continue;
                }

                specs.Add(new(
                    Convert.ToString(reader["group_name"] is DBNull ? "Specifications" : reader["group_name"], CultureInfo.InvariantCulture) ?? "Specifications",
                    label,
                    value,
                    Convert.ToString(reader["unit"] is DBNull ? string.Empty : reader["unit"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["value_type"] is DBNull ? "text" : reader["value_type"], CultureInfo.InvariantCulture) ?? "text"));
            }
        }
        catch
        {
            // Spec tables optional.
        }

        return specs;
    }

    private static void AddGenuineBrandKeys(HashSet<string> keys, string? brand)
    {
        var key = NormalizeGenuineBrandKey(brand);
        if (key.Length > 0)
        {
            keys.Add(key);
        }
    }

    private static string NormalizeGenuineBrandKey(string? brand)
    {
        if (string.IsNullOrWhiteSpace(brand))
        {
            return string.Empty;
        }

        return brand.Trim().ToUpperInvariant();
    }

    private static (string Manufacturer, string Article, string Name, decimal Price) ParseQuoteProductObject(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty, string.Empty, 0m);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            string Get(string name)
                => root.TryGetProperty(name, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String
                    ? el.GetString() ?? string.Empty
                    : string.Empty;

            var mfr = Get("manufacturer");
            var article = Get("article_show");
            if (string.IsNullOrWhiteSpace(article))
            {
                article = Get("article");
            }

            var name = Get("name");
            var price = 0m;
            if (root.TryGetProperty("price", out var priceEl))
            {
                if (priceEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    price = priceEl.GetDecimal();
                }
                else if (priceEl.ValueKind == System.Text.Json.JsonValueKind.String
                         && decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                {
                    price = parsed;
                }
            }

            return (mfr, article, name, price);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty, 0m);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<ErpInventoryStockDigestResult> BuildErpInventoryStockDigestAsync(int limit, int? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var wh = warehouseId is > 0 ? warehouseId.Value : 0;
        var empty = new ErpInventoryStockSummaryResult(0, 0m, 0m, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var summary = empty with { Source = "database", Message = string.Empty };
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpInventoryStockSummary;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    summary = new(
                        Convert.ToInt64(reader["row_count"] is DBNull ? 0 : reader["row_count"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["qty_on_hand"] is DBNull ? 0m : reader["qty_on_hand"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["stock_value"] is DBNull ? 0m : reader["stock_value"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["warehouse_count"] is DBNull ? 0 : reader["warehouse_count"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture),
                        "database",
                        string.Empty);
                }
            }

            var rows = new List<ErpInventoryStockDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpInventoryStockRows;
                AddParameter(list, "@limit", safeLimit);
                AddParameter(list, "@warehouseId", wh);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpInventoryStockDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["warehouse_id"] is DBNull ? 0 : reader["warehouse_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["item_id"] is DBNull ? 0 : reader["item_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["sku"] is DBNull ? string.Empty : reader["sku"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["item_type"] is DBNull ? string.Empty : reader["item_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["unit"] is DBNull ? string.Empty : reader["unit"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["warehouse_name"] is DBNull ? string.Empty : reader["warehouse_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["qty_on_hand"] is DBNull ? 0m : reader["qty_on_hand"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["avg_unit_cost"] is DBNull ? 0m : reader["avg_unit_cost"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["batch_no"] is DBNull ? string.Empty : reader["batch_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["variant_label"] is DBNull ? string.Empty : reader["variant_label"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["expiry_date"] is DBNull ? string.Empty : reader["expiry_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
                }
            }

            var low = new List<ErpInventoryLowStockDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpInventoryLowStockRows;
                AddParameter(list, "@limit", Math.Min(safeLimit, 200));
                AddParameter(list, "@warehouseId", wh);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    low.Add(new ErpInventoryLowStockDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["warehouse_id"] is DBNull ? 0 : reader["warehouse_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["item_id"] is DBNull ? 0 : reader["item_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["sku"] is DBNull ? string.Empty : reader["sku"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["warehouse_name"] is DBNull ? string.Empty : reader["warehouse_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["qty_on_hand"] is DBNull ? 0m : reader["qty_on_hand"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["reorder_level"] is DBNull ? 0m : reader["reorder_level"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["avg_unit_cost"] is DBNull ? 0m : reader["avg_unit_cost"], CultureInfo.InvariantCulture)));
                }
            }

            return new(summary, rows, low, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], [], 0, "database-error", ex.Message);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);

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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<ErpCompaniesDigestResult> BuildErpCompaniesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var packs = new Dictionary<long, string>();
            try
            {
                await using (var packCmd = connection.CreateCommand())
                {
                    packCmd.CommandText = LegacySurfaceDashboardSql.SelectErpCompanyIndustryPacks;
                    await using var packReader = await packCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await packReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var companyId = Convert.ToInt64(packReader["company_id"], CultureInfo.InvariantCulture);
                        var pack = Convert.ToString(packReader["industry_pack"] is DBNull ? string.Empty : packReader["industry_pack"], CultureInfo.InvariantCulture) ?? string.Empty;
                        packs[companyId] = pack;
                    }
                }
            }
            catch
            {
                // Table may be absent on lean tenants — companies list still works.
            }

            var rows = new List<ErpCompanyDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpCompanies;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture);
                    packs.TryGetValue(id, out var industryPack);
                    rows.Add(new ErpCompanyDigest(
                        id,
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency_code"] is DBNull ? string.Empty : reader["currency_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["country_code"] is DBNull ? string.Empty : reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        industryPack ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 1 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var carriers = 0; var active = 0; var shipments = 0; var open = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCarrierStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    carriers = Convert.ToInt32(reader["carrier_count"] is DBNull ? 0 : reader["carrier_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_carriers"] is DBNull ? 0 : reader["active_carriers"], CultureInfo.InvariantCulture);
                    shipments = Convert.ToInt32(reader["shipment_count"] is DBNull ? 0 : reader["shipment_count"], CultureInfo.InvariantCulture);
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
                    var code = Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var region = "";
                    var blurb = "";
                    if (CpChannelCatalogs.TryGetCarrier(code, out var meta))
                    {
                        region = meta.Region;
                        blurb = meta.Blurb;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = meta.Name;
                        }
                    }

                    rows.Add(new CpCarrierDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        code,
                        name,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["demo_mode"] is DBNull ? 0 : reader["demo_mode"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        region,
                        blurb));
                }
            }

            var summary = new CpCarriersSummary(carriers, active, shipments, open, "database", string.Empty);
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
        var empty = new CpPaymentGatewaysSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var gateways = 0; var enabled = 0; var active = 0; var selectable = 0; var accounts = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPaymentGatewayStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    gateways = Convert.ToInt32(reader["gateway_count"] is DBNull ? 0 : reader["gateway_count"], CultureInfo.InvariantCulture);
                    enabled = Convert.ToInt32(reader["enabled_gateways"] is DBNull ? 0 : reader["enabled_gateways"], CultureInfo.InvariantCulture);
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
                        Convert.ToInt32(reader["anable"] is DBNull ? 0 : reader["anable"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt32(reader["is_selectable"] is DBNull ? 0 : reader["is_selectable"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpPaymentGatewaysSummary(gateways, enabled, active, selectable, accounts, "database", string.Empty);
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
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<CpPurchaseRequestsDigestResult> BuildCpPurchaseRequestsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPurchaseRequestsSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var reqs = 0; var draft = 0; var pending = 0; var lines = 0; var cats = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPurchaseRequestStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    reqs = Convert.ToInt32(reader["req_count"] is DBNull ? 0 : reader["req_count"], CultureInfo.InvariantCulture);
                    draft = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    pending = Convert.ToInt32(reader["pending_approval"] is DBNull ? 0 : reader["pending_approval"], CultureInfo.InvariantCulture);
                    lines = Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture);
                    cats = Convert.ToInt32(reader["category_count"] is DBNull ? 0 : reader["category_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPurchaseRequestDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPurchaseRequests;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPurchaseRequestDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["req_number"] is DBNull ? string.Empty : reader["req_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["requester"] is DBNull ? string.Empty : reader["requester"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["business_unit_id"] is DBNull ? 0 : reader["business_unit_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["total"] is DBNull ? 0 : reader["total"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["requires_approval"] is DBNull ? 0 : reader["requires_approval"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToString(reader["po_ref"] is DBNull ? string.Empty : reader["po_ref"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPurchaseRequestsSummary(reqs, draft, pending, lines, cats, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPromotionsDigestResult> BuildCpPromotionsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPromotionsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var promos = 0; var active = 0; var percent = 0; var loyalty = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPromotionStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    promos = Convert.ToInt32(reader["promotion_count"] is DBNull ? 0 : reader["promotion_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_promotions"] is DBNull ? 0 : reader["active_promotions"], CultureInfo.InvariantCulture);
                    percent = Convert.ToInt32(reader["percent_promotions"] is DBNull ? 0 : reader["percent_promotions"], CultureInfo.InvariantCulture);
                    loyalty = Convert.ToInt32(reader["loyalty_accounts"] is DBNull ? 0 : reader["loyalty_accounts"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPromotionDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPromotions;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPromotionDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["type"] is DBNull ? string.Empty : reader["type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["value"] is DBNull ? 0 : reader["value"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["min_spend"] is DBNull ? 0 : reader["min_spend"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["valid_from"] is DBNull ? 0 : reader["valid_from"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["valid_to"] is DBNull ? 0 : reader["valid_to"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpPromotionsSummary(promos, active, percent, loyalty, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCrmOpportunitiesDigestResult> BuildCpCrmOpportunitiesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCrmOpportunitiesSummary(0, 0, 0, 0m, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var opps = 0; var open = 0; var won = 0; var pipeline = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCrmOpportunityStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    opps = Convert.ToInt32(reader["opportunity_count"] is DBNull ? 0 : reader["opportunity_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_opportunities"] is DBNull ? 0 : reader["open_opportunities"], CultureInfo.InvariantCulture);
                    won = Convert.ToInt32(reader["won_opportunities"] is DBNull ? 0 : reader["won_opportunities"], CultureInfo.InvariantCulture);
                    pipeline = Convert.ToDecimal(reader["pipeline_amount"] is DBNull ? 0 : reader["pipeline_amount"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCrmOpportunityDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCrmOpportunities;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCrmOpportunityDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["stage"] is DBNull ? string.Empty : reader["stage"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["amount"] is DBNull ? 0 : reader["amount"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["probability"] is DBNull ? 0 : reader["probability"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["close_date"] is DBNull ? 0 : reader["close_date"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["owner_user_id"] is DBNull ? 0 : reader["owner_user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["lead_id"] is DBNull ? 0 : reader["lead_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpCrmOpportunitiesSummary(opps, open, won, pipeline, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpIntegrationsDigestResult> BuildCpIntegrationsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        Dictionary<string, bool>? flags = null;
        var source = "catalog";
        var message = string.Empty;

        if (_connections.IsConfigured)
        {
            try
            {
                await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
                flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpIntegrationFeatureFlags;
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var key = Convert.ToString(reader["feature_key"] is DBNull ? string.Empty : reader["feature_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    flags[key] = Convert.ToInt32(reader["enabled"] is DBNull ? 0 : reader["enabled"], CultureInfo.InvariantCulture) != 0;
                }

                source = flags.Count > 0 ? "database" : "catalog";
            }
            catch (Exception ex)
            {
                // Hub catalog still returns; flags are optional overlay.
                source = "catalog";
                message = ex.Message;
            }
        }
        else
        {
            message = "TenantRegistry DB is not configured; hub catalog defaults used.";
            source = "migration";
        }

        var rows = CpIntegrationsHubCatalog.BuildTenantDigests(flags, safeLimit);
        var summary = CpIntegrationsHubCatalog.Summarize(rows, source, message);
        return new(summary, rows, rows.Count, source, message);
    }

    public async Task<ErpBankReconciliationDigestResult> BuildErpBankReconciliationDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpBankReconciliationSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var lines = 0; var unmatched = 0; var matched = 0; var credit = 0m; var debit = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpBankReconciliationStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    lines = Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture);
                    unmatched = Convert.ToInt32(reader["unmatched_count"] is DBNull ? 0 : reader["unmatched_count"], CultureInfo.InvariantCulture);
                    matched = Convert.ToInt32(reader["matched_count"] is DBNull ? 0 : reader["matched_count"], CultureInfo.InvariantCulture);
                    credit = Convert.ToDecimal(reader["credit_total"] is DBNull ? 0 : reader["credit_total"], CultureInfo.InvariantCulture);
                    debit = Convert.ToDecimal(reader["debit_total"] is DBNull ? 0 : reader["debit_total"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpBankStatementLineDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpBankReconciliationLines;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpBankStatementLineDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["account_id"] is DBNull ? 0 : reader["account_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["line_date"] is DBNull ? 0 : reader["line_date"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["description"] is DBNull ? string.Empty : reader["description"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["amount"] is DBNull ? 0 : reader["amount"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["direction"] is DBNull ? 0 : reader["direction"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["matched_entry_id"] is DBNull ? 0 : reader["matched_entry_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["import_batch"] is DBNull ? string.Empty : reader["import_batch"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new ErpBankReconciliationSummary(lines, unmatched, matched, credit, debit, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpStockTransfersDigestResult> BuildErpStockTransfersDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpStockTransfersSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var transfers = 0; var draft = 0; var transit = 0; var received = 0; var totalQty = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpStockTransferStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    transfers = Convert.ToInt32(reader["transfer_count"] is DBNull ? 0 : reader["transfer_count"], CultureInfo.InvariantCulture);
                    draft = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    transit = Convert.ToInt32(reader["in_transit_count"] is DBNull ? 0 : reader["in_transit_count"], CultureInfo.InvariantCulture);
                    received = Convert.ToInt32(reader["received_count"] is DBNull ? 0 : reader["received_count"], CultureInfo.InvariantCulture);
                    totalQty = Convert.ToDecimal(reader["total_qty"] is DBNull ? 0 : reader["total_qty"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpStockTransferDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpStockTransfers;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpStockTransferDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["transfer_no"] is DBNull ? string.Empty : reader["transfer_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["from_warehouse_id"] is DBNull ? 0 : reader["from_warehouse_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["to_warehouse_id"] is DBNull ? 0 : reader["to_warehouse_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["reason"] is DBNull ? string.Empty : reader["reason"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["total_items"] is DBNull ? 0 : reader["total_items"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["total_qty"] is DBNull ? 0 : reader["total_qty"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["shipped_at"] is DBNull ? string.Empty : reader["shipped_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["received_at"] is DBNull ? string.Empty : reader["received_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["created_by"] is DBNull ? 0 : reader["created_by"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new ErpStockTransfersSummary(transfers, draft, transit, received, totalQty, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpSalesQuotationsDigestResult> BuildErpSalesQuotationsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpSalesQuotationsSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var quotes = 0; var draft = 0; var sent = 0; var accepted = 0; var subtotal = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpSalesQuotationStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    quotes = Convert.ToInt32(reader["quote_count"] is DBNull ? 0 : reader["quote_count"], CultureInfo.InvariantCulture);
                    draft = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    sent = Convert.ToInt32(reader["sent_count"] is DBNull ? 0 : reader["sent_count"], CultureInfo.InvariantCulture);
                    accepted = Convert.ToInt32(reader["accepted_count"] is DBNull ? 0 : reader["accepted_count"], CultureInfo.InvariantCulture);
                    subtotal = Convert.ToDecimal(reader["subtotal_sum"] is DBNull ? 0 : reader["subtotal_sum"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpSalesQuotationDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpSalesQuotations;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpSalesQuotationDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["opportunity_id"] is DBNull ? 0 : reader["opportunity_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["lead_id"] is DBNull ? 0 : reader["lead_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["customer_user_id"] is DBNull ? 0 : reader["customer_user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["quote_number"] is DBNull ? string.Empty : reader["quote_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency_code"] is DBNull ? string.Empty : reader["currency_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["subtotal"] is DBNull ? 0 : reader["subtotal"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["shop_order_id"] is DBNull ? 0 : reader["shop_order_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new ErpSalesQuotationsSummary(quotes, draft, sent, accepted, subtotal, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpWorkspaceFavoritesDigestResult> BuildErpWorkspaceFavoritesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpWorkspaceFavoritesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var shortcuts = 0; var pinned = 0; var users = 0; var erpSurface = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpWorkspaceFavoriteStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    shortcuts = Convert.ToInt32(reader["shortcut_count"] is DBNull ? 0 : reader["shortcut_count"], CultureInfo.InvariantCulture);
                    pinned = Convert.ToInt32(reader["pinned_count"] is DBNull ? 0 : reader["pinned_count"], CultureInfo.InvariantCulture);
                    users = Convert.ToInt32(reader["user_count"] is DBNull ? 0 : reader["user_count"], CultureInfo.InvariantCulture);
                    erpSurface = Convert.ToInt32(reader["erp_surface_count"] is DBNull ? 0 : reader["erp_surface_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpWorkspaceFavoriteDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpWorkspaceFavorites;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpWorkspaceFavoriteDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["surface"] is DBNull ? string.Empty : reader["surface"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["shortcut_key"] is DBNull ? string.Empty : reader["shortcut_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["icon_class"] is DBNull ? string.Empty : reader["icon_class"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["target_url"] is DBNull ? string.Empty : reader["target_url"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["target_tab"] is DBNull ? string.Empty : reader["target_tab"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_pinned"] is DBNull ? 0 : reader["is_pinned"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new ErpWorkspaceFavoritesSummary(shortcuts, pinned, users, erpSurface, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpFixedAssetsDigestResult> BuildErpFixedAssetsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpFixedAssetsSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var assets = 0; var active = 0; var disposed = 0; var cost = 0m; var book = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpFixedAssetStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    assets = Convert.ToInt32(reader["asset_count"] is DBNull ? 0 : reader["asset_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    disposed = Convert.ToInt32(reader["disposed_count"] is DBNull ? 0 : reader["disposed_count"], CultureInfo.InvariantCulture);
                    cost = Convert.ToDecimal(reader["cost_total"] is DBNull ? 0 : reader["cost_total"], CultureInfo.InvariantCulture);
                    book = Convert.ToDecimal(reader["book_value_total"] is DBNull ? 0 : reader["book_value_total"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpFixedAssetDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpFixedAssets;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpFixedAssetDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["asset_code"] is DBNull ? string.Empty : reader["asset_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["category_id"] is DBNull ? 0 : reader["category_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["acquisition_date"] is DBNull ? string.Empty : reader["acquisition_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["cost"] is DBNull ? 0 : reader["cost"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["salvage_value"] is DBNull ? 0 : reader["salvage_value"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["useful_life_months"] is DBNull ? 0 : reader["useful_life_months"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["depreciation_method"] is DBNull ? string.Empty : reader["depreciation_method"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["accumulated_depreciation"] is DBNull ? 0 : reader["accumulated_depreciation"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["book_value"] is DBNull ? 0 : reader["book_value"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["location"] is DBNull ? string.Empty : reader["location"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new ErpFixedAssetsSummary(assets, active, disposed, cost, book, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public Task<ErpReportCenterDigestResult> BuildErpReportCenterDigestAsync(string? key, int limit, CancellationToken cancellationToken = default, int? companyId = null)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var reports = ErpReportCenterRegistry.All
            .Select(e => new ErpReportCenterReportDigest(e.Key, e.Area, e.Name, e.Desc))
            .ToList();
        var areaCount = reports.Select(r => r.Area).Distinct(StringComparer.Ordinal).Count();
        var selectedKey = string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();

        if (!_connections.IsConfigured || string.IsNullOrEmpty(selectedKey))
        {
            var migSummary = new ErpReportCenterSummary(
                reports.Count,
                areaCount,
                selectedKey,
                0,
                "migration",
                _connections.IsConfigured ? string.Empty : "TenantRegistry DB is not configured.");
            return Task.FromResult(new ErpReportCenterDigestResult(
                migSummary,
                reports,
                [],
                [],
                reports.Count,
                migSummary.Source,
                migSummary.Message));
        }

        return BuildErpReportCenterDigestCoreAsync(reports, areaCount, selectedKey, safeLimit, companyId, cancellationToken);
    }

    private async Task<ErpReportCenterDigestResult> BuildErpReportCenterDigestCoreAsync(
        IReadOnlyList<ErpReportCenterReportDigest> reports,
        int areaCount,
        string selectedKey,
        int safeLimit,
        int? companyId,
        CancellationToken cancellationToken)
    {
        var entry = ErpReportCenterRegistry.Find(selectedKey);
        if (entry is null)
        {
            var missing = new ErpReportCenterSummary(reports.Count, areaCount, selectedKey, 0, "migration", "Report key not found in epc_rc_registry mirror.");
            return new(missing, reports, [], [], reports.Count, missing.Source, missing.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<string> columns;
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows;

            if (entry.Kind == ErpReportCenterRegistry.SourceKind.Computed)
            {
                (columns, rows) = await RunErpReportCenterComputedAsync(connection, entry.Key, safeLimit, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                (columns, rows) = await RunErpReportCenterTablePeekAsync(connection, entry, safeLimit, companyId, cancellationToken).ConfigureAwait(false);
            }

            var summary = new ErpReportCenterSummary(reports.Count, areaCount, selectedKey, rows.Count, "database", string.Empty);
            return new(summary, reports, columns, rows, reports.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = new ErpReportCenterSummary(reports.Count, areaCount, selectedKey, 0, "database-error", ex.Message);
            return new(err, reports, [], [], reports.Count, "database-error", ex.Message);
        }
    }

    private async Task<(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows)> RunErpReportCenterTablePeekAsync(
        DbConnection connection,
        ErpReportCenterRegistry.Entry entry,
        int safeLimit,
        int? companyId,
        CancellationToken cancellationToken)
    {
        foreach (var table in new[] { entry.Table, entry.FallbackTable })
        {
            if (string.IsNullOrWhiteSpace(table) || !Regex.IsMatch(table, "^[A-Za-z0-9_]+$"))
            {
                continue;
            }

            try
            {
                var useCompany = companyId is > 0 && await TableHasCompanyIdAsync(connection, table, cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = string.Format(
                    CultureInfo.InvariantCulture,
                    useCompany
                        ? LegacySurfaceDashboardSql.SelectErpReportCenterTableRowsByCompanyTemplate
                        : LegacySurfaceDashboardSql.SelectErpReportCenterTableRowsTemplate,
                    table);
                if (useCompany)
                {
                    AddParameter(command, "@companyId", companyId!.Value);
                }

                AddParameter(command, "@limit", safeLimit);
                var (columns, rows) = await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
                if (rows.Count > 0 || string.IsNullOrWhiteSpace(entry.FallbackTable) || table == entry.FallbackTable)
                {
                    return (columns, rows);
                }
            }
            catch
            {
                // try fallback table
            }
        }

        return ([], []);
    }

    private static async Task<bool> TableHasCompanyIdAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpReportCenterHasCompanyIdTemplate;
            AddParameter(command, "@tableName", table);
            var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(scalar is DBNull or null ? 0 : scalar, CultureInfo.InvariantCulture) > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows)> RunErpReportCenterComputedAsync(
        DbConnection connection,
        string key,
        int safeLimit,
        CancellationToken cancellationToken)
    {
        switch (key.ToLowerInvariant())
        {
            case "ap_vendor_list":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpSuppliers;
                AddParameter(command, "@limit", safeLimit);
                return await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
            }
            case "bank_accounts":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpCashAccounts;
                AddParameter(command, "@limit", safeLimit);
                return await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
            }
            case "ar_customer_list":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpCreditProfiles;
                AddParameter(command, "@limit", safeLimit);
                return await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
            }
            case "credit_holds":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpCreditHolds;
                AddParameter(command, "@limit", safeLimit);
                return await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
            }
            case "gl_trial_balance":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpTrialBalanceRows;
                AddParameter(command, "@limit", safeLimit);
                var (columns, all) = await ReadDictionaryRowsAsync(command, cancellationToken).ConfigureAwait(false);
                var filtered = all
                    .Where(r => Math.Abs(ParseDecimal(r.GetValueOrDefault("balance"))) >= 0.005m)
                    .Select(r =>
                    {
                        var bal = ParseDecimal(r.GetValueOrDefault("balance"));
                        var side = r.GetValueOrDefault("normal_side") ?? "debit";
                        var debit = 0m;
                        var credit = 0m;
                        if (side == "debit")
                        {
                            if (bal >= 0) debit = bal; else credit = Math.Abs(bal);
                        }
                        else
                        {
                            if (bal >= 0) credit = bal; else debit = Math.Abs(bal);
                        }

                        return (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["code"] = r.GetValueOrDefault("code") ?? string.Empty,
                            ["name"] = r.GetValueOrDefault("name") ?? string.Empty,
                            ["account_type"] = r.GetValueOrDefault("account_type") ?? string.Empty,
                            ["balance"] = bal.ToString(CultureInfo.InvariantCulture),
                            ["debit"] = debit.ToString(CultureInfo.InvariantCulture),
                            ["credit"] = credit.ToString(CultureInfo.InvariantCulture),
                        };
                    })
                    .ToList();
                return (["code", "name", "account_type", "balance", "debit", "credit"], filtered);
            }
            case "exec_working_capital":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpReportCenterWorkingCapital;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return ([], []);
                }

                var ar = Convert.ToDecimal(reader["ar"] is DBNull ? 0m : reader["ar"], CultureInfo.InvariantCulture);
                var ap = Convert.ToDecimal(reader["ap"] is DBNull ? 0m : reader["ap"], CultureInfo.InvariantCulture);
                var inv = Convert.ToDecimal(reader["inventory"] is DBNull ? 0m : reader["inventory"], CultureInfo.InvariantCulture);
                var cash = Convert.ToDecimal(reader["cash"] is DBNull ? 0m : reader["cash"], CultureInfo.InvariantCulture);
                var net = ar + inv + cash - ap;
                var ratio = Math.Round((ar + inv + cash) / (ap > 0 ? ap : 1m), 2);
                IReadOnlyDictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Accounts Receivable"] = ar.ToString("0.00", CultureInfo.InvariantCulture),
                    ["Accounts Payable"] = ap.ToString("0.00", CultureInfo.InvariantCulture),
                    ["Inventory Value"] = inv.ToString("0.00", CultureInfo.InvariantCulture),
                    ["Cash & Bank"] = cash.ToString("0.00", CultureInfo.InvariantCulture),
                    ["Net Working Capital"] = net.ToString("0.00", CultureInfo.InvariantCulture),
                    ["Current Ratio"] = ratio.ToString(CultureInfo.InvariantCulture),
                };
                return (row.Keys.ToList(), [row]);
            }
            case "exec_ar_aging":
            {
                await using var command = connection.CreateCommand();
                command.CommandText = LegacySurfaceDashboardSql.SelectErpReportCenterArAgingExec;
                var buckets = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    ["current"] = 0, ["d30"] = 0, ["d60"] = 0, ["d90"] = 0, ["over90"] = 0,
                };
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var amt = Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture);
                    var orderDate = Convert.ToInt64(reader["order_date"] is DBNull ? 0 : reader["order_date"], CultureInfo.InvariantCulture);
                    var age = orderDate > 0 ? (now - orderDate) / 86400.0 : 0;
                    if (age <= 30) buckets["current"] += amt;
                    else if (age <= 60) buckets["d30"] += amt;
                    else if (age <= 90) buckets["d60"] += amt;
                    else if (age <= 120) buckets["d90"] += amt;
                    else buckets["over90"] += amt;
                }

                IReadOnlyDictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Current (0-30 days)"] = buckets["current"].ToString("0.00", CultureInfo.InvariantCulture),
                    ["31-60 days"] = buckets["d30"].ToString("0.00", CultureInfo.InvariantCulture),
                    ["61-90 days"] = buckets["d60"].ToString("0.00", CultureInfo.InvariantCulture),
                    ["91-120 days"] = buckets["d90"].ToString("0.00", CultureInfo.InvariantCulture),
                    ["Over 120 days"] = buckets["over90"].ToString("0.00", CultureInfo.InvariantCulture),
                };
                return (row.Keys.ToList(), [row]);
            }
            case "exec_cash_forecast":
            {
                var avgIn = 0m;
                var avgOut = 0m;
                for (var i = 3; i >= 1; i--)
                {
                    var from = new DateTimeOffset(DateTime.UtcNow.Date.AddMonths(-i).AddDays(1 - DateTime.UtcNow.Date.AddMonths(-i).Day), TimeSpan.Zero).ToUnixTimeSeconds();
                    var toMonth = DateTime.UtcNow.Date.AddMonths(-i + 1).AddDays(1 - DateTime.UtcNow.Date.AddMonths(-i + 1).Day).AddSeconds(-1);
                    var to = new DateTimeOffset(toMonth, TimeSpan.Zero).ToUnixTimeSeconds();
                    await using var command = connection.CreateCommand();
                    command.CommandText = LegacySurfaceDashboardSql.SelectErpReportCenterCashHistory;
                    AddParameter(command, "@from", from);
                    AddParameter(command, "@to", to);
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        avgIn += Convert.ToDecimal(reader["inflow"] is DBNull ? 0m : reader["inflow"], CultureInfo.InvariantCulture);
                        avgOut += Convert.ToDecimal(reader["outflow"] is DBNull ? 0m : reader["outflow"], CultureInfo.InvariantCulture);
                    }
                }

                avgIn /= 3m;
                avgOut /= 3m;
                var rows = new List<IReadOnlyDictionary<string, string>>();
                for (var i = 1; i <= 3; i++)
                {
                    var month = DateTime.UtcNow.Date.AddMonths(i).ToString("MMM yyyy", CultureInfo.InvariantCulture);
                    rows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Month"] = month,
                        ["Expected Inflow"] = Math.Round(avgIn, 2).ToString("0.00", CultureInfo.InvariantCulture),
                        ["Expected Outflow"] = Math.Round(avgOut, 2).ToString("0.00", CultureInfo.InvariantCulture),
                        ["Net Cash Flow"] = Math.Round(avgIn - avgOut, 2).ToString("0.00", CultureInfo.InvariantCulture),
                    });
                }

                return (["Month", "Expected Inflow", "Expected Outflow", "Net Cash Flow"], rows);
            }
            default:
                return ([], []);
        }
    }

    private static async Task<(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyDictionary<string, string>> Rows)> ReadDictionaryRowsAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        var rows = new List<IReadOnlyDictionary<string, string>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columns)
            {
                var val = reader[col];
                dict[col] = val is DBNull or null
                    ? string.Empty
                    : Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            rows.Add(dict);
        }

        return (columns, rows);
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    public async Task<ErpProcessFlowTasksDigestResult> BuildErpProcessFlowTasksDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new ErpProcessFlowTasksSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var tasks = 0; var open = 0; var done = 0; var overdue = 0; var cancelled = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectErpProcessFlowTaskStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tasks = Convert.ToInt32(reader["task_count"] is DBNull ? 0 : reader["task_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    done = Convert.ToInt32(reader["done_count"] is DBNull ? 0 : reader["done_count"], CultureInfo.InvariantCulture);
                    overdue = Convert.ToInt32(reader["overdue_count"] is DBNull ? 0 : reader["overdue_count"], CultureInfo.InvariantCulture);
                    cancelled = Convert.ToInt32(reader["cancelled_count"] is DBNull ? 0 : reader["cancelled_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<ErpProcessFlowTaskDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpProcessFlowTasks;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new ErpProcessFlowTaskDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["process_id"] is DBNull ? 0 : reader["process_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["current_step_no"] is DBNull ? 0 : reader["current_step_no"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["current_assignee_id"] is DBNull ? 0 : reader["current_assignee_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["current_department"] is DBNull ? string.Empty : reader["current_department"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["initiator_id"] is DBNull ? 0 : reader["initiator_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["subject_type"] is DBNull ? string.Empty : reader["subject_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["subject_id"] is DBNull ? 0 : reader["subject_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["started_at"] is DBNull ? 0 : reader["started_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["due_at"] is DBNull ? 0 : reader["due_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["completed_at"] is DBNull ? 0 : reader["completed_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new ErpProcessFlowTasksSummary(tasks, open, done, overdue, cancelled, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpAgingDigestResult> BuildErpAgingDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var b = new[] { 30, 60, 90 };
        var arLabels = new[] { "Not due", "1-30", "31-60", "61-90", "90+" };
        var invLabels = new[] { "0-30", "31-60", "61-90", "91-180", "180+" };
        var empty = new ErpAgingSummary(b[0], b[1], b[2], 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, arLabels, arLabels, invLabels, [], [], [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var arMap = new Dictionary<long, ErpAgingPartyDigest>();
            var arTotals = new decimal[5];
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = LegacySurfaceDashboardSql.SelectErpAgingArDocuments;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var outstanding = Convert.ToDecimal(reader["total_incl_vat"] is DBNull ? 0m : reader["total_incl_vat"], CultureInfo.InvariantCulture)
                        - Convert.ToDecimal(reader["paid_amount"] is DBNull ? 0m : reader["paid_amount"], CultureInfo.InvariantCulture);
                    if (outstanding <= 0.005m) continue;
                    var due = Convert.ToInt64(reader["payment_due_date"] is DBNull ? 0 : reader["payment_due_date"], CultureInfo.InvariantCulture);
                    if (due <= 0) due = Convert.ToInt64(reader["issue_date"] is DBNull ? 0 : reader["issue_date"], CultureInfo.InvariantCulture);
                    var days = due > 0 ? (int)((now - due) / 86400) : 0;
                    var idx = AgingBucketIndex(days, b, overdue: true);
                    var userId = Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture);
                    var email = Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var name = string.IsNullOrWhiteSpace(email) ? $"User #{userId}" : email;
                    AccumulateAging(arMap, userId, name, idx, outstanding, arTotals);
                }
            }
            catch { /* table may be missing */ }

            var apMap = new Dictionary<long, ErpAgingPartyDigest>();
            var apTotals = new decimal[5];
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = LegacySurfaceDashboardSql.SelectErpAgingApDocuments;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var outstanding = Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture)
                        - Convert.ToDecimal(reader["paid"] is DBNull ? 0m : reader["paid"], CultureInfo.InvariantCulture);
                    if (outstanding <= 0.005m) continue;
                    var d = Convert.ToInt64(reader["purchase_date"] is DBNull ? 0 : reader["purchase_date"], CultureInfo.InvariantCulture);
                    var days = d > 0 ? (int)((now - d) / 86400) : 0;
                    var idx = AgingBucketIndex(days, b, overdue: true);
                    var supplierId = Convert.ToInt64(reader["supplier_id"] is DBNull ? 0 : reader["supplier_id"], CultureInfo.InvariantCulture);
                    var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) name = $"Supplier #{supplierId}";
                    AccumulateAging(apMap, supplierId, name, idx, outstanding, apTotals);
                }
            }
            catch { /* table may be missing */ }

            var invMap = new Dictionary<long, ErpAgingPartyDigest>();
            var invTotals = new decimal[5];
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = LegacySurfaceDashboardSql.SelectErpAgingInventoryRows;
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var qty = Convert.ToDecimal(reader["qty_on_hand"] is DBNull ? 0m : reader["qty_on_hand"], CultureInfo.InvariantCulture);
                    var cost = Convert.ToDecimal(reader["avg_unit_cost"] is DBNull ? 0m : reader["avg_unit_cost"], CultureInfo.InvariantCulture);
                    var value = Math.Round(qty * cost, 2);
                    if (value <= 0.005m) continue;
                    var lastIn = Convert.ToInt64(reader["last_in"] is DBNull ? 0 : reader["last_in"], CultureInfo.InvariantCulture);
                    if (lastIn <= 0) lastIn = Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture);
                    var days = lastIn > 0 ? (int)((now - lastIn) / 86400) : 0;
                    var idx = AgingBucketIndex(days, b, overdue: false);
                    var itemId = Convert.ToInt64(reader["item_id"], CultureInfo.InvariantCulture);
                    var sku = Convert.ToString(reader["sku"] is DBNull ? string.Empty : reader["sku"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var label = string.IsNullOrWhiteSpace(sku) ? name : $"{sku} · {name}";
                    AccumulateAging(invMap, itemId, label, idx, value, invTotals);
                }
            }
            catch { /* table may be missing */ }

            var arRows = arMap.Values.OrderByDescending(r => r.Total).Take(safeLimit).ToList();
            var apRows = apMap.Values.OrderByDescending(r => r.Total).Take(safeLimit).ToList();
            var invRows = invMap.Values.OrderByDescending(r => r.Total).Take(safeLimit).ToList();
            var summary = new ErpAgingSummary(
                b[0], b[1], b[2],
                Math.Round(arTotals.Sum(), 2),
                Math.Round(apTotals.Sum(), 2),
                Math.Round(invTotals.Sum(), 2),
                "database",
                string.Empty);
            return new(summary, arLabels, arLabels, invLabels, arRows, apRows, invRows, arRows.Count + apRows.Count + invRows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, arLabels, arLabels, invLabels, [], [], [], 0, "database-error", ex.Message);
        }
    }

    private static int AgingBucketIndex(int days, int[] b, bool overdue)
    {
        if (overdue)
        {
            if (days <= 0) return 0;
            if (days <= b[0]) return 1;
            if (days <= b[1]) return 2;
            if (days <= b[2]) return 3;
            return 4;
        }

        if (days <= b[0]) return 0;
        if (days <= b[1]) return 1;
        if (days <= b[2]) return 2;
        if (days <= b[2] * 2) return 3;
        return 4;
    }

    private static void AccumulateAging(
        Dictionary<long, ErpAgingPartyDigest> map,
        long key,
        string name,
        int idx,
        decimal amount,
        decimal[] totals)
    {
        totals[idx] += amount;
        if (!map.TryGetValue(key, out var row))
        {
            row = new ErpAgingPartyDigest(name, 0, 0, 0, 0, 0, 0);
        }

        var buckets = new[] { row.Bucket0, row.Bucket1, row.Bucket2, row.Bucket3, row.Bucket4 };
        buckets[idx] += amount;
        map[key] = new ErpAgingPartyDigest(name, buckets[0], buckets[1], buckets[2], buckets[3], buckets[4], row.Total + amount);
    }

    public async Task<ErpInventoryMovementsDigestResult> BuildErpInventoryMovementsDigestAsync(
        int limit,
        int? itemId = null,
        int? warehouseId = null,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var item = itemId is > 0 ? itemId.Value : 0;
        var wh = warehouseId is > 0 ? warehouseId.Value : 0;
        var empty = new ErpInventoryMovementsSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var totalCount = 0;
            try
            {
                await using var countCmd = connection.CreateCommand();
                countCmd.CommandText = LegacySurfaceDashboardSql.CountErpInventoryMovements;
                var scalar = await countCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                totalCount = Convert.ToInt32(scalar is DBNull or null ? 0 : scalar, CultureInfo.InvariantCulture);
            }
            catch { /* ignore */ }

            var chronological = new List<ErpInventoryMovementDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectErpInventoryMovements;
                AddParameter(list, "@limit", safeLimit);
                AddParameter(list, "@itemId", item);
                AddParameter(list, "@warehouseId", wh);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var balByKey = new Dictionary<string, decimal>(StringComparer.Ordinal);
                var inTypes = new HashSet<string>(["opening", "purchase_in", "transfer_in", "return_in"], StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var movementType = Convert.ToString(reader["movement_type"] is DBNull ? string.Empty : reader["movement_type"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var warehouseIdVal = Convert.ToInt64(reader["warehouse_id"] is DBNull ? 0 : reader["warehouse_id"], CultureInfo.InvariantCulture);
                    var itemIdVal = Convert.ToInt64(reader["item_id"] is DBNull ? 0 : reader["item_id"], CultureInfo.InvariantCulture);
                    var qty = Convert.ToDecimal(reader["qty"] is DBNull ? 0m : reader["qty"], CultureInfo.InvariantCulture);
                    var signed = inTypes.Contains(movementType) ? qty : -qty;
                    var key = $"{itemIdVal}:{warehouseIdVal}";
                    balByKey.TryGetValue(key, out var bal);
                    bal += signed;
                    balByKey[key] = bal;
                    chronological.Add(new ErpInventoryMovementDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        movementType,
                        warehouseIdVal,
                        itemIdVal,
                        Convert.ToString(reader["sku"] is DBNull ? string.Empty : reader["sku"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["item_name"] is DBNull ? string.Empty : reader["item_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["warehouse_name"] is DBNull ? string.Empty : reader["warehouse_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        qty,
                        signed,
                        Convert.ToDecimal(reader["unit_cost"] is DBNull ? 0m : reader["unit_cost"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["total_cost"] is DBNull ? 0m : reader["total_cost"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["batch_no"] is DBNull ? string.Empty : reader["batch_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["movement_date"] is DBNull ? 0 : reader["movement_date"], CultureInfo.InvariantCulture),
                        Math.Round(bal, 3)));
                }
            }

            chronological.Reverse();
            var inCount = chronological.Count(m => m.SignedQty > 0);
            var outCount = chronological.Count(m => m.SignedQty < 0);
            var summary = new ErpInventoryMovementsSummary(
                totalCount > 0 ? totalCount : chronological.Count,
                inCount,
                outCount,
                chronological.Where(m => m.SignedQty > 0).Sum(m => m.Qty),
                chronological.Where(m => m.SignedQty < 0).Sum(m => m.Qty),
                "database",
                string.Empty);
            return new(summary, chronological, chronological.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPageBuilderDigestResult> BuildCpPageBuilderDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPageBuilderSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var layouts = 0; var published = 0; var draft = 0; var sites = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPageBuilderStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    layouts = Convert.ToInt32(reader["layout_count"] is DBNull ? 0 : reader["layout_count"], CultureInfo.InvariantCulture);
                    published = Convert.ToInt32(reader["published_count"] is DBNull ? 0 : reader["published_count"], CultureInfo.InvariantCulture);
                    draft = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    sites = Convert.ToInt32(reader["site_count"] is DBNull ? 0 : reader["site_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPageBuilderLayoutDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPageBuilderLayouts;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPageBuilderLayoutDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["page_key"] is DBNull ? string.Empty : reader["page_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["is_published"] is DBNull ? 0 : reader["is_published"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["published_at"] is DBNull ? 0 : reader["published_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPageBuilderSummary(layouts, published, draft, sites, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpProductCatalogueDigestResult> BuildCpProductCatalogueDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpProductCatalogueSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var products = 0; var published = 0; var unpublished = 0; var categories = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpProductCatalogueStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    products = Convert.ToInt32(reader["product_count"] is DBNull ? 0 : reader["product_count"], CultureInfo.InvariantCulture);
                    published = Convert.ToInt32(reader["published_count"] is DBNull ? 0 : reader["published_count"], CultureInfo.InvariantCulture);
                    unpublished = Convert.ToInt32(reader["unpublished_count"] is DBNull ? 0 : reader["unpublished_count"], CultureInfo.InvariantCulture);
                    categories = Convert.ToInt32(reader["category_count"] is DBNull ? 0 : reader["category_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpProductCatalogueDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpProductCatalogue;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpProductCatalogueDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["category_id"] is DBNull ? 0 : reader["category_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["alias"] is DBNull ? string.Empty : reader["alias"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["published_flag"] is DBNull ? 0 : reader["published_flag"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpProductCatalogueSummary(products, published, unpublished, categories, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPlatformGovernanceDigestResult> BuildCpPlatformGovernanceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPlatformGovernanceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            var rules = 0; var active = 0; var required = 0; var categories = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPlatformGovernanceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rules = Convert.ToInt32(reader["rule_count"] is DBNull ? 0 : reader["rule_count"], CultureInfo.InvariantCulture);
                    active = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    required = Convert.ToInt32(reader["required_count"] is DBNull ? 0 : reader["required_count"], CultureInfo.InvariantCulture);
                    categories = Convert.ToInt32(reader["category_count"] is DBNull ? 0 : reader["category_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPlatformGovernanceRuleDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPlatformGovernanceRules;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPlatformGovernanceRuleDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["rule_key"] is DBNull ? string.Empty : reader["rule_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["enforcement"] is DBNull ? string.Empty : reader["enforcement"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["scope"] is DBNull ? string.Empty : reader["scope"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["module_link"] is DBNull ? string.Empty : reader["module_link"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPlatformGovernanceSummary(rules, active, required, categories, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpEinvoiceDocumentsDigestResult> BuildCpEinvoiceDocumentsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpEinvoiceDocumentsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var docs = 0; var open = 0; var submitted = 0; var total = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpEinvoiceDocumentStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    docs = Convert.ToInt32(reader["document_count"] is DBNull ? 0 : reader["document_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    submitted = Convert.ToInt32(reader["submitted_count"] is DBNull ? 0 : reader["submitted_count"], CultureInfo.InvariantCulture);
                    total = Convert.ToDecimal(reader["total_incl_vat"] is DBNull ? 0 : reader["total_incl_vat"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpEinvoiceDocumentDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpEinvoiceDocuments;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpEinvoiceDocumentDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["uuid"] is DBNull ? string.Empty : reader["uuid"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["invoice_number"] is DBNull ? string.Empty : reader["invoice_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["doc_category"] is DBNull ? string.Empty : reader["doc_category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["issue_date"] is DBNull ? 0 : reader["issue_date"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["currency_code"] is DBNull ? string.Empty : reader["currency_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["total_incl_vat"] is DBNull ? 0 : reader["total_incl_vat"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["validation_ok"] is DBNull ? 0 : reader["validation_ok"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpEinvoiceDocumentsSummary(docs, open, submitted, total, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpJewelleryRepairsDigestResult> BuildCpJewelleryRepairsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpJewelleryRepairsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var repairs = 0; var open = 0; var authorized = 0; var items = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryRepairStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    repairs = Convert.ToInt32(reader["repair_count"] is DBNull ? 0 : reader["repair_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    authorized = Convert.ToInt32(reader["authorized_count"] is DBNull ? 0 : reader["authorized_count"], CultureInfo.InvariantCulture);
                    items = Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpJewelleryRepairDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryRepairs;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpJewelleryRepairDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["branch"] is DBNull ? string.Empty : reader["branch"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["voc_type"] is DBNull ? string.Empty : reader["voc_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["voc_date"] is DBNull ? string.Empty : reader["voc_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["voc_no"] is DBNull ? 0 : reader["voc_no"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["customer_name"] is DBNull ? string.Empty : reader["customer_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["delivery_date"] is DBNull ? string.Empty : reader["delivery_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["authorized"] is DBNull ? 0 : reader["authorized"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpJewelleryRepairsSummary(repairs, open, authorized, items, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCrmTicketsDigestResult> BuildCpCrmTicketsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCrmTicketsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var tickets = 0; var open = 0; var high = 0; var messages = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCrmTicketStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tickets = Convert.ToInt32(reader["ticket_count"] is DBNull ? 0 : reader["ticket_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    high = Convert.ToInt32(reader["high_priority_count"] is DBNull ? 0 : reader["high_priority_count"], CultureInfo.InvariantCulture);
                    messages = Convert.ToInt32(reader["message_count"] is DBNull ? 0 : reader["message_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCrmTicketDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCrmTickets;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCrmTicketDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["customer_user_id"] is DBNull ? 0 : reader["customer_user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["subject"] is DBNull ? string.Empty : reader["subject"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["assigned_user_id"] is DBNull ? 0 : reader["assigned_user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0));
                }
            }

            var summary = new CpCrmTicketsSummary(tickets, open, high, messages, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpMarketingGrowthDigestResult> BuildCpMarketingGrowthDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpMarketingGrowthSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var tasks = 0; var done = 0; var kpis = 0; var reviews = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpMarketingGrowthStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tasks = Convert.ToInt32(reader["task_count"] is DBNull ? 0 : reader["task_count"], CultureInfo.InvariantCulture);
                    done = Convert.ToInt32(reader["tasks_done"] is DBNull ? 0 : reader["tasks_done"], CultureInfo.InvariantCulture);
                    kpis = Convert.ToInt32(reader["kpi_log_count"] is DBNull ? 0 : reader["kpi_log_count"], CultureInfo.InvariantCulture);
                    reviews = Convert.ToInt32(reader["review_count"] is DBNull ? 0 : reader["review_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpMarketingGrowthReviewDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpMarketingGrowthReviews;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpMarketingGrowthReviewDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["strategy_key"] is DBNull ? string.Empty : reader["strategy_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["review_type"] is DBNull ? string.Empty : reader["review_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["score"] is DBNull ? 0 : reader["score"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["created_at"] is DBNull ? 0 : reader["created_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["created_by"] is DBNull ? 0 : reader["created_by"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpMarketingGrowthSummary(tasks, done, kpis, reviews, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSoc2ComplianceDigestResult> BuildCpSoc2ComplianceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSoc2ComplianceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var controls = 0; var implemented = 0; var evidence = 0; var policies = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSoc2ComplianceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    controls = Convert.ToInt32(reader["control_count"] is DBNull ? 0 : reader["control_count"], CultureInfo.InvariantCulture);
                    implemented = Convert.ToInt32(reader["implemented_count"] is DBNull ? 0 : reader["implemented_count"], CultureInfo.InvariantCulture);
                    evidence = Convert.ToInt32(reader["evidence_count"] is DBNull ? 0 : reader["evidence_count"], CultureInfo.InvariantCulture);
                    policies = Convert.ToInt32(reader["policy_count"] is DBNull ? 0 : reader["policy_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSoc2ControlDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSoc2Controls;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSoc2ControlDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["control_id"] is DBNull ? string.Empty : reader["control_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["owner"] is DBNull ? string.Empty : reader["owner"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["frequency"] is DBNull ? string.Empty : reader["frequency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["risk_level"] is DBNull ? string.Empty : reader["risk_level"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpSoc2ComplianceSummary(controls, implemented, evidence, policies, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCostModelsDigestResult> BuildCpCostModelsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCostModelsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var items = 0; var txns = 0; var closes = 0; var models = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCostModelsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    items = Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture);
                    txns = Convert.ToInt32(reader["txn_count"] is DBNull ? 0 : reader["txn_count"], CultureInfo.InvariantCulture);
                    closes = Convert.ToInt32(reader["close_count"] is DBNull ? 0 : reader["close_count"], CultureInfo.InvariantCulture);
                    models = Convert.ToInt32(reader["model_count"] is DBNull ? 0 : reader["model_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCostModelItemDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCostModelItems;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCostModelItemDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["item_id"] is DBNull ? 0 : reader["item_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["model"] is DBNull ? string.Empty : reader["model"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["std_cost"] is DBNull ? 0 : reader["std_cost"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpCostModelsSummary(items, txns, closes, models, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpFinAdvancedDigestResult> BuildCpFinAdvancedDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFinAdvancedSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var periods = 0; var openPeriods = 0; var rules = 0; var accruals = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpFinAdvancedStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    periods = Convert.ToInt32(reader["period_count"] is DBNull ? 0 : reader["period_count"], CultureInfo.InvariantCulture);
                    openPeriods = Convert.ToInt32(reader["open_period_count"] is DBNull ? 0 : reader["open_period_count"], CultureInfo.InvariantCulture);
                    rules = Convert.ToInt32(reader["alloc_rule_count"] is DBNull ? 0 : reader["alloc_rule_count"], CultureInfo.InvariantCulture);
                    accruals = Convert.ToInt32(reader["accrual_count"] is DBNull ? 0 : reader["accrual_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpFinPeriodDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpFinPeriods;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpFinPeriodDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["fy"] is DBNull ? 0 : reader["fy"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["period_no"] is DBNull ? 0 : reader["period_no"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["start_date"] is DBNull ? 0 : reader["start_date"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["end_date"] is DBNull ? 0 : reader["end_date"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpFinAdvancedSummary(periods, openPeriods, rules, accruals, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpBlockchainProofsDigestResult> BuildCpBlockchainProofsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpBlockchainProofsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var proofs = 0; var pending = 0; var anchored = 0; var batches = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpBlockchainProofStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    proofs = Convert.ToInt32(reader["proof_count"] is DBNull ? 0 : reader["proof_count"], CultureInfo.InvariantCulture);
                    pending = Convert.ToInt32(reader["pending_count"] is DBNull ? 0 : reader["pending_count"], CultureInfo.InvariantCulture);
                    anchored = Convert.ToInt32(reader["anchored_count"] is DBNull ? 0 : reader["anchored_count"], CultureInfo.InvariantCulture);
                    batches = Convert.ToInt32(reader["batch_count"] is DBNull ? 0 : reader["batch_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpBlockchainProofDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpBlockchainProofs;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    long? batchId = reader["batch_id"] is DBNull ? null : Convert.ToInt64(reader["batch_id"], CultureInfo.InvariantCulture);
                    rows.Add(new CpBlockchainProofDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["proof_uid"] is DBNull ? string.Empty : reader["proof_uid"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tenant_key"] is DBNull ? string.Empty : reader["tenant_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["record_type"] is DBNull ? string.Empty : reader["record_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["record_id"] is DBNull ? string.Empty : reader["record_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["payload_hash"] is DBNull ? string.Empty : reader["payload_hash"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        batchId,
                        Convert.ToString(reader["anchor_ref"] is DBNull ? string.Empty : reader["anchor_ref"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpBlockchainProofsSummary(proofs, pending, anchored, batches, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpLandedCostDigestResult> BuildCpLandedCostDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpLandedCostSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var sheets = 0; var posted = 0; var expenses = 0; var lines = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpLandedCostStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    sheets = Convert.ToInt32(reader["sheet_count"] is DBNull ? 0 : reader["sheet_count"], CultureInfo.InvariantCulture);
                    posted = Convert.ToInt32(reader["posted_count"] is DBNull ? 0 : reader["posted_count"], CultureInfo.InvariantCulture);
                    expenses = Convert.ToInt32(reader["expense_count"] is DBNull ? 0 : reader["expense_count"], CultureInfo.InvariantCulture);
                    lines = Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpLandedCostSheetDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpLandedCostSheets;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpLandedCostSheetDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["sheet_no"] is DBNull ? string.Empty : reader["sheet_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["po_reference"] is DBNull ? string.Empty : reader["po_reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["grn_reference"] is DBNull ? string.Empty : reader["grn_reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["supplier_id"] is DBNull ? 0 : reader["supplier_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["supplier_name"] is DBNull ? string.Empty : reader["supplier_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["goods_value"] is DBNull ? 0 : reader["goods_value"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["total_expenses"] is DBNull ? 0 : reader["total_expenses"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["distribution_method"] is DBNull ? string.Empty : reader["distribution_method"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpLandedCostSummary(sheets, posted, expenses, lines, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpWarehouseWmsDigestResult> BuildCpWarehouseWmsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpWarehouseWmsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var locations = 0; var lps = 0; var waves = 0; var openWork = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpWarehouseWmsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    locations = Convert.ToInt32(reader["location_count"] is DBNull ? 0 : reader["location_count"], CultureInfo.InvariantCulture);
                    lps = Convert.ToInt32(reader["lp_count"] is DBNull ? 0 : reader["lp_count"], CultureInfo.InvariantCulture);
                    waves = Convert.ToInt32(reader["wave_count"] is DBNull ? 0 : reader["wave_count"], CultureInfo.InvariantCulture);
                    openWork = Convert.ToInt32(reader["open_work_count"] is DBNull ? 0 : reader["open_work_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpWarehouseWmsWorkDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpWarehouseWmsWork;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpWarehouseWmsWorkDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["work_type"] is DBNull ? string.Empty : reader["work_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["wave_id"] is DBNull ? 0 : reader["wave_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["item"] is DBNull ? string.Empty : reader["item"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["qty"] is DBNull ? 0 : reader["qty"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["assigned_to"] is DBNull ? string.Empty : reader["assigned_to"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpWarehouseWmsSummary(locations, lps, waves, openWork, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAiServiceDigestResult> BuildCpAiServiceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAiServiceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var queries = 0; var success = 0; var blocked = 0; var providers = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAiServiceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    queries = Convert.ToInt32(reader["query_count"] is DBNull ? 0 : reader["query_count"], CultureInfo.InvariantCulture);
                    success = Convert.ToInt32(reader["success_count"] is DBNull ? 0 : reader["success_count"], CultureInfo.InvariantCulture);
                    blocked = Convert.ToInt32(reader["blocked_count"] is DBNull ? 0 : reader["blocked_count"], CultureInfo.InvariantCulture);
                    providers = Convert.ToInt32(reader["provider_count"] is DBNull ? 0 : reader["provider_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAiServiceQueryDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAiServiceQueries;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAiServiceQueryDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["service"] is DBNull ? string.Empty : reader["service"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["intent"] is DBNull ? string.Empty : reader["intent"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["tokens_used"] is DBNull ? 0 : reader["tokens_used"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["execution_ms"] is DBNull ? 0 : reader["execution_ms"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["pii_stripped"] is DBNull ? 0 : reader["pii_stripped"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpAiServiceSummary(queries, success, blocked, providers, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpReturnsRmaDigestResult> BuildCpReturnsRmaDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpReturnsRmaSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var rmas = 0; var open = 0; var warranties = 0; var items = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpReturnsRmaStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rmas = Convert.ToInt32(reader["rma_count"] is DBNull ? 0 : reader["rma_count"], CultureInfo.InvariantCulture);
                    open = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    warranties = Convert.ToInt32(reader["active_warranty_count"] is DBNull ? 0 : reader["active_warranty_count"], CultureInfo.InvariantCulture);
                    items = Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpReturnsRmaRequestDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpReturnsRmaRequests;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    long? warrantyId = reader["warranty_id"] is DBNull ? null : Convert.ToInt64(reader["warranty_id"], CultureInfo.InvariantCulture);
                    rows.Add(new CpReturnsRmaRequestDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["rma_number"] is DBNull ? string.Empty : reader["rma_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        warrantyId,
                        Convert.ToInt64(reader["customer_id"] is DBNull ? 0 : reader["customer_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["customer_name"] is DBNull ? string.Empty : reader["customer_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reason"] is DBNull ? string.Empty : reader["reason"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["resolution_type"] is DBNull ? string.Empty : reader["resolution_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpReturnsRmaSummary(rmas, open, warranties, items, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpIsolationAuditDigestResult> BuildCpIsolationAuditDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpIsolationAuditSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var runs = 0; var failedRuns = 0; var violations = 0; var sites = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpIsolationAuditStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    runs = Convert.ToInt32(reader["run_count"] is DBNull ? 0 : reader["run_count"], CultureInfo.InvariantCulture);
                    failedRuns = Convert.ToInt32(reader["failed_run_count"] is DBNull ? 0 : reader["failed_run_count"], CultureInfo.InvariantCulture);
                    violations = Convert.ToInt32(reader["violation_count"] is DBNull ? 0 : reader["violation_count"], CultureInfo.InvariantCulture);
                    sites = Convert.ToInt32(reader["site_count"] is DBNull ? 0 : reader["site_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpIsolationAuditRunDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpIsolationAuditRuns;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpIsolationAuditRunDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["run_at"] is DBNull ? string.Empty : reader["run_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["total_tenants"] is DBNull ? 0 : reader["total_tenants"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["passed"] is DBNull ? 0 : reader["passed"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["failed"] is DBNull ? 0 : reader["failed"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["warnings"] is DBNull ? 0 : reader["warnings"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["triggered_by"] is DBNull ? string.Empty : reader["triggered_by"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpIsolationAuditSummary(runs, failedRuns, violations, sites, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAmlComplianceDigestResult> BuildCpAmlComplianceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAmlComplianceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var kyc = 0; var pending = 0; var flagged = 0; var rules = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAmlComplianceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    kyc = Convert.ToInt32(reader["kyc_count"] is DBNull ? 0 : reader["kyc_count"], CultureInfo.InvariantCulture);
                    pending = Convert.ToInt32(reader["pending_kyc_count"] is DBNull ? 0 : reader["pending_kyc_count"], CultureInfo.InvariantCulture);
                    flagged = Convert.ToInt32(reader["flagged_txn_count"] is DBNull ? 0 : reader["flagged_txn_count"], CultureInfo.InvariantCulture);
                    rules = Convert.ToInt32(reader["active_rule_count"] is DBNull ? 0 : reader["active_rule_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAmlComplianceKycDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAmlComplianceKyc;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAmlComplianceKycDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["customer_id"] is DBNull ? 0 : reader["customer_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["customer_name"] is DBNull ? string.Empty : reader["customer_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["id_type"] is DBNull ? string.Empty : reader["id_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["risk_level"] is DBNull ? string.Empty : reader["risk_level"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["pep_status"] is DBNull ? 0 : reader["pep_status"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["verification_status"] is DBNull ? string.Empty : reader["verification_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpAmlComplianceSummary(kyc, pending, flagged, rules, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpJewelleryMastersDigestResult> BuildCpJewelleryMastersDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpJewelleryMastersSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var karats = 0; var rates = 0; var barcodes = 0; var diamonds = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryMastersStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    karats = Convert.ToInt32(reader["karat_count"] is DBNull ? 0 : reader["karat_count"], CultureInfo.InvariantCulture);
                    rates = Convert.ToInt32(reader["rate_type_count"] is DBNull ? 0 : reader["rate_type_count"], CultureInfo.InvariantCulture);
                    barcodes = Convert.ToInt32(reader["barcode_count"] is DBNull ? 0 : reader["barcode_count"], CultureInfo.InvariantCulture);
                    diamonds = Convert.ToInt32(reader["diamond_count"] is DBNull ? 0 : reader["diamond_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpJewelleryMastersKaratDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryMastersKarats;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpJewelleryMastersKaratDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["karat_code"] is DBNull ? string.Empty : reader["karat_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["std_purity"] is DBNull ? 0 : reader["std_purity"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["range_from"] is DBNull ? 0 : reader["range_from"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["range_to"] is DBNull ? 0 : reader["range_to"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["sp_gravity"] is DBNull ? 0 : reader["sp_gravity"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["division"] is DBNull ? string.Empty : reader["division"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpJewelleryMastersSummary(karats, rates, barcodes, diamonds, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpConsolidationsDigestResult> BuildCpConsolidationsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpConsolidationsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var entities = 0; var figures = 0; var ic = 0; var openIc = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpConsolidationsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entities = Convert.ToInt32(reader["entity_count"] is DBNull ? 0 : reader["entity_count"], CultureInfo.InvariantCulture);
                    figures = Convert.ToInt32(reader["figure_count"] is DBNull ? 0 : reader["figure_count"], CultureInfo.InvariantCulture);
                    ic = Convert.ToInt32(reader["ic_count"] is DBNull ? 0 : reader["ic_count"], CultureInfo.InvariantCulture);
                    openIc = Convert.ToInt32(reader["open_ic_count"] is DBNull ? 0 : reader["open_ic_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpConsolidationsEntityDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpConsolidationsEntities;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpConsolidationsEntityDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency_code"] is DBNull ? string.Empty : reader["currency_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["ownership_pct"] is DBNull ? 0 : reader["ownership_pct"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_home"] is DBNull ? 0 : reader["is_home"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["parent_code"] is DBNull ? string.Empty : reader["parent_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpConsolidationsSummary(entities, figures, ic, openIc, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCrmActivitiesDigestResult> BuildCpCrmActivitiesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCrmActivitiesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var activityCount = 0; var openCount = 0; var overdueCount = 0; var doneCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCrmActivitiesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    activityCount = Convert.ToInt32(reader["activity_count"] is DBNull ? 0 : reader["activity_count"], CultureInfo.InvariantCulture);
                    openCount = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    overdueCount = Convert.ToInt32(reader["overdue_count"] is DBNull ? 0 : reader["overdue_count"], CultureInfo.InvariantCulture);
                    doneCount = Convert.ToInt32(reader["done_count"] is DBNull ? 0 : reader["done_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCrmActivitiesActivityDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCrmActivities;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCrmActivitiesActivityDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["activity_type"] is DBNull ? string.Empty : reader["activity_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["related_type"] is DBNull ? string.Empty : reader["related_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["related_id"] is DBNull ? 0 : reader["related_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["due_date"] is DBNull ? 0 : reader["due_date"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["done"] is DBNull ? 0 : reader["done"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["owner_user_id"] is DBNull ? 0 : reader["owner_user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["active"] is DBNull ? 1 : reader["active"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpCrmActivitiesSummary(activityCount, openCount, overdueCount, doneCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAuthMfaDigestResult> BuildCpAuthMfaDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAuthMfaSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var secretCount = 0; var confirmedCount = 0; var backupUnused = 0; var policyCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAuthMfaStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    secretCount = Convert.ToInt32(reader["secret_count"] is DBNull ? 0 : reader["secret_count"], CultureInfo.InvariantCulture);
                    confirmedCount = Convert.ToInt32(reader["confirmed_count"] is DBNull ? 0 : reader["confirmed_count"], CultureInfo.InvariantCulture);
                    backupUnused = Convert.ToInt32(reader["backup_unused_count"] is DBNull ? 0 : reader["backup_unused_count"], CultureInfo.InvariantCulture);
                    policyCount = Convert.ToInt32(reader["policy_count"] is DBNull ? 0 : reader["policy_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAuthMfaSecretDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAuthMfaSecrets;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAuthMfaSecretDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["method"] is DBNull ? string.Empty : reader["method"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["confirmed"] is DBNull ? 0 : reader["confirmed"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["last_used_at"] is DBNull ? string.Empty : reader["last_used_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpAuthMfaSummary(secretCount, confirmedCount, backupUnused, policyCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpElectronicReportingDigestResult> BuildCpElectronicReportingDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpElectronicReportingSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var formatCount = 0; var fieldCount = 0; var runCount = 0; var outputTypeCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpElectronicReportingStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    formatCount = Convert.ToInt32(reader["format_count"] is DBNull ? 0 : reader["format_count"], CultureInfo.InvariantCulture);
                    fieldCount = Convert.ToInt32(reader["field_count"] is DBNull ? 0 : reader["field_count"], CultureInfo.InvariantCulture);
                    runCount = Convert.ToInt32(reader["run_count"] is DBNull ? 0 : reader["run_count"], CultureInfo.InvariantCulture);
                    outputTypeCount = Convert.ToInt32(reader["output_type_count"] is DBNull ? 0 : reader["output_type_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpElectronicReportingFormatDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpElectronicReportingFormats;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpElectronicReportingFormatDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["output_type"] is DBNull ? string.Empty : reader["output_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["root_element"] is DBNull ? string.Empty : reader["root_element"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["row_element"] is DBNull ? string.Empty : reader["row_element"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpElectronicReportingSummary(formatCount, fieldCount, runCount, outputTypeCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCollectionsDunningDigestResult> BuildCpCollectionsDunningDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCollectionsDunningSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var queueCount = 0; var openCount = 0; var profileCount = 0; var logCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCollectionsDunningStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    queueCount = Convert.ToInt32(reader["queue_count"] is DBNull ? 0 : reader["queue_count"], CultureInfo.InvariantCulture);
                    openCount = Convert.ToInt32(reader["open_count"] is DBNull ? 0 : reader["open_count"], CultureInfo.InvariantCulture);
                    profileCount = Convert.ToInt32(reader["profile_count"] is DBNull ? 0 : reader["profile_count"], CultureInfo.InvariantCulture);
                    logCount = Convert.ToInt32(reader["log_count"] is DBNull ? 0 : reader["log_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCollectionsDunningQueueDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCollectionsDunningQueue;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCollectionsDunningQueueDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["customer_id"] is DBNull ? 0 : reader["customer_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["invoice_ref"] is DBNull ? string.Empty : reader["invoice_ref"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["invoice_amount"] is DBNull ? 0 : reader["invoice_amount"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["amount_due"] is DBNull ? 0 : reader["amount_due"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["due_date"] is DBNull ? string.Empty : reader["due_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["days_overdue"] is DBNull ? 0 : reader["days_overdue"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["dunning_step"] is DBNull ? 0 : reader["dunning_step"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["updated_at"] is DBNull ? string.Empty : reader["updated_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpCollectionsDunningSummary(queueCount, openCount, profileCount, logCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpMarketplaceChannelsDigestResult> BuildCpMarketplaceChannelsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpMarketplaceChannelsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var channelCount = 0; var activeCount = 0; var skuMapCount = 0; var orderCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpMarketplaceChannelsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    channelCount = Convert.ToInt32(reader["channel_count"] is DBNull ? 0 : reader["channel_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    skuMapCount = Convert.ToInt32(reader["sku_map_count"] is DBNull ? 0 : reader["sku_map_count"], CultureInfo.InvariantCulture);
                    orderCount = Convert.ToInt32(reader["order_count"] is DBNull ? 0 : reader["order_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpMarketplaceChannelsChannelDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpMarketplaceChannels;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var code = Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var name = Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var family = "";
                    var region = "";
                    var api = "";
                    var blurb = "";
                    if (CpChannelCatalogs.TryGetMarketplace(code, out var meta))
                    {
                        family = meta.Family;
                        region = meta.Region;
                        api = meta.Api;
                        blurb = meta.Blurb;
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = meta.Name;
                        }
                    }

                    rows.Add(new CpMarketplaceChannelsChannelDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        code,
                        name,
                        Convert.ToString(reader["marketplace_id"] is DBNull ? string.Empty : reader["marketplace_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["demo_mode"] is DBNull ? 0 : reader["demo_mode"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["last_sync_at"] is DBNull ? 0 : reader["last_sync_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        family,
                        region,
                        api,
                        blurb));
                }
            }

            var summary = new CpMarketplaceChannelsSummary(channelCount, activeCount, skuMapCount, orderCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDemandIntelligenceDigestResult> BuildCpDemandIntelligenceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDemandIntelligenceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var countryCount = 0; var articleDemandCount = 0; var priceListDemandCount = 0; var userDemandCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpDemandIntelligenceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    countryCount = Convert.ToInt32(reader["country_count"] is DBNull ? 0 : reader["country_count"], CultureInfo.InvariantCulture);
                    articleDemandCount = Convert.ToInt32(reader["article_demand_count"] is DBNull ? 0 : reader["article_demand_count"], CultureInfo.InvariantCulture);
                    priceListDemandCount = Convert.ToInt32(reader["price_list_demand_count"] is DBNull ? 0 : reader["price_list_demand_count"], CultureInfo.InvariantCulture);
                    userDemandCount = Convert.ToInt32(reader["user_demand_count"] is DBNull ? 0 : reader["user_demand_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpDemandIntelligenceCountryDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDemandIntelligenceCountries;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpDemandIntelligenceCountryDigest(
                        Convert.ToString(reader["code"] is DBNull ? string.Empty : reader["code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpDemandIntelligenceSummary(countryCount, articleDemandCount, priceListDemandCount, userDemandCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCreditLimitsDigestResult> BuildCpCreditLimitsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCreditLimitsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var limitCount = 0; var activeCount = 0; var heldCount = 0; var txnCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCreditLimitsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    limitCount = Convert.ToInt32(reader["limit_count"] is DBNull ? 0 : reader["limit_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    heldCount = Convert.ToInt32(reader["held_count"] is DBNull ? 0 : reader["held_count"], CultureInfo.InvariantCulture);
                    txnCount = Convert.ToInt32(reader["txn_count"] is DBNull ? 0 : reader["txn_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCreditLimitsLimitDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCreditLimits;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCreditLimitsLimitDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["customer_id"] is DBNull ? 0 : reader["customer_id"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["credit_limit"] is DBNull ? 0 : reader["credit_limit"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["balance_used"] is DBNull ? 0 : reader["balance_used"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["risk_score"] is DBNull ? 0 : reader["risk_score"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["payment_terms"] is DBNull ? string.Empty : reader["payment_terms"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["updated_at"] is DBNull ? string.Empty : reader["updated_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpCreditLimitsSummary(limitCount, activeCount, heldCount, txnCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpInsuranceComplianceDigestResult> BuildCpInsuranceComplianceDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpInsuranceComplianceSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var policyCount = 0; var activeCount = 0; var claimCount = 0; var documentCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpInsuranceComplianceStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    policyCount = Convert.ToInt32(reader["policy_count"] is DBNull ? 0 : reader["policy_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    claimCount = Convert.ToInt32(reader["claim_count"] is DBNull ? 0 : reader["claim_count"], CultureInfo.InvariantCulture);
                    documentCount = Convert.ToInt32(reader["document_count"] is DBNull ? 0 : reader["document_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpInsuranceCompliancePolicyDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpInsuranceCompliancePolicies;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpInsuranceCompliancePolicyDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["policy_no"] is DBNull ? string.Empty : reader["policy_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["policy_class"] is DBNull ? string.Empty : reader["policy_class"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["insurer"] is DBNull ? string.Empty : reader["insurer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["sum_insured"] is DBNull ? 0 : reader["sum_insured"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["premium"] is DBNull ? 0 : reader["premium"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["expiry_date"] is DBNull ? 0 : reader["expiry_date"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpInsuranceComplianceSummary(policyCount, activeCount, claimCount, documentCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpAuditTrailDigestResult> BuildCpAuditTrailDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAuditTrailSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var entryCount = 0; var actionCount = 0; var adminCount = 0; var entityTypeCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAuditTrailStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    entryCount = Convert.ToInt32(reader["entry_count"] is DBNull ? 0 : reader["entry_count"], CultureInfo.InvariantCulture);
                    actionCount = Convert.ToInt32(reader["action_count"] is DBNull ? 0 : reader["action_count"], CultureInfo.InvariantCulture);
                    adminCount = Convert.ToInt32(reader["admin_count"] is DBNull ? 0 : reader["admin_count"], CultureInfo.InvariantCulture);
                    entityTypeCount = Convert.ToInt32(reader["entity_type_count"] is DBNull ? 0 : reader["entity_type_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAuditTrailEntryDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAuditTrailEntries;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAuditTrailEntryDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_unix"] is DBNull ? 0 : reader["time_unix"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["admin_id"] is DBNull ? 0 : reader["admin_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["action"] is DBNull ? string.Empty : reader["action"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["entity_type"] is DBNull ? string.Empty : reader["entity_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["entity_id"] is DBNull ? 0 : reader["entity_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["summary"] is DBNull ? string.Empty : reader["summary"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpAuditTrailSummary(entryCount, actionCount, adminCount, entityTypeCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDocExpiryDigestResult> BuildCpDocExpiryDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDocExpirySummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var documentCount = 0; var activeCount = 0; var expiredCount = 0; var reminderCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpDocExpiryStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    documentCount = Convert.ToInt32(reader["document_count"] is DBNull ? 0 : reader["document_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    expiredCount = Convert.ToInt32(reader["expired_count"] is DBNull ? 0 : reader["expired_count"], CultureInfo.InvariantCulture);
                    reminderCount = Convert.ToInt32(reader["reminder_count"] is DBNull ? 0 : reader["reminder_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpDocExpiryDocumentDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDocExpiryDocuments;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpDocExpiryDocumentDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["doc_type"] is DBNull ? string.Empty : reader["doc_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["ref_no"] is DBNull ? string.Empty : reader["ref_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["owner"] is DBNull ? string.Empty : reader["owner"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["issuer"] is DBNull ? string.Empty : reader["issuer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["expiry_date"] is DBNull ? 0 : reader["expiry_date"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["source_module"] is DBNull ? string.Empty : reader["source_module"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpDocExpirySummary(documentCount, activeCount, expiredCount, reminderCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpTenantConfigDigestResult> BuildCpTenantConfigDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpTenantConfigSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var configCount = 0; var groupCount = 0; var editableCount = 0; var historyCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpTenantConfigStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    configCount = Convert.ToInt32(reader["config_count"] is DBNull ? 0 : reader["config_count"], CultureInfo.InvariantCulture);
                    groupCount = Convert.ToInt32(reader["group_count"] is DBNull ? 0 : reader["group_count"], CultureInfo.InvariantCulture);
                    editableCount = Convert.ToInt32(reader["editable_count"] is DBNull ? 0 : reader["editable_count"], CultureInfo.InvariantCulture);
                    historyCount = Convert.ToInt32(reader["history_count"] is DBNull ? 0 : reader["history_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpTenantConfigEntryDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpTenantConfigEntries;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpTenantConfigEntryDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["config_group"] is DBNull ? string.Empty : reader["config_group"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["config_key"] is DBNull ? string.Empty : reader["config_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["value_type"] is DBNull ? string.Empty : reader["value_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["editable"] is DBNull ? 0 : reader["editable"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["updated_by"] is DBNull ? 0 : reader["updated_by"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["updated_at"] is DBNull ? string.Empty : reader["updated_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpTenantConfigSummary(configCount, groupCount, editableCount, historyCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpJewelleryStockVerificationDigestResult> BuildCpJewelleryStockVerificationDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpJewelleryStockVerificationSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var verificationCount = 0; var inProgressCount = 0; var completeCount = 0; var lineCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryStockVerificationStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    verificationCount = Convert.ToInt32(reader["verification_count"] is DBNull ? 0 : reader["verification_count"], CultureInfo.InvariantCulture);
                    inProgressCount = Convert.ToInt32(reader["in_progress_count"] is DBNull ? 0 : reader["in_progress_count"], CultureInfo.InvariantCulture);
                    completeCount = Convert.ToInt32(reader["complete_count"] is DBNull ? 0 : reader["complete_count"], CultureInfo.InvariantCulture);
                    lineCount = Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpJewelleryStockVerificationRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryStockVerificationRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpJewelleryStockVerificationRowDigest(
                        Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["branch"] is DBNull ? string.Empty : reader["branch"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["voc_type"] is DBNull ? string.Empty : reader["voc_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["voc_date"] is DBNull ? string.Empty : reader["voc_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["voc_no"] is DBNull ? 0 : reader["voc_no"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["location"] is DBNull ? string.Empty : reader["location"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["total_pcs"] is DBNull ? 0 : reader["total_pcs"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["scanned_pcs"] is DBNull ? 0 : reader["scanned_pcs"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["remaining_pcs"] is DBNull ? 0 : reader["remaining_pcs"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_by"] is DBNull ? string.Empty : reader["created_by"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpJewelleryStockVerificationSummary(verificationCount, inProgressCount, completeCount, lineCount, "database", string.Empty);
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
    {
        var commerce = 0;
        var erpOnly = 0;
        var demo = 0;
        var platform = 0;
        foreach (var t in tenants)
        {
            switch (ResolveBosTenantType(t))
            {
                case "erp_only":
                    erpOnly++;
                    break;
                case "demo":
                    demo++;
                    break;
                case "platform":
                    platform++;
                    break;
                default:
                    commerce++;
                    break;
            }
        }

        return new(
            tenants.Count,
            tenants.Count(item => item.IsActive),
            adminSessions,
            tenants.Count(item => item.HasDb),
            erpOnly,
            commerce,
            demo,
            platform,
            source,
            message);
    }

    /// <summary>Mirrors PHP <c>epc_bos_resolve_tenant_type</c>.</summary>
    private static string ResolveBosTenantType(PortalTenantDigest t)
    {
        if (t.ErpOnly)
        {
            return "erp_only";
        }

        if (string.Equals(t.Status, "demo", StringComparison.OrdinalIgnoreCase)
            || t.SiteKey.StartsWith("demo_", StringComparison.OrdinalIgnoreCase))
        {
            return "demo";
        }

        if (string.Equals(t.IndustryCode, "platform_host", StringComparison.OrdinalIgnoreCase))
        {
            return "platform";
        }

        if (string.Equals(t.IndustryCode, "erp_standalone", StringComparison.OrdinalIgnoreCase))
        {
            return "erp_only";
        }

        return "commerce";
    }

    private static ErpDashboardSummary EmptyErpSummary(string source, string message)
        => new(
            0, 0, 0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0, "open", 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0,
            source, message);

    /// <summary>Mirrors PHP <c>epc_erp_cc_approval_queue</c> — only emits rows when count &gt; 0.</summary>
    public static IReadOnlyList<ErpApprovalQueueItemDigest> BuildErpApprovalQueue(ErpDashboardSummary s)
    {
        var queue = new List<ErpApprovalQueueItemDigest>(6);
        if (s.DraftSalesOrders > 0)
        {
            var n = s.DraftSalesOrders;
            queue.Add(new(
                "draft_so", "Sales",
                n + " draft sales order" + (n > 1 ? "s" : "") + " awaiting confirmation",
                n, "Open Sales Orders", "/erp/?area=sales&tab=sales_orders", "warning", "fa-file-text"));
        }

        if (s.PendingPurchaseOrders > 0)
        {
            var n = s.PendingPurchaseOrders;
            queue.Add(new(
                "pending_po", "Procurement",
                n + " purchase order" + (n > 1 ? "s" : "") + " pending approval",
                n, "Open Purchase Orders", "/erp/?area=procurement&tab=purchase_orders", "warning", "fa-truck"));
        }

        if (s.UnpostedGlJournals > 0)
        {
            var n = s.UnpostedGlJournals;
            queue.Add(new(
                "unposted_gl", "Finance",
                n + " unposted GL journal" + (n > 1 ? "s" : ""),
                n, "Open General Ledger", "/erp/?area=finance&tab=gl", "info", "fa-book"));
        }

        if (s.OverdueInvoices > 0)
        {
            var n = s.OverdueInvoices;
            queue.Add(new(
                "overdue_invoices", "Finance",
                n + " overdue invoice" + (n > 1 ? "s" : "") + " (30+ days)",
                n, "Open Aging Report", "/erp/?area=finance&tab=aging", "danger", "fa-exclamation-triangle"));
        }

        if (s.LowStockItems > 0)
        {
            var n = s.LowStockItems;
            queue.Add(new(
                "low_stock", "Inventory",
                n + " item" + (n > 1 ? "s" : "") + " at or below reorder level",
                n, "Open Inventory", "/erp/?area=inventory&tab=items", "warning", "fa-archive"));
        }

        if (s.PendingEinvoices > 0)
        {
            var n = s.PendingEinvoices;
            queue.Add(new(
                "pending_einvoice", "Compliance",
                n + " e-invoice" + (n > 1 ? "s" : "") + " pending submission",
                n, "Open E-Invoicing", "/erp/?area=finance&tab=einvoice", "info", "fa-paper-plane"));
        }

        return queue;
    }

    private static BosFleetSummary EmptyBosSummary(string source, string message)
        => new(0, 0, 0, 0, 0, 0, 0, 0, source, message);

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

    private static async Task<decimal> ScalarDecimalParamSafeAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                AddParameter(command, name, value);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToDecimal(result ?? 0m, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0m;
        }
    }

    private static async Task<int> ScalarIntParamSafeAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                AddParameter(command, name, value);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(result ?? 0, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<string> ScalarStringParamSafeAsync(
        DbConnection connection,
        string sql,
        string fallback,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                AddParameter(command, name, value);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var text = Convert.ToString(result, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
        catch
        {
            return fallback;
        }
    }

    private static decimal ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return 0m;
        }

        return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static int ReadInt(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return 0;
        }

        return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task<CpTaxExternalReportingDigestResult> BuildCpTaxExternalReportingDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpTaxExternalReportingSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var ruleCount = 0; var activeCount = 0; var stagingCount = 0; var auditCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpTaxExternalReportingStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ruleCount = Convert.ToInt32(reader["rule_count"] is DBNull ? 0 : reader["rule_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    stagingCount = Convert.ToInt32(reader["staging_count"] is DBNull ? 0 : reader["staging_count"], CultureInfo.InvariantCulture);
                    auditCount = Convert.ToInt32(reader["audit_count"] is DBNull ? 0 : reader["audit_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpTaxExternalReportingRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpTaxExternalReportingRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpTaxExternalReportingRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["country"] is DBNull ? string.Empty : reader["country"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["rule_key"] is DBNull ? string.Empty : reader["rule_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["version"] is DBNull ? 0 : reader["version"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["rule_source"] is DBNull ? string.Empty : reader["rule_source"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["valid_from"] is DBNull ? 0 : reader["valid_from"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["valid_to"] is DBNull ? 0 : reader["valid_to"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpTaxExternalReportingSummary(ruleCount, activeCount, stagingCount, auditCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPoApprovalsDigestResult> BuildCpPoApprovalsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPoApprovalsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var requestCount = 0; var pendingCount = 0; var approvedCount = 0; var stepCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPoApprovalsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    requestCount = Convert.ToInt32(reader["request_count"] is DBNull ? 0 : reader["request_count"], CultureInfo.InvariantCulture);
                    pendingCount = Convert.ToInt32(reader["pending_count"] is DBNull ? 0 : reader["pending_count"], CultureInfo.InvariantCulture);
                    approvedCount = Convert.ToInt32(reader["approved_count"] is DBNull ? 0 : reader["approved_count"], CultureInfo.InvariantCulture);
                    stepCount = Convert.ToInt32(reader["step_count"] is DBNull ? 0 : reader["step_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPoApprovalsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPoApprovalsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPoApprovalsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["po_number"] is DBNull ? string.Empty : reader["po_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["requester_id"] is DBNull ? 0 : reader["requester_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["vendor_name"] is DBNull ? string.Empty : reader["vendor_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["currency"] is DBNull ? string.Empty : reader["currency"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["total"] is DBNull ? 0 : reader["total"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["current_tier"] is DBNull ? 0 : reader["current_tier"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpPoApprovalsSummary(requestCount, pendingCount, approvedCount, stepCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpFinanceCloseDigestResult> BuildCpFinanceCloseDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFinanceCloseSummary(0, 0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var batchCount = 0; var postedBatchCount = 0; var openingLineCount = 0; var periodCount = 0; var closedPeriodCount = 0; var closeLogCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpFinanceCloseStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    batchCount = Convert.ToInt32(reader["batch_count"] is DBNull ? 0 : reader["batch_count"], CultureInfo.InvariantCulture);
                    postedBatchCount = Convert.ToInt32(reader["posted_batch_count"] is DBNull ? 0 : reader["posted_batch_count"], CultureInfo.InvariantCulture);
                    openingLineCount = Convert.ToInt32(reader["opening_line_count"] is DBNull ? 0 : reader["opening_line_count"], CultureInfo.InvariantCulture);
                    periodCount = Convert.ToInt32(reader["period_count"] is DBNull ? 0 : reader["period_count"], CultureInfo.InvariantCulture);
                    closedPeriodCount = Convert.ToInt32(reader["closed_period_count"] is DBNull ? 0 : reader["closed_period_count"], CultureInfo.InvariantCulture);
                    closeLogCount = Convert.ToInt32(reader["close_log_count"] is DBNull ? 0 : reader["close_log_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpFinanceCloseRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpFinanceCloseRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpFinanceCloseRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["module"] is DBNull ? string.Empty : reader["module"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["as_of_date"] is DBNull ? string.Empty : reader["as_of_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["reference"] is DBNull ? string.Empty : reader["reference"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["admin_id"] is DBNull ? 0 : reader["admin_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_posted"] is DBNull ? 0 : reader["time_posted"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpFinanceCloseSummary(batchCount, postedBatchCount, openingLineCount, periodCount, closedPeriodCount, closeLogCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpJewelleryFixingDigestResult> BuildCpJewelleryFixingDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpJewelleryFixingSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var fixingCount = 0; var openFixingCount = 0; var purchaseFixCount = 0; var settlementCount = 0; var pettyCashCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryFixingStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    fixingCount = Convert.ToInt32(reader["fixing_count"] is DBNull ? 0 : reader["fixing_count"], CultureInfo.InvariantCulture);
                    openFixingCount = Convert.ToInt32(reader["open_fixing_count"] is DBNull ? 0 : reader["open_fixing_count"], CultureInfo.InvariantCulture);
                    purchaseFixCount = Convert.ToInt32(reader["purchase_fix_count"] is DBNull ? 0 : reader["purchase_fix_count"], CultureInfo.InvariantCulture);
                    settlementCount = Convert.ToInt32(reader["settlement_count"] is DBNull ? 0 : reader["settlement_count"], CultureInfo.InvariantCulture);
                    pettyCashCount = Convert.ToInt32(reader["petty_cash_count"] is DBNull ? 0 : reader["petty_cash_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpJewelleryFixingRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpJewelleryFixingRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpJewelleryFixingRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["branch"] is DBNull ? string.Empty : reader["branch"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["fix_type"] is DBNull ? string.Empty : reader["fix_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["fix_date"] is DBNull ? string.Empty : reader["fix_date"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["fix_no"] is DBNull ? 0 : reader["fix_no"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["party_code"] is DBNull ? string.Empty : reader["party_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["party_name"] is DBNull ? string.Empty : reader["party_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["metal"] is DBNull ? string.Empty : reader["metal"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["karat"] is DBNull ? string.Empty : reader["karat"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["fix_qty_gms"] is DBNull ? 0 : reader["fix_qty_gms"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["fix_amount"] is DBNull ? 0 : reader["fix_amount"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_by"] is DBNull ? string.Empty : reader["created_by"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpJewelleryFixingSummary(fixingCount, openFixingCount, purchaseFixCount, settlementCount, pettyCashCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpWebTrackerDigestResult> BuildCpWebTrackerDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpWebTrackerSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var sessionCount = 0; var pageviewCount = 0; var eventCount = 0; var countryCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpWebTrackerStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    sessionCount = Convert.ToInt32(reader["session_count"] is DBNull ? 0 : reader["session_count"], CultureInfo.InvariantCulture);
                    pageviewCount = Convert.ToInt32(reader["pageview_count"] is DBNull ? 0 : reader["pageview_count"], CultureInfo.InvariantCulture);
                    eventCount = Convert.ToInt32(reader["event_count"] is DBNull ? 0 : reader["event_count"], CultureInfo.InvariantCulture);
                    countryCount = Convert.ToInt32(reader["country_count"] is DBNull ? 0 : reader["country_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpWebTrackerRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpWebTrackerRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpWebTrackerRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["session_uid"] is DBNull ? string.Empty : reader["session_uid"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["pageview_count"] is DBNull ? 0 : reader["pageview_count"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["event_count"] is DBNull ? 0 : reader["event_count"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["country_code"] is DBNull ? string.Empty : reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["device_type"] is DBNull ? string.Empty : reader["device_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["browser"] is DBNull ? string.Empty : reader["browser"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["first_seen_at"] is DBNull ? 0 : reader["first_seen_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["last_seen_at"] is DBNull ? 0 : reader["last_seen_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpWebTrackerSummary(sessionCount, pageviewCount, eventCount, countryCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAbandonedCartsDigestResult> BuildCpAbandonedCartsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAbandonedCartsSummary(0, 0, 0, 0, 0, 0m, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var lineCount = 0; var guestLineCount = 0; var userLineCount = 0; var guestSessionCount = 0; var userCartCount = 0;
            var cartSum = 0m;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAbandonedCartsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    lineCount = Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture);
                    guestLineCount = Convert.ToInt32(reader["guest_line_count"] is DBNull ? 0 : reader["guest_line_count"], CultureInfo.InvariantCulture);
                    userLineCount = Convert.ToInt32(reader["user_line_count"] is DBNull ? 0 : reader["user_line_count"], CultureInfo.InvariantCulture);
                    guestSessionCount = Convert.ToInt32(reader["guest_session_count"] is DBNull ? 0 : reader["guest_session_count"], CultureInfo.InvariantCulture);
                    userCartCount = Convert.ToInt32(reader["user_cart_count"] is DBNull ? 0 : reader["user_cart_count"], CultureInfo.InvariantCulture);
                    cartSum = Convert.ToDecimal(reader["cart_sum"] is DBNull ? 0 : reader["cart_sum"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAbandonedCartsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAbandonedCartsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAbandonedCartsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["session_id"] is DBNull ? 0 : reader["session_id"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["price"] is DBNull ? 0 : reader["price"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["count_need"] is DBNull ? 0 : reader["count_need"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["checked_for_order"] is DBNull ? 0 : reader["checked_for_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["product_type"] is DBNull ? 0 : reader["product_type"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time"] is DBNull ? 0 : reader["time"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["price_sum"] is DBNull ? 0 : reader["price_sum"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpAbandonedCartsSummary(lineCount, guestLineCount, userLineCount, guestSessionCount, userCartCount, cartSum, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpQuoteRequestsDigestResult> BuildCpQuoteRequestsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpQuoteRequestsSummary(0, 0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var quoteCount = 0; var draftCount = 0; var submittedCount = 0; var quotedCount = 0; var acceptedCount = 0; var itemCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpQuoteRequestsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    quoteCount = Convert.ToInt32(reader["quote_count"] is DBNull ? 0 : reader["quote_count"], CultureInfo.InvariantCulture);
                    draftCount = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    submittedCount = Convert.ToInt32(reader["submitted_count"] is DBNull ? 0 : reader["submitted_count"], CultureInfo.InvariantCulture);
                    quotedCount = Convert.ToInt32(reader["quoted_count"] is DBNull ? 0 : reader["quoted_count"], CultureInfo.InvariantCulture);
                    acceptedCount = Convert.ToInt32(reader["accepted_count"] is DBNull ? 0 : reader["accepted_count"], CultureInfo.InvariantCulture);
                    itemCount = Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpQuoteRequestsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpQuoteRequestsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpQuoteRequestsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["session_id"] is DBNull ? 0 : reader["session_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_submitted"] is DBNull ? 0 : reader["time_submitted"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["accepted_order_id"] is DBNull ? 0 : reader["accepted_order_id"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpQuoteRequestsSummary(quoteCount, draftCount, submittedCount, quotedCount, acceptedCount, itemCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPlatformCommunicationDigestResult> BuildCpPlatformCommunicationDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPlatformCommunicationSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            var settingCount = 0; var taskCount = 0; var openTaskCount = 0; var highPriorityCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPlatformCommunicationStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    settingCount = Convert.ToInt32(reader["setting_count"] is DBNull ? 0 : reader["setting_count"], CultureInfo.InvariantCulture);
                    taskCount = Convert.ToInt32(reader["task_count"] is DBNull ? 0 : reader["task_count"], CultureInfo.InvariantCulture);
                    openTaskCount = Convert.ToInt32(reader["open_task_count"] is DBNull ? 0 : reader["open_task_count"], CultureInfo.InvariantCulture);
                    highPriorityCount = Convert.ToInt32(reader["high_priority_count"] is DBNull ? 0 : reader["high_priority_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPlatformCommunicationRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPlatformCommunicationRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPlatformCommunicationRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["assigned_to"] is DBNull ? 0 : reader["assigned_to"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["due_at"] is DBNull ? 0 : reader["due_at"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["created_at"] is DBNull ? 0 : reader["created_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPlatformCommunicationSummary(settingCount, taskCount, openTaskCount, highPriorityCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpInfoBlocksDigestResult> BuildCpInfoBlocksDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpInfoBlocksSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            var blockCount = 0; var activeCount = 0; var placementCount = 0; var localeCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpInfoBlocksStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    blockCount = Convert.ToInt32(reader["block_count"] is DBNull ? 0 : reader["block_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    placementCount = Convert.ToInt32(reader["placement_count"] is DBNull ? 0 : reader["placement_count"], CultureInfo.InvariantCulture);
                    localeCount = Convert.ToInt32(reader["locale_count"] is DBNull ? 0 : reader["locale_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpInfoBlocksRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpInfoBlocksRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpInfoBlocksRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["block_key"] is DBNull ? string.Empty : reader["block_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["scope"] is DBNull ? string.Empty : reader["scope"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["placement"] is DBNull ? string.Empty : reader["placement"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["locale"] is DBNull ? string.Empty : reader["locale"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpInfoBlocksSummary(blockCount, activeCount, placementCount, localeCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpFreeToolsDigestResult> BuildCpFreeToolsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFreeToolsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            var accountCount = 0; var saveCount = 0; var settingCount = 0; var activeAccountCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpFreeToolsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    accountCount = Convert.ToInt32(reader["account_count"] is DBNull ? 0 : reader["account_count"], CultureInfo.InvariantCulture);
                    saveCount = Convert.ToInt32(reader["save_count"] is DBNull ? 0 : reader["save_count"], CultureInfo.InvariantCulture);
                    settingCount = Convert.ToInt32(reader["setting_count"] is DBNull ? 0 : reader["setting_count"], CultureInfo.InvariantCulture);
                    activeAccountCount = Convert.ToInt32(reader["active_account_count"] is DBNull ? 0 : reader["active_account_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpFreeToolsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpFreeToolsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpFreeToolsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["company"] is DBNull ? string.Empty : reader["company"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["country"] is DBNull ? string.Empty : reader["country"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["use_count"] is DBNull ? 0 : reader["use_count"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["login_count"] is DBNull ? 0 : reader["login_count"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_last_seen"] is DBNull ? 0 : reader["time_last_seen"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpFreeToolsSummary(accountCount, saveCount, settingCount, activeAccountCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpConfigSandboxDigestResult> BuildCpConfigSandboxDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpConfigSandboxSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var snapshotCount = 0; var activeSnapshotCount = 0; var promotedSnapshotCount = 0; var changeCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpConfigSandboxStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    snapshotCount = Convert.ToInt32(reader["snapshot_count"] is DBNull ? 0 : reader["snapshot_count"], CultureInfo.InvariantCulture);
                    activeSnapshotCount = Convert.ToInt32(reader["active_snapshot_count"] is DBNull ? 0 : reader["active_snapshot_count"], CultureInfo.InvariantCulture);
                    promotedSnapshotCount = Convert.ToInt32(reader["promoted_snapshot_count"] is DBNull ? 0 : reader["promoted_snapshot_count"], CultureInfo.InvariantCulture);
                    changeCount = Convert.ToInt32(reader["change_count"] is DBNull ? 0 : reader["change_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpConfigSandboxRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpConfigSandboxRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpConfigSandboxRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["snapshot_name"] is DBNull ? string.Empty : reader["snapshot_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["created_by"] is DBNull ? 0 : reader["created_by"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["promoted_at"] is DBNull ? string.Empty : reader["promoted_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpConfigSandboxSummary(snapshotCount, activeSnapshotCount, promotedSnapshotCount, changeCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpMarketplaceAppsDigestResult> BuildCpMarketplaceAppsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpMarketplaceAppsSummary(0, 0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var appCount = 0; var publishedCount = 0; var installCount = 0; var activeInstallCount = 0; var reviewCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpMarketplaceAppsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    appCount = Convert.ToInt32(reader["app_count"] is DBNull ? 0 : reader["app_count"], CultureInfo.InvariantCulture);
                    publishedCount = Convert.ToInt32(reader["published_count"] is DBNull ? 0 : reader["published_count"], CultureInfo.InvariantCulture);
                    installCount = Convert.ToInt32(reader["install_count"] is DBNull ? 0 : reader["install_count"], CultureInfo.InvariantCulture);
                    activeInstallCount = Convert.ToInt32(reader["active_install_count"] is DBNull ? 0 : reader["active_install_count"], CultureInfo.InvariantCulture);
                    reviewCount = Convert.ToInt32(reader["review_count"] is DBNull ? 0 : reader["review_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpMarketplaceAppsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpMarketplaceAppsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpMarketplaceAppsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["app_key"] is DBNull ? string.Empty : reader["app_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["short_desc"] is DBNull ? string.Empty : reader["short_desc"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["developer"] is DBNull ? string.Empty : reader["developer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["version"] is DBNull ? string.Empty : reader["version"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["pricing"] is DBNull ? string.Empty : reader["pricing"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["price_monthly"] is DBNull ? 0 : reader["price_monthly"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["downloads"] is DBNull ? 0 : reader["downloads"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["avg_rating"] is DBNull ? 0 : reader["avg_rating"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["review_count"] is DBNull ? 0 : reader["review_count"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["published_at"] is DBNull ? string.Empty : reader["published_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpMarketplaceAppsSummary(appCount, publishedCount, installCount, activeInstallCount, reviewCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpNotificationsDigestResult> BuildCpNotificationsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpNotificationsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var notificationCount = 0; var unreadCount = 0; var prefCount = 0; var channelCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpNotificationsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    notificationCount = Convert.ToInt32(reader["notification_count"] is DBNull ? 0 : reader["notification_count"], CultureInfo.InvariantCulture);
                    unreadCount = Convert.ToInt32(reader["unread_count"] is DBNull ? 0 : reader["unread_count"], CultureInfo.InvariantCulture);
                    prefCount = Convert.ToInt32(reader["pref_count"] is DBNull ? 0 : reader["pref_count"], CultureInfo.InvariantCulture);
                    channelCount = Convert.ToInt32(reader["channel_count"] is DBNull ? 0 : reader["channel_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpNotificationsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpNotificationsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpNotificationsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["tenant_key"] is DBNull ? string.Empty : reader["tenant_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["channel"] is DBNull ? string.Empty : reader["channel"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["category"] is DBNull ? string.Empty : reader["category"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["severity"] is DBNull ? string.Empty : reader["severity"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["is_read"] is DBNull ? 0 : reader["is_read"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpNotificationsSummary(notificationCount, unreadCount, prefCount, channelCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPortalSettingsDigestResult> BuildCpPortalSettingsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPortalSettingsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            var siteCount = 0; var industryCount = 0; var accessModeCount = 0; var deployTargetCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPortalSettingsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    siteCount = Convert.ToInt32(reader["site_count"] is DBNull ? 0 : reader["site_count"], CultureInfo.InvariantCulture);
                    industryCount = Convert.ToInt32(reader["industry_count"] is DBNull ? 0 : reader["industry_count"], CultureInfo.InvariantCulture);
                    accessModeCount = Convert.ToInt32(reader["access_mode_count"] is DBNull ? 0 : reader["access_mode_count"], CultureInfo.InvariantCulture);
                    deployTargetCount = Convert.ToInt32(reader["deploy_target_count"] is DBNull ? 0 : reader["deploy_target_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPortalSettingsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPortalSettingsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPortalSettingsRowDigest(
                        Convert.ToString(reader["host"] is DBNull ? string.Empty : reader["host"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["industry_code"] is DBNull ? string.Empty : reader["industry_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["system_name"] is DBNull ? string.Empty : reader["system_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["hub_name"] is DBNull ? string.Empty : reader["hub_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tagline"] is DBNull ? string.Empty : reader["tagline"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["domain_path"] is DBNull ? string.Empty : reader["domain_path"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["theme_template"] is DBNull ? string.Empty : reader["theme_template"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["access_mode"] is DBNull ? string.Empty : reader["access_mode"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["cp_default_lang"] is DBNull ? string.Empty : reader["cp_default_lang"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["country_code"] is DBNull ? string.Empty : reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpPortalSettingsSummary(siteCount, industryCount, accessModeCount, deployTargetCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDataMigrationsDigestResult> BuildCpDataMigrationsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDataMigrationsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var migrationCount = 0; var completedCount = 0; var failedCount = 0; var rowCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpDataMigrationsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    migrationCount = Convert.ToInt32(reader["migration_count"] is DBNull ? 0 : reader["migration_count"], CultureInfo.InvariantCulture);
                    completedCount = Convert.ToInt32(reader["completed_count"] is DBNull ? 0 : reader["completed_count"], CultureInfo.InvariantCulture);
                    failedCount = Convert.ToInt32(reader["failed_count"] is DBNull ? 0 : reader["failed_count"], CultureInfo.InvariantCulture);
                    rowCount = Convert.ToInt32(reader["row_count"] is DBNull ? 0 : reader["row_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpDataMigrationsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDataMigrationsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpDataMigrationsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["company_id"] is DBNull ? 0 : reader["company_id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["migration_type"] is DBNull ? string.Empty : reader["migration_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["entity_type"] is DBNull ? string.Empty : reader["entity_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["file_name"] is DBNull ? string.Empty : reader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["total_rows"] is DBNull ? 0 : reader["total_rows"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["valid_rows"] is DBNull ? 0 : reader["valid_rows"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["error_rows"] is DBNull ? 0 : reader["error_rows"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["imported_rows"] is DBNull ? 0 : reader["imported_rows"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["imported_by_name"] is DBNull ? string.Empty : reader["imported_by_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_completed"] is DBNull ? 0 : reader["time_completed"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpDataMigrationsSummary(migrationCount, completedCount, failedCount, rowCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }
    public async Task<CpGeoRegionsDigestResult> BuildCpGeoRegionsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpGeoRegionsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var nodeCount = 0; var level1Count = 0; var level2Count = 0; var mappedOfficeCount = 0;
            nodeCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpGeoRegionsNodeCount, cancellationToken).ConfigureAwait(false);
            level1Count = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpGeoRegionsLevel1Count, cancellationToken).ConfigureAwait(false);
            level2Count = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpGeoRegionsLevel2Count, cancellationToken).ConfigureAwait(false);
            mappedOfficeCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpGeoRegionsMappedOfficeCount, cancellationToken).ConfigureAwait(false);

            var rows = new List<CpGeoRegionsRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpGeoRegionsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpGeoRegionsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["level"] is DBNull ? 0 : reader["level"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["parent"] is DBNull ? 0 : reader["parent"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["child_count"] is DBNull ? 0 : reader["child_count"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["value_lang_id"] is DBNull ? 0 : reader["value_lang_id"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpGeoRegionsSummary(nodeCount, level1Count, level2Count, mappedOfficeCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpProductFiltersDigestResult> BuildCpProductFiltersDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpProductFiltersSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var filterCount = 0; var withStorageScope = 0; var withPriceBand = 0; var withTimeBand = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpProductFiltersStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    filterCount = Convert.ToInt32(reader["filter_count"] is DBNull ? 0 : reader["filter_count"], CultureInfo.InvariantCulture);
                    withStorageScope = Convert.ToInt32(reader["with_storage_scope"] is DBNull ? 0 : reader["with_storage_scope"], CultureInfo.InvariantCulture);
                    withPriceBand = Convert.ToInt32(reader["with_price_band"] is DBNull ? 0 : reader["with_price_band"], CultureInfo.InvariantCulture);
                    withTimeBand = Convert.ToInt32(reader["with_time_band"] is DBNull ? 0 : reader["with_time_band"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpProductFiltersRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpProductFiltersRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpProductFiltersRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["min_price"] is DBNull ? 0 : reader["min_price"], CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader["max_price"] is DBNull ? 0 : reader["max_price"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["min_time"] is DBNull ? 0 : reader["min_time"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["max_time"] is DBNull ? 0 : reader["max_time"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpProductFiltersSummary(filterCount, withStorageScope, withPriceBand, withTimeBand, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSearchTabsDigestResult> BuildCpSearchTabsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSearchTabsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var tabCount = 0; var enabledCount = 0; var disabledCount = 0; var maxOrder = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSearchTabsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    tabCount = Convert.ToInt32(reader["tab_count"] is DBNull ? 0 : reader["tab_count"], CultureInfo.InvariantCulture);
                    enabledCount = Convert.ToInt32(reader["enabled_count"] is DBNull ? 0 : reader["enabled_count"], CultureInfo.InvariantCulture);
                    disabledCount = Convert.ToInt32(reader["disabled_count"] is DBNull ? 0 : reader["disabled_count"], CultureInfo.InvariantCulture);
                    maxOrder = Convert.ToInt32(reader["max_order"] is DBNull ? 0 : reader["max_order"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSearchTabsRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSearchTabsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSearchTabsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["enabled"] is DBNull ? 0 : reader["enabled"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpSearchTabsSummary(tabCount, enabledCount, disabledCount, maxOrder, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSystemRequestsDigestResult> BuildCpSystemRequestsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSystemRequestsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var requestCount = 0; var unviewedCount = 0; var viewedCount = 0; var withUserCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSystemRequestsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    requestCount = Convert.ToInt32(reader["request_count"] is DBNull ? 0 : reader["request_count"], CultureInfo.InvariantCulture);
                    unviewedCount = Convert.ToInt32(reader["unviewed_count"] is DBNull ? 0 : reader["unviewed_count"], CultureInfo.InvariantCulture);
                    viewedCount = Convert.ToInt32(reader["viewed_count"] is DBNull ? 0 : reader["viewed_count"], CultureInfo.InvariantCulture);
                    withUserCount = Convert.ToInt32(reader["with_user_count"] is DBNull ? 0 : reader["with_user_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSystemRequestsRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSystemRequestsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSystemRequestsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_unix"] is DBNull ? 0 : reader["time_unix"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["viewed"] is DBNull ? 0 : reader["viewed"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpSystemRequestsSummary(requestCount, unviewedCount, viewedCount, withUserCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpAdditionalTextsDigestResult> BuildCpAdditionalTextsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAdditionalTextsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var textCount = 0; var beforeMainCount = 0; var withTitleCount = 0; var withDescriptionCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAdditionalTextsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    textCount = Convert.ToInt32(reader["text_count"] is DBNull ? 0 : reader["text_count"], CultureInfo.InvariantCulture);
                    beforeMainCount = Convert.ToInt32(reader["before_main_count"] is DBNull ? 0 : reader["before_main_count"], CultureInfo.InvariantCulture);
                    withTitleCount = Convert.ToInt32(reader["with_title_count"] is DBNull ? 0 : reader["with_title_count"], CultureInfo.InvariantCulture);
                    withDescriptionCount = Convert.ToInt32(reader["with_description_count"] is DBNull ? 0 : reader["with_description_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAdditionalTextsRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAdditionalTextsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAdditionalTextsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["url"] is DBNull ? string.Empty : reader["url"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["before_main"] is DBNull ? 0 : reader["before_main"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title_tag"] is DBNull ? string.Empty : reader["title_tag"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["keywords_tag"] is DBNull ? string.Empty : reader["keywords_tag"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpAdditionalTextsSummary(textCount, beforeMainCount, withTitleCount, withDescriptionCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSliderBannersDigestResult> BuildCpSliderBannersDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSliderBannersSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var imageCount = 0; var connected = 0; var cntImg = 0; var cntImgNext = 0;
            imageCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSliderBannersImageCount, cancellationToken).ConfigureAwait(false);
            connected = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSliderBannersConnected, cancellationToken).ConfigureAwait(false);
            cntImg = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSliderBannersCntImg, cancellationToken).ConfigureAwait(false);
            cntImgNext = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSliderBannersCntImgNext, cancellationToken).ConfigureAwait(false);

            var rows = new List<CpSliderBannersRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSliderBannersRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSliderBannersRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["link"] is DBNull ? string.Empty : reader["link"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["href"] is DBNull ? string.Empty : reader["href"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpSliderBannersSummary(imageCount, connected, cntImg, cntImgNext, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpStructureDumpsDigestResult> BuildCpStructureDumpsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpStructureDumpsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var dumpCount = 0; var totalRecords = 0; var latestTimeCreated = 0; var withFileCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpStructureDumpsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    dumpCount = Convert.ToInt32(reader["dump_count"] is DBNull ? 0 : reader["dump_count"], CultureInfo.InvariantCulture);
                    totalRecords = Convert.ToInt32(reader["total_records"] is DBNull ? 0 : reader["total_records"], CultureInfo.InvariantCulture);
                    latestTimeCreated = Convert.ToInt32(reader["latest_time_created"] is DBNull ? 0 : reader["latest_time_created"], CultureInfo.InvariantCulture);
                    withFileCount = Convert.ToInt32(reader["with_file_count"] is DBNull ? 0 : reader["with_file_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpStructureDumpsRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpStructureDumpsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpStructureDumpsRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["fields_in_dump"] is DBNull ? string.Empty : reader["fields_in_dump"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["file_name"] is DBNull ? string.Empty : reader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["records_count"] is DBNull ? 0 : reader["records_count"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpStructureDumpsSummary(dumpCount, totalRecords, latestTimeCreated, withFileCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpCommunicationsTestDigestResult> BuildCpCommunicationsTestDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCommunicationsTestSummary(0, 0, "", "", "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var smsActiveCount = 0; var smsTotalCount = 0; var emailLastStatus = ""; var smsLastStatus = "";
            smsActiveCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpCommunicationsTestSmsActiveCount, cancellationToken).ConfigureAwait(false);
            smsTotalCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpCommunicationsTestSmsTotalCount, cancellationToken).ConfigureAwait(false);
            try
            {
                await using var st = connection.CreateCommand();
                st.CommandText = LegacySurfaceDashboardSql.SelectCpCommunicationsTestEmailLastStatus;
                var sv = await st.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                emailLastStatus = Convert.ToString(sv is DBNull or null ? string.Empty : sv, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch { emailLastStatus = ""; }
            try
            {
                await using var st = connection.CreateCommand();
                st.CommandText = LegacySurfaceDashboardSql.SelectCpCommunicationsTestSmsLastStatus;
                var sv = await st.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                smsLastStatus = Convert.ToString(sv is DBNull or null ? string.Empty : sv, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch { smsLastStatus = ""; }

            var rows = new List<CpCommunicationsTestRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCommunicationsTestRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCommunicationsTestRowDigest(
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_selectable"] is DBNull ? 0 : reader["is_selectable"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["handler"] is DBNull ? string.Empty : reader["handler"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpCommunicationsTestSummary(smsActiveCount, smsTotalCount, emailLastStatus, smsLastStatus, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpLanguagesDigestResult> BuildCpLanguagesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpLanguagesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var languageCount = 0; var activeCount = 0; var defaultCount = 0; var inactiveCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpLanguagesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    languageCount = Convert.ToInt32(reader["language_count"] is DBNull ? 0 : reader["language_count"], CultureInfo.InvariantCulture);
                    activeCount = Convert.ToInt32(reader["active_count"] is DBNull ? 0 : reader["active_count"], CultureInfo.InvariantCulture);
                    defaultCount = Convert.ToInt32(reader["default_count"] is DBNull ? 0 : reader["default_count"], CultureInfo.InvariantCulture);
                    inactiveCount = Convert.ToInt32(reader["inactive_count"] is DBNull ? 0 : reader["inactive_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpLanguagesRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpLanguagesRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpLanguagesRowDigest(
                        Convert.ToString(reader["lang_code"] is DBNull ? string.Empty : reader["lang_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_default"] is DBNull ? 0 : reader["is_default"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpLanguagesSummary(languageCount, activeCount, defaultCount, inactiveCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpPluginsManagerDigestResult> BuildCpPluginsManagerDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpPluginsManagerSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var pluginCount = 0; var activatedCount = 0; var frontendCount = 0; var lockedCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpPluginsManagerStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    pluginCount = Convert.ToInt32(reader["plugin_count"] is DBNull ? 0 : reader["plugin_count"], CultureInfo.InvariantCulture);
                    activatedCount = Convert.ToInt32(reader["activated_count"] is DBNull ? 0 : reader["activated_count"], CultureInfo.InvariantCulture);
                    frontendCount = Convert.ToInt32(reader["frontend_count"] is DBNull ? 0 : reader["frontend_count"], CultureInfo.InvariantCulture);
                    lockedCount = Convert.ToInt32(reader["locked_count"] is DBNull ? 0 : reader["locked_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpPluginsManagerRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpPluginsManagerRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpPluginsManagerRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["activated"] is DBNull ? 0 : reader["activated"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["control_lock"] is DBNull ? 0 : reader["control_lock"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpPluginsManagerSummary(pluginCount, activatedCount, frontendCount, lockedCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpTemplatesManagerDigestResult> BuildCpTemplatesManagerDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpTemplatesManagerSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var templateCount = 0; var frontendCount = 0; var currentFrontendCount = 0; var currentBackendCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpTemplatesManagerStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    templateCount = Convert.ToInt32(reader["template_count"] is DBNull ? 0 : reader["template_count"], CultureInfo.InvariantCulture);
                    frontendCount = Convert.ToInt32(reader["frontend_count"] is DBNull ? 0 : reader["frontend_count"], CultureInfo.InvariantCulture);
                    currentFrontendCount = Convert.ToInt32(reader["current_frontend_count"] is DBNull ? 0 : reader["current_frontend_count"], CultureInfo.InvariantCulture);
                    currentBackendCount = Convert.ToInt32(reader["current_backend_count"] is DBNull ? 0 : reader["current_backend_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpTemplatesManagerRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpTemplatesManagerRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpTemplatesManagerRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["current_flag"] is DBNull ? 0 : reader["current_flag"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["phone_support"] is DBNull ? 0 : reader["phone_support"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["tablet_support"] is DBNull ? 0 : reader["tablet_support"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpTemplatesManagerSummary(templateCount, frontendCount, currentFrontendCount, currentBackendCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpDesignTokensDigestResult> BuildCpDesignTokensDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDesignTokensSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var tokenCount = 0; var tenantCount = 0; var whiteLabelCount = 0; var updatedRecentCount = 0;
            tokenCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpDesignTokensTokenCount, cancellationToken).ConfigureAwait(false);
            tenantCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpDesignTokensTenantCount, cancellationToken).ConfigureAwait(false);
            whiteLabelCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpDesignTokensWhiteLabelCount, cancellationToken).ConfigureAwait(false);
            updatedRecentCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpDesignTokensUpdatedRecentCount, cancellationToken).ConfigureAwait(false);

            var rows = new List<CpDesignTokensRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpDesignTokensRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpDesignTokensRowDigest(
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["setting_key"] is DBNull ? string.Empty : reader["setting_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["updated_at"] is DBNull ? string.Empty : reader["updated_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpDesignTokensSummary(tokenCount, tenantCount, whiteLabelCount, updatedRecentCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpSitemapDigestResult> BuildCpSitemapDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSitemapSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var contentUrlCount = 0; var categoryCount = 0; var productCount = 0; var frontendContentCount = 0;
            contentUrlCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSitemapContentUrlCount, cancellationToken).ConfigureAwait(false);
            categoryCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSitemapCategoryCount, cancellationToken).ConfigureAwait(false);
            productCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSitemapProductCount, cancellationToken).ConfigureAwait(false);
            frontendContentCount = await ScalarIntSafeAsync(connection, LegacySurfaceDashboardSql.CountCpSitemapFrontendContentCount, cancellationToken).ConfigureAwait(false);

            var rows = new List<CpSitemapRowDigest>();
            try
            {
                await using var list = connection.CreateCommand();
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSitemapRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSitemapRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["alias"] is DBNull ? string.Empty : reader["alias"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["value_lang_id"] is DBNull ? 0 : reader["value_lang_id"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["is_frontend"] is DBNull ? 0 : reader["is_frontend"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["published_flag"] is DBNull ? 0 : reader["published_flag"], CultureInfo.InvariantCulture)));
                }
            }
            catch
            {
                // Row list is best-effort when schema drifts; KPIs may still be useful.
            }

            var summary = new CpSitemapSummary(contentUrlCount, categoryCount, productCount, frontendContentCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<CpFailoverStatusDigestResult> BuildCpFailoverStatusDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFailoverStatusSummary(0, 0, 0, 0, "migration", "Failover status is filesystem-based.");
        try
        {
            // Prefer content root / repo root style paths used by PHP failover helpers.
            var roots = new[]
            {
                Directory.GetCurrentDirectory(),
                Path.Combine(Directory.GetCurrentDirectory(), ".."),
                Path.Combine(Directory.GetCurrentDirectory(), "../.."),
                "/workspace",
                "/var/www",
            };
            string? Find(string name)
            {
                foreach (var root in roots)
                {
                    try
                    {
                        var p = Path.GetFullPath(Path.Combine(root, name));
                        if (File.Exists(p)) return p;
                        var alt = Path.GetFullPath(Path.Combine(root, "var", name));
                        if (File.Exists(alt)) return alt;
                    }
                    catch { /* ignore */ }
                }
                return null;
            }

            var modePath = Find("epc-platform-status.mode");
            var statusPath = Find("epc-platform-status.json");
            var configPath = Find("epc-platform-failover.config.json");
            var modeFilePresent = modePath is null ? 0 : 1;
            var statusJsonPresent = statusPath is null ? 0 : 1;
            var configPresent = configPath is null ? 0 : 1;
            var backupMode = 0;
            if (modePath is not null)
            {
                try
                {
                    var mode = (await File.ReadAllTextAsync(modePath, cancellationToken).ConfigureAwait(false)).Trim();
                    if (mode.Contains("backup", StringComparison.OrdinalIgnoreCase) || mode.Equals("1", StringComparison.Ordinal))
                        backupMode = 1;
                }
                catch { /* ignore */ }
            }

            var rows = new List<CpFailoverStatusRowDigest>();
            void Add(string? path, string kind)
            {
                if (rows.Count >= safeLimit) return;
                rows.Add(new CpFailoverStatusRowDigest(path ?? kind, path is null ? 0 : 1, kind));
            }
            Add(modePath, "mode");
            Add(statusPath, "status_json");
            Add(configPath, "config");

            var summary = new CpFailoverStatusSummary(modeFilePresent, statusJsonPresent, configPresent, backupMode, "filesystem", string.Empty);
            return new(summary, rows, rows.Count, "filesystem", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "filesystem-error", Message = ex.Message };
            return new(err, [], 0, "filesystem-error", ex.Message);
        }
    }

    public async Task<CpOpsGuidesDigestResult> BuildCpOpsGuidesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpOpsGuidesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var groupCount = 0; var itemCount = 0; var showAnywayCount = 0; var urlItemCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpOpsGuidesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groupCount = Convert.ToInt32(reader["group_count"] is DBNull ? 0 : reader["group_count"], CultureInfo.InvariantCulture);
                    itemCount = Convert.ToInt32(reader["item_count"] is DBNull ? 0 : reader["item_count"], CultureInfo.InvariantCulture);
                    showAnywayCount = Convert.ToInt32(reader["show_anyway_count"] is DBNull ? 0 : reader["show_anyway_count"], CultureInfo.InvariantCulture);
                    urlItemCount = Convert.ToInt32(reader["url_item_count"] is DBNull ? 0 : reader["url_item_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpOpsGuidesRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpOpsGuidesRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpOpsGuidesRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["items_group"] is DBNull ? 0 : reader["items_group"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["url"] is DBNull ? string.Empty : reader["url"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["show_anyway"] is DBNull ? 0 : reader["show_anyway"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader["sort_order"] is DBNull ? 0 : reader["sort_order"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpOpsGuidesSummary(groupCount, itemCount, showAnywayCount, urlItemCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public Task<CpFileManagerDigestResult> BuildCpFileManagerDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFileManagerSummary(0, 0, 0, 0, "migration", "File manager root not found.");
        try
        {
            var candidates = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "content", "files"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "content", "files"),
                "/workspace/content/files",
                "/var/www/content/files",
            };
            string? root = null;
            foreach (var c in candidates)
            {
                try
                {
                    var full = Path.GetFullPath(c);
                    if (Directory.Exists(full)) { root = full; break; }
                }
                catch { /* ignore */ }
            }
            if (root is null)
            {
                return Task.FromResult(new CpFileManagerDigestResult(empty, [], 0, "migration", empty.Message));
            }

            var fileCount = 0; var dirCount = 0; long totalBytes = 0;
            var rows = new List<CpFileManagerRowDigest>();
            foreach (var entry in Directory.EnumerateFileSystemEntries(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name) || name.StartsWith('.')) continue;
                var isDir = Directory.Exists(entry);
                long size = 0;
                if (isDir) dirCount++;
                else
                {
                    try { size = new FileInfo(entry).Length; } catch { size = 0; }
                    fileCount++;
                    totalBytes += size;
                }
                if (rows.Count < safeLimit)
                {
                    rows.Add(new CpFileManagerRowDigest(name, isDir ? 1 : 0, size, isDir ? "" : Path.GetExtension(name).TrimStart('.')));
                }
            }

            var summary = new CpFileManagerSummary(1, fileCount, dirCount, totalBytes, "filesystem", string.Empty);
            return Task.FromResult(new CpFileManagerDigestResult(summary, rows, rows.Count, "filesystem", string.Empty));
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "filesystem-error", Message = ex.Message };
            return Task.FromResult(new CpFileManagerDigestResult(err, [], 0, "filesystem-error", ex.Message));
        }
    }

    public Task<CpServerIpDigestResult> BuildCpServerIpDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpServerIpSummary(0, 0, 0, 0, "migration", "No local addresses.");
        try
        {
            var rows = new List<CpServerIpRowDigest>();
            var hasIpv4 = 0; var hasIpv6 = 0; var anyNonLoop = 0;
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    var addr = ua.Address;
                    if (addr.AddressFamily is not (System.Net.Sockets.AddressFamily.InterNetwork or System.Net.Sockets.AddressFamily.InterNetworkV6))
                        continue;
                    var isLoop = System.Net.IPAddress.IsLoopback(addr) ? 1 : 0;
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) hasIpv4 = 1;
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6) hasIpv6 = 1;
                    if (isLoop == 0) anyNonLoop = 1;
                    if (rows.Count < safeLimit)
                    {
                        rows.Add(new CpServerIpRowDigest(addr.ToString(), addr.AddressFamily.ToString(), isLoop));
                    }
                }
            }
            var summary = new CpServerIpSummary(rows.Count, hasIpv4, hasIpv6, anyNonLoop == 0 ? 1 : 0, "runtime", string.Empty);
            return Task.FromResult(new CpServerIpDigestResult(summary, rows, rows.Count, "runtime", string.Empty));
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "runtime-error", Message = ex.Message };
            return Task.FromResult(new CpServerIpDigestResult(err, [], 0, "runtime-error", ex.Message));
        }
    }

    public Task<CpDebugConsoleDigestResult> BuildCpDebugConsoleDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpDebugConsoleSummary(0, 0, 1, 0, "migration", "Debug tmp root not found.");
        try
        {
            var root = CpDebugConsoleAllowlist.FindTmpRoot();
            if (root is null)
            {
                return Task.FromResult(new CpDebugConsoleDigestResult(empty, [], 0, "migration", empty.Message));
            }

            var rootFull = Path.GetFullPath(root);
            var rows = new List<CpDebugConsoleRowDigest>();
            var fileCount = 0;
            long totalBytes = 0;
            foreach (var entry in Directory.EnumerateFiles(rootFull))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string entryFull;
                try
                {
                    entryFull = Path.GetFullPath(entry);
                }
                catch
                {
                    continue;
                }

                // Confine to tmp root — never follow escapes outside the allowlisted directory.
                if (!entryFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !string.Equals(entryFull, rootFull, StringComparison.Ordinal))
                {
                    continue;
                }

                var basename = Path.GetFileName(entryFull);
                if (!CpDebugConsoleAllowlist.IsAllowedBasename(basename))
                {
                    continue;
                }

                fileCount++;
                long size = 0;
                long mtime = 0;
                try
                {
                    var info = new FileInfo(entryFull);
                    // Metadata only — never open/read file contents (no LFI).
                    size = info.Length;
                    totalBytes += size;
                    mtime = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds();
                }
                catch
                {
                    continue;
                }

                if (rows.Count < safeLimit)
                {
                    rows.Add(new CpDebugConsoleRowDigest(basename, size, mtime, 1));
                }
            }

            var summary = new CpDebugConsoleSummary(fileCount, 1, 1, totalBytes, "filesystem", string.Empty);
            return Task.FromResult(new CpDebugConsoleDigestResult(summary, rows, rows.Count, "filesystem", string.Empty));
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "filesystem-error", Message = ex.Message };
            return Task.FromResult(new CpDebugConsoleDigestResult(err, [], 0, "filesystem-error", ex.Message));
        }
    }


    public async Task<CpStatisticsDigestResult> BuildCpStatisticsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpStatisticsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var orderCount = 0; var queryCount = 0; var uniqueArticles = 0; var activeDays = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpStatisticsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    orderCount = Convert.ToInt32(reader["order_count"] is DBNull ? 0 : reader["order_count"], CultureInfo.InvariantCulture);
                    queryCount = Convert.ToInt32(reader["query_count"] is DBNull ? 0 : reader["query_count"], CultureInfo.InvariantCulture);
                    uniqueArticles = Convert.ToInt32(reader["unique_articles"] is DBNull ? 0 : reader["unique_articles"], CultureInfo.InvariantCulture);
                    activeDays = Convert.ToInt32(reader["active_days"] is DBNull ? 0 : reader["active_days"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpStatisticsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpStatisticsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpStatisticsRowDigest(
                        Convert.ToString(reader["article"] is DBNull ? string.Empty : reader["article"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["brand"] is DBNull ? string.Empty : reader["brand"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["hits"] is DBNull ? 0 : reader["hits"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader["last_seen"] is DBNull ? 0 : reader["last_seen"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpStatisticsSummary(orderCount, queryCount, uniqueArticles, activeDays, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpAccessoriesDigestResult> BuildCpAccessoriesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpAccessoriesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var listingCount = 0; var publishedCount = 0; var categoryCount = 0; var photoCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpAccessoriesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    listingCount = Convert.ToInt32(reader["listing_count"] is DBNull ? 0 : reader["listing_count"], CultureInfo.InvariantCulture);
                    publishedCount = Convert.ToInt32(reader["published_count"] is DBNull ? 0 : reader["published_count"], CultureInfo.InvariantCulture);
                    categoryCount = Convert.ToInt32(reader["category_count"] is DBNull ? 0 : reader["category_count"], CultureInfo.InvariantCulture);
                    photoCount = Convert.ToInt32(reader["photo_count"] is DBNull ? 0 : reader["photo_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpAccessoriesRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpAccessoriesRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpAccessoriesRowDigest(
                        Convert.ToInt32(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["make"] is DBNull ? string.Empty : reader["make"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["model"] is DBNull ? string.Empty : reader["model"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToDecimal(reader["price"] is DBNull ? 0 : reader["price"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpAccessoriesSummary(listingCount, publishedCount, categoryCount, photoCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpSynonymsDigestResult> BuildCpSynonymsDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSynonymsSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var manufacturerCount = 0; var synonymCount = 0; var orphanCount = 0; var mappedCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSynonymsStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    manufacturerCount = Convert.ToInt32(reader["manufacturer_count"] is DBNull ? 0 : reader["manufacturer_count"], CultureInfo.InvariantCulture);
                    synonymCount = Convert.ToInt32(reader["synonym_count"] is DBNull ? 0 : reader["synonym_count"], CultureInfo.InvariantCulture);
                    orphanCount = Convert.ToInt32(reader["orphan_count"] is DBNull ? 0 : reader["orphan_count"], CultureInfo.InvariantCulture);
                    mappedCount = Convert.ToInt32(reader["mapped_count"] is DBNull ? 0 : reader["mapped_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSynonymsRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSynonymsRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSynonymsRowDigest(
                        Convert.ToString(reader["manufacturer"] is DBNull ? string.Empty : reader["manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["synonym"] is DBNull ? string.Empty : reader["synonym"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["manufacturer_id"] is DBNull ? 0 : reader["manufacturer_id"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpSynonymsSummary(manufacturerCount, synonymCount, orphanCount, mappedCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpSeoDigestResult> BuildCpSeoDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSeoSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            // Prefer shop DB (docpart on ePartsCart) so content SEO rows match the live storefront.
            await using var connection = await OpenStorefrontShopAsync(cancellationToken).ConfigureAwait(false);
            var urlCount = 0; var indexedReady = 0; var pingJobs = 0; var warmJobs = 0;
            var robotsIndexable = 0; var withDescription = 0;
            var homeTitle = string.Empty; var homeDescription = string.Empty;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSeoStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    urlCount = Convert.ToInt32(reader["url_count"] is DBNull ? 0 : reader["url_count"], CultureInfo.InvariantCulture);
                    indexedReady = Convert.ToInt32(reader["indexed_ready"] is DBNull ? 0 : reader["indexed_ready"], CultureInfo.InvariantCulture);
                    robotsIndexable = Convert.ToInt32(reader["robots_indexable"] is DBNull ? 0 : reader["robots_indexable"], CultureInfo.InvariantCulture);
                    withDescription = Convert.ToInt32(reader["with_description"] is DBNull ? 0 : reader["with_description"], CultureInfo.InvariantCulture);
                    homeTitle = Convert.ToString(reader["home_title_tag"] is DBNull ? string.Empty : reader["home_title_tag"], CultureInfo.InvariantCulture) ?? string.Empty;
                    homeDescription = Convert.ToString(reader["home_description_tag"] is DBNull ? string.Empty : reader["home_description_tag"], CultureInfo.InvariantCulture) ?? string.Empty;
                    pingJobs = Convert.ToInt32(reader["ping_jobs"] is DBNull ? 0 : reader["ping_jobs"], CultureInfo.InvariantCulture);
                    warmJobs = Convert.ToInt32(reader["warm_jobs"] is DBNull ? 0 : reader["warm_jobs"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSeoRowDigest>
            {
                new("url_count", urlCount.ToString(CultureInfo.InvariantCulture)),
                new("indexed_ready", indexedReady.ToString(CultureInfo.InvariantCulture)),
                new("robots_indexable", robotsIndexable.ToString(CultureInfo.InvariantCulture)),
                new("pages_with_description", withDescription.ToString(CultureInfo.InvariantCulture)),
                new("home_title_tag", string.IsNullOrWhiteSpace(homeTitle) || homeTitle == "0" ? "(empty — ASP.NET HomeMetaDescription used)" : homeTitle),
                new("home_description_tag", string.IsNullOrWhiteSpace(homeDescription) || homeDescription == "0" ? "(empty — ASP.NET HomeMetaDescription used)" : homeDescription),
                new("aspnet_chpu_seo", "PHP-parity title/description/keywords + Product JSON-LD on /en/parts/{BRAND}/{ARTICLE}"),
                new("aspnet_home_seo", "canonical + OG + hreflang + JSON-LD via /storefront/app body fallback"),
                new("sitemap_xml", "/sitemap.xml → PHP sitemap-index (warehouse shards)"),
                new("sitemap_pages", "see /cp/sitemap-app"),
                new("ping_jobs", pingJobs.ToString(CultureInfo.InvariantCulture) + " (warm/ping remain PHP cron)"),
                new("warm_jobs", warmJobs.ToString(CultureInfo.InvariantCulture)),
            };
            if (rows.Count > safeLimit)
            {
                rows = rows.Take(safeLimit).ToList();
            }

            var summary = new CpSeoSummary(urlCount, indexedReady, pingJobs, warmJobs, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpSocialHubDigestResult> BuildCpSocialHubDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSocialHubSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var accountCount = 0; var draftCount = 0; var publishedCount = 0; var errorCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSocialHubStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    accountCount = Convert.ToInt32(reader["account_count"] is DBNull ? 0 : reader["account_count"], CultureInfo.InvariantCulture);
                    draftCount = Convert.ToInt32(reader["draft_count"] is DBNull ? 0 : reader["draft_count"], CultureInfo.InvariantCulture);
                    publishedCount = Convert.ToInt32(reader["published_count"] is DBNull ? 0 : reader["published_count"], CultureInfo.InvariantCulture);
                    errorCount = Convert.ToInt32(reader["error_count"] is DBNull ? 0 : reader["error_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSocialHubRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSocialHubRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSocialHubRowDigest(
                        Convert.ToString(reader["platform"] is DBNull ? string.Empty : reader["platform"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["username"] is DBNull ? string.Empty : reader["username"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["draft_status"] is DBNull ? string.Empty : reader["draft_status"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpSocialHubSummary(accountCount, draftCount, publishedCount, errorCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpTenantFeaturesDigestResult> BuildCpTenantFeaturesDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpTenantFeaturesSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var siteCount = 0; var flagCount = 0; var enabledCount = 0; var disabledCount = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpTenantFeaturesStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    siteCount = Convert.ToInt32(reader["site_count"] is DBNull ? 0 : reader["site_count"], CultureInfo.InvariantCulture);
                    flagCount = Convert.ToInt32(reader["flag_count"] is DBNull ? 0 : reader["flag_count"], CultureInfo.InvariantCulture);
                    enabledCount = Convert.ToInt32(reader["enabled_count"] is DBNull ? 0 : reader["enabled_count"], CultureInfo.InvariantCulture);
                    disabledCount = Convert.ToInt32(reader["disabled_count"] is DBNull ? 0 : reader["disabled_count"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpTenantFeaturesRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpTenantFeaturesRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpTenantFeaturesRowDigest(
                        Convert.ToString(reader["site_key"] is DBNull ? string.Empty : reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["feature_key"] is DBNull ? string.Empty : reader["feature_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["enabled"] is DBNull ? 0 : reader["enabled"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToInt64(reader["updated_at"] is DBNull ? 0 : reader["updated_at"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpTenantFeaturesSummary(siteCount, flagCount, enabledCount, disabledCount, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpCustomerBoardDigestResult> BuildCpCustomerBoardDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpCustomerBoardSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var userCount = 0; var withEmail = 0; var withPhone = 0; var recentLogins = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpCustomerBoardStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    userCount = Convert.ToInt32(reader["user_count"] is DBNull ? 0 : reader["user_count"], CultureInfo.InvariantCulture);
                    withEmail = Convert.ToInt32(reader["with_email"] is DBNull ? 0 : reader["with_email"], CultureInfo.InvariantCulture);
                    withPhone = Convert.ToInt32(reader["with_phone"] is DBNull ? 0 : reader["with_phone"], CultureInfo.InvariantCulture);
                    recentLogins = Convert.ToInt32(reader["recent_logins"] is DBNull ? 0 : reader["recent_logins"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpCustomerBoardRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpCustomerBoardRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpCustomerBoardRowDigest(
                        Convert.ToInt32(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["phone"] is DBNull ? string.Empty : reader["phone"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(reader["reg_time"] is DBNull ? 0 : reader["reg_time"], CultureInfo.InvariantCulture)));
                }
            }

            var summary = new CpCustomerBoardSummary(userCount, withEmail, withPhone, recentLogins, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpFulfillmentQueueDigestResult> BuildCpFulfillmentQueueDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpFulfillmentQueueSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var queued = 0; var picking = 0; var shipping = 0; var delivered = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpFulfillmentQueueStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    queued = Convert.ToInt32(reader["queued"] is DBNull ? 0 : reader["queued"], CultureInfo.InvariantCulture);
                    picking = Convert.ToInt32(reader["picking"] is DBNull ? 0 : reader["picking"], CultureInfo.InvariantCulture);
                    shipping = Convert.ToInt32(reader["shipping"] is DBNull ? 0 : reader["shipping"], CultureInfo.InvariantCulture);
                    delivered = Convert.ToInt32(reader["delivered"] is DBNull ? 0 : reader["delivered"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpFulfillmentQueueRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpFulfillmentQueueRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpFulfillmentQueueRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["order_number"] is DBNull ? string.Empty : reader["order_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["customer_name"] is DBNull ? string.Empty : reader["customer_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["priority"] is DBNull ? string.Empty : reader["priority"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["warehouse"] is DBNull ? string.Empty : reader["warehouse"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["carrier"] is DBNull ? string.Empty : reader["carrier"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpFulfillmentQueueSummary(queued, picking, shipping, delivered, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpSsoSamlDigestResult> BuildCpSsoSamlDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpSsoSamlSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var providerCount = 0; var activeProviders = 0; var sessionCount = 0; var activeSessions = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpSsoSamlStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    providerCount = Convert.ToInt32(reader["provider_count"] is DBNull ? 0 : reader["provider_count"], CultureInfo.InvariantCulture);
                    activeProviders = Convert.ToInt32(reader["active_providers"] is DBNull ? 0 : reader["active_providers"], CultureInfo.InvariantCulture);
                    sessionCount = Convert.ToInt32(reader["session_count"] is DBNull ? 0 : reader["session_count"], CultureInfo.InvariantCulture);
                    activeSessions = Convert.ToInt32(reader["active_sessions"] is DBNull ? 0 : reader["active_sessions"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpSsoSamlRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpSsoSamlRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpSsoSamlRowDigest(
                        Convert.ToString(reader["provider_name"] is DBNull ? string.Empty : reader["provider_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["provider_type"] is DBNull ? string.Empty : reader["provider_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(reader["active"] is DBNull ? 0 : reader["active"], CultureInfo.InvariantCulture) != 0,
                        Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpSsoSamlSummary(providerCount, activeProviders, sessionCount, activeSessions, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }


    public async Task<CpEventBusDigestResult> BuildCpEventBusDigestAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        var empty = new CpEventBusSummary(0, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        if (!_connections.IsConfigured)
        {
            return new(empty, [], 0, "migration", empty.Message);
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            var eventCount = 0; var typeCount = 0; var tenantCount = 0; var last24h = 0;
            await using (var stats = connection.CreateCommand())
            {
                stats.CommandText = LegacySurfaceDashboardSql.SelectCpEventBusStats;
                await using var reader = await stats.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    eventCount = Convert.ToInt32(reader["event_count"] is DBNull ? 0 : reader["event_count"], CultureInfo.InvariantCulture);
                    typeCount = Convert.ToInt32(reader["type_count"] is DBNull ? 0 : reader["type_count"], CultureInfo.InvariantCulture);
                    tenantCount = Convert.ToInt32(reader["tenant_count"] is DBNull ? 0 : reader["tenant_count"], CultureInfo.InvariantCulture);
                    last24h = Convert.ToInt32(reader["last_24h"] is DBNull ? 0 : reader["last_24h"], CultureInfo.InvariantCulture);
                }
            }

            var rows = new List<CpEventBusRowDigest>();
            await using (var list = connection.CreateCommand())
            {
                list.CommandText = LegacySurfaceDashboardSql.SelectCpEventBusRows;
                AddParameter(list, "@limit", safeLimit);
                await using var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(new CpEventBusRowDigest(
                        Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
                        Convert.ToString(reader["event_type"] is DBNull ? string.Empty : reader["event_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["tenant_key"] is DBNull ? string.Empty : reader["tenant_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["actor_type"] is DBNull ? string.Empty : reader["actor_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(reader["created_at"] is DBNull ? string.Empty : reader["created_at"], CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            var summary = new CpEventBusSummary(eventCount, typeCount, tenantCount, last24h, "database", string.Empty);
            return new(summary, rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            var err = empty with { Source = "database-error", Message = ex.Message };
            return new(err, [], 0, "database-error", ex.Message);
        }
    }

    public async Task<OnPremisesLicenseListResult> ListOnPremisesLicensesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectOnPremisesLicenses;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<OnPremisesLicenseDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var rawKey = Convert.ToString(reader["license_key"] is DBNull ? string.Empty : reader["license_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                rows.Add(new OnPremisesLicenseDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    MaskOnPremisesLicenseKey(rawKey),
                    Convert.ToString(reader["customer_name"] is DBNull ? string.Empty : reader["customer_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["tier"] is DBNull ? string.Empty : reader["tier"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["users_max"] is DBNull ? 0 : reader["users_max"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["hostname"] is DBNull ? string.Empty : reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["issued_at"] is DBNull ? 0 : reader["issued_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["activated_at"] is DBNull ? 0 : reader["activated_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["last_seen_at"] is DBNull ? 0 : reader["last_seen_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["expires_at"] is DBNull ? 0 : reader["expires_at"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpDeliveryNoteListResult> ListErpDeliveryNotesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpDeliveryNotes;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpDeliveryNoteDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpDeliveryNoteDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["note_no"] is DBNull ? string.Empty : reader["note_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["carrier"] is DBNull ? string.Empty : reader["carrier"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["tracking_no"] is DBNull ? string.Empty : reader["tracking_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["shipped_at"] is DBNull ? 0 : reader["shipped_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["delivered_at"] is DBNull ? 0 : reader["delivered_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpRfqListResult> ListErpRfqsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpRfqs;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpRfqDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpRfqDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["rfq_no"] is DBNull ? string.Empty : reader["rfq_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["supplier_id"] is DBNull ? 0 : reader["supplier_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["amount_est"] is DBNull ? 0m : reader["amount_est"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["currency_code"] is DBNull ? "AED" : reader["currency_code"], CultureInfo.InvariantCulture) ?? "AED",
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["due_date"] is DBNull ? 0 : reader["due_date"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["order_id"] is DBNull ? 0 : reader["order_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpThreeWayMatchListResult> ListErpThreeWayMatchAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpThreeWayMatch;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpThreeWayMatchDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpThreeWayMatchDigest(
                    Convert.ToInt64(reader["po_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["po_no"] is DBNull ? string.Empty : reader["po_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["po_status"] is DBNull ? string.Empty : reader["po_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["po_total"] is DBNull ? 0m : reader["po_total"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["purchase_id"] is DBNull ? 0 : reader["purchase_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["invoice_number"] is DBNull ? string.Empty : reader["invoice_number"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["invoice_total"] is DBNull ? 0m : reader["invoice_total"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["purchase_status"] is DBNull ? string.Empty : reader["purchase_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["receipt_count"] is DBNull ? 0 : reader["receipt_count"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpContactListResult> ListErpContactsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpContacts;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpContactDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpContactDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["party_type"] is DBNull ? string.Empty : reader["party_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["name"] is DBNull ? string.Empty : reader["name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["company"] is DBNull ? string.Empty : reader["company"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["email"] is DBNull ? string.Empty : reader["email"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["phone"] is DBNull ? string.Empty : reader["phone"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["trn"] is DBNull ? string.Empty : reader["trn"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["city"] is DBNull ? string.Empty : reader["city"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["country_code"] is DBNull ? "AE" : reader["country_code"], CultureInfo.InvariantCulture) ?? "AE",
                    Convert.ToInt64(reader["linked_user_id"] is DBNull ? 0 : reader["linked_user_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["linked_supplier_id"] is DBNull ? 0 : reader["linked_supplier_id"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["active"] is DBNull ? 1 : reader["active"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpPaymentBatchListResult> ListErpPaymentBatchesAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpPaymentBatches;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpPaymentBatchDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpPaymentBatchDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["batch_no"] is DBNull ? string.Empty : reader["batch_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["batch_type"] is DBNull ? string.Empty : reader["batch_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["account_id"] is DBNull ? 0 : reader["account_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["account_name"] is DBNull ? string.Empty : reader["account_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader["line_count"] is DBNull ? 0 : reader["line_count"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["execution_date"] is DBNull ? 0 : reader["execution_date"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpFiscalPeriodListResult> ListErpFiscalPeriodsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpFiscalPeriods;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpFiscalPeriodDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpFiscalPeriodDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["year_month"] is DBNull ? string.Empty : reader["year_month"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader["soft_closed"] is DBNull ? 0 : reader["soft_closed"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt32(reader["locked"] is DBNull ? 0 : reader["locked"], CultureInfo.InvariantCulture) != 0,
                    Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }


    public async Task<ErpAgendaEventListResult> ListErpAgendaEventsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpAgendaEvents;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpAgendaEventDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpAgendaEventDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["event_type"] is DBNull ? string.Empty : reader["event_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["start_at"] is DBNull ? 0 : reader["start_at"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["end_at"] is DBNull ? 0 : reader["end_at"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["entity_type"] is DBNull ? string.Empty : reader["entity_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["entity_id"] is DBNull ? 0 : reader["entity_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["location"] is DBNull ? string.Empty : reader["location"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpDocumentListResult> ListErpDocumentsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpDocuments;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpDocumentDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpDocumentDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["entity_type"] is DBNull ? string.Empty : reader["entity_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["entity_id"] is DBNull ? 0 : reader["entity_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["doc_category"] is DBNull ? string.Empty : reader["doc_category"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader["file_name"] is DBNull ? string.Empty : reader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["file_size"] is DBNull ? 0 : reader["file_size"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["mime_type"] is DBNull ? string.Empty : reader["mime_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["time_created"] is DBNull ? 0 : reader["time_created"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public async Task<ErpExpenseReportListResult> ListErpExpenseReportsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        if (!_connections.IsConfigured)
        {
            return new([], 0, "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTenantShopAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = LegacySurfaceDashboardSql.SelectErpExpenseReports;
            AddParameter(command, "@limit", safeLimit);
            var rows = new List<ErpExpenseReportDigest>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpExpenseReportDigest(
                    Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["report_no"] is DBNull ? string.Empty : reader["report_no"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["staff_user_id"] is DBNull ? 0 : reader["staff_user_id"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["title"] is DBNull ? string.Empty : reader["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToDecimal(reader["total_amount"] is DBNull ? 0m : reader["total_amount"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"] is DBNull ? string.Empty : reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader["period_from"] is DBNull ? 0 : reader["period_from"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["period_to"] is DBNull ? 0 : reader["period_to"], CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader["time_updated"] is DBNull ? 0 : reader["time_updated"], CultureInfo.InvariantCulture)));
            }

            return new(rows, rows.Count, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new([], 0, "database-error", ex.Message);
        }
    }

    public static string MaskOnPremisesLicenseKey(string? key)
    {
        var k = (key ?? string.Empty).Trim();
        if (k.Length == 0)
        {
            return string.Empty;
        }

        return k.Length <= 8 ? k : k[..4] + "…" + k[^4..];
    }

}
