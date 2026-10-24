using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Bridge from the auto-parts workspace into the general ledger: posts trading
/// journals for received purchases, invoiced sales (with output VAT), cost of
/// goods sold and customer receipts so the income statement, balance sheet and
/// dashboard insight figures reflect the same numbers the workspace shows.
/// Idempotent — rows already journaled under their AP-* reference are skipped.
/// Mirrors <see cref="ErpFitOutGlPostWriteService"/>.
/// </summary>
public interface IErpAutoPartsGlPostWriteService
{
    Task<string> PostAsync(long orderId, string? period, CancellationToken cancellationToken = default);
}

public sealed class ErpAutoPartsGlPostWriteService : IErpAutoPartsGlPostWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpAutoPartsGlPostWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<string> PostAsync(long orderId, string? period, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — GL posting skipped.";
        }

        if (!string.IsNullOrWhiteSpace(period)
            && !DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return "Invalid period — use YYYY-MM or leave it empty.";
        }

        var (fromUtc, toUtc) = PeriodWindow(period);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsGl.EnsureCoaAsync(connection, cancellationToken).ConfigureAwait(false);
        var companyId = await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);

        var purchases = 0;
        var invoices = 0;
        var cogs = 0;
        var receipts = 0;
        var skipped = 0;
        var failed = 0;

        var orders = new List<(long Id, string No, string Type, string Status, decimal Subtotal, decimal Vat, decimal Total, long Date)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`order_no`,`order_type`,`status`,`subtotal`,`vat`,`total`,`event_date`"
                + " FROM `ecomae_autoparts_orders`"
                + " WHERE (?<=0 OR `id`=?) AND (?=0 OR `event_date`>=?) AND (?=0 OR `event_date`<?) ORDER BY `id`");
            ErpDb.AddParameters(command, orderId, orderId, fromUtc, fromUtc, toUtc, toUtc);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                orders.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetInt64(7)));
            }
        }

        foreach (var o in orders)
        {
            try
            {
            if (o.Type == "purchase" && o.Status is "received")
            {
                var reference = "AP-PO-" + o.Id.ToString(CultureInfo.InvariantCulture) + "-RCV";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, reference, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, o.Id, reference,
                    "Parts purchase received — " + o.No, ErpAutoPartsIfrs.PurchaseReceived(o.Subtotal, o.Vat), o.Date, cancellationToken).ConfigureAwait(false) > 0)
                {
                    purchases++;
                }
            }
            else if (o.Type == "sale" && o.Status is "invoiced" or "paid")
            {
                var invRef = "AP-SO-" + o.Id.ToString(CultureInfo.InvariantCulture) + "-INV";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, invRef, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, o.Id, invRef,
                    "Parts sale invoiced — " + o.No + " " + o.Status, ErpAutoPartsIfrs.SaleInvoiced(o.Subtotal, o.Vat), o.Date, cancellationToken).ConfigureAwait(false) > 0)
                {
                    invoices++;
                }

                // COGS at purchase cost for the parts on this sale.
                var cost = await CogsAsync(connection, o.Id, cancellationToken).ConfigureAwait(false);
                var cogsRef = "AP-SO-" + o.Id.ToString(CultureInfo.InvariantCulture) + "-COGS";
                if (cost > 0)
                {
                    if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, cogsRef, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        skipped++;
                    }
                    else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, o.Id, cogsRef,
                        "Cost of parts sold — " + o.No, ErpAutoPartsIfrs.CostOfSale(cost), o.Date, cancellationToken).ConfigureAwait(false) > 0)
                    {
                        cogs++;
                    }
                }
            }

            if (o.Type == "sale" && o.Status == "paid")
            {
                var payRef = "AP-SO-" + o.Id.ToString(CultureInfo.InvariantCulture) + "-PAY";
                if (await ErpAutoPartsGl.PostedJournalIdAsync(connection, payRef, cancellationToken).ConfigureAwait(false) > 0)
                {
                    skipped++;
                }
                else if (await ErpAutoPartsGl.PostAsync(_gl, connection, null, 0, companyId, o.Id, payRef,
                    "Customer payment — " + o.No, ErpAutoPartsIfrs.PaymentReceived(o.Total), o.Date, cancellationToken).ConfigureAwait(false) > 0)
                {
                    receipts++;
                }
            }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
            }
        }

        return "Posted to GL: " + purchases + " purchase journals, " + invoices + " sales invoice journals, "
            + cogs + " COGS journals, " + receipts + " receipt journals"
            + (skipped > 0 ? " (" + skipped + " already posted skipped)" : "")
            + (failed > 0 ? " (" + failed + " failed — retry on next save)" : "")
            + " — income statement, balance sheet and dashboard figures now include the auto-parts numbers.";
    }

    private static async Task<decimal> CogsAsync(DbConnection connection, long orderId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(SUM(l.`qty`*p.`purchase_cost`),0) FROM `ecomae_autoparts_order_lines` l"
            + " LEFT JOIN `ecomae_autoparts_parts` p ON p.`id`=l.`part_id` WHERE l.`order_id`=?");
        ErpDb.AddParameters(command, orderId);
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static (long FromUtc, long ToUtc) PeriodWindow(string? period)
    {
        if (!string.IsNullOrWhiteSpace(period)
            && DateOnly.TryParseExact(period.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
        {
            var from = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var to = new DateTimeOffset(month.Year, month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds();
            return (from, to);
        }
        return (0, 0);
    }
}
