using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP document control rendering (<c>content/shop/document_control/epc_document_control_helpers.php</c>):
/// the schema with the default templates (<c>epc_doc_control_ensure_schema</c>, <c>epc_document_control_templates_default.php</c>),
/// <c>epc_dc_render_template</c> with the order context or the preview placeholders, and the sales / packing / delivery line tables.
/// </summary>
public static partial class ErpDocumentControlRender
{
    private const string CompanySeedFooter =
        "This document is issued in accordance with UAE Federal Tax Authority (FTA) requirements. VAT Registration Number (TRN) must appear on all tax invoices. Retain records for minimum 5 years.";

    private const string SharedCss = @"
.epc-doc { font-family: Arial, Helvetica, sans-serif; font-size: 12px; color: #111; max-width: 900px; margin: 0 auto; }
.epc-doc table { width: 100%; border-collapse: collapse; }
.epc-doc th, .epc-doc td { border: 1px solid #ccc; padding: 6px 8px; vertical-align: top; }
.epc-doc th { background: #f4f4f4; text-align: left; }
.epc-doc .no-border td, .epc-doc .no-border th { border: none; }
.epc-doc .right { text-align: right; }
.epc-doc .title { font-size: 20px; font-weight: bold; margin: 0 0 8px; }
.epc-doc .muted { color: #555; font-size: 11px; }
.epc-doc .legal { font-size: 10px; color: #444; border-top: 1px solid #999; margin-top: 16px; padding-top: 8px; }
";

    private const string Header = @"<div class=""epc-doc"">
<table class=""no-border""><tr>
<td width=""55%""><img src=""{{company_logo}}"" alt=""Logo"" style=""max-height:70px;max-width:220px;"" onerror=""this.style.display='none'"" />
<div class=""title"">{{company_legal_name}}</div>
<div>{{company_address}}</div>
<div>TRN: {{company_trn}} | Tel: {{company_phone}} | {{company_email}}</div>
</td>
<td width=""45%"" class=""right"">
<div class=""title"">{{document_title}}</div>
<div><strong>No:</strong> {{document_number}}</div>
<div><strong>Date:</strong> {{document_date}}</div>
<div><strong>Order ref:</strong> {{order_id}}</div>
</td></tr></table>";

    private const string Footer = @"<div class=""legal"">{{legal_footer}}</div></div>";

    private const string FtaInvoiceBody = @"
<table class=""no-border"" style=""margin:12px 0""><tr>
<td width=""50%""><strong>Bill To</strong><br/>{{buyer_name}}<br/>{{buyer_address}}<br/>TRN: {{buyer_trn}}</td>
<td width=""50%""><strong>Ship To</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}</td>
</tr></table>
<p class=""muted"">Tax Invoice — UAE Federal Tax Authority (FTA) compliant format. Supply date: {{supply_date}}</p>
{{lines_table}}
<table style=""width:320px;margin-left:auto;margin-top:12px"">
<tr><td>Subtotal (excl. VAT)</td><td class=""right"">{{subtotal_excl_vat}}</td></tr>
<tr><td>VAT ({{vat_rate}}%)</td><td class=""right"">{{vat_amount}}</td></tr>
<tr><th>Total (incl. VAT)</th><th class=""right"">{{total_incl_vat}}</th></tr>
</table>
<p><strong>Amount in words:</strong> {{amount_words}}</p>
<p><strong>Payment terms:</strong> {{payment_terms}}</p>
<p><strong>Bank:</strong> {{bank_name}} — IBAN: {{bank_iban}}</p>";

    private const string PackingBody = @"
<table class=""no-border"" style=""margin:12px 0""><tr>
<td><strong>Customer</strong><br/>{{buyer_name}}<br/>{{buyer_address}}</td>
<td><strong>Delivery</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}<br/>Carrier: {{carrier}}<br/>Tracking: {{tracking_no}}</td>
</tr></table>
<p class=""muted"">Packing list — items included in this shipment (no tax values).</p>
{{lines_table_packing}}
<p><strong>Packages:</strong> {{package_count}} &nbsp; <strong>Total weight:</strong> {{total_weight}}</p>
<p><strong>Prepared by:</strong> {{prepared_by}} &nbsp; <strong>Checked by:</strong> _______________</p>";

    private const string DeliveryBody = @"
<table class=""no-border"" style=""margin:12px 0""><tr>
<td><strong>Deliver To</strong><br/>{{ship_to_name}}<br/>{{ship_to_address}}<br/>Phone: {{ship_to_phone}}</td>
<td><strong>From</strong><br/>{{company_legal_name}}<br/>{{company_address}}</td>
</tr></table>
<p class=""muted"">Delivery note — proof of delivery. Not a tax invoice.</p>
{{lines_table_delivery}}
<p><strong>Vehicle / driver:</strong> {{driver_info}}</p>
<p><strong>Received by (name & signature):</strong> _________________________ &nbsp; Date: __________</p>
<p><strong>Condition notes:</strong> {{delivery_notes}}</p>";

    private const string ReceiptBody = @"
<table class=""no-border"" style=""margin:12px 0""><tr>
<td><strong>Received from</strong><br/>{{buyer_name}}<br/>{{buyer_address}}</td>
<td><strong>Payment for</strong><br/>Invoice / Order: {{document_number}}<br/>Order ID: {{order_id}}</td>
</tr></table>
<p class=""muted"">Official payment receipt — not a tax invoice unless marked as Tax Invoice.</p>
<table style=""width:360px;margin:12px 0"">
<tr><td>Amount received</td><td class=""right""><strong>{{amount_received}}</strong></td></tr>
<tr><td>Method</td><td class=""right"">{{payment_method}}</td></tr>
<tr><td>Reference</td><td class=""right"">{{payment_reference}}</td></tr>
<tr><td>Date received</td><td class=""right"">{{payment_date}}</td></tr>
</table>
<p><strong>Amount in words:</strong> {{amount_words}}</p>
<p>Authorized signature: _________________________</p>";

    public sealed record DefaultTemplate(string Code, string Title, string Description, string Category, int SortOrder, string HeaderHtml, string BodyHtml, string FooterHtml, string CssExtra);

    /// <summary>PHP <c>epc_doc_control_default_templates()</c>.</summary>
    public static IReadOnlyList<DefaultTemplate> DefaultTemplates { get; } =
    [
        new("fta_tax_invoice", "FTA Tax Invoice", "UAE FTA-compliant tax invoice with TRN, VAT breakdown, and line items.", "sales", 10, Header.Replace("{{document_title}}", "TAX INVOICE", StringComparison.Ordinal), FtaInvoiceBody, Footer, SharedCss),
        new("packing_slip", "Packing Slip", "Shipment packing list with quantities and SKU references.", "logistics", 20, Header.Replace("{{document_title}}", "PACKING SLIP", StringComparison.Ordinal), PackingBody, Footer, SharedCss),
        new("delivery_note", "Delivery Note", "Proof-of-delivery document for warehouse and customer sign-off.", "logistics", 30, Header.Replace("{{document_title}}", "DELIVERY NOTE", StringComparison.Ordinal), DeliveryBody, Footer, SharedCss),
        new("payment_receipt", "Payment Receipt", "Customer payment acknowledgment with method and reference.", "finance", 40, Header.Replace("{{document_title}}", "PAYMENT RECEIPT", StringComparison.Ordinal), ReceiptBody, Footer, SharedCss),
    ];

    /// <summary>PHP <c>epc_doc_control_ensure_schema</c>: the three tables, the company row and the default templates (title, description and category refreshed).</summary>
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_document_company` ("
            + "`id` TINYINT UNSIGNED NOT NULL DEFAULT 1 PRIMARY KEY,"
            + "`legal_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`trade_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`address_line1` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`address_line2` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`city` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`country` VARCHAR(80) NOT NULL DEFAULT 'United Arab Emirates',"
            + "`trn` VARCHAR(32) NOT NULL DEFAULT '',"
            + "`phone` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`email` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`website` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`logo_path` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`bank_name` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`bank_iban` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`legal_footer` TEXT NULL,"
            + "`updated_at` INT NOT NULL DEFAULT 0"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_document_templates` ("
            + "`code` VARCHAR(32) NOT NULL PRIMARY KEY,"
            + "`title` VARCHAR(120) NOT NULL,"
            + "`description` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`category` VARCHAR(32) NOT NULL DEFAULT 'sales',"
            + "`header_html` MEDIUMTEXT NULL,"
            + "`body_html` MEDIUMTEXT NULL,"
            + "`footer_html` MEDIUMTEXT NULL,"
            + "`css_extra` TEXT NULL,"
            + "`active` TINYINT(1) NOT NULL DEFAULT 1,"
            + "`sort_order` INT NOT NULL DEFAULT 0,"
            + "`updated_at` INT NOT NULL DEFAULT 0"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            "CREATE TABLE IF NOT EXISTS `epc_document_attachments` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,"
            + "`entity_type` VARCHAR(32) NOT NULL DEFAULT 'order',"
            + "`entity_id` INT NOT NULL DEFAULT 0,"
            + "`doc_category` VARCHAR(32) NOT NULL DEFAULT 'supplier_invoice',"
            + "`supplier_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`reference_no` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`file_name` VARCHAR(255) NOT NULL,"
            + "`file_path` VARCHAR(512) NOT NULL,"
            + "`mime_type` VARCHAR(120) NOT NULL DEFAULT '',"
            + "`file_size` INT NOT NULL DEFAULT 0,"
            + "`notes` TEXT NULL,"
            + "`uploaded_by` INT NOT NULL DEFAULT 0,"
            + "`uploaded_at` INT NOT NULL DEFAULT 0,"
            + "KEY `idx_entity` (`entity_type`, `entity_id`),"
            + "KEY `idx_category` (`doc_category`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_document_company`", cancellationToken).ConfigureAwait(false) == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_document_company`"
                    + " (`id`, `legal_name`, `trade_name`, `address_line1`, `city`, `country`, `trn`, `phone`, `email`, `website`, `legal_footer`, `updated_at`)"
                    + " VALUES (1, '', '', '', 'Dubai', 'United Arab Emirates', '', '', '', '', ?, ?)"),
                cancellationToken,
                CompanySeedFooter,
                now).ConfigureAwait(false);
        }

        foreach (var t in DefaultTemplates)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_document_templates`"
                    + " (`code`, `title`, `description`, `category`, `header_html`, `body_html`, `footer_html`, `css_extra`, `active`, `sort_order`, `updated_at`)"
                    + " VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?)"
                    + " ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `description` = VALUES(`description`), `category` = VALUES(`category`), `updated_at` = VALUES(`updated_at`)"),
                cancellationToken,
                t.Code,
                t.Title,
                t.Description,
                t.Category,
                t.HeaderHtml,
                t.BodyHtml,
                t.FooterHtml,
                t.CssExtra,
                t.SortOrder,
                now).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// PHP <c>epc_dc_render_template($db, $code, $order_id)</c>. Throws <see cref="ErpWriteException"/> with the PHP message
    /// for an inactive or unknown template and for an order that is missing or not successfully created.
    /// </summary>
    public static Task<string> RenderTemplateAsync(DbConnection connection, string code, long orderId, CancellationToken cancellationToken)
        => RenderTemplateAsync(connection, code, orderId, 0, cancellationToken);

    /// <summary>PHP <c>epc_dc_render_template($db, $code, $order_id, array('invoice_id' => $invoiceId))</c>: an invoice wins over the order.</summary>
    public static async Task<string> RenderTemplateAsync(DbConnection connection, string code, long orderId, long invoiceId, CancellationToken cancellationToken)
    {
        await EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var template = await RowAsync(
            connection,
            ErpDb.Positional("SELECT * FROM `epc_document_templates` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
        if (template is null || PhpInt(Get(template, "active")) == 0)
        {
            throw new ErpWriteException("Template not found or inactive");
        }

        var placeholders = invoiceId > 0
            ? await InvoicePlaceholdersAsync(connection, invoiceId, cancellationToken).ConfigureAwait(false)
            : orderId > 0
                ? await OrderPlaceholdersAsync(connection, orderId, cancellationToken).ConfigureAwait(false)
                : await PreviewPlaceholdersAsync(connection, cancellationToken).ConfigureAwait(false);

        var html = Get(template, "header_html") + Get(template, "body_html") + Get(template, "footer_html");
        foreach (var (key, value) in placeholders)
        {
            html = html.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        }

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>" + H(Get(template, "title")) + "</title>"
            + "<style>" + Get(template, "css_extra") + "@media print { body { margin: 0; } .no-print { display:none; } }</style></head><body>"
            + "<div class=\"no-print\" style=\"padding:10px;background:#eef2ff;margin-bottom:12px;\">"
            + "<button onclick=\"window.print()\">Print</button> <button onclick=\"window.close()\">Close</button></div>"
            + html + "</body></html>";
    }

    private static async Task<List<(string Key, string Value)>> PreviewPlaceholdersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var company = await CompanyAsync(connection, cancellationToken).ConfigureAwait(false);
        return
        [
            ("company_logo", Get(company, "logo_path", "/content/files/epc_doc/logo.png")),
            ("company_legal_name", Get(company, "legal_name", "Preview Company")),
            ("company_address", Get(company, "address_line1").Trim()),
            ("company_trn", Get(company, "trn")),
            ("company_phone", Get(company, "phone")),
            ("company_email", Get(company, "email")),
            ("document_number", "PREVIEW-001"),
            ("document_date", PhpDate("dd MMM yyyy", DateTimeOffset.UtcNow.ToUnixTimeSeconds())),
            ("order_id", "0"),
            ("legal_footer", Get(company, "legal_footer")),
            ("lines_table", "<p><em>Preview — select an order or invoice to populate line items.</em></p>"),
            ("lines_table_packing", "<p><em>Preview packing lines.</em></p>"),
            ("lines_table_delivery", "<p><em>Preview delivery lines.</em></p>"),
        ];
    }

    /// <summary>PHP <c>epc_dc_order_context()</c> placeholders.</summary>
    private static async Task<List<(string Key, string Value)>> OrderPlaceholdersAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var order = await RowAsync(
            connection,
            ErpDb.Positional("SELECT * FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false)
            ?? throw new ErpWriteException("Order not found");

        var exclusions = new StringBuilder();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                exclusions.Append(" AND `status` != ").Append(reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }
        catch (DbException)
        {
        }

        var items = await RowsAsync(
            connection,
            ErpDb.Positional("SELECT `t2_manufacturer`, `t2_article`, `t2_name`, `price`, `count_need` FROM `shop_orders_items` WHERE `order_id` = ?" + exclusions),
            cancellationToken,
            orderId).ConfigureAwait(false);

        var userId = (int)PhpInt(Get(order, "user_id"));
        var buyer = await RawBuyerProfileAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var company = await CompanyAsync(connection, cancellationToken).ConfigureAwait(false);

        var tax = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var customer = await ErpDashboardReadService.CustomerContextAsync(connection, userId, cancellationToken).ConfigureAwait(false);
        var supplyRate = ErpDashboardReadService.SupplyRate(customer.Country, false, tax.RatePercent);
        var inclusive = ErpDashboardReadService.DisplayMode(customer.VatType) == "inclusive" && supplyRate > 0m;

        var rate = tax.RatePercent;
        decimal subtotal = 0m, vatTotal = 0m;
        var rows = new List<DcLine>();
        foreach (var item in items)
        {
            var qty = PhpDecimal(Get(item, "count_need"));
            var price = PhpDecimal(Get(item, "price"));
            var line = ErpDashboardReadService.LineAmounts(price, qty, supplyRate, inclusive, tax.SalesEnabled);
            var unitNet = supplyRate > 0m && tax.SalesEnabled && inclusive
                ? (Math.Max(0m, qty) > 0m ? Math.Round(line.LineNet / Math.Max(0m, qty), 4, MidpointRounding.AwayFromZero) : 0m)
                : Round2(Math.Max(0m, price));
            var net = Round2(line.LineNet);
            var vat = Round2(line.VatAmount);
            subtotal += net;
            vatTotal += vat;
            if (rate <= 0m && line.TaxRate > 0m)
            {
                rate = line.TaxRate;
            }

            rows.Add(new DcLine(Get(item, "t2_manufacturer"), Get(item, "t2_article"), Get(item, "t2_name"), qty, Round2(unitNet), net, vat, Round2(line.Gross), line.TaxRate));
        }

        subtotal = Round2(subtotal);
        vatTotal = Round2(vatTotal);
        var totalIncl = Round2(subtotal + vatTotal);
        var paid = Round2(await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(SUM(`amount`),0) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?"),
            cancellationToken,
            orderId).ConfigureAwait(false));

        var invoiceNumber = "INV-" + orderId.ToString(CultureInfo.InvariantCulture).PadLeft(6, '0');
        var einvoiceNumber = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false) ?? string.Empty;
        if (einvoiceNumber.Length > 0)
        {
            invoiceNumber = einvoiceNumber;
        }

        var buyerName = Get(buyer, "buyer_name").Trim();
        if (buyerName.Length == 0)
        {
            buyerName = Get(buyer, "company", "Customer").Trim();
        }

        var buyerEmail = Get(buyer, "email").Trim();
        var buyerPhone = Get(buyer, "phone").Trim();
        if (buyerEmail.Length == 0 || buyerPhone.Length == 0)
        {
            var user = await RowAsync(connection, ErpDb.Positional("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"), cancellationToken, userId).ConfigureAwait(false);
            if (buyerEmail.Length == 0)
            {
                buyerEmail = Get(user, "email").Trim();
            }

            if (buyerPhone.Length == 0)
            {
                buyerPhone = Get(user, "phone").Trim();
            }
        }

        var buyerAddress = JoinFilled(", ", Get(buyer, "address_line1"), Get(buyer, "city"), Get(buyer, "emirate"), Get(buyer, "country_code", "AE")).Trim();
        if (buyerAddress.Length == 0 || buyerAddress == "AE")
        {
            buyerAddress = JoinFilled(", ", buyerEmail, buyerPhone, Get(buyer, "city", "Dubai"), "United Arab Emirates").Trim();
        }

        var legal = Get(company, "legal_name").Trim();
        if (legal.Length == 0)
        {
            legal = Get(company, "trade_name", "Company").Trim();
        }

        var logo = Get(company, "logo_path").Trim();
        if (logo.Length == 0)
        {
            logo = "/content/files/epc_doc/logo.png";
        }

        var orderTime = PhpInt(Get(order, "time"));
        return
        [
            ("company_logo", logo),
            ("company_legal_name", legal),
            ("company_trade_name", Get(company, "trade_name")),
            ("company_address", JoinFilled(", ", Get(company, "address_line1"), Get(company, "address_line2"), Get(company, "city"), Get(company, "country")).Trim()),
            ("company_trn", Get(company, "trn")),
            ("company_phone", Get(company, "phone")),
            ("company_email", Get(company, "email")),
            ("company_website", Get(company, "website")),
            ("document_number", invoiceNumber),
            ("document_date", PhpDate("dd MMM yyyy", orderTime)),
            ("order_id", orderId.ToString(CultureInfo.InvariantCulture)),
            ("supply_date", PhpDate("dd MMM yyyy", orderTime)),
            ("buyer_name", buyerName),
            ("buyer_address", buyerAddress),
            ("buyer_trn", Get(buyer, "trn")),
            ("ship_to_name", buyerName),
            ("ship_to_address", buyerAddress),
            ("ship_to_phone", Get(buyer, "phone")),
            ("subtotal_excl_vat", Money(subtotal)),
            ("vat_amount", Money(vatTotal)),
            ("total_incl_vat", Money(totalIncl)),
            ("vat_rate", PhpNumber(rate)),
            ("amount_words", AmountWordsEn(totalIncl)),
            ("payment_terms", "Due on receipt unless agreed otherwise"),
            ("bank_name", Get(company, "bank_name")),
            ("bank_iban", Get(company, "bank_iban")),
            ("legal_footer", Get(company, "legal_footer")),
            ("carrier", "—"),
            ("tracking_no", "—"),
            ("package_count", "1"),
            ("total_weight", "—"),
            ("prepared_by", "Warehouse"),
            ("driver_info", "—"),
            ("delivery_notes", string.Empty),
            ("amount_received", Money(paid > 0m ? paid : totalIncl)),
            ("payment_method", PhpEmpty(Get(order, "paid")) ? "Pending" : "Paid in full"),
            ("payment_reference", "ORD-" + orderId.ToString(CultureInfo.InvariantCulture)),
            ("payment_date", PhpDate("dd MMM yyyy", DateTimeOffset.UtcNow.ToUnixTimeSeconds())),
            ("lines_table", LinesTableSales(rows, rate)),
            ("lines_table_packing", LinesTablePacking(rows)),
            ("lines_table_delivery", LinesTablePacking(rows)),
        ];
    }

    private sealed record DcLine(string Manufacturer, string Article, string Name, decimal Qty, decimal UnitNet, decimal LineNet, decimal Vat, decimal Gross, decimal TaxRate);

    private static string LinesTableSales(IReadOnlyList<DcLine> rows, decimal vatRate)
    {
        var html = new StringBuilder("<table><thead><tr>\n<th>#</th><th>Description</th><th>SKU / Part</th><th class=\"right\">Qty</th><th class=\"right\">Unit (excl.)</th><th class=\"right\">Net</th><th class=\"right\">VAT ")
            .Append(H(PhpNumber(vatRate))).Append("%</th><th class=\"right\">Total</th>\n</tr></thead><tbody>");
        var i = 0;
        foreach (var r in rows)
        {
            i++;
            html.Append("<tr><td>").Append(i.ToString(CultureInfo.InvariantCulture)).Append("</td><td>").Append(H(LineName(r))).Append("</td><td>").Append(H(Sku(r))).Append("</td>")
                .Append("<td class=\"right\">").Append(H(PhpNumber(r.Qty))).Append("</td>")
                .Append("<td class=\"right\">").Append(Money(r.UnitNet)).Append("</td>")
                .Append("<td class=\"right\">").Append(Money(r.LineNet)).Append("</td>")
                .Append("<td class=\"right\">").Append(Money(r.Vat)).Append(" <span class=\"muted\">(").Append(H(PhpNumber(r.TaxRate))).Append("%)</span></td>")
                .Append("<td class=\"right\">").Append(Money(r.Gross)).Append("</td></tr>");
        }

        return html.Append("</tbody></table>").ToString();
    }

    private static string LinesTablePacking(IReadOnlyList<DcLine> rows)
    {
        var html = new StringBuilder("<table><thead><tr><th>#</th><th>Description</th><th>SKU</th><th class=\"right\">Qty</th><th>Bin / Notes</th></tr></thead><tbody>");
        var i = 0;
        foreach (var r in rows)
        {
            i++;
            html.Append("<tr><td>").Append(i.ToString(CultureInfo.InvariantCulture)).Append("</td><td>").Append(H(LineName(r))).Append("</td><td>").Append(H(Sku(r))).Append("</td>")
                .Append("<td class=\"right\">").Append(H(PhpNumber(r.Qty))).Append("</td><td></td></tr>");
        }

        return html.Append("</tbody></table>").ToString();
    }

    private static string Sku(DcLine r) => (r.Manufacturer + " " + r.Article).Trim();

    private static string LineName(DcLine r) => r.Name.Trim();

    /// <summary>PHP <c>epc_dc_amount_words_en()</c>.</summary>
    public static string AmountWordsEn(decimal amount)
    {
        amount = Round2(amount);
        var whole = (long)Math.Floor(amount);
        var fils = (long)Math.Round((amount - whole) * 100m, MidpointRounding.AwayFromZero);
        var words = NumberWords(whole);
        var output = char.ToUpperInvariant(words[0]) + words[1..] + " UAE Dirhams";
        if (fils > 0)
        {
            output += " and " + NumberWords(fils) + " Fils";
        }

        return output + " only";
    }

    /// <summary>PHP <c>epc_dc_number_words()</c>.</summary>
    public static string NumberWords(long n)
    {
        if (n == 0)
        {
            return "zero";
        }

        string[] ones = ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
        string[] tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
        string[] scales = ["", " thousand", " million", " billion"];
        var parts = new List<string>();
        var scale = 0;
        while (n > 0)
        {
            var chunk = n % 1000;
            if (chunk != 0)
            {
                var w = string.Empty;
                var h = chunk / 100;
                var r = chunk % 100;
                if (h != 0)
                {
                    w += ones[h] + " hundred";
                    if (r != 0)
                    {
                        w += " and ";
                    }
                }

                if (r < 20)
                {
                    w += ones[r];
                }
                else
                {
                    w += tens[r / 10];
                    if (r % 10 != 0)
                    {
                        w += "-" + ones[r % 10];
                    }
                }

                parts.Add(w.Trim() + (scale < scales.Length ? scales[scale] : string.Empty));
            }

            n /= 1000;
            scale++;
        }

        parts.Reverse();
        return string.Join(' ', parts);
    }

    /// <summary>PHP <c>epc_dc_money()</c>.</summary>
    public static string Money(decimal n) => PhpNumberFormat(n) + " AED";

    /// <summary>PHP <c>number_format($n, 2, '.', ',')</c>.</summary>
    public static string PhpNumberFormat(decimal n)
        => Math.Round(n, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>(string)</c> of a float: integral values print without decimals.</summary>
    public static string PhpNumber(decimal n) => n.ToString("0.############", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>htmlspecialchars($v, ENT_QUOTES, 'UTF-8')</c>.</summary>
    public static string H(string? value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>PHP <c>date()</c> in the server time zone.</summary>
    public static string PhpDate(string format, long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString(format, CultureInfo.InvariantCulture);

    /// <summary>PHP <c>epc_einvoice_buyer_profile()</c>: the stored buyer profile row, else the one derived from the user, else nothing.</summary>
    private static async Task<Dictionary<string, string>> RawBuyerProfileAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var stored = await RowAsync(connection, ErpDb.Positional("SELECT * FROM `epc_einvoice_buyer_profiles` WHERE `user_id` = ? LIMIT 1"), cancellationToken, userId).ConfigureAwait(false);
            if (stored is not null)
            {
                return stored;
            }
        }
        catch (DbException)
        {
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["name"] = string.Empty,
            ["surname"] = string.Empty,
            ["email"] = string.Empty,
            ["phone"] = string.Empty,
            ["company"] = string.Empty,
            ["address"] = string.Empty,
            ["city"] = string.Empty,
        };
        string? regCountry = null, regTrn = null;
        foreach (var row in await RowsAsync(connection, ErpDb.Positional("SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ?"), cancellationToken, userId).ConfigureAwait(false))
        {
            var key = Get(row, "data_key");
            var value = Get(row, "data_value").Trim();
            if (fields.ContainsKey(key))
            {
                fields[key] = value;
            }
            else if (key == "epc_reg_country")
            {
                regCountry ??= value;
            }
            else if (key == "epc_reg_trn")
            {
                regTrn ??= value;
            }
        }

        var user = await RowAsync(connection, ErpDb.Positional("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"), cancellationToken, userId).ConfigureAwait(false);
        var name = (fields["company"].Length > 0 ? fields["company"] : (fields["name"] + " " + fields["surname"]).Trim()).Trim();
        var country = "AE";
        var registered = (regCountry ?? "AE").Trim().ToUpperInvariant();
        if (registered.Length == 2)
        {
            country = registered;
        }

        country = ErpDashboardReadService.NormalizeCountry(country);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["user_id"] = userId.ToString(CultureInfo.InvariantCulture),
            ["buyer_name"] = name.Length > 0 ? name : "Customer #" + userId.ToString(CultureInfo.InvariantCulture),
            ["trn"] = new string((regTrn ?? string.Empty).Where(char.IsAsciiDigit).ToArray()),
            ["address_line1"] = fields["address"],
            ["city"] = fields["city"].Length > 0 ? fields["city"] : country == "AE" ? "Dubai" : string.Empty,
            ["emirate"] = country == "AE" ? "Dubai" : string.Empty,
            ["country_code"] = country.Length > 0 ? country : "AE",
            ["phone"] = fields["phone"].Length > 0 ? fields["phone"] : Get(user, "phone"),
            ["email"] = fields["email"].Length > 0 ? fields["email"] : Get(user, "email"),
        };
    }

    private static async Task<Dictionary<string, string>> CompanyAsync(DbConnection connection, CancellationToken cancellationToken)
        => await RowAsync(connection, "SELECT * FROM `epc_document_company` WHERE `id` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

    private static async Task<Dictionary<string, string>?> RowAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = await RowsAsync(connection, sql, cancellationToken, args).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<List<Dictionary<string, string>>> RowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = new List<Dictionary<string, string>>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = PdoString(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>The string PDO returns for a column; MySqlConnector reads <c>TINYINT(1)</c> as a boolean, PDO as "1"/"0".</summary>
    internal static string PdoString(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetValue(ordinal) switch
            {
                bool b => b ? "1" : "0",
                var v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty,
            };

    private static string Get(IReadOnlyDictionary<string, string>? row, string key, string fallback = "")
        => row is not null && row.TryGetValue(key, out var value) ? value : fallback;

    private static string JoinFilled(string separator, params string[] parts)
        => string.Join(separator, parts.Where(p => !PhpEmpty(p)));

    private static bool PhpEmpty(string? value) => string.IsNullOrEmpty(value) || value == "0";

    private static long PhpInt(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? (long)Math.Truncate(d) : 0;
    }

    private static decimal PhpDecimal(string? value)
        => decimal.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
