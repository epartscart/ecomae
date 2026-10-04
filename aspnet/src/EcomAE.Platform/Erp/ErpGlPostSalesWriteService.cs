using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live port of PHP <c>epc_erp_gl_post_sales_orders</c>: for each successfully_created storefront order in the
/// window (newest first, 500 cap) without an active <c>source_type='sales'</c> journal, posts
/// Dr 1100 AR gross / Cr 4000 revenue net / Cr 2100 VAT output using the customer-VAT order totals
/// shared with the dashboard twin. Zero-net orders are skipped. Schema stays PHP-owned.
/// </summary>
public interface IErpGlPostSalesWriteService
{
    Task<int> PostAsync(long dateFrom, long dateTo, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpGlPostSalesWriteService : IErpGlPostSalesWriteService
{
    private const int Limit = 500;
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpGlPostSalesWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<int> PostAsync(long dateFrom, long dateTo, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var ar = await CoaAsync(c, "1100", cancellationToken).ConfigureAwait(false);
        var rev = await CoaAsync(c, "4000", cancellationToken).ConfigureAwait(false);
        var vatOut = await CoaAsync(c, "2100", cancellationToken).ConfigureAwait(false);
        if (ar <= 0 || rev <= 0)
        {
            throw new ErpWriteException("Sales COA accounts missing");
        }

        var tax = await ErpDashboardReadService.LoadTenantVatAsync(c, cancellationToken).ConfigureAwait(false);

        var orders = new List<(long Id, long Time, long UserId, string HowGet)>();
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `shop_orders`.`id`, `shop_orders`.`time`, `shop_orders`.`user_id`, `shop_orders`.`how_get_json`"
                + " FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= ? AND `time` <= ?"
                + " ORDER BY `time` DESC LIMIT " + Limit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(cmd, dateFrom, dateTo);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                orders.Add((
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    r.IsDBNull(1) ? 0 : Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    r.IsDBNull(2) ? 0 : Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
                    r.IsDBNull(3) ? string.Empty : Convert.ToString(r.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        catch (DbException ex)
        {
            throw new ErpWriteException("GL post-sales requires the PHP shop_orders schema: " + ex.Message);
        }

        var posted = 0;
        var ctxCache = new Dictionary<long, ErpDashboardReadService.CustomerVatContext>();
        foreach (var o in orders)
        {
            if (!ctxCache.TryGetValue(o.UserId, out var ctx))
            {
                ctx = await ErpDashboardReadService.CustomerContextAsync(c, o.UserId, cancellationToken).ConfigureAwait(false);
                ctxCache[o.UserId] = ctx;
            }

            var items = await ErpDashboardReadService.ItemsAsync(c, o.Id, cancellationToken).ConfigureAwait(false);
            var dest = ErpDashboardReadService.DestinationCountry(o.HowGet, ctx.Country);
            var exports = dest != "AE";
            var rate = ErpDashboardReadService.SupplyRate(ctx.Country, exports, tax.RatePercent);
            var inclusive = ErpDashboardReadService.DisplayMode(ctx.VatType) == "inclusive" && rate > 0m;
            decimal net = 0m, vat = 0m, gross = 0m;
            foreach (var (unit, qty) in items)
            {
                var line = ErpDashboardReadService.LineAmounts(unit, qty, rate, inclusive, tax.SalesEnabled);
                net += line.LineNet;
                vat += line.VatAmount;
                gross += line.Gross;
            }

            var sale = ErpDashboardReadService.Round2(net);
            if (sale <= 0m)
            {
                continue;
            }

            vat = ErpDashboardReadService.Round2(vat);
            var total = ErpDashboardReadService.Round2(gross);
            var existing = await ErpDb.LongAsync(c, null, ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'sales' AND `source_id` = ? AND `active` = 1 LIMIT 1"), cancellationToken, o.Id).ConfigureAwait(false);
            if (existing > 0)
            {
                continue;
            }

            var treatment = SalesTreatment(tax, ctx.Country, exports);
            var id = o.Id.ToString(CultureInfo.InvariantCulture);
            var lines = new List<ErpGlLine>
            {
                new(ar, total, 0m, "Order #" + id),
                new(rev, 0m, sale, "Sales revenue (ex VAT)"),
            };
            if (vat > 0m && vatOut > 0)
            {
                lines.Add(new ErpGlLine(vatOut, 0m, vat, "VAT output (" + treatment + ")"));
            }

            await _gl.PostJournalAsync(c, new ErpGlJournalHeader
            {
                JournalDate = o.Time,
                Reference = "ORD-" + id,
                Description = "Sales recognition order #" + id,
                SourceType = "sales",
                SourceId = o.Id,
                LegislationRef = "vat-decree-8-2017",
            }, lines, adminId, cancellationToken).ConfigureAwait(false);
            posted++;
        }

        return posted;
    }

    /// <summary>PHP <c>epc_uae_vat_sales_treatment_for_order</c> → <c>epc_uae_vat_treatment_from_supply_reason</c>.</summary>
    public static string SalesTreatment(ErpDashboardReadService.TenantVat tax, string buyerCountry, bool exports)
    {
        ArgumentNullException.ThrowIfNull(tax);
        if (!tax.SalesEnabled)
        {
            return "exempt";
        }

        return exports || ErpDashboardReadService.NormalizeCountry(buyerCountry) != "AE" ? "export" : "standard";
    }

    private static async Task<long> CoaAsync(DbConnection c, string code, CancellationToken ct)
    {
        try
        {
            return await ErpDb.LongAsync(c, null, ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"), ct, code).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw new ErpWriteException("GL post-sales requires the PHP epc_erp_coa_accounts schema: " + ex.Message);
        }
    }
}
