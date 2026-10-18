using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public interface IErpShippingPackService
{
    Task<ErpShippingPackSnapshot> LoadAsync(long packId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PrepareAsync(ErpShippingPackPrepare request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ReleaseAsync(long packId, CancellationToken cancellationToken = default);
}

public sealed record ErpShippingPackSummary(
    long Id,
    string ShippingInvoiceNo,
    string PackingSlipNo,
    long CustomerId,
    string CustomerName,
    string Currency,
    decimal Gross,
    int Packages,
    int InvoiceCount,
    bool Active,
    long TimeCreated);

public sealed record ErpShippingPackInvoiceRow(long DocumentId, string Number, decimal Net, decimal Vat, decimal Gross);

public sealed record ErpShippingPackLineRow(
    long DocumentId,
    string InvoiceNumber,
    int LineNo,
    string Item,
    string Description,
    decimal Qty,
    string Uom,
    decimal UnitPrice,
    decimal Net,
    decimal TaxRate,
    decimal Vat,
    decimal Gross);

public sealed record ErpShippingPackDetail(
    long Id,
    string ShippingInvoiceNo,
    string PackingSlipNo,
    long CustomerId,
    string CustomerName,
    string Currency,
    decimal Net,
    decimal Vat,
    decimal Gross,
    int Packages,
    decimal WeightKg,
    string Carrier,
    string TrackingNo,
    string ShipTo,
    bool Active,
    long TimeCreated,
    IReadOnlyList<ErpShippingPackInvoiceRow> Invoices,
    IReadOnlyList<ErpShippingPackLineRow> Rows);

public sealed record ErpShippingPackSnapshot(ErpShippingPackDetail? Pack, IReadOnlyList<ErpShippingPackSummary> Recent);

public sealed record ErpShippingPackPrepare(
    IReadOnlyList<long> InvoiceIds,
    int Packages,
    decimal WeightKg,
    string? Carrier,
    string? TrackingNo,
    string? ShipTo);

public sealed class ErpShippingPackService : IErpShippingPackService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpShippingPackService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpShippingPackSnapshot> LoadAsync(long packId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpShippingPackSnapshot(null, []);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var recent = await RecentAsync(connection, cancellationToken).ConfigureAwait(false);
        var pack = packId > 0 ? await DetailAsync(connection, packId, cancellationToken).ConfigureAwait(false) : null;
        return new ErpShippingPackSnapshot(pack, recent);
    }

    public async Task<ErpSimpleWriteResult> PrepareAsync(ErpShippingPackPrepare request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shipment = ErpShippingPack.RefuseShipment(request.Packages, request.WeightKg);
        if (shipment.Count > 0)
        {
            return ErpSimpleWriteResult.Fail("blocked", string.Join(" ", shipment));
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var ids = (request.InvoiceIds ?? []).Where(id => id > 0).ToArray();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var sources = await SourcesAsync(connection, ids, cancellationToken).ConfigureAwait(false);
        var packed = await PackedAsync(connection, ids, cancellationToken).ConfigureAwait(false);
        var reasons = ErpShippingPack.Refuse(ids, sources, packed);
        if (reasons.Count > 0)
        {
            return ErpSimpleWriteResult.Fail("blocked", string.Join(" ", reasons));
        }

        var ordered = ids.Select(id => sources.First(item => item.Id == id)).ToArray();
        var built = ErpShippingPack.Compose(ordered);
        var carrier = Clip(request.Carrier, 80);
        var tracking = Clip(request.TrackingNo, 80);
        var shipTo = Clip(request.ShipTo, 255);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var count = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT COUNT(*) FROM `epc_erp_shipping_packs`",
            cancellationToken).ConfigureAwait(false);
        var number = (count + 1).ToString(CultureInfo.InvariantCulture);
        var shippingNo = "SHI-" + number;
        var slipNo = "PKS-" + number;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_shipping_packs` (`shipping_invoice_no`,`packing_slip_no`,`customer_id`,`customer_name`,`currency_code`,`net_amount`,`vat_amount`,`gross_amount`,`packages`,`weight_kg`,`carrier`,`tracking_no`,`ship_to`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,1,?)"),
                cancellationToken,
                shippingNo,
                slipNo,
                built.CustomerId,
                Clip(built.CustomerName, 255),
                built.Currency,
                built.Net,
                built.Vat,
                built.Gross,
                request.Packages,
                ErpShippingPack.RoundQty(request.WeightKg),
                carrier,
                tracking,
                shipTo,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var invoice in built.Invoices)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_shipping_pack_invoices` (`pack_id`,`document_id`,`invoice_number`,`net_amount`,`vat_amount`,`gross_amount`) VALUES (?,?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    invoice.Id,
                    Clip(invoice.Number, 64),
                    invoice.Net,
                    invoice.Vat,
                    invoice.Gross).ConfigureAwait(false);
            }

            foreach (var line in built.Lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_shipping_pack_rows` (`pack_id`,`document_id`,`invoice_number`,`line_no`,`item_name`,`item_description`,`qty`,`uom_code`,`unit_price`,`line_net`,`tax_rate`,`tax_amount`,`gross_amount`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    line.DocumentId,
                    Clip(line.InvoiceNumber, 64),
                    line.LineNo,
                    Clip(line.Item, 255),
                    Clip(line.Description, 255),
                    line.Qty,
                    Clip(line.Uom, 16),
                    line.UnitPrice,
                    line.Net,
                    line.TaxRate,
                    line.Vat,
                    line.Gross).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Shipping invoice " + shippingNo + " and packing slip " + slipNo + " are ready. The source invoices stay unchanged.",
                id);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> ReleaseAsync(long packId, CancellationToken cancellationToken = default)
    {
        if (packId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Open a shipping pack to release it.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_shipping_packs` SET `active`=0 WHERE `id`=? AND `active`=1"),
            cancellationToken,
            packId).ConfigureAwait(false);
        if (updated > 0)
        {
            return ErpSimpleWriteResult.Ok("Shipping pack released. Those invoices can be merged again.", packId);
        }

        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_shipping_packs` WHERE `id`=?"),
            cancellationToken,
            packId).ConfigureAwait(false);
        return exists > 0
            ? ErpSimpleWriteResult.Fail("blocked", "This shipping pack is already released.")
            : ErpSimpleWriteResult.Fail("invalid", "Shipping pack was not found.");
    }

    private static async Task<IReadOnlyList<ErpShippingPack.Source>> SourcesAsync(
        DbConnection connection,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var marks = string.Join(",", ids.Select(_ => "?"));
        var headers = new List<ErpShippingPack.Source>();
        var lines = new Dictionary<long, List<ErpShippingPack.Line>>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`invoice_number`,''), IFNULL(`user_id`,0), IFNULL(`currency_code`,''), IFNULL(`status`,''), IFNULL(`doc_category`,''), IFNULL(`active`,1), IFNULL(`subtotal_ex_vat`,0), IFNULL(`total_vat`,0), IFNULL(`total_incl_vat`,0), IFNULL(`buyer_json`,'')"
                + " FROM `epc_einvoice_documents` WHERE `id` IN (" + marks + ")");
            ErpDb.AddParameters(command, ids.Select(id => (object)id).ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Long(reader, 0);
                headers.Add(new ErpShippingPack.Source(
                    id,
                    Long(reader, 2),
                    Text(reader, 1),
                    Text(reader, 3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Long(reader, 6) != 0,
                    Dec(reader, 7),
                    Dec(reader, 8),
                    Dec(reader, 9),
                    BuyerName(Text(reader, 10), Long(reader, 2)),
                    []));
            }
        }
        catch (DbException)
        {
            return [];
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `document_id`, IFNULL(`line_no`,0), IFNULL(`item_name`,''), IFNULL(`item_description`,''), IFNULL(`quantity`,0), IFNULL(`uom_code`,''), IFNULL(`unit_price`,0), IFNULL(`line_net`,0), IFNULL(`tax_rate`,0), IFNULL(`tax_amount`,0), IFNULL(`gross_amount`,0)"
                + " FROM `epc_einvoice_lines` WHERE `document_id` IN (" + marks + ") ORDER BY `document_id`, `line_no`, `id`");
            ErpDb.AddParameters(command, ids.Select(id => (object)id).ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var documentId = Long(reader, 0);
                if (!lines.TryGetValue(documentId, out var bucket))
                {
                    bucket = [];
                    lines[documentId] = bucket;
                }

                bucket.Add(new ErpShippingPack.Line(
                    documentId,
                    string.Empty,
                    (int)Long(reader, 1),
                    Text(reader, 2),
                    Text(reader, 3),
                    Dec(reader, 4),
                    Text(reader, 5),
                    Dec(reader, 6),
                    Dec(reader, 7),
                    Dec(reader, 8),
                    Dec(reader, 9),
                    Dec(reader, 10)));
            }
        }
        catch (DbException)
        {
            lines.Clear();
        }

        return headers.Select(header => header with
        {
            Lines = lines.TryGetValue(header.Id, out var bucket) ? bucket : [],
        }).ToArray();
    }

    private static async Task<IReadOnlySet<long>> PackedAsync(
        DbConnection connection,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        var packed = new HashSet<long>();
        if (ids.Count == 0)
        {
            return packed;
        }

        try
        {
            var marks = string.Join(",", ids.Select(_ => "?"));
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT i.`document_id` FROM `epc_erp_shipping_pack_invoices` i"
                + " INNER JOIN `epc_erp_shipping_packs` p ON p.`id`=i.`pack_id`"
                + " WHERE p.`active`=1 AND i.`document_id` IN (" + marks + ")");
            ErpDb.AddParameters(command, ids.Select(id => (object)id).ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                packed.Add(Long(reader, 0));
            }
        }
        catch (DbException)
        {
            return packed;
        }

        return packed;
    }

    private static async Task<IReadOnlyList<ErpShippingPackSummary>> RecentAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ErpShippingPackSummary>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT p.`id`, IFNULL(p.`shipping_invoice_no`,''), IFNULL(p.`packing_slip_no`,''), IFNULL(p.`customer_id`,0), IFNULL(p.`customer_name`,''), IFNULL(p.`currency_code`,''), IFNULL(p.`gross_amount`,0), IFNULL(p.`packages`,0), IFNULL(p.`active`,0), IFNULL(p.`time_created`,0), COUNT(i.`id`)"
                + " FROM `epc_erp_shipping_packs` p LEFT JOIN `epc_erp_shipping_pack_invoices` i ON i.`pack_id`=p.`id`"
                + " GROUP BY p.`id`, p.`shipping_invoice_no`, p.`packing_slip_no`, p.`customer_id`, p.`customer_name`, p.`currency_code`, p.`gross_amount`, p.`packages`, p.`active`, p.`time_created`"
                + " ORDER BY p.`id` DESC LIMIT 12";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpShippingPackSummary(
                    Long(reader, 0),
                    Text(reader, 1),
                    Text(reader, 2),
                    Long(reader, 3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Dec(reader, 6),
                    (int)Long(reader, 7),
                    (int)Long(reader, 10),
                    Long(reader, 8) != 0,
                    Long(reader, 9)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }

    private static async Task<ErpShippingPackDetail?> DetailAsync(DbConnection connection, long packId, CancellationToken cancellationToken)
    {
        ErpShippingPackDetail? header = null;
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`shipping_invoice_no`,''), IFNULL(`packing_slip_no`,''), IFNULL(`customer_id`,0), IFNULL(`customer_name`,''), IFNULL(`currency_code`,''), IFNULL(`net_amount`,0), IFNULL(`vat_amount`,0), IFNULL(`gross_amount`,0), IFNULL(`packages`,0), IFNULL(`weight_kg`,0), IFNULL(`carrier`,''), IFNULL(`tracking_no`,''), IFNULL(`ship_to`,''), IFNULL(`active`,0), IFNULL(`time_created`,0)"
                + " FROM `epc_erp_shipping_packs` WHERE `id`=? LIMIT 1");
            ErpDb.AddParameters(command, packId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                header = new ErpShippingPackDetail(
                    Long(reader, 0),
                    Text(reader, 1),
                    Text(reader, 2),
                    Long(reader, 3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Dec(reader, 6),
                    Dec(reader, 7),
                    Dec(reader, 8),
                    (int)Long(reader, 9),
                    Dec(reader, 10),
                    Text(reader, 11),
                    Text(reader, 12),
                    Text(reader, 13),
                    Long(reader, 14) != 0,
                    Long(reader, 15),
                    [],
                    []);
            }
        }
        catch (DbException)
        {
            return null;
        }

        if (header is null)
        {
            return null;
        }

        var invoices = new List<ErpShippingPackInvoiceRow>();
        var rows = new List<ErpShippingPackLineRow>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `document_id`, IFNULL(`invoice_number`,''), IFNULL(`net_amount`,0), IFNULL(`vat_amount`,0), IFNULL(`gross_amount`,0) FROM `epc_erp_shipping_pack_invoices` WHERE `pack_id`=? ORDER BY `id`");
            ErpDb.AddParameters(command, packId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                invoices.Add(new ErpShippingPackInvoiceRow(Long(reader, 0), Text(reader, 1), Dec(reader, 2), Dec(reader, 3), Dec(reader, 4)));
            }
        }
        catch (DbException)
        {
            invoices.Clear();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `document_id`, IFNULL(`invoice_number`,''), IFNULL(`line_no`,0), IFNULL(`item_name`,''), IFNULL(`item_description`,''), IFNULL(`qty`,0), IFNULL(`uom_code`,''), IFNULL(`unit_price`,0), IFNULL(`line_net`,0), IFNULL(`tax_rate`,0), IFNULL(`tax_amount`,0), IFNULL(`gross_amount`,0)"
                + " FROM `epc_erp_shipping_pack_rows` WHERE `pack_id`=? ORDER BY `id`");
            ErpDb.AddParameters(command, packId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpShippingPackLineRow(
                    Long(reader, 0),
                    Text(reader, 1),
                    (int)Long(reader, 2),
                    Text(reader, 3),
                    Text(reader, 4),
                    Dec(reader, 5),
                    Text(reader, 6),
                    Dec(reader, 7),
                    Dec(reader, 8),
                    Dec(reader, 9),
                    Dec(reader, 10),
                    Dec(reader, 11)));
            }
        }
        catch (DbException)
        {
            rows.Clear();
        }

        return header with { Invoices = invoices, Rows = rows };
    }

    private static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_shipping_packs` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`shipping_invoice_no` varchar(32) NOT NULL DEFAULT '',"
            + "`packing_slip_no` varchar(32) NOT NULL DEFAULT '',"
            + "`customer_id` int(11) NOT NULL DEFAULT 0,"
            + "`customer_name` varchar(255) NOT NULL DEFAULT '',"
            + "`currency_code` varchar(8) NOT NULL DEFAULT 'AED',"
            + "`net_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`packages` int(11) NOT NULL DEFAULT 0,"
            + "`weight_kg` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + "`carrier` varchar(80) NOT NULL DEFAULT '',"
            + "`tracking_no` varchar(80) NOT NULL DEFAULT '',"
            + "`ship_to` varchar(255) NOT NULL DEFAULT '',"
            + "`active` tinyint(1) NOT NULL DEFAULT 1,"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `x_customer` (`customer_id`,`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Merged shipping documents'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_shipping_pack_invoices` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`pack_id` int(11) NOT NULL DEFAULT 0,"
            + "`document_id` int(11) NOT NULL DEFAULT 0,"
            + "`invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + "`net_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "PRIMARY KEY (`id`), KEY `x_pack` (`pack_id`), KEY `x_doc` (`document_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Invoices on a shipping pack'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_shipping_pack_rows` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`pack_id` int(11) NOT NULL DEFAULT 0,"
            + "`document_id` int(11) NOT NULL DEFAULT 0,"
            + "`invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + "`line_no` int(11) NOT NULL DEFAULT 0,"
            + "`item_name` varchar(255) NOT NULL DEFAULT '',"
            + "`item_description` varchar(255) NOT NULL DEFAULT '',"
            + "`qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + "`uom_code` varchar(16) NOT NULL DEFAULT '',"
            + "`unit_price` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + "`line_net` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`tax_rate` decimal(8,2) NOT NULL DEFAULT 0.00,"
            + "`tax_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "`gross_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + "PRIMARY KEY (`id`), KEY `x_pack` (`pack_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Lines on a shipping pack'",
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuyerName(string json, long customerId)
    {
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("buyer_name", out var name))
                {
                    var text = name.GetString()?.Trim() ?? string.Empty;
                    if (text.Length > 0)
                    {
                        return text;
                    }
                }
            }
            catch (JsonException)
            {
                // A buyer card that is not JSON still leaves the customer id.
            }
        }

        return "Customer " + customerId.ToString(CultureInfo.InvariantCulture);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);

    private static decimal Dec(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static long Long(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
}
