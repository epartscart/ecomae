using System.Data.Common;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

public interface IErpO2cDocumentPrintService
{
    Task<string?> RenderSalesOrderAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> RenderInvoiceAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> RenderPurchaseOrderAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> RenderPurchaseAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> RenderLpoAsync(long orderId, long poId, CancellationToken cancellationToken = default);
    Task<string?> RenderJewelleryVoucherAsync(long id, CancellationToken cancellationToken = default);
}

/// <summary>Printable document renderer for O2C sales orders and invoices (PHP invoice_print-style HTML + lines grid).</summary>
public sealed class ErpO2cDocumentPrintService : IErpO2cDocumentPrintService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpO2cDocumentPrintService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<string?> RenderSalesOrderAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.`so_no`, o.`title`, o.`status`, o.`amount_ex_vat`, o.`vat_amount`, o.`total_amount`,
                   o.`notes`, o.`time_created`, IFNULL(u.`email`, '') AS `customer_email`, o.`sales_invoice_id`
            FROM `epc_erp_sales_orders` o
            LEFT JOIN `users` u ON u.`user_id` = o.`customer_user_id`
            WHERE o.`id` = @p0
            LIMIT 1
            """;
        ErpDb.AddParameters(command, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var soNo = ReadString(reader, 0);
        var title = ReadString(reader, 1);
        var status = ReadString(reader, 2);
        var amountExVat = ReadDecimal(reader, 3);
        var vatAmount = ReadDecimal(reader, 4);
        var totalAmount = ReadDecimal(reader, 5);
        var notes = ReadString(reader, 6);
        var timeCreated = ReadLong(reader, 7);
        var customerEmail = ReadString(reader, 8);
        var salesInvoiceId = ReadLong(reader, 9);
        await reader.DisposeAsync().ConfigureAwait(false);

        var lines = await LoadSalesOrderLinesAsync(connection, id, cancellationToken).ConfigureAwait(false);

        var builder = StartHtml($"Sales order {Encode(soNo)}");
        builder.Append("<h1>Sales order ").Append(Encode(soNo)).Append("</h1>");
        builder.Append("<p>");
        Meta(builder, "Customer", customerEmail);
        Meta(builder, "Title", title);
        Meta(builder, "Status", status);
        if (salesInvoiceId > 0)
        {
            Meta(builder, "Invoice", $"INV-{salesInvoiceId}");
        }
        builder.Append("</p>");
        builder.Append("""
            <table><thead><tr><th style="text-align:left">Item</th><th style="text-align:left">Description</th>
            <th style="text-align:right">Qty</th><th style="text-align:right">Unit price</th><th style="text-align:right">Amount</th></tr></thead><tbody>
            """);
        foreach (var line in lines)
        {
            builder.Append("<tr><td>").Append(Encode(Blank(line.ItemCode))).Append("</td><td>")
                .Append(Encode(Blank(line.Description))).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.Qty)).Append("</td><td style=\"text-align:right\">").Append(N(line.UnitPrice)).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.Amount)).Append("</td></tr>");
        }
        builder.Append("</tbody></table>");
        Totals(builder, amountExVat, vatAmount, totalAmount);
        return FinishHtml(builder, notes, timeCreated);
    }

    public async Task<string?> RenderInvoiceAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.`invoice_number`, d.`status`, d.`currency_code`, d.`subtotal_ex_vat`, d.`total_vat`,
                   d.`total_incl_vat`, d.`paid_amount`, d.`amount_due`, d.`issue_date`, d.`payment_due_date`,
                   d.`doc_category`, IFNULL(u.`email`, '') AS `customer_email`, d.`sales_order_id`, d.`fta_report_status`
            FROM `epc_einvoice_documents` d
            LEFT JOIN `users` u ON u.`user_id` = d.`user_id`
            WHERE d.`id` = @p0
            LIMIT 1
            """;
        ErpDb.AddParameters(command, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var invoiceNumber = ReadString(reader, 0);
        var status = ReadString(reader, 1);
        var currency = ReadString(reader, 2);
        var subtotal = ReadDecimal(reader, 3);
        var vat = ReadDecimal(reader, 4);
        var total = ReadDecimal(reader, 5);
        var paid = ReadDecimal(reader, 6);
        var due = ReadDecimal(reader, 7);
        var issueDate = ReadLong(reader, 8);
        var dueDate = ReadLong(reader, 9);
        var docCategory = ReadString(reader, 10);
        var customerEmail = ReadString(reader, 11);
        var salesOrderId = ReadLong(reader, 12);
        var ftaStatus = ReadString(reader, 13);
        await reader.DisposeAsync().ConfigureAwait(false);

        var lines = await LoadInvoiceLinesAsync(connection, id, cancellationToken).ConfigureAwait(false);

        var builder = StartHtml($"Invoice {Encode(invoiceNumber)}");
        var docLabel = docCategory switch
        {
            "tax_invoice" => "Tax invoice",
            "credit_note" => "Credit note",
            "proforma_invoice" => "Proforma invoice",
            _ => string.IsNullOrWhiteSpace(docCategory) ? "Invoice" : docCategory.Replace('_', ' '),
        };
        builder.Append("<h1>").Append(Encode(docLabel))
            .Append(' ').Append(Encode(invoiceNumber)).Append("</h1>");
        builder.Append("<p>");
        Meta(builder, "Customer", customerEmail);
        Meta(builder, "Issue date", FmtDate(issueDate));
        if (dueDate > 0)
        {
            Meta(builder, "Due date", FmtDate(dueDate));
        }
        Meta(builder, "Status", status);
        Meta(builder, "Currency", currency);
        if (salesOrderId > 0)
        {
            Meta(builder, "Sales order", $"SO-{salesOrderId}");
        }
        if (!string.IsNullOrWhiteSpace(ftaStatus))
        {
            Meta(builder, "FTA report", ftaStatus);
        }
        builder.Append("</p>");
        builder.Append("""
            <table><thead><tr><th style="text-align:right">#</th><th style="text-align:left">Item</th>
            <th style="text-align:right">Qty</th><th>UoM</th><th style="text-align:right">Unit price</th>
            <th style="text-align:right">Net</th><th style="text-align:right">VAT %</th><th style="text-align:right">VAT</th>
            <th style="text-align:right">Gross</th></tr></thead><tbody>
            """);
        foreach (var line in lines)
        {
            builder.Append("<tr><td style=\"text-align:right\">").Append(line.LineNo).Append("</td><td>")
                .Append(Encode(Blank(line.ItemName))).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.Quantity)).Append("</td><td>").Append(Encode(Blank(line.UomCode))).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.UnitPrice)).Append("</td><td style=\"text-align:right\">").Append(N(line.LineNet)).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.TaxRate)).Append("</td><td style=\"text-align:right\">").Append(N(line.TaxAmount)).Append("</td><td style=\"text-align:right\">")
                .Append(N(line.GrossAmount)).Append("</td></tr>");
        }
        builder.Append("</tbody></table>");
        Totals(builder, subtotal, vat, total);
        builder.Append("<p style=\"margin-top:8px;\"><strong>Paid:</strong> ").Append(N(paid))
            .Append("&nbsp;&nbsp;<strong>Due:</strong> ").Append(N(due)).Append("</p>");
        return FinishHtml(builder, null, 0);
    }

    public async Task<string?> RenderPurchaseOrderAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.`po_no`, o.`status`, o.`amount_ex_vat`, o.`vat_amount`, o.`total_amount`,
                   IFNULL(o.`notes`, ''), o.`time_created`, IFNULL(s.`name`, ''), o.`purchase_id`, o.`order_id`,
                   IFNULL(o.`supplier_id`,0)
            FROM `epc_erp_purchase_orders` o
            LEFT JOIN `epc_erp_suppliers` s ON s.`id` = o.`supplier_id`
            WHERE o.`id` = @p0
            LIMIT 1
            """;
        ErpDb.AddParameters(command, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var poNo = ReadString(reader, 0);
        var status = ReadString(reader, 1);
        var amountExVat = ReadDecimal(reader, 2);
        var vatAmount = ReadDecimal(reader, 3);
        var totalAmount = ReadDecimal(reader, 4);
        var notes = ReadString(reader, 5);
        var timeCreated = ReadLong(reader, 6);
        var supplierName = ReadString(reader, 7);
        var purchaseId = ReadLong(reader, 8);
        var orderId = ReadLong(reader, 9);
        var supplierId = ReadLong(reader, 10);
        await reader.DisposeAsync().ConfigureAwait(false);

        var lines = new List<PoLine>();
        await using (var lineCommand = connection.CreateCommand())
        {
            lineCommand.CommandText = """
                SELECT `item_code`, `description`, `qty`, `unit_cost_ex_vat`, `line_ex_vat`, `qty_received`
                FROM `epc_erp_po_lines`
                WHERE `po_id` = @p0
                ORDER BY `line_no`, `id`
                """;
            ErpDb.AddParameters(lineCommand, id);
            await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new PoLine(ReadString(lineReader, 0), ReadString(lineReader, 1), ReadDecimal(lineReader, 2), ReadDecimal(lineReader, 3), ReadDecimal(lineReader, 4), ReadDecimal(lineReader, 5)));
            }
        }

        var issuer = await LoadIssuerAsync(connection, cancellationToken).ConfigureAwait(false);
        var party = await LoadSupplierAsync(connection, supplierId, supplierName, cancellationToken).ConfigureAwait(false);
        var printLines = new List<ErpPurchasePrintLine>();
        var i = 0;
        foreach (var line in lines)
        {
            i++;
            var vatShare = amountExVat <= 0m ? 0m : decimal.Round(vatAmount * (line.Amount / amountExVat), 2, MidpointRounding.AwayFromZero);
            printLines.Add(new ErpPurchasePrintLine(
                i,
                string.IsNullOrWhiteSpace(line.Description) ? line.ItemCode : line.Description,
                line.Qty,
                line.UnitCost,
                line.Amount,
                amountExVat > 0m ? decimal.Round(100m * vatAmount / amountExVat, 2, MidpointRounding.AwayFromZero) : 0m,
                vatShare,
                line.Amount + vatShare));
        }

        return ErpPurchaseDocumentPrint.Render(new ErpPurchasePrintDoc(
            ErpPurchasePrintKind.PurchaseOrder,
            string.IsNullOrWhiteSpace(poNo) ? "#" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) : poNo,
            FmtDate(timeCreated),
            "As agreed",
            string.Empty,
            "AED",
            string.Empty,
            ErpPurchaseDocumentPrint.IsOfficialPo(status),
            status,
            issuer,
            party,
            printLines,
            [],
            amountExVat,
            vatAmount,
            totalAmount,
            notes,
            ProcessNote(orderId, purchaseId, 0)));
    }

    public async Task<string?> RenderLpoAsync(long orderId, long poId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        if (poId > 0)
        {
            var po = await RenderPurchaseOrderAsync(poId, cancellationToken).ConfigureAwait(false);
            return po is null
                ? null
                : po.Replace("data-kind=\"purchaseorder\"", "data-kind=\"lpo\"", StringComparison.Ordinal)
                    .Replace("PURCHASE ORDER", "LOCAL PURCHASE ORDER", StringComparison.Ordinal)
                    .Replace("PO NO", "LPO NO", StringComparison.Ordinal);
        }

        if (orderId <= 0)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT o.`id`, IFNULL(o.`time`,0), IFNULL(s.`name`, IFNULL(st.`name`, 'Supplier')),
                       IFNULL(st.`email`,''), IFNULL(st.`phone`,'')
                FROM `shop_orders` o
                LEFT JOIN `shop_orders_items` i ON i.`order_id` = o.`id`
                LEFT JOIN `shop_storages` st ON st.`id` = i.`storage_id`
                LEFT JOIN `epc_erp_suppliers` s ON s.`storage_id` = i.`storage_id`
                WHERE o.`id` = @p0 AND IFNULL(o.`successfully_created`,0) = 1
                LIMIT 1
                """;
            ErpDb.AddParameters(command, orderId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var when = ReadLong(reader, 1);
            var supplierName = ReadString(reader, 2);
            var email = ReadString(reader, 3);
            var phone = ReadString(reader, 4);
            await reader.DisposeAsync().ConfigureAwait(false);

            var lines = new List<ErpPurchasePrintLine>();
            await using (var lineCommand = connection.CreateCommand())
            {
                lineCommand.CommandText = """
                    SELECT IFNULL(`t2_name`, IFNULL(`t2_article`,'')), IFNULL(`count_need`,0), IFNULL(`price`,0)
                    FROM `shop_orders_items` WHERE `order_id` = @p0 ORDER BY `id`
                    """;
                ErpDb.AddParameters(lineCommand, orderId);
                await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var n = 0;
                while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    n++;
                    var qty = ReadDecimal(lineReader, 1);
                    var price = ReadDecimal(lineReader, 2);
                    var amount = decimal.Round(qty * price, 2, MidpointRounding.AwayFromZero);
                    lines.Add(new ErpPurchasePrintLine(n, ReadString(lineReader, 0), qty, price, amount, 0m, 0m, amount));
                }
            }

            var total = lines.Sum(l => l.AmountExVat);
            var issuer = await LoadIssuerAsync(connection, cancellationToken).ConfigureAwait(false);
            return ErpPurchaseDocumentPrint.Render(new ErpPurchasePrintDoc(
                ErpPurchasePrintKind.Lpo,
                "LPO-" + orderId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FmtDate(when),
                "As agreed",
                string.Empty,
                "AED",
                string.Empty,
                true,
                "posted",
                issuer,
                new ErpPurchasePrintParty(string.Empty, supplierName, string.Empty, phone, email, string.Empty),
                lines,
                [],
                total,
                0m,
                total,
                string.Empty,
                "Shop order #" + orderId.ToString(System.Globalization.CultureInfo.InvariantCulture) + " · LPO → PO → PI"));
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<string?> RenderPurchaseAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.`invoice_number`, p.`status`, p.`amount_ex_vat`, p.`vat_amount`, p.`total_amount`,
                   IFNULL(p.`note`, ''), p.`purchase_date`, IFNULL(s.`name`, ''), p.`order_id`,
                   IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a
                           WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0) AS `paid`,
                   IFNULL(p.`supplier_id`,0)
            FROM `epc_erp_purchases` p
            LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`
            WHERE p.`id` = @p0
            LIMIT 1
            """;
        ErpDb.AddParameters(command, id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var invoiceNumber = ReadString(reader, 0);
        var status = ReadString(reader, 1);
        var amountExVat = ReadDecimal(reader, 2);
        var vatAmount = ReadDecimal(reader, 3);
        var totalAmount = ReadDecimal(reader, 4);
        var notes = ReadString(reader, 5);
        var purchaseDate = ReadLong(reader, 6);
        var supplierName = ReadString(reader, 7);
        var orderId = ReadLong(reader, 8);
        var paid = ReadDecimal(reader, 9);
        var supplierId = ReadLong(reader, 10);
        await reader.DisposeAsync().ConfigureAwait(false);

        var lines = new List<BillLine>();
        await using (var lineCommand = connection.CreateCommand())
        {
            lineCommand.CommandText = """
                SELECT `item_id`, `qty`, `unit_cost`, `batch_no`, `expiry_date`
                FROM `epc_erp_purchase_inv_lines`
                WHERE `purchase_id` = @p0
                ORDER BY `id`
                """;
            ErpDb.AddParameters(lineCommand, id);
            await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new BillLine(ReadLong(lineReader, 0), ReadDecimal(lineReader, 1), ReadDecimal(lineReader, 2), ReadString(lineReader, 3), ReadString(lineReader, 4)));
            }
        }

        var issuer = await LoadIssuerAsync(connection, cancellationToken).ConfigureAwait(false);
        var party = await LoadSupplierAsync(connection, supplierId, supplierName, cancellationToken).ConfigureAwait(false);
        var printLines = new List<ErpPurchasePrintLine>();
        var i = 0;
        foreach (var line in lines)
        {
            i++;
            var amount = decimal.Round(line.Qty * line.UnitCost, 2, MidpointRounding.AwayFromZero);
            var vatShare = amountExVat <= 0m ? 0m : decimal.Round(vatAmount * (amount / amountExVat), 2, MidpointRounding.AwayFromZero);
            var desc = line.ItemId > 0 ? "Item #" + line.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Purchase line";
            if (!string.IsNullOrWhiteSpace(line.BatchNo))
            {
                desc += " · batch " + line.BatchNo;
            }

            printLines.Add(new ErpPurchasePrintLine(
                i,
                desc,
                line.Qty,
                line.UnitCost,
                amount,
                amountExVat > 0m ? decimal.Round(100m * vatAmount / amountExVat, 2, MidpointRounding.AwayFromZero) : 0m,
                vatShare,
                amount + vatShare));
        }

        return ErpPurchaseDocumentPrint.Render(new ErpPurchasePrintDoc(
            ErpPurchasePrintKind.PurchaseInvoice,
            string.IsNullOrWhiteSpace(invoiceNumber) ? "#" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) : invoiceNumber,
            FmtDate(purchaseDate),
            paid > 0m ? "Part paid" : "Due on receipt",
            string.Empty,
            "AED",
            string.Empty,
            ErpPurchaseDocumentPrint.IsOfficialPi(status),
            status,
            issuer,
            party,
            printLines,
            paid > 0m ? [new ErpPurchasePrintAddon("Paid", paid), new ErpPurchasePrintAddon("Due", Math.Max(totalAmount - paid, 0m))] : [],
            amountExVat,
            vatAmount,
            totalAmount,
            notes,
            ProcessNote(orderId, id, 0)));
    }

    public async Task<string?> RenderJewelleryVoucherAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`voc_type`,''), IFNULL(`voc_no`,0), IFNULL(`voc_date`,''), IFNULL(`party_code`,''),
                       IFNULL(`party_name`,''), IFNULL(`email`,''), IFNULL(`tel1`, IFNULL(`mobile`,'')),
                       IFNULL(`salesman`,''), IFNULL(`metal_rate`,0), IFNULL(`is_fixed`,0),
                       IFNULL(`net_amount`,0), IFNULL(`vat_amount`,0), IFNULL(`total_with_vat`,0),
                       IFNULL(`status`,''), IFNULL(`authorized`,0), IFNULL(`narration`,''),
                       IFNULL(`supp_inv_no`,''), IFNULL(`cr_days`,0), IFNULL(`city`,''), IFNULL(`po_box`,'')
                FROM `epc_jewel_voucher` WHERE `id` = @p0 LIMIT 1
                """;
            ErpDb.AddParameters(command, id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var vocType = ReadString(reader, 0);
            var vocNo = ReadString(reader, 1);
            var vocDate = ReadString(reader, 2);
            var partyCode = ReadString(reader, 3);
            var partyName = ReadString(reader, 4);
            var email = ReadString(reader, 5);
            var phone = ReadString(reader, 6);
            var salesman = ReadString(reader, 7);
            var metalRate = ReadDecimal(reader, 8);
            var isFixed = ReadLong(reader, 9) != 0;
            var net = ReadDecimal(reader, 10);
            var vat = ReadDecimal(reader, 11);
            var total = ReadDecimal(reader, 12);
            var status = ReadString(reader, 13);
            var authorized = ReadLong(reader, 14) != 0;
            var notes = ReadString(reader, 15);
            var suppInv = ReadString(reader, 16);
            var crDays = ReadLong(reader, 17);
            var city = ReadString(reader, 18);
            var poBox = ReadString(reader, 19);
            await reader.DisposeAsync().ConfigureAwait(false);

            var lines = new List<ErpPurchasePrintLine>();
            var addons = new List<ErpPurchasePrintAddon>();
            await using (var lineCommand = connection.CreateCommand())
            {
                lineCommand.CommandText = """
                    SELECT IFNULL(`description`,''), IFNULL(`qty`,0), IFNULL(`gr_wt`,0), IFNULL(`purity`,0),
                           IFNULL(`pure_wt`,0), IFNULL(`mkg_rate`,0), IFNULL(`mkg_amount`,0),
                           IFNULL(`metal_amount`,0), IFNULL(`net_amount`,0), IFNULL(`total_with_vat`,0),
                           IFNULL(`total_amount`,0)
                    FROM `epc_jewel_voucher_lines` WHERE `voucher_id` = @p0 ORDER BY `line_no`, `id`
                    """;
                ErpDb.AddParameters(lineCommand, id);
                await using var lineReader = await lineCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var n = 0;
                while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    n++;
                    var lineNet = ReadDecimal(lineReader, 8);
                    var lineGross = ReadDecimal(lineReader, 9);
                    var fallback = ReadDecimal(lineReader, 10);
                    if (lineNet == 0m)
                    {
                        lineNet = fallback;
                    }

                    if (lineGross == 0m)
                    {
                        lineGross = lineNet;
                    }

                    lines.Add(new ErpPurchasePrintLine(
                        n,
                        ReadString(lineReader, 0),
                        ReadDecimal(lineReader, 1),
                        ReadDecimal(lineReader, 5),
                        lineNet,
                        vat > 0m && net > 0m ? decimal.Round(100m * vat / net, 2, MidpointRounding.AwayFromZero) : 0m,
                        lineGross - lineNet,
                        lineGross,
                        ReadDecimal(lineReader, 2),
                        ReadDecimal(lineReader, 3),
                        ReadDecimal(lineReader, 4),
                        ReadDecimal(lineReader, 5),
                        ReadDecimal(lineReader, 6),
                        ReadDecimal(lineReader, 7)));
                }
            }

            try
            {
                await using var addCommand = connection.CreateCommand();
                addCommand.CommandText = """
                    SELECT IFNULL(`description`, IFNULL(`addin_code`,'')), IFNULL(`amount`,0)
                    FROM `epc_jewel_voucher_addins` WHERE `voucher_id` = @p0 ORDER BY `line_no`, `id`
                    """;
                ErpDb.AddParameters(addCommand, id);
                await using var addReader = await addCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await addReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    addons.Add(new ErpPurchasePrintAddon(ReadString(addReader, 0), ReadDecimal(addReader, 1)));
                }
            }
            catch (DbException)
            {
            }

            var issuer = await LoadIssuerAsync(connection, cancellationToken).ConfigureAwait(false);
            var address = JoinFilled(", ", city, poBox.Length > 0 ? "P.O. Box " + poBox : "");
            var metal = ErpJwVocTypes.MetalPurchaseAliases.Contains(vocType, StringComparer.OrdinalIgnoreCase);
            return ErpPurchaseDocumentPrint.Render(new ErpPurchasePrintDoc(
                metal ? ErpPurchasePrintKind.MetalPurchase : ErpPurchasePrintKind.PurchaseInvoice,
                (string.IsNullOrWhiteSpace(suppInv) ? vocType + "-" + vocNo : suppInv),
                vocDate,
                crDays > 0 ? crDays.ToString(System.Globalization.CultureInfo.InvariantCulture) + " days" : "As agreed",
                salesman,
                "AED",
                metalRate > 0m ? metalRate.ToString("N3", System.Globalization.CultureInfo.InvariantCulture) + (isFixed ? " / FIXED" : "") : string.Empty,
                ErpPurchaseDocumentPrint.IsOfficialJewellery(status, authorized),
                status,
                issuer,
                new ErpPurchasePrintParty(partyCode, partyName, address, phone, email, string.Empty),
                lines,
                addons,
                net,
                vat,
                total > 0m ? total : net + vat,
                notes,
                "Jewellery purchase voucher · LPO → PO → PI"));
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static string ProcessNote(long orderId, long purchaseId, long poId)
    {
        var bits = new List<string> { "Purchase process: LPO → PO → PI" };
        if (orderId > 0)
        {
            bits.Add("Shop / LPO #" + orderId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (poId > 0)
        {
            bits.Add("PO #" + poId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (purchaseId > 0)
        {
            bits.Add("PI #" + purchaseId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return string.Join(" · ", bits);
    }

    private static string JoinFilled(string sep, params string[] parts)
        => string.Join(sep, parts.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static async Task<ErpPurchasePrintParty> LoadIssuerAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT IFNULL(`legal_name`, IFNULL(`trade_name`,'')), IFNULL(`address_line1`,''),
                       IFNULL(`address_line2`,''), IFNULL(`city`,''), IFNULL(`phone`,''),
                       IFNULL(`email`,''), IFNULL(`trn`,'')
                FROM `epc_document_company` WHERE `id` = 1 LIMIT 1
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = ReadString(reader, 0);
                var address = JoinFilled(", ", ReadString(reader, 1), ReadString(reader, 2), ReadString(reader, 3));
                if (name.Length > 0)
                {
                    return new ErpPurchasePrintParty(string.Empty, name, address, ReadString(reader, 4), ReadString(reader, 5), ReadString(reader, 6));
                }
            }
        }
        catch (DbException)
        {
        }

        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `name`, IFNULL(`value`,'') FROM `settings`
                WHERE `name` IN ('company_name','company_trn','company_address','company_phone','company_email','company_legal_name')
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                settings[ReadString(reader, 0)] = ReadString(reader, 1);
            }
        }
        catch (DbException)
        {
        }

        settings.TryGetValue("company_legal_name", out var legal);
        settings.TryGetValue("company_name", out var trade);
        settings.TryGetValue("company_address", out var addr);
        settings.TryGetValue("company_phone", out var phone);
        settings.TryGetValue("company_email", out var email);
        settings.TryGetValue("company_trn", out var trn);
        return new ErpPurchasePrintParty(
            string.Empty,
            string.IsNullOrWhiteSpace(legal) ? (string.IsNullOrWhiteSpace(trade) ? "ECOM AE" : trade) : legal,
            addr ?? string.Empty,
            phone ?? string.Empty,
            email ?? string.Empty,
            trn ?? string.Empty);
    }

    private static async Task<ErpPurchasePrintParty> LoadSupplierAsync(
        DbConnection connection,
        long supplierId,
        string fallbackName,
        CancellationToken cancellationToken)
    {
        if (supplierId <= 0 && string.IsNullOrWhiteSpace(fallbackName))
        {
            return new ErpPurchasePrintParty(string.Empty, "Supplier", string.Empty, string.Empty, string.Empty, string.Empty);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = supplierId > 0
                ? """
                    SELECT IFNULL(`supplier_code`, IFNULL(`vendor_account`,'')), IFNULL(`name`,''),
                           IFNULL(`address`,''), IFNULL(`street`,''), IFNULL(`city`,''),
                           IFNULL(`contact_phone`,''), IFNULL(`contact_email`,''), IFNULL(`trn`,'')
                    FROM `epc_erp_suppliers` WHERE `id` = @p0 LIMIT 1
                    """
                : """
                    SELECT IFNULL(`supplier_code`, IFNULL(`vendor_account`,'')), IFNULL(`name`,''),
                           IFNULL(`address`,''), IFNULL(`street`,''), IFNULL(`city`,''),
                           IFNULL(`contact_phone`,''), IFNULL(`contact_email`,''), IFNULL(`trn`,'')
                    FROM `epc_erp_suppliers` WHERE `name` = @p0 LIMIT 1
                    """;
            ErpDb.AddParameters(command, supplierId > 0 ? supplierId : fallbackName);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new ErpPurchasePrintParty(
                    ReadString(reader, 0),
                    string.IsNullOrWhiteSpace(ReadString(reader, 1)) ? fallbackName : ReadString(reader, 1),
                    JoinFilled(", ", ReadString(reader, 2), ReadString(reader, 3), ReadString(reader, 4)),
                    ReadString(reader, 5),
                    ReadString(reader, 6),
                    ReadString(reader, 7));
            }
        }
        catch (DbException)
        {
        }

        return new ErpPurchasePrintParty(string.Empty, fallbackName, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private static async Task<List<SoLine>> LoadSalesOrderLinesAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        var lines = new List<SoLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `item_code`, `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat`
            FROM `epc_erp_sales_order_lines`
            WHERE `sales_order_id` = @p0
            ORDER BY `line_no`, `id`
            """;
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new SoLine(ReadString(reader, 0), ReadString(reader, 1), ReadDecimal(reader, 2), ReadDecimal(reader, 3), ReadDecimal(reader, 4)));
        }
        return lines;
    }

    private static async Task<List<InvLine>> LoadInvoiceLinesAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        var lines = new List<InvLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT `line_no`, `item_name`, `quantity`, `uom_code`, `unit_price`, `line_net`, `tax_rate`, `tax_amount`, `gross_amount`
            FROM `epc_einvoice_lines`
            WHERE `document_id` = @p0
            ORDER BY `line_no`, `id`
            """;
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new InvLine(
                Convert.ToInt32(reader.IsDBNull(0) ? 0 : reader.GetValue(0)),
                ReadString(reader, 1), ReadDecimal(reader, 2), ReadString(reader, 3),
                ReadDecimal(reader, 4), ReadDecimal(reader, 5), ReadDecimal(reader, 6),
                ReadDecimal(reader, 7), ReadDecimal(reader, 8)));
        }
        return lines;
    }

    private static StringBuilder StartHtml(string title)
        => new StringBuilder("""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <title>
            """)
        .Append(title)
        .Append("""
            </title>
            <style>
            body{font-family:Arial,sans-serif;margin:40px;color:#111;}h1{font-size:20px;margin:0 0 12px;}
            table{border-collapse:collapse;width:100%;margin-top:16px;}td,th{border:1px solid #bbb;padding:6px 8px;font-size:13px;}
            th{background:#f3f4f6;}.totals{margin-top:14px;width:280px;margin-left:auto;}
            .totals td{border:none;border-top:1px solid #ddd;padding:4px 8px;}
            @media print{.no-print{display:none;}}
            </style></head><body>
            <div class="no-print" style="margin-bottom:16px;">
            <button type="button" onclick="window.print()" style="padding:8px 14px;font-size:13px;cursor:pointer;">Print / Save as PDF</button>
            </div>
            """);

    private static void Meta(StringBuilder builder, string label, string? value)
        => builder.Append("<strong>").Append(Encode(label)).Append(":</strong> ")
            .Append(Encode(string.IsNullOrWhiteSpace(value) ? "—" : value)).Append("<br>");

    private static void Totals(StringBuilder builder, decimal net, decimal vat, decimal total)
        => builder.Append("""
            <table class="totals"><tbody>
            <tr><td style="text-align:left"><strong>Net (ex VAT)</strong></td><td style="text-align:right">
            """)
            .Append(N(net))
            .Append("</td></tr><tr><td style=\"text-align:left\"><strong>VAT</strong></td><td style=\"text-align:right\">")
            .Append(N(vat))
            .Append("</td></tr><tr><td style=\"text-align:left\"><strong>Total</strong></td><td style=\"text-align:right\"><strong>")
            .Append(N(total))
            .Append("</strong></td></tr></tbody></table>");

    private static string FinishHtml(StringBuilder builder, string? notes, long timeCreated)
    {
        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.Append("<p style=\"margin-top:14px;\">").Append(Encode(notes).Replace("\n", "<br>", StringComparison.Ordinal)).Append("</p>");
        }
        if (timeCreated > 0)
        {
            builder.Append("<p style=\"margin-top:24px;font-size:12px;color:#666;\">Created ").Append(FmtDate(timeCreated)).Append("</p>");
        }
        builder.Append("<p style=\"margin-top:16px;font-size:12px;color:#666;\">Generated by ECOM AE ERP</p></body></html>");
        return builder.ToString();
    }

    private static string ReadString(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;

    private static long ReadLong(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal));

    private static decimal ReadDecimal(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string N(decimal value) => value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);

    private static string FmtDate(long time)
        => DateTimeOffset.FromUnixTimeSeconds(time).ToLocalTime().ToString("yyyy-MM-dd");

    private sealed record SoLine(string ItemCode, string Description, decimal Qty, decimal UnitPrice, decimal Amount);

    private sealed record PoLine(string ItemCode, string Description, decimal Qty, decimal UnitCost, decimal Amount, decimal QtyReceived);

    private sealed record BillLine(long ItemId, decimal Qty, decimal UnitCost, string BatchNo, string ExpiryDate);

    private sealed record InvLine(int LineNo, string ItemName, decimal Quantity, string UomCode, decimal UnitPrice, decimal LineNet, decimal TaxRate, decimal TaxAmount, decimal GrossAmount);
}
