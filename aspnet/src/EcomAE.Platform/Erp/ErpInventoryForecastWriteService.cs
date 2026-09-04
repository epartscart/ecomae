using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_forecast_compute</c> UPSERT.</summary>
public interface IErpInventoryForecastWriteService
{
    Task<ErpSimpleWriteResult> RecomputeSkuAsync(string siteKey, string sku, int currentStock, string productName, int leadTimeDays, CancellationToken cancellationToken = default);
}

public sealed class ErpInventoryForecastWriteService : IErpInventoryForecastWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInventoryForecastWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RecomputeSkuAsync(
        string siteKey,
        string sku,
        int currentStock,
        string productName,
        int leadTimeDays,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var key = (siteKey ?? string.Empty).Trim();
        var code = (sku ?? string.Empty).Trim();
        if (key.Length == 0 || code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site and SKU are required.");
        }

        if (leadTimeDays <= 0)
        {
            leadTimeDays = 7;
        }

        if (currentStock < 0)
        {
            currentStock = 0;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        decimal avgDaily = 0;
        await using (var demand = connection.CreateCommand())
        {
            demand.CommandText = """
                SELECT COALESCE(SUM(`qty_sold` - `qty_returned`), 0) / 90
                FROM `epc_demand_history`
                WHERE `site_key` = @site AND `sku` = @sku AND `period` >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                """;
            Add(demand, "@site", key);
            Add(demand, "@sku", code);
            try
            {
                var avgObj = await demand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                avgDaily = Convert.ToDecimal(avgObj is DBNull or null ? 0m : avgObj, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                avgDaily = 0;
            }
        }

        var safetyStock = (int)Math.Ceiling((double)avgDaily * leadTimeDays * 1.65);
        var reorderPoint = (int)Math.Ceiling((double)avgDaily * leadTimeDays) + safetyStock;
        var annualDemand = avgDaily * 365m;
        var eoq = annualDemand <= 0 ? 0 : (int)Math.Round(Math.Sqrt((double)(2m * annualDemand * 50m / 5m)), MidpointRounding.AwayFromZero);
        var daysOfStock = avgDaily > 0 ? (int)Math.Floor((decimal)currentStock / avgDaily) : 999;
        var stockoutDate = avgDaily > 0
            ? DateTime.UtcNow.Date.AddDays(daysOfStock).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
        var status = currentStock <= 0
            ? "stockout"
            : currentStock <= safetyStock
                ? "critical"
                : currentStock <= reorderPoint
                    ? "low"
                    : "healthy";

        await using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO `epc_inventory_forecast`
                (`site_key`, `sku`, `product_name`, `current_stock`, `avg_daily_demand`,
                 `lead_time_days`, `safety_stock`, `reorder_point`, `eoq`, `days_of_stock`,
                 `stockout_date`, `forecast_status`, `last_computed`)
            VALUES (@site, @sku, @name, @stock, @avg, @lead, @safety, @rop, @eoq, @days, @stockout, @status, NOW())
            ON DUPLICATE KEY UPDATE
                `product_name` = VALUES(`product_name`),
                `current_stock` = VALUES(`current_stock`),
                `avg_daily_demand` = VALUES(`avg_daily_demand`),
                `lead_time_days` = VALUES(`lead_time_days`),
                `safety_stock` = VALUES(`safety_stock`),
                `reorder_point` = VALUES(`reorder_point`),
                `eoq` = VALUES(`eoq`),
                `days_of_stock` = VALUES(`days_of_stock`),
                `stockout_date` = VALUES(`stockout_date`),
                `forecast_status` = VALUES(`forecast_status`),
                `last_computed` = NOW()
            """;
        Add(upsert, "@site", key);
        Add(upsert, "@sku", code);
        Add(upsert, "@name", productName ?? string.Empty);
        Add(upsert, "@stock", currentStock);
        Add(upsert, "@avg", avgDaily);
        Add(upsert, "@lead", leadTimeDays);
        Add(upsert, "@safety", safetyStock);
        Add(upsert, "@rop", reorderPoint);
        Add(upsert, "@eoq", eoq);
        Add(upsert, "@days", daysOfStock);
        Add(upsert, "@stockout", (object?)stockoutDate ?? DBNull.Value);
        Add(upsert, "@status", status);
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Forecast recomputed (" + status + ").", 0);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
