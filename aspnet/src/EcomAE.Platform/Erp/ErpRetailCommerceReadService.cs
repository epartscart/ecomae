using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Read twin of PHP <c>erp_tabs_retail_commerce.php</c> / <c>erp_tabs_retail_barcode.php</c>
/// (<c>epc_rtl_summary</c>, <c>epc_rtl_channels</c>, <c>epc_rtl_assortments</c>,
/// <c>epc_rtl_discounts</c>, <c>epc_rtl_transactions</c>, <c>epc_rtl_statement_totals</c>).
/// Industry-neutral: retail channels exist for every tenant, not only jewellery.
/// </summary>
public interface IErpRetailCommerceReadService
{
    Task<ErpRetailCommerceSnapshot> LoadAsync(long companyId, long channelId, CancellationToken cancellationToken = default);
}

public sealed record ErpRetailChannelRow(long Id, string Code, string Name, string ChannelType, string Currency, bool Active, int AssortCount);
public sealed record ErpRetailAssortmentRow(long ItemId, string Sku, string ItemName, bool Active);
public sealed record ErpRetailDiscountRow(long Id, long ChannelId, string ChannelCode, string Code, string Name, string DiscType, decimal Value, bool Active);
public sealed record ErpRetailTxnRow(long Id, string ReceiptNo, decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal Total, string Tender, long TxnTime);
public sealed record ErpRetailStatement(int Count, decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal Total, IReadOnlyList<KeyValuePair<string, decimal>> ByTender);
public sealed record ErpRetailItemRow(long Id, string Sku, string Name, string Unit, decimal Qty, decimal AvgCost);
public sealed record ErpRetailBarcodeFormatRow(long Id, string Code, string Name, string Symbology, string Pattern, string Note, bool Active);

public sealed record ErpRetailCommerceSnapshot(
    bool Provisioned,
    long CompanyId,
    long SelectedChannelId,
    int ChannelCount,
    int ActiveDiscountCount,
    int TransactionCount,
    decimal SalesTotal,
    IReadOnlyList<ErpRetailChannelRow> Channels,
    IReadOnlyList<ErpRetailAssortmentRow> Assortment,
    IReadOnlyList<ErpRetailDiscountRow> Discounts,
    IReadOnlyList<ErpRetailTxnRow> Transactions,
    ErpRetailStatement Statement,
    IReadOnlyList<ErpRetailItemRow> Items,
    IReadOnlyList<ErpRetailBarcodeFormatRow> BarcodeFormats,
    string Message)
{
    public static ErpRetailCommerceSnapshot Empty(string message) => new(
        false, 0, 0, 0, 0, 0, 0m, [], [], [], [],
        new ErpRetailStatement(0, 0m, 0m, 0m, 0m, 0m, []), [], [], message);
}

public sealed class ErpRetailCommerceReadService : IErpRetailCommerceReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRetailCommerceReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpRetailCommerceSnapshot> LoadAsync(long companyId, long channelId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpRetailCommerceSnapshot.Empty("TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpRtlSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
            var tables = await ErpTableCatalog.CountPresentAsync(
                connection,
                ["epc_rtl_channel", "epc_rtl_assortment", "epc_rtl_discount", "epc_rtl_txn"],
                cancellationToken).ConfigureAwait(false);
            if (tables < 4)
            {
                return ErpRetailCommerceSnapshot.Empty("Retail tables could not be provisioned (PHP epc_rtl_ensure_schema parity).")
                    with { Items = await ItemsAsync(connection, cancellationToken).ConfigureAwait(false),
                           BarcodeFormats = await FormatsAsync(connection, cancellationToken).ConfigureAwait(false) };
            }

            if (companyId <= 0)
            {
                companyId = await ErpDb.LongAsync(connection, null,
                    "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active` = 1 ORDER BY `id` LIMIT 1",
                    cancellationToken).ConfigureAwait(false);
            }

            var cid = companyId.ToString(CultureInfo.InvariantCulture);
            var wa = companyId > 0 ? " AND company_id=" + cid : string.Empty;
            var wc = companyId > 0 ? " AND c.`company_id`=" + cid : string.Empty;
            var channelCount = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_rtl_channel` WHERE 1=1" + wa, cancellationToken).ConfigureAwait(false);
            var discountCount = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_rtl_discount` WHERE active=1" + wa, cancellationToken).ConfigureAwait(false);
            var txnCount = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_rtl_txn` WHERE 1=1" + wa, cancellationToken).ConfigureAwait(false);
            var salesTotal = await ErpDb.DecimalAsync(connection, null, "SELECT COALESCE(SUM(total),0) FROM `epc_rtl_txn` WHERE 1=1" + wa, cancellationToken).ConfigureAwait(false);

            var channels = new List<ErpRetailChannelRow>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT c.`id`,c.`code`,c.`name`,c.`channel_type`,c.`currency`,c.`active`,"
                    + "(SELECT COUNT(*) FROM `epc_rtl_assortment` a WHERE a.`channel_id`=c.`id` AND a.`active`=1) AS assort_count"
                    + " FROM `epc_rtl_channel` c WHERE 1=1" + wc + " ORDER BY c.`code`";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    channels.Add(new ErpRetailChannelRow(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Str(reader, 1), Str(reader, 2), Str(reader, 3), Str(reader, 4),
                        Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture) == 1,
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture)));
                }
            }

            if (channelId <= 0 || channels.All(c => c.Id != channelId))
            {
                channelId = channels.Count > 0 ? channels[0].Id : 0;
            }

            var assortment = new List<ErpRetailAssortmentRow>();
            if (channelId > 0)
            {
                var invItemsPresent = await ErpDb.TableKnownAsync(connection, "epc_erp_inv_items", cancellationToken).ConfigureAwait(false);
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(invItemsPresent
                    ? "SELECT a.`item_id`, COALESCE(i.`sku`,''), COALESCE(i.`name`,''), a.`active`"
                      + " FROM `epc_rtl_assortment` a LEFT JOIN `epc_erp_inv_items` i ON i.`id`=a.`item_id`"
                      + " WHERE a.`channel_id`=? ORDER BY a.`item_id`"
                    : "SELECT a.`item_id`, '', '', a.`active` FROM `epc_rtl_assortment` a"
                      + " WHERE a.`channel_id`=? ORDER BY a.`item_id`");
                ErpDb.AddParameters(command, channelId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    assortment.Add(new ErpRetailAssortmentRow(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Str(reader, 1), Str(reader, 2),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 1));
                }
            }

            var discounts = new List<ErpRetailDiscountRow>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT `id`,`channel_id`,`code`,`name`,`disc_type`,`value`,`active` FROM `epc_rtl_discount` WHERE 1=1" + wa + " ORDER BY `id` DESC LIMIT 200";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var chId = Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                    discounts.Add(new ErpRetailDiscountRow(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        chId,
                        channels.FirstOrDefault(c => c.Id == chId)?.Code ?? "All",
                        Str(reader, 2), Str(reader, 3), Str(reader, 4),
                        Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1));
                }
            }

            var txns = new List<ErpRetailTxnRow>();
            if (channelId > 0 && companyId > 0)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`,`receipt_no`,`gross`,`discount`,`net`,`tax`,`total`,`tender_type`,`txn_time`"
                    + " FROM `epc_rtl_txn` WHERE company_id=? AND channel_id=? ORDER BY id DESC LIMIT 50");
                ErpDb.AddParameters(command, companyId, channelId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    txns.Add(new ErpRetailTxnRow(
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Str(reader, 1),
                        Dec(reader, 2), Dec(reader, 3), Dec(reader, 4), Dec(reader, 5), Dec(reader, 6),
                        Str(reader, 7),
                        Convert.ToInt64(reader.GetValue(8), CultureInfo.InvariantCulture)));
                }
            }

            var byTender = txns.GroupBy(t => string.IsNullOrEmpty(t.Tender) ? "cash" : t.Tender)
                .Select(g => new KeyValuePair<string, decimal>(g.Key, Math.Round(g.Sum(t => t.Total), 2)))
                .ToList();
            var statement = new ErpRetailStatement(
                txns.Count,
                Math.Round(txns.Sum(t => t.Gross), 2),
                Math.Round(txns.Sum(t => t.Discount), 2),
                Math.Round(txns.Sum(t => t.Net), 2),
                Math.Round(txns.Sum(t => t.Tax), 2),
                Math.Round(txns.Sum(t => t.Total), 2),
                byTender);

            return new ErpRetailCommerceSnapshot(
                true, companyId, channelId, channelCount, discountCount, txnCount, Math.Round(salesTotal, 2),
                channels, assortment, discounts, txns, statement,
                await ItemsAsync(connection, cancellationToken).ConfigureAwait(false),
                await FormatsAsync(connection, cancellationToken).ConfigureAwait(false),
                string.Empty);
        }
        catch (DbException ex)
        {
            return ErpRetailCommerceSnapshot.Empty("database-error: " + ex.Message);
        }
    }

    private static async Task<IReadOnlyList<ErpRetailItemRow>> ItemsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpRetailItemRow>();
        var present = await ErpTableCatalog.CountPresentAsync(
            connection,
            ["epc_erp_inv_items", "epc_erp_inv_stock"],
            cancellationToken).ConfigureAwait(false);
        if (present < 2)
        {
            return rows;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT i.`id`, i.`sku`, i.`name`, i.`unit`,"
            + " (SELECT COALESCE(SUM(s.`qty_on_hand`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS qty,"
            + " (SELECT COALESCE(AVG(s.`avg_unit_cost`),0) FROM `epc_erp_inv_stock` s WHERE s.`item_id` = i.`id`) AS cost"
            + " FROM `epc_erp_inv_items` i WHERE i.`active` = 1 ORDER BY i.`sku` LIMIT 200";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpRetailItemRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Str(reader, 1), Str(reader, 2), Str(reader, 3), Dec(reader, 4), Dec(reader, 5)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ErpRetailBarcodeFormatRow>> FormatsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpRetailBarcodeFormatRow>();
        var present = await ErpTableCatalog.CountPresentAsync(
            connection,
            ["epc_erp_pm_barcode_formats"],
            cancellationToken).ConfigureAwait(false);
        if (present < 1)
        {
            return rows;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`,`code`,`name`,`symbology`,`pattern`,`note`,`active` FROM `epc_erp_pm_barcode_formats` ORDER BY `code` LIMIT 200";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpRetailBarcodeFormatRow(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Str(reader, 1), Str(reader, 2), Str(reader, 3), Str(reader, 4), Str(reader, 5),
                Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1));
        }

        return rows;
    }

    private static string Str(DbDataReader reader, int i)
        => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;

    private static decimal Dec(DbDataReader reader, int i)
        => reader.IsDBNull(i) ? 0m : Convert.ToDecimal(reader.GetValue(i), CultureInfo.InvariantCulture);
}
