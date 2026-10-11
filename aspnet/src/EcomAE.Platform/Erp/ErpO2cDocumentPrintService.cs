using System.Data.Common;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Erp;

public interface IErpO2cDocumentPrintService
{
    Task<string?> RenderSalesOrderAsync(long id, CancellationToken cancellationToken = default);
    Task<string?> RenderInvoiceAsync(long id, CancellationToken cancellationToken = default);
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

    private sealed record InvLine(int LineNo, string ItemName, decimal Quantity, string UomCode, decimal UnitPrice, decimal LineNet, decimal TaxRate, decimal TaxAmount, decimal GrossAmount);
}
