using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string OrderPrintPath = "/content/shop/print_docs/service/print.php";

    private const string PrintHtmlType = "text/html; charset=utf-8";

    /// <summary>The query, cookies and referer PHP <c>print_docs/service/print.php</c> reads.</summary>
    public sealed record OrderPrintRequest(
        string? DocName,
        string? OrderId,
        string? CsrfKey,
        bool CsrfAdminFlag,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer,
        string ShopCurrency);

    /// <summary>
    /// PHP <c>content/shop/print_docs/service/print.php</c>: admin or customer CSRF, then the sales receipt
    /// (<c>get_html_sales_receipt.php</c>) or the UAE tax invoice (<c>get_html_uae_tax_invoice.php</c>) for
    /// <c>invoice_for_payment</c>, <c>uae_tax_invoice</c> and <c>fta_tax_invoice</c>. Unknown documents print the sales receipt.
    /// </summary>
    public static async Task<RawHttp> PrintOrderDocumentAsync(
        DbConnection connection,
        OrderPrintRequest request,
        IErpInvoiceFromOrderWriteService invoices,
        IErpDocControlWriteService? docControl,
        CancellationToken cancellationToken)
    {
        var useAdmin = request.CsrfAdminFlag
            || (!PhpEmptyValue(request.AdminSession) && (PhpEmptyValue(request.Session) || PhpEmptyValue(request.UserCookie)))
            || RefererIsControlPanel(request.Referer);

        var csrf = request.CsrfKey;
        if (useAdmin && (csrf ?? string.Empty).Trim().Length == 0)
        {
            var adminKey = await PrintSessionKeyAsync(connection, request.AdminSession, request.AdminUser, true, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(adminKey))
            {
                csrf = adminKey;
            }
        }

        if (csrf is null)
        {
            return PrintCsrfError("Error! CSRF 1");
        }

        if (PhpEmptyValue(csrf))
        {
            return PrintCsrfError("Error! CSRF 3");
        }

        var stored = useAdmin
            ? await PrintSessionKeyAsync(connection, request.AdminSession, request.AdminUser, true, cancellationToken).ConfigureAwait(false)
            : await PrintSessionKeyAsync(connection, request.Session, request.UserCookie, false, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return PrintCsrfError("Error! CSRF 3.1");
        }

        if (!string.Equals(stored, csrf, StringComparison.Ordinal))
        {
            return PrintCsrfError("Error! CSRF 4");
        }

        var userId = await CookieUserIdAsync(connection, request.Session, request.UserCookie, cancellationToken).ConfigureAwait(false);
        var docName = Regex.Replace(request.DocName ?? string.Empty, "[^a-zA-Z0-9_\\-]", string.Empty);
        var orderId = ProtocolLong(request.OrderId);
        if (docName.Length == 0 || orderId <= 0)
        {
            return new RawHttp("doc_name and order_id are required", PrintHtmlType, 400);
        }

        var taxInvoice = docName is "invoice_for_payment" or "uae_tax_invoice" or "fta_tax_invoice";
        var staff = await IsAdminSessionAsync(connection, request.AdminSession, request.AdminUser, cancellationToken).ConfigureAwait(false)
            || await IsBackendGroupAsync(connection, request.Session, request.UserCookie, cancellationToken).ConfigureAwait(false);
        if (!staff && userId <= 0)
        {
            return new RawHttp("Not authorized", PrintHtmlType, 403);
        }

        var order = staff
            ? await PrintRowAsync(connection, ErpDb.Positional("SELECT * FROM `shop_orders` WHERE `id` = ?"), cancellationToken, orderId).ConfigureAwait(false)
            : await PrintRowAsync(connection, ErpDb.Positional("SELECT * FROM `shop_orders` WHERE `user_id` = ? AND `id` = ?"), cancellationToken, userId, orderId).ConfigureAwait(false);
        if (order is null)
        {
            return new RawHttp("No such order", PrintHtmlType, 404);
        }

        var html = taxInvoice
            ? await TaxInvoiceHtmlAsync(connection, order, orderId, invoices, docControl, cancellationToken).ConfigureAwait(false)
            : await SalesReceiptHtmlAsync(connection, order, orderId, request.ShopCurrency, "Sales receipt", cancellationToken).ConfigureAwait(false);
        if (html is RawHttp failure)
        {
            return failure;
        }

        var body = (string)html;
        return body.Length == 0
            ? new RawHttp("Print document could not be generated", PrintHtmlType, 500)
            : new RawHttp(body, PrintHtmlType);
    }

    private static RawHttp PrintCsrfError(string message)
        => new(JsonSerializer.Serialize(new { error = message, message, status = false }), PrintHtmlType);

    private static bool RefererIsControlPanel(string? referer)
    {
        if (string.IsNullOrEmpty(referer))
        {
            return false;
        }

        var path = Uri.TryCreate(referer, UriKind.Absolute, out var uri) ? uri.AbsolutePath : referer.Split('?', '#')[0];
        return path.Length > 0 && Regex.IsMatch(path, "/(?:cp|control)(?:/|$)", RegexOptions.IgnoreCase);
    }

    /// <summary>The <c>csrf_guard_key</c> of PHP <c>DP_User::getAdminSession()</c> / <c>getUserSession()</c>; null when there is no session row.</summary>
    private static async Task<string?> PrintSessionKeyAsync(DbConnection connection, string? session, string? userCookie, bool admin, CancellationToken cancellationToken)
    {
        var row = await PrintRowAsync(
            connection,
            ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') AS `k` FROM `sessions` WHERE `session` = ? AND `user_id` = ?" + (admin ? " AND `type` = 1" : string.Empty) + " LIMIT 1"),
            cancellationToken,
            session ?? string.Empty,
            userCookie ?? string.Empty).ConfigureAwait(false);
        return row?["k"];
    }

    /// <summary>PHP <c>get_html_sales_receipt.php</c>.</summary>
    private static async Task<object> SalesReceiptHtmlAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> order,
        long orderId,
        string shopCurrency,
        string title,
        CancellationToken cancellationToken)
    {
        var items = await PrintRowsAsync(
            connection,
            ErpDb.Positional("SELECT `t2_manufacturer`, `t2_article`, `t2_name`, `price`, `count_need` FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC"),
            cancellationToken,
            orderId).ConfigureAwait(false);

        var currency = "AED";
        try
        {
            var cur = await PrintRowAsync(connection, ErpDb.Positional("SELECT `sign`, `caption_short` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1"), cancellationToken, shopCurrency).ConfigureAwait(false);
            if (cur is not null)
            {
                currency = !PhpEmptyValue(cur["caption_short"]) ? cur["caption_short"] : cur["sign"];
            }
        }
        catch (DbException)
        {
        }

        var companyName = "ePartsCart";
        var companyPhone = string.Empty;
        var companyAddress = string.Empty;
        try
        {
            if (await PrintRowAsync(connection, "SHOW TABLES LIKE 'epc_document_company'", cancellationToken).ConfigureAwait(false) is not null)
            {
                var co = await PrintRowAsync(connection, "SELECT `legal_name`, `trade_name`, `phone`, `address_line1`, `city`, `country` FROM `epc_document_company` WHERE `id` = 1 LIMIT 1", cancellationToken).ConfigureAwait(false);
                if (co is not null)
                {
                    var name = (!PhpEmptyValue(co["trade_name"]) ? co["trade_name"] : co["legal_name"]).Trim();
                    companyName = name.Length > 0 ? name : companyName;
                    companyPhone = co["phone"];
                    companyAddress = string.Join(", ", new[] { co["address_line1"], co["city"], co["country"] }.Where(p => !PhpEmptyValue(p))).Trim();
                }
            }
        }
        catch (DbException)
        {
        }

        var buyer = string.Empty;
        var orderUser = PrintField(order, "user_id");
        if (!PhpEmptyValue(orderUser))
        {
            try
            {
                var u = await PrintRowAsync(connection, ErpDb.Positional("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"), cancellationToken, ProtocolLong(orderUser)).ConfigureAwait(false);
                if (u is not null)
                {
                    buyer = string.Join(" / ", new[] { u["email"], u["phone"] }.Where(p => !PhpEmptyValue(p))).Trim();
                }
            }
            catch (DbException)
            {
            }
        }

        if (buyer.Length == 0)
        {
            buyer = string.Join(" / ", new[] { PrintField(order, "email_not_auth"), PrintField(order, "phone_not_auth") }.Where(p => !PhpEmptyValue(p))).Trim();
        }

        if (buyer.Length == 0)
        {
            buyer = "Customer #" + ProtocolLong(orderUser).ToString(CultureInfo.InvariantCulture);
        }

        var orderTime = PrintField(order, "time");
        var time = ErpDocumentControlRender.PhpDate("yyyy-MM-dd HH:mm", !PhpEmptyValue(orderTime) ? ProtocolLong(orderTime) : DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var h = ErpDocumentControlRender.H;
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\" />\n<title>").Append(h(title)).Append(" #").Append(orderId.ToString(CultureInfo.InvariantCulture)).Append("</title>\n<style>\n")
            .Append("body { font-family: Arial, Helvetica, sans-serif; font-size: 13px; color: #111; margin: 24px; }\n")
            .Append("h1 { font-size: 22px; margin: 0 0 6px; }\n")
            .Append(".muted { color: #555; font-size: 12px; }\n")
            .Append("table { width: 100%; border-collapse: collapse; margin-top: 16px; }\n")
            .Append("th, td { border: 1px solid #ccc; padding: 8px; vertical-align: top; }\n")
            .Append("th { background: #f5f5f5; text-align: left; }\n")
            .Append(".right { text-align: right; }\n")
            .Append(".totals { width: 280px; margin-left: auto; margin-top: 12px; }\n")
            .Append(".sign { margin-top: 36px; }\n")
            .Append("@media print { body { margin: 12px; } .no-print { display: none; } }\n")
            .Append("</style>\n</head>\n<body>\n<div class=\"no-print\" style=\"margin-bottom:12px;\">\n\t<button onclick=\"window.print()\">Print</button>\n</div>\n")
            .Append("<h1>").Append(h(title)).Append("</h1>\n<div class=\"muted\">").Append(h(companyName));
        if (companyAddress.Length > 0)
        {
            html.Append(" — ").Append(h(companyAddress));
        }

        if (companyPhone.Length > 0)
        {
            html.Append(" — ").Append(h(companyPhone));
        }

        html.Append("</div>\n<p>\n\t<strong>Receipt / Order No:</strong> ").Append(orderId.ToString(CultureInfo.InvariantCulture)).Append("<br />\n")
            .Append("\t<strong>Date:</strong> ").Append(h(time)).Append("<br />\n")
            .Append("\t<strong>Customer:</strong> ").Append(h(buyer)).Append("</p>\n\n<table>\n\t<thead>\n\t\t<tr>\n")
            .Append("\t\t\t<th style=\"width:40px;\">#</th>\n\t\t\t<th>Brand</th>\n\t\t\t<th>Part number</th>\n\t\t\t<th>Description</th>\n")
            .Append("\t\t\t<th class=\"right\" style=\"width:70px;\">Qty</th>\n\t\t\t<th class=\"right\" style=\"width:100px;\">Price</th>\n")
            .Append("\t\t\t<th class=\"right\" style=\"width:110px;\">Amount</th>\n\t\t</tr>\n\t</thead>\n\t<tbody>\n\t");

        var n = 0;
        var total = 0m;
        foreach (var item in items)
        {
            n++;
            var qty = PrintDecimal(item["count_need"]);
            var price = PrintDecimal(item["price"]);
            var amount = qty * price;
            total += amount;
            var qtyText = Math.Round(qty, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
            html.Append("\t\t<tr>\n\t\t\t<td>").Append(n.ToString(CultureInfo.InvariantCulture)).Append("</td>\n")
                .Append("\t\t\t<td>").Append(h(item["t2_manufacturer"])).Append("</td>\n")
                .Append("\t\t\t<td>").Append(h(item["t2_article"])).Append("</td>\n")
                .Append("\t\t\t<td>").Append(h(item["t2_name"])).Append("</td>\n")
                .Append("\t\t\t<td class=\"right\">").Append(qtyText).Append("</td>\n")
                .Append("\t\t\t<td class=\"right\">").Append(ErpDocumentControlRender.PhpNumberFormat(price)).Append("</td>\n")
                .Append("\t\t\t<td class=\"right\">").Append(ErpDocumentControlRender.PhpNumberFormat(amount)).Append("</td>\n\t\t</tr>\n\t\t");
        }

        if (n == 0)
        {
            html.Append("\t\t<tr><td colspan=\"7\">No order lines found.</td></tr>\n\t\t");
        }

        html.Append("\t</tbody>\n</table>\n\n<table class=\"totals\">\n\t<tr>\n\t\t<th>Total (").Append(h(currency)).Append(")</th>\n")
            .Append("\t\t<th class=\"right\">").Append(ErpDocumentControlRender.PhpNumberFormat(total)).Append("</th>\n\t</tr>\n</table>\n\n")
            .Append("<div class=\"sign\">\n\t<p><strong>Received by (name &amp; signature):</strong> _______________________________</p>\n")
            .Append("\t<p><strong>Date:</strong> __________</p>\n</div>\n</body>\n</html>\n");
        return html.ToString();
    }

    /// <summary>
    /// PHP <c>get_html_uae_tax_invoice.php</c>: the saved e-invoice of the order, else the PINT-AE invoice built from the
    /// order (saved when it validates), else the document control <c>fta_tax_invoice</c> template.
    /// </summary>
    private static async Task<object> TaxInvoiceHtmlAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> order,
        long orderId,
        IErpInvoiceFromOrderWriteService invoices,
        IErpDocControlWriteService? docControl,
        CancellationToken cancellationToken)
    {
        await ErpDocumentControlRender.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (docControl is not null)
        {
            await docControl.SyncSellerAsync(connection, 0, true, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var saved = await SavedTaxInvoiceAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            if (saved is not null && saved.Lines.Count > 0)
            {
                return TaxInvoicePrintHtml(saved with { Buyer = await EnrichBuyerAsync(connection, saved.Buyer, order, cancellationToken).ConfigureAwait(false) });
            }
        }
        catch (Exception ex) when (ex is DbException or JsonException or ErpWriteException)
        {
        }

        try
        {
            var built = await invoices.BuildDocumentDraftAsync(connection, orderId, null, cancellationToken).ConfigureAwait(false);
            var draft = ErpInvoiceFromOrderWriteService.WithBuyer(built, await EnrichBuyerAsync(connection, built.Buyer, order, cancellationToken).ConfigureAwait(false));
            var html = TaxInvoicePrintHtml(FromDraft(draft, draft.Buyer));
            if (draft.Errors.Count == 0)
            {
                try
                {
                    await invoices.SaveDocumentDraftAsync(connection, draft, 0, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is DbException or ErpWriteException)
                {
                }
            }

            return html;
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException)
        {
        }

        try
        {
            return await ErpDocumentControlRender.RenderTemplateAsync(connection, "fta_tax_invoice", orderId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException)
        {
            return new RawHttp("Unable to generate UAE tax invoice: " + ErpDocumentControlRender.H(ex.Message), PrintHtmlType, 500);
        }
    }

    public sealed record TaxInvoicePrintLine(int LineNo, string ItemName, decimal Quantity, decimal UnitPrice, decimal LineNet, decimal TaxRate, decimal VatLineAed, decimal GrossAmount);

    /// <summary>The fields PHP <c>epc_erp_invoice_print_html()</c> reads from a saved or built e-invoice.</summary>
    public sealed record TaxInvoicePrint(
        string InvoiceNumber,
        IReadOnlyDictionary<string, string> Seller,
        IReadOnlyDictionary<string, string> Buyer,
        long IssueDate,
        long? VatPointDate,
        long PaymentDueDate,
        string CurrencyCode,
        string InvoiceTypeCode,
        long OrderId,
        IReadOnlyList<TaxInvoicePrintLine> Lines,
        decimal SubtotalExVat,
        decimal TotalVat,
        decimal TotalInclVat,
        decimal AmountDue,
        string PaymentTerms,
        string Uuid);

    private static TaxInvoicePrint FromDraft(ErpOrderEinvoiceDraft draft, IReadOnlyDictionary<string, string> buyer)
        => new(
            draft.InvoiceNumber,
            draft.Seller,
            buyer,
            draft.IssueDate,
            draft.IssueDate,
            draft.DueDate,
            "AED",
            "380",
            draft.OrderId,
            draft.Lines.Select(l => new TaxInvoicePrintLine(l.LineNo, l.ItemName, l.Quantity, l.UnitPrice, l.LineNet, l.TaxRate, l.TaxAmount, l.GrossAmount)).ToList(),
            draft.Subtotal,
            draft.TotalVat,
            draft.TotalIncl,
            draft.AmountDue,
            draft.PaymentTerms,
            draft.Uuid);

    /// <summary>PHP <c>epc_einvoice_get_document()</c> for the newest active e-invoice of the order.</summary>
    private static async Task<TaxInvoicePrint?> SavedTaxInvoiceAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var id = ProtocolLong((await PrintRowAsync(
            connection,
            ErpDb.Positional("SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false))?["id"]);
        if (id <= 0)
        {
            return null;
        }

        var doc = await PrintRowAsync(connection, ErpDb.Positional("SELECT * FROM `epc_einvoice_documents` WHERE `id` = ? AND `active` = 1 LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        var lines = (await PrintRowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_einvoice_lines` WHERE `document_id` = ? ORDER BY `line_no`"), cancellationToken, id).ConfigureAwait(false))
            .Select(l => new TaxInvoicePrintLine(
                (int)ProtocolLong(l["line_no"]),
                l["item_name"],
                PrintDecimal(l["quantity"]),
                PrintDecimal(l["unit_price"]),
                PrintDecimal(l["line_net"]),
                PrintDecimal(l["tax_rate"]),
                PrintDecimal(PrintField(l, "vat_line_aed")),
                PrintDecimal(l["gross_amount"])))
            .ToList();
        var vatPoint = PrintField(doc, "vat_point_date");
        return new TaxInvoicePrint(
            PrintField(doc, "invoice_number"),
            JsonStrings(PrintField(doc, "seller_json")),
            JsonStrings(PrintField(doc, "buyer_json")),
            ProtocolLong(PrintField(doc, "issue_date")),
            vatPoint.Length == 0 ? null : ProtocolLong(vatPoint),
            ProtocolLong(PrintField(doc, "payment_due_date")),
            PrintField(doc, "currency_code"),
            PrintField(doc, "invoice_type_code"),
            ProtocolLong(PrintField(doc, "order_id")),
            lines,
            PrintDecimal(PrintField(doc, "subtotal_ex_vat")),
            PrintDecimal(PrintField(doc, "total_vat")),
            PrintDecimal(PrintField(doc, "total_incl_vat")),
            PrintDecimal(PrintField(doc, "amount_due")),
            PrintField(doc, "payment_terms"),
            PrintField(doc, "uuid"));
    }

    private static Dictionary<string, string> JsonStrings(string json)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return values;
        }

        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (var property in parsed.RootElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.True => "1",
                JsonValueKind.False => string.Empty,
                _ => property.Value.GetRawText(),
            };
        }

        return values;
    }

    /// <summary>The PHP <c>$epc_enrich_buyer</c> closure: fill the Bill To fields of B2C tax invoices.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> EnrichBuyerAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> order,
        CancellationToken cancellationToken)
    {
        string Pick(string key, string alternate, string fallback)
            => (source.TryGetValue(key, out var v) ? v : source.TryGetValue(alternate, out var a) ? a : fallback).Trim();

        var buyer = new Dictionary<string, string>(source, StringComparer.Ordinal);
        var name = (source.TryGetValue("buyer_name", out var n) ? n : string.Empty).Trim();
        var address = Pick("buyer_address_line1", "address_line1", string.Empty);
        var city = Pick("buyer_city", "city", "Dubai");
        var emirate = Pick("buyer_emirate", "emirate", "Dubai");
        var country = Pick("buyer_country_code", "country_code", "AE").ToUpperInvariant();
        var email = Pick("buyer_email", "email", string.Empty);
        var phone = Pick("buyer_phone", "phone", string.Empty);

        var uid = ProtocolLong(PrintField(order, "user_id"));
        if (uid > 0 && (email.Length == 0 || phone.Length == 0 || name.Length == 0))
        {
            try
            {
                var u = await PrintRowAsync(connection, ErpDb.Positional("SELECT `email`, `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"), cancellationToken, uid).ConfigureAwait(false);
                if (email.Length == 0)
                {
                    email = (u?["email"] ?? string.Empty).Trim();
                }

                if (phone.Length == 0)
                {
                    phone = (u?["phone"] ?? string.Empty).Trim();
                }
            }
            catch (DbException)
            {
            }
        }

        if (email.Length == 0)
        {
            email = PrintField(order, "email_not_auth").Trim();
        }

        if (phone.Length == 0)
        {
            phone = PrintField(order, "phone_not_auth").Trim();
        }

        if (name.Length == 0)
        {
            name = email.Length > 0 ? email : "Customer #" + uid.ToString(CultureInfo.InvariantCulture);
        }

        if (address.Length == 0)
        {
            var parts = new[] { email, phone }.Where(p => !PhpEmptyValue(p)).ToArray();
            address = parts.Length > 0 ? string.Join(" / ", parts) : "Address on customer file";
        }

        buyer["buyer_name"] = name;
        buyer["buyer_address_line1"] = address;
        buyer["buyer_city"] = city.Length > 0 ? city : "Dubai";
        buyer["buyer_emirate"] = emirate.Length > 0 ? emirate : "Dubai";
        buyer["buyer_country_code"] = country.Length > 0 ? country : "AE";
        buyer["buyer_email"] = email;
        buyer["buyer_phone"] = phone;
        if (PhpEmptyValue(buyer.GetValueOrDefault("buyer_peppol_endpoint")))
        {
            buyer["buyer_peppol_endpoint"] = "0235:9900000098";
        }

        if (PhpEmptyValue(buyer.GetValueOrDefault("buyer_electronic_id")))
        {
            buyer["buyer_electronic_id"] = "0235";
        }

        return buyer;
    }

    /// <summary>PHP <c>epc_erp_invoice_print_html()</c> without the blockchain proof block.</summary>
    public static string TaxInvoicePrintHtml(TaxInvoicePrint doc)
    {
        var h = ErpDocumentControlRender.H;
        static string Nf(decimal v) => ErpDocumentControlRender.PhpNumberFormat(v);
        static string Coalesce(IReadOnlyDictionary<string, string> map, string key, string fallback) => map.TryGetValue(key, out var v) ? v : fallback;
        static bool Filled(IReadOnlyDictionary<string, string> map, string key) => map.TryGetValue(key, out var v) && !PhpEmptyValue(v);
        static string Date(long unix) => ErpDocumentControlRender.PhpDate("yyyy-MM-dd", unix);

        var seller = doc.Seller;
        var buyer = doc.Buyer;
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Tax Invoice ").Append(h(doc.InvoiceNumber)).Append("</title>\n\t<style>\n")
            .Append("\tbody{font-family:\"Segoe UI\",Arial,sans-serif;margin:32px;color:#0f172a;font-size:13px;}\n")
            .Append("\t.hdr{border-bottom:3px solid #1d4ed8;padding-bottom:16px;margin-bottom:24px;}\n")
            .Append("\t.hdr h1{margin:0;font-size:22px;color:#1d4ed8;}\n")
            .Append("\t.grid{display:flex;gap:24px;margin-bottom:20px;}\n")
            .Append("\t.box{flex:1;}\n")
            .Append("\t.box h3{margin:0 0 8px;font-size:11px;text-transform:uppercase;color:#64748b;}\n")
            .Append("\ttable{width:100%;border-collapse:collapse;margin:16px 0;}\n")
            .Append("\tth,td{border:1px solid #e2e8f0;padding:8px;text-align:left;}\n")
            .Append("\tth{background:#f8fafc;font-size:11px;text-transform:uppercase;}\n")
            .Append("\t.totals{max-width:320px;margin-left:auto;}\n")
            .Append("\t.totals td{border:none;padding:4px 8px;}\n")
            .Append("\t.totals tr.total td{font-weight:bold;font-size:15px;border-top:2px solid #1d4ed8;}\n")
            .Append("\t.bc-proof{margin-top:24px;padding:12px 14px;border:1px solid #bfdbfe;background:#eff6ff;border-radius:6px;font-size:12px;}\n")
            .Append("\t.bc-proof strong{color:#1d4ed8;}\n")
            .Append("\t.bc-proof a{color:#1d4ed8;word-break:break-all;}\n")
            .Append("\t.foot{margin-top:40px;font-size:11px;color:#64748b;}\n")
            .Append("\t@media print{.no-print{display:none;}}\n")
            .Append("\t</style></head><body>");
        html.Append("<div class=\"no-print\" style=\"margin-bottom:16px;\">")
            .Append("<button type=\"button\" onclick=\"window.print()\" style=\"padding:8px 14px;font-size:13px;cursor:pointer;\">")
            .Append("Print / Save as PDF</button>")
            .Append("<span style=\"margin-left:10px;color:#64748b;font-size:12px;\">Use your browser print dialog → Save as PDF</span>")
            .Append("</div>");
        html.Append("<div class=\"hdr\"><h1>TAX INVOICE</h1><p style=\"margin:4px 0 0;color:#64748b;\">UAE Federal Tax Authority · e-Invoice (PINT-AE) · ")
            .Append(h(doc.InvoiceNumber)).Append("</p></div>");

        var sellerAddress = (Coalesce(seller, "seller_address_line1", string.Empty) + ", " + Coalesce(seller, "seller_city", string.Empty) + ", "
            + Coalesce(seller, "seller_emirate", string.Empty) + ", " + Coalesce(seller, "seller_country_code", "AE")).Trim(' ', ',');
        var sellerReg = (Coalesce(seller, "seller_legal_reg_type", string.Empty) + " " + Coalesce(seller, "seller_legal_reg_no", string.Empty)).Trim();
        html.Append("<div class=\"grid\"><div class=\"box\"><h3>Seller</h3><strong>").Append(h(Coalesce(seller, "seller_name", "Company"))).Append("</strong><br>TRN: ")
            .Append(h(Coalesce(seller, "seller_trn", "—")))
            .Append(sellerReg.Length > 0 ? "<br>Legal reg: " + h(sellerReg) : string.Empty)
            .Append("<br>").Append(h(sellerAddress))
            .Append(Filled(seller, "seller_phone") ? "<br>Tel: " + h(seller["seller_phone"]) : string.Empty)
            .Append(Filled(seller, "seller_email") ? "<br>" + h(seller["seller_email"]) : string.Empty)
            .Append("</div>");

        var buyerTrn = Coalesce(buyer, "buyer_trn", string.Empty).Trim();
        var buyerAddress = (Coalesce(buyer, "buyer_address_line1", string.Empty) + ", " + Coalesce(buyer, "buyer_city", string.Empty) + ", "
            + Coalesce(buyer, "buyer_emirate", string.Empty) + ", " + Coalesce(buyer, "buyer_country_code", "AE")).Trim(' ', ',');
        html.Append("<div class=\"box\"><h3>Buyer</h3><strong>").Append(h(Coalesce(buyer, "buyer_name", "—"))).Append("</strong><br>TRN: ")
            .Append(h(buyerTrn.Length > 0 ? buyerTrn : "Not registered / B2C")).Append("<br>").Append(h(buyerAddress))
            .Append(Filled(buyer, "buyer_phone") ? "<br>Tel: " + h(buyer["buyer_phone"]) : string.Empty)
            .Append(Filled(buyer, "buyer_email") ? "<br>" + h(buyer["buyer_email"]) : string.Empty)
            .Append("</div>");

        html.Append("<div class=\"box\"><h3>Invoice</h3>Issue date: ").Append(Date(doc.IssueDate))
            .Append("<br>Supply / VAT point: ").Append(Date(doc.VatPointDate ?? doc.IssueDate))
            .Append("<br>Due: ").Append(Date(doc.PaymentDueDate))
            .Append("<br>Currency: ").Append(h(doc.CurrencyCode))
            .Append("<br>Type code: ").Append(h(doc.InvoiceTypeCode)).Append(" (Tax Invoice)");
        if (doc.OrderId > 0)
        {
            html.Append("<br>Order #").Append(doc.OrderId.ToString(CultureInfo.InvariantCulture));
        }

        html.Append("</div></div>");
        html.Append("<p style=\"font-size:12px;color:#475569;margin:0 0 12px;\">This is a Tax Invoice for UAE VAT purposes. Seller TRN must appear on all tax invoices. Retain for minimum 5 years.</p>");
        html.Append("<table><thead><tr><th>#</th><th>Description</th><th>Qty</th><th>Unit</th><th>Net</th><th>VAT %</th><th>VAT</th><th>Gross</th></tr></thead><tbody>");
        foreach (var line in doc.Lines)
        {
            html.Append("<tr><td>").Append(line.LineNo.ToString(CultureInfo.InvariantCulture)).Append("</td><td>").Append(h(line.ItemName)).Append("</td>")
                .Append("<td>").Append(h(Nf(line.Quantity))).Append("</td>")
                .Append("<td>").Append(Nf(line.UnitPrice)).Append("</td><td>").Append(Nf(line.LineNet)).Append("</td>")
                .Append("<td>").Append(Nf(line.TaxRate)).Append("</td><td>").Append(Nf(line.VatLineAed)).Append("</td>")
                .Append("<td>").Append(Nf(line.GrossAmount)).Append("</td></tr>");
        }

        html.Append("</tbody></table>");
        html.Append("<table class=\"totals\"><tr><td>Subtotal ex VAT</td><td style=\"text-align:right;\">").Append(Nf(doc.SubtotalExVat)).Append(' ').Append(h(doc.CurrencyCode)).Append("</td></tr>")
            .Append("<tr><td>VAT</td><td style=\"text-align:right;\">").Append(Nf(doc.TotalVat)).Append("</td></tr>")
            .Append("<tr class=\"total\"><td>Total incl. VAT</td><td style=\"text-align:right;\">").Append(Nf(doc.TotalInclVat)).Append("</td></tr>")
            .Append("<tr><td>Amount due</td><td style=\"text-align:right;\"><strong>").Append(Nf(doc.AmountDue)).Append("</strong></td></tr></table>");
        if (!PhpEmptyValue(doc.PaymentTerms))
        {
            html.Append("<p><strong>Payment terms:</strong> ").Append(h(doc.PaymentTerms)).Append("</p>");
        }

        html.Append("<div class=\"foot\">UAE FTA Tax Invoice / e-Invoice (PINT-AE) · Specification ")
            .Append(h("urn:peppol:pint:billing-1@ae-1"))
            .Append("<br>UUID ").Append(h(doc.Uuid))
            .Append(" · Retain records for minimum 5 years as required by UAE law.</div></body></html>");
        return html.ToString();
    }

    private static bool PhpEmptyValue(string? value) => string.IsNullOrEmpty(value) || value == "0";

    private static string PrintField(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value : string.Empty;

    private static decimal PrintDecimal(string? value)
        => decimal.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    private static async Task<Dictionary<string, string>?> PrintRowAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        var rows = await PrintRowsAsync(connection, sql, cancellationToken, args).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    private static async Task<List<Dictionary<string, string>>> PrintRowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
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
                row[reader.GetName(i)] = ErpDocumentControlRender.PdoString(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }
}
