using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>epc_tcp_dash_stats()</c> result — zero-safe when contained or unavailable.</summary>
public sealed record CpDashboardStats(
    int OrdersToday,
    int OrdersWeek,
    int OrdersPrevWeek,
    int Products,
    int CatalogueProducts,
    int WarehouseQty,
    int GoodsQty,
    int SkuCount,
    int Vendors,
    int Clients,
    int PendingTasks,
    int ReturnsOpen,
    int VinOpen,
    IReadOnlyList<string> DayLabels,
    IReadOnlyList<int> DayCounts)
{
    public static CpDashboardStats Empty(DateTime today)
    {
        var labels = new List<string>(7);
        for (var i = 6; i >= 0; i--)
        {
            labels.Add(DayLabel(today.AddDays(-i)));
        }

        return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, labels, new int[7]);
    }

    /// <summary>PHP <c>date('D j')</c>.</summary>
    public static string DayLabel(DateTime day)
        => day.ToString("ddd", CultureInfo.InvariantCulture) + " "
           + day.Day.ToString(CultureInfo.InvariantCulture);
}

/// <summary>PHP <c>epc_co_profile_get()</c> fields the CP dashboard header uses.</summary>
public sealed record CpDashboardCompany(string CompanyName, string Currency)
{
    public static CpDashboardCompany Empty { get; } = new(string.Empty, "AED");
}

/// <summary>Live tenant KPIs for the CP command centre (PHP <c>epc_tenant_cp_dashboard.php</c>).</summary>
public interface ICpTenantDashboardService
{
    Task<CpDashboardStats> LoadStatsAsync(CancellationToken cancellationToken = default);

    Task<CpDashboardCompany> LoadCompanyAsync(CancellationToken cancellationToken = default);
}

public sealed class CpTenantDashboardService : ICpTenantDashboardService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(120);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly TimeProvider _clock;

    public CpTenantDashboardService(
        IErpWriteConnectionFactory connections,
        IMemoryCache cache,
        IHttpContextAccessor? httpContextAccessor = null,
        TimeProvider? clock = null)
    {
        _connections = connections;
        _cache = cache;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<CpDashboardStats> LoadStatsAsync(CancellationToken cancellationToken = default)
    {
        var today = _clock.GetLocalNow().Date;
        var empty = CpDashboardStats.Empty(today);

        // PHP: containment returns the zero-initialised stats before any query runs.
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (TenantDataGuard.IsContained(tenant) || !_connections.IsConfigured)
        {
            return empty;
        }

        var cacheKey = "epc_tcp_dash_stats:v7:" + (tenant?.DatabaseName ?? "default");
        if (_cache.TryGetValue(cacheKey, out CpDashboardStats? cached) && cached is not null)
        {
            return cached;
        }

        CpDashboardStats stats;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            stats = await ComputeAsync(connection, today, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return empty;
        }

        _cache.Set(cacheKey, stats, CacheTtl);
        return stats;
    }

    public async Task<CpDashboardCompany> LoadCompanyAsync(CancellationToken cancellationToken = default)
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (TenantDataGuard.IsContained(tenant) || !_connections.IsConfigured)
        {
            return CpDashboardCompany.Empty;
        }

        var cacheKey = "epc_tcp_dash_company:v1:" + (tenant?.DatabaseName ?? "default");
        if (_cache.TryGetValue(cacheKey, out CpDashboardCompany? cached) && cached is not null)
        {
            return cached;
        }

        var company = CpDashboardCompany.Empty;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (await TableExistsAsync(connection, "epc_co_profile", cancellationToken).ConfigureAwait(false))
            {
                var legal = await ErpDb.StringAsync(
                    connection,
                    null,
                    "SELECT COALESCE(`trade_name`, '') FROM `epc_co_profile` WHERE `id` = 1",
                    cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(legal))
                {
                    legal = await ErpDb.StringAsync(
                        connection,
                        null,
                        "SELECT COALESCE(`legal_name`, '') FROM `epc_co_profile` WHERE `id` = 1",
                        cancellationToken).ConfigureAwait(false);
                }

                var currency = await ErpDb.StringAsync(
                    connection,
                    null,
                    "SELECT COALESCE(`base_currency`, '') FROM `epc_co_profile` WHERE `id` = 1",
                    cancellationToken).ConfigureAwait(false);

                company = new CpDashboardCompany(
                    (legal ?? string.Empty).Trim(),
                    string.IsNullOrWhiteSpace(currency) ? "AED" : currency.Trim());
            }
        }
        catch (DbException)
        {
            return CpDashboardCompany.Empty;
        }

        _cache.Set(cacheKey, company, CacheTtl);
        return company;
    }

    private static async Task<CpDashboardStats> ComputeAsync(
        DbConnection connection,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var ordersToday = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE())",
            cancellationToken).ConfigureAwait(false);

        var ordersWeek = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)",
            cancellationToken).ConfigureAwait(false);

        var ordersPrevWeek = await CountAsync(
            connection,
            """
            SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1
              AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 13 DAY)
              AND `time` < UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)
            """,
            cancellationToken).ConfigureAwait(false);

        var catalogueProducts = await CountAsync(
            connection,
            "SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `published_flag` = 1",
            cancellationToken).ConfigureAwait(false);
        var products = catalogueProducts;

        // Own package warehouse stock (interface_type = 1) — fallback only.
        var ownWarehouseQty = 0;
        if (await TableExistsAsync(connection, "shop_storages_data", cancellationToken).ConfigureAwait(false))
        {
            ownWarehouseQty = await CountAsync(
                connection,
                """
                SELECT COALESCE(SUM(sd.`exist`), 0)
                  FROM `shop_storages_data` sd
                  INNER JOIN `shop_storages` s ON s.`id` = sd.`storage_id`
                 WHERE s.`interface_type` = 1 AND sd.`exist` > 0
                """,
                cancellationToken).ConfigureAwait(false);
        }

        var skuCount = 0;
        var vendors = 0;
        if (await TableExistsAsync(connection, "shop_docpart_prices", cancellationToken).ConfigureAwait(false))
        {
            if (await ColumnExistsAsync(connection, "shop_docpart_prices", "records_count", cancellationToken).ConfigureAwait(false))
            {
                skuCount = await CountAsync(
                    connection,
                    "SELECT COALESCE(SUM(`records_count`), 0) FROM `shop_docpart_prices`",
                    cancellationToken).ConfigureAwait(false);
            }

            if (skuCount <= 0
                && await TableExistsAsync(connection, "shop_docpart_prices_data", cancellationToken).ConfigureAwait(false))
            {
                skuCount = await CountAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_docpart_prices_data`",
                    cancellationToken).ConfigureAwait(false);
            }

            if (await TableExistsAsync(connection, "shop_storages", cancellationToken).ConfigureAwait(false))
            {
                vendors = await CountAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_storages` WHERE `interface_type` = 2",
                    cancellationToken).ConfigureAwait(false);
            }

            if (vendors <= 0)
            {
                vendors = await CountAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_docpart_prices` WHERE COALESCE(`records_count`, 0) > 0",
                    cancellationToken).ConfigureAwait(false);
            }

            if (vendors <= 0)
            {
                vendors = await CountAsync(
                    connection,
                    "SELECT COUNT(*) FROM `shop_docpart_prices`",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var goodsQty = 0;
        if (await TableExistsAsync(connection, "shop_docpart_prices_data", cancellationToken).ConfigureAwait(false))
        {
            goodsQty = await CountAsync(
                connection,
                "SELECT COALESCE(SUM(`exist`), 0) FROM `shop_docpart_prices_data` WHERE `exist` > 0",
                cancellationToken).ConfigureAwait(false);
        }

        if (skuCount > 0)
        {
            products = skuCount;
        }

        var warehouseQty = goodsQty > 0 ? goodsQty : ownWarehouseQty > 0 ? ownWarehouseQty : 0;

        var clients = await CountAsync(
            connection,
            """
            SELECT COUNT(*) FROM `users` u
             WHERE u.`user_id` > 0
               AND NOT EXISTS (
                   SELECT 1 FROM `users_groups_bind` b
                   INNER JOIN `groups` g ON g.`id` = b.`group_id`
                    WHERE b.`user_id` = u.`user_id` AND g.`for_backend` = 1
               )
            """,
            cancellationToken).ConfigureAwait(false);
        if (clients == 0)
        {
            clients = await CountAsync(
                connection,
                "SELECT COUNT(*) FROM `users` WHERE `user_id` > 0",
                cancellationToken).ConfigureAwait(false);
        }

        var pendingTasks = await CountAsync(
            connection,
            """
            SELECT COUNT(*) FROM `shop_orders`
             WHERE `successfully_created` = 1
               AND `status` IN (
                   SELECT `id` FROM (
                       SELECT `id` FROM `shop_orders_statuses_ref`
                        WHERE `for_inverse` != 1 AND `for_finish` != 1 AND `for_created` != 1
                   ) open_statuses
               )
            """,
            cancellationToken).ConfigureAwait(false);

        var returnsOpen = 0;
        if (await TableExistsAsync(connection, "shop_orders_returns", cancellationToken).ConfigureAwait(false))
        {
            returnsOpen = await CountAsync(
                connection,
                "SELECT COUNT(*) FROM `shop_orders_returns` WHERE COALESCE(`status`,0) NOT IN (2,3,9)",
                cancellationToken).ConfigureAwait(false);
        }

        var vinOpen = 0;
        if (await TableExistsAsync(connection, "shop_docpart_vin", cancellationToken).ConfigureAwait(false))
        {
            vinOpen = await CountAsync(
                connection,
                "SELECT COUNT(*) FROM `shop_docpart_vin` WHERE COALESCE(`viewed`,0) = 0",
                cancellationToken).ConfigureAwait(false);
        }
        else if (await TableExistsAsync(connection, "shop_docpart_requests", cancellationToken).ConfigureAwait(false))
        {
            vinOpen = await CountAsync(
                connection,
                "SELECT COUNT(*) FROM `shop_docpart_requests` WHERE COALESCE(`viewed`,0) = 0",
                cancellationToken).ConfigureAwait(false);
        }

        var (labels, counts) = await LoadChartAsync(connection, today, cancellationToken).ConfigureAwait(false);

        return new CpDashboardStats(
            ordersToday,
            ordersWeek,
            ordersPrevWeek,
            products,
            catalogueProducts,
            warehouseQty,
            goodsQty,
            skuCount,
            vendors,
            clients,
            pendingTasks,
            returnsOpen,
            vinOpen,
            labels,
            counts);
    }

    private static async Task<(IReadOnlyList<string> Labels, IReadOnlyList<int> Counts)> LoadChartAsync(
        DbConnection connection,
        DateTime today,
        CancellationToken cancellationToken)
    {
        var labels = new List<string>(7);
        var days = new List<string>(7);
        for (var i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            labels.Add(CpDashboardStats.DayLabel(day));
            days.Add(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        var counts = new int[7];
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT FROM_UNIXTIME(`time`, '%Y-%m-%d') AS d, COUNT(*) AS c
                  FROM `shop_orders`
                 WHERE `successfully_created` = 1 AND `time` >= UNIX_TIMESTAMP(CURDATE() - INTERVAL 6 DAY)
                 GROUP BY d
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var day = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var index = days.IndexOf(day);
                if (index >= 0)
                {
                    counts[index] = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                }
            }
        }
        catch (DbException)
        {
            // PHP swallows chart failures and keeps zeroes.
        }

        return (labels, counts);
    }

    /// <summary>PHP guards every KPI query independently so a missing table never breaks /cp.</summary>
    private static async Task<int> CountAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            return (int)await ErpDb.LongAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        try
        {
            var n = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
                cancellationToken,
                table).ConfigureAwait(false);
            return n > 0;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        try
        {
            var n = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
                cancellationToken,
                table,
                column).ConfigureAwait(false);
            return n > 0;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
