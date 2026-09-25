using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_quote</c>, <c>epc_crm_quote_pdf_path</c>,
/// <c>epc_crm_quote_email_stub</c>, <c>epc_crm_accept_quote</c> and <c>epc_crm_adv_quote_tax_totals</c>.
/// Email is the PHP queue stub (JSON file under <c>content/files/epc_crm_quote_emails</c>); it does not invent a send (no SMTP).
/// Schema-ensure stays Classic.
/// </summary>
public interface ICpCrmQuoteWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmQuoteSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<CpCrmQuoteTaxTotals> TaxTotalsAsync(long quoteId, CancellationToken cancellationToken = default);

    Task<CpCrmQuotePreviewResult> PreviewAsync(long quoteId, CancellationToken cancellationToken = default);

    Task<CpCrmQuoteEmailResult> QueueEmailAsync(long quoteId, string? toEmail, CancellationToken cancellationToken = default);

    Task<CpCrmQuoteAcceptResult> AcceptAsync(long quoteId, CancellationToken cancellationToken = default);
}

public sealed record CpCrmQuoteTaxTotals(
    long QuoteId,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal Total,
    string TaxLabel,
    string Currency,
    string Engine);

public sealed record CpCrmQuotePreviewResult(bool Succeeded, string Message, string Path);

public sealed record CpCrmQuoteEmailResult(bool Succeeded, string Message, bool Queued, string To, string PreviewPath);

public sealed record CpCrmQuoteAcceptResult(bool Succeeded, string Message, long QuoteId, long OrderId);

public sealed record CpCrmQuoteSaveRequest(
    long Id,
    long OpportunityId,
    long LeadId,
    long CustomerUserId,
    string? QuoteNumber,
    string? Status,
    string? Notes,
    string? LineDescription,
    decimal LineQty,
    decimal LineUnitPrice);

public sealed class CpCrmQuoteWriteService : ICpCrmQuoteWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "draft", "sent", "accepted", "rejected",
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly ICpCurrencyLiveRatesService _currency;
    private readonly string _filesRoot;

    public CpCrmQuoteWriteService(
        IErpWriteConnectionFactory connections,
        IErpTaxAmountCalculator tax,
        ICpCurrencyLiveRatesService currency,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(connections, tax, currency, Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files"))
    {
    }

    public CpCrmQuoteWriteService(
        IErpWriteConnectionFactory connections,
        IErpTaxAmountCalculator tax,
        ICpCurrencyLiveRatesService currency,
        string filesRoot)
    {
        _connections = connections;
        _tax = tax;
        _currency = currency;
        _filesRoot = filesRoot;
    }

    /// <summary>PHP: <c>'quote_' . preg_replace('/[^a-zA-Z0-9_-]/', '_', $quote_number) . '.html'</c>.</summary>
    public static string PreviewFileName(string quoteNumber)
    {
        var chars = quoteNumber.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_').ToArray();
        return "quote_" + new string(chars) + ".html";
    }

    public static string PreviewUrl(string quoteNumber) => "/content/files/epc_crm_quotes/" + PreviewFileName(quoteNumber);

    /// <summary>PHP <c>epc_crm_quote_pdf_path</c> HTML body (tenant currency instead of the hard-coded AED).</summary>
    public static string PreviewHtml(CpCrmQuoteRow quote, IReadOnlyList<CpCrmQuoteLine> lines, string currency, DateTimeOffset generated)
    {
        var sb = new System.Text.StringBuilder();
        var number = System.Net.WebUtility.HtmlEncode(quote.QuoteNumber);
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Quote ").Append(number).Append("</title>\n");
        sb.Append("\t<style>body{font-family:Arial,sans-serif;margin:40px;color:#0f172a;} h1{font-size:22px;} table{border-collapse:collapse;width:100%;margin:16px 0;}\n");
        sb.Append("\tth,td{border:1px solid #cbd5e1;padding:8px;text-align:left;} th{background:#f1f5f9;}</style></head><body>");
        sb.Append("<h1>Commercial proposal ").Append(number).Append("</h1>");
        sb.Append("<p><strong>Status:</strong> ").Append(System.Net.WebUtility.HtmlEncode(quote.Status)).Append("<br>");
        sb.Append("<strong>Total (ex VAT):</strong> ").Append(CpCrmDeskService.Money(quote.Subtotal)).Append(' ').Append(System.Net.WebUtility.HtmlEncode(currency)).Append("</p>");
        if (quote.Notes.Length > 0)
        {
            sb.Append("<p>").Append(System.Net.WebUtility.HtmlEncode(quote.Notes).Replace("\n", "<br />\n", StringComparison.Ordinal)).Append("</p>");
        }

        sb.Append("<table><thead><tr><th>Description</th><th>Qty</th><th>Unit ").Append(System.Net.WebUtility.HtmlEncode(currency)).Append("</th><th>Line total</th></tr></thead><tbody>");
        foreach (var ln in lines)
        {
            sb.Append("<tr><td>").Append(System.Net.WebUtility.HtmlEncode(ln.Description)).Append("</td><td>").Append(CpCrmDeskService.Money(ln.Qty)).Append("</td>");
            sb.Append("<td>").Append(CpCrmDeskService.Money(ln.UnitPrice)).Append("</td><td>").Append(CpCrmDeskService.Money(ln.LineTotal)).Append("</td></tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append("<p style=\"margin-top:32px;font-size:12px;color:#64748b;\">Generated ").Append(generated.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)).Append(" — ECOM AE ERP CRM</p></body></html>");
        return sb.ToString();
    }

    public static string NormalizeStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return Statuses.Contains(raw) ? raw : "draft";
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static string NextQuoteNumber(long countPlusOne, DateTimeOffset now)
    {
        var n = countPlusOne < 1 ? 1 : countPlusOne;
        return "Q-" + now.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-" + n.ToString("0000", CultureInfo.InvariantCulture);
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmQuoteSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quote id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var oppId = request.OpportunityId < 0 ? 0 : request.OpportunityId;
        var leadId = request.LeadId < 0 ? 0 : request.LeadId;
        var custId = request.CustomerUserId < 0 ? 0 : request.CustomerUserId;
        var status = NormalizeStatus(request.Status);
        var notes = (request.Notes ?? string.Empty).Trim();
        var number = Clip(request.QuoteNumber, 32);
        var lineDesc = Clip(request.LineDescription, 512);
        var qty = request.LineQty < 0.001m ? 1 : request.LineQty;
        var price = request.LineUnitPrice < 0 ? 0 : request.LineUnitPrice;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (oppId > 0)
            {
                await using var select = connection.CreateCommand();
                select.CommandText = ErpDb.Positional(
                    "SELECT `lead_id`, `linked_user_id` FROM `epc_crm_opportunities` WHERE `id`=? LIMIT 1");
                ErpDb.AddParameters(select, oppId);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (leadId <= 0)
                    {
                        leadId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    }

                    if (custId <= 0)
                    {
                        custId = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                    }
                }
            }

            long quoteId;
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_quotes` WHERE `id`=?"),
                    cancellationToken,
                    request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Quote was not found.");
                }

                if (number.Length == 0)
                {
                    number = Clip(
                        await ErpDb.StringAsync(
                            connection, null,
                            ErpDb.Positional("SELECT `quote_number` FROM `epc_crm_quotes` WHERE `id`=?"),
                            cancellationToken, request.Id),
                        32);
                }

                if (notes.Length == 0)
                {
                    notes = (await ErpDb.StringAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_quotes` WHERE `id`=?"),
                        cancellationToken, request.Id) ?? string.Empty).Trim();
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_quotes`
                        SET `opportunity_id`=?, `lead_id`=?, `customer_user_id`=?, `quote_number`=?, `status`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    oppId, leadId, custId, number, status, notes, now, request.Id).ConfigureAwait(false);
                quoteId = request.Id;
            }
            else
            {
                if (number.Length == 0)
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_quotes`"),
                        cancellationToken).ConfigureAwait(false);
                    number = NextQuoteNumber(count + 1, DateTimeOffset.UtcNow);
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_crm_quotes`
                        (`opportunity_id`, `lead_id`, `customer_user_id`, `quote_number`, `status`, `subtotal`, `notes`, `time_created`, `time_updated`)
                        VALUES (?, ?, ?, ?, ?, 0, ?, ?, ?)
                        """),
                    cancellationToken,
                    oppId, leadId, custId, number, status, notes, now, now).ConfigureAwait(false);
                quoteId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            }

            if (lineDesc.Length > 0)
            {
                var sort = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(MAX(`sort_order`), 0) + 1 FROM `epc_crm_quote_lines` WHERE `quote_id`=?"),
                    cancellationToken,
                    quoteId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`)
                        VALUES (?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    quoteId, lineDesc, qty, price, sort).ConfigureAwait(false);
            }

            var sum = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(SUM(`qty` * `unit_price`), 0) FROM `epc_crm_quote_lines` WHERE `quote_id`=?"),
                cancellationToken,
                quoteId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_crm_quotes` SET `subtotal`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                sum, now, quoteId).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok("Quote saved", quoteId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM quote table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>
    /// PHP <c>epc_crm_adv_quote_tax_totals</c>: line sum (else stored subtotal), then the tenant-country tax
    /// engine (<see cref="IErpTaxAmountCalculator"/>) for the quote's customer. Never hard-codes a country rate.
    /// </summary>
    public async Task<CpCrmQuoteTaxTotals> TaxTotalsAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        var currency = await CurrencyAsync(cancellationToken).ConfigureAwait(false);
        var none = new CpCrmQuoteTaxTotals(quoteId, 0m, 0m, 0m, 0m, "Tax", currency, "none");
        if (quoteId <= 0 || !_connections.IsConfigured)
        {
            return none;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var detail = await LoadQuoteAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            if (detail is null)
            {
                return none;
            }

            var quoteCurrency = detail.Quote.CurrencyCode.Length > 0 ? detail.Quote.CurrencyCode : currency;
            var subtotal = detail.Lines.Sum(l => l.Qty * l.UnitPrice);
            if (subtotal <= 0)
            {
                subtotal = detail.Quote.Subtotal;
            }

            subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
            var amounts = await _tax.CalcAsync(connection, null, subtotal, (int)Math.Clamp(detail.Quote.CustomerUserId, 0, int.MaxValue), 0, false, cancellationToken).ConfigureAwait(false);
            var rate = amounts.TaxRate;
            var label = amounts.TaxLabel.Length > 0
                ? amounts.TaxLabel + " " + rate.ToString("0.##", CultureInfo.InvariantCulture) + "%"
                : rate > 0 ? "Tax " + rate.ToString("0.##", CultureInfo.InvariantCulture) + "%" : "No tax";
            return new CpCrmQuoteTaxTotals(quoteId, subtotal, rate, amounts.VatAmount, amounts.TotalAmount, label, quoteCurrency, "tenant_tax:" + amounts.CountryCode);
        }
        catch (DbException)
        {
            return none;
        }
    }

    public async Task<CpCrmQuotePreviewResult> PreviewAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return new CpCrmQuotePreviewResult(false, "Quote not found", "");
        }

        if (!_connections.IsConfigured)
        {
            return new CpCrmQuotePreviewResult(false, "TenantRegistry DB is not configured.", "");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var detail = await LoadQuoteAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            if (detail is null)
            {
                return new CpCrmQuotePreviewResult(false, "Quote not found", "");
            }

            var path = await WritePreviewAsync(detail, cancellationToken).ConfigureAwait(false);
            return path.Length > 0
                ? new CpCrmQuotePreviewResult(true, "Preview generated", path)
                : new CpCrmQuotePreviewResult(false, "Preview folder is not writable", "");
        }
        catch (DbException)
        {
            return new CpCrmQuotePreviewResult(false, "CRM quote table is missing — schema-ensure stays Classic.", "");
        }
    }

    public async Task<CpCrmQuoteEmailResult> QueueEmailAsync(long quoteId, string? toEmail, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0 || !_connections.IsConfigured)
        {
            return new CpCrmQuoteEmailResult(false, "Quote not found", false, "", "");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var detail = await LoadQuoteAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            if (detail is null)
            {
                return new CpCrmQuoteEmailResult(false, "Quote not found", false, "", "");
            }

            var preview = await WritePreviewAsync(detail, cancellationToken).ConfigureAwait(false);
            var to = (toEmail ?? string.Empty).Trim();
            if (to.Length == 0 && detail.Quote.CustomerUserId > 0)
            {
                to = (await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"), cancellationToken, detail.Quote.CustomerUserId).ConfigureAwait(false) ?? "").Trim();
            }

            if (to.Length == 0)
            {
                to = "customer@example.com";
            }

            var now = DateTimeOffset.UtcNow;
            var stub = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["to"] = to,
                ["subject"] = "Proposal " + detail.Quote.QuoteNumber,
                ["body"] = "Please find your commercial proposal attached.",
                ["attachment"] = preview,
                ["time"] = now.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
            };
            var queued = false;
            try
            {
                var dir = Path.Combine(_filesRoot, "epc_crm_quote_emails");
                Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "stub_" + quoteId.ToString(CultureInfo.InvariantCulture) + "_" + now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + ".json");
                await File.WriteAllTextAsync(file, System.Text.Json.JsonSerializer.Serialize(stub, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);
                queued = true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (detail.Quote.Status == "draft")
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_crm_quotes` SET `status` = 'sent', `time_updated` = ? WHERE `id` = ?"), cancellationToken, now.ToUnixTimeSeconds(), quoteId).ConfigureAwait(false);
            }

            return new CpCrmQuoteEmailResult(true, queued ? "Quote email queued" : "Quote marked sent (queue folder not writable)", queued, to, preview);
        }
        catch (DbException)
        {
            return new CpCrmQuoteEmailResult(false, "CRM quote table is missing — schema-ensure stays Classic.", false, "", "");
        }
    }

    /// <summary>PHP <c>epc_crm_accept_quote</c> + <c>epc_crm_create_order_stub_from_quote</c>; idempotent once accepted with an order.</summary>
    public async Task<CpCrmQuoteAcceptResult> AcceptAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0 || !_connections.IsConfigured)
        {
            return new CpCrmQuoteAcceptResult(false, "Quote not found", quoteId, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var detail = await LoadQuoteAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            if (detail is null)
            {
                return new CpCrmQuoteAcceptResult(false, "Quote not found", quoteId, 0);
            }

            var quote = detail.Quote;
            if (quote.Status == "accepted" && quote.ShopOrderId > 0)
            {
                return new CpCrmQuoteAcceptResult(true, "Quote already accepted", quoteId, quote.ShopOrderId);
            }

            var userId = quote.CustomerUserId;
            if (userId <= 0 && quote.OpportunityId > 0)
            {
                await using var c = connection.CreateCommand();
                c.CommandText = ErpDb.Positional("SELECT IFNULL(o.`linked_user_id`,0), IFNULL(l.`email`,'') FROM `epc_crm_opportunities` o LEFT JOIN `epc_crm_leads` l ON l.`id` = o.`lead_id` WHERE o.`id` = ? LIMIT 1");
                ErpDb.AddParameters(c, quote.OpportunityId);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    userId = r.IsDBNull(0) ? 0 : Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    var email = r.IsDBNull(1) ? "" : Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? "";
                    await r.CloseAsync().ConfigureAwait(false);
                    if (userId <= 0 && email.Length > 0)
                    {
                        userId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`user_id`,0) FROM `users` WHERE `email` = ? LIMIT 1"), cancellationToken, email).ConfigureAwait(false);
                    }
                }
            }

            var orderStatus = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(`id`,0) FROM `shop_orders_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            if (orderStatus <= 0)
            {
                return new CpCrmQuoteAcceptResult(false, "No order status configured", quoteId, 0);
            }

            var itemStatus = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(`id`,0) FROM `shop_orders_items_statuses_ref` WHERE `for_created` = 1 ORDER BY `order` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            var officeId = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(`id`,0) FROM `shop_offices` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            var storageId = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(`id`,0) FROM `shop_storages` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `shop_orders` (`user_id`, `session_id`, `time`, `successfully_created`, `status`, `paid`, `how_get`, `how_get_json`, `phone_not_auth`, `email_not_auth`, `office_id`) VALUES (?, 0, ?, 1, ?, 0, 1, '{}', '', '', ?)"),
                cancellationToken, Math.Max(0, userId), now, orderStatus, officeId).ConfigureAwait(false);
            var orderId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);

            var lines = detail.Lines.Count > 0
                ? detail.Lines
                : [new CpCrmQuoteLine(0, "Quote " + quote.QuoteNumber, 1, quote.Subtotal, 1)];
            var sort = 0;
            foreach (var ln in lines)
            {
                sort++;
                var qty = (int)Math.Max(1, ln.Qty);
                var desc = ln.Description.Length > 255 ? ln.Description[..255] : ln.Description;
                var article = "CRM-Q" + quoteId.ToString(CultureInfo.InvariantCulture) + "-" + sort.ToString(CultureInfo.InvariantCulture);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                    "INSERT INTO `shop_orders_items` (`order_id`, `product_type`, `price`, `count_need`, `product_id`, `status`, `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`, `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `sao_state`, `sao_robot`, `t2_json_params`) VALUES (?, 2, ?, ?, 0, ?, 'CRM', ?, ?, ?, 10, 1, 1, '', 1, 100, 0, 0, ?, ?, '', 0, '')"),
                    cancellationToken, orderId, ln.UnitPrice, qty, itemStatus, article, article, desc, officeId, storageId).ConfigureAwait(false);
                var itemId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                if (storageId > 0)
                {
                    await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                        "INSERT INTO `shop_orders_items_details` (`order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) VALUES (?, ?, ?, ?, 0, 0, 0, 0, 0)"),
                        cancellationToken, orderId, itemId, officeId, storageId).ConfigureAwait(false);
                }
            }

            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, 0, 1, ?, 1)"),
                cancellationToken, orderId, now, "CRM quote #" + quote.QuoteNumber + " accepted — draft order stub.").ConfigureAwait(false);
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(
                "UPDATE `epc_crm_quotes` SET `status` = 'accepted', `shop_order_id` = ?, `time_updated` = ? WHERE `id` = ?"),
                cancellationToken, orderId, now, quoteId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CpCrmQuoteAcceptResult(true, "Quote accepted, order #" + orderId.ToString(CultureInfo.InvariantCulture) + " created", quoteId, orderId);
        }
        catch (DbException ex)
        {
            return new CpCrmQuoteAcceptResult(false, "Accept failed: " + ex.Message, quoteId, 0);
        }
    }

    private async Task<string> CurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (_, alpha) = await _currency.GetMainCurrencyAsync(cancellationToken).ConfigureAwait(false);
            return alpha.Length > 0 ? alpha : "AED";
        }
        catch (DbException)
        {
            return "AED";
        }
    }

    private async Task<string> WritePreviewAsync(CpCrmQuoteDetail detail, CancellationToken cancellationToken)
    {
        var currency = detail.Quote.CurrencyCode.Length > 0 ? detail.Quote.CurrencyCode : await CurrencyAsync(cancellationToken).ConfigureAwait(false);
        var html = PreviewHtml(detail.Quote, detail.Lines, currency, DateTimeOffset.UtcNow);
        try
        {
            var dir = Path.Combine(_filesRoot, "epc_crm_quotes");
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, PreviewFileName(detail.Quote.QuoteNumber)), html, cancellationToken).ConfigureAwait(false);
            return PreviewUrl(detail.Quote.QuoteNumber);
        }
        catch (IOException)
        {
            return "";
        }
        catch (UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static async Task<CpCrmQuoteDetail?> LoadQuoteAsync(DbConnection connection, long id, CancellationToken cancellationToken)
    {
        CpCrmQuoteRow? quote = null;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT q.`id`, IFNULL(q.`opportunity_id`,0), IFNULL(q.`lead_id`,0), IFNULL(q.`customer_user_id`,0), IFNULL(q.`quote_number`,''), IFNULL(q.`status`,'draft'), IFNULL(q.`currency_code`,''), IFNULL(q.`subtotal`,0), IFNULL(q.`shop_order_id`,0), IFNULL(q.`notes`,''), IFNULL(q.`time_created`,0), IFNULL(q.`time_updated`,0) FROM `epc_crm_quotes` q WHERE q.`id` = ? AND q.`active` = 1 LIMIT 1");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                quote = new CpCrmQuoteRow(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToString(r.GetValue(4), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(r.GetValue(5), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(r.GetValue(6), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToDecimal(r.GetValue(7), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(8), CultureInfo.InvariantCulture),
                    Convert.ToString(r.GetValue(9), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToInt64(r.GetValue(10), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(11), CultureInfo.InvariantCulture),
                    "");
            }
        }

        if (quote is null)
        {
            return null;
        }

        var lines = new List<CpCrmQuoteLine>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`description`,''), IFNULL(`qty`,0), IFNULL(`unit_price`,0), IFNULL(`sort_order`,0) FROM `epc_crm_quote_lines` WHERE `quote_id` = ? ORDER BY `sort_order`, `id`");
            ErpDb.AddParameters(c, id);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new CpCrmQuoteLine(
                    Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? "",
                    Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt32(r.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        return new CpCrmQuoteDetail(quote, lines);
    }
}
