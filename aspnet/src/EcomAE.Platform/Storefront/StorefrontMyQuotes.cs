using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/my_quotes.php</c> (<c>/shop/quotes</c>): the login panel for a visitor, a quote
/// detail (customer or administrator view) with the submit / accept scripts, or the quote list. Byte for byte, including
/// the whitespace PHP leaves around its tags.
/// </summary>
public static class StorefrontMyQuotes
{
    public const int LoginTextStringId = 4559;

    public const string LoginFormPostfix = "my_quotes";

    public sealed record Input(
        long UserId,
        long AdminId,
        long DetailId,
        string LangHref,
        string BackendDir,
        string CurrencyShowMode,
        string? ShopCurrency,
        string? CurrencyCookie,
        string? CountryCookie,
        string LoginFormHtml);

    /// <summary>PHP <c>date('Y-m-d H:i', $unix)</c> in the server time zone.</summary>
    public static string PhpDate(long unix) => ErpDocumentControlRender.PhpDate("yyyy-MM-dd HH:mm", unix);

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Input input,
        Func<int, string> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var records = await EpcCurrency.RecordsAsync(connection, input.ShopCurrency, cancellationToken).ConfigureAwait(false);
        var iso = await EpcCurrency.SelectedIsoAsync(connection, records, input.ShopCurrency, input.UserId, input.CurrencyCookie, input.CountryCookie, cancellationToken)
            .ConfigureAwait(false);
        string Money(double amount) => EpcCurrency.FormatAmount(amount, records, iso, input.CurrencyShowMode);

        var admin = input.AdminId > 0;
        var id = input.DetailId;
        var lang = H(input.LangHref);
        var sb = new StringBuilder();
        if (input.UserId <= 0 && !admin)
        {
            sb.Append("\t<div class=\"epc-quotes-panel epc-quotes-panel--login\">\n\t\t<div class=\"epc-quotes-panel__icon\"><i class=\"fa fa-file-text-o\" aria-hidden=\"true\"></i></div>\n\t\t<div class=\"epc-quotes-panel__content\">\n\t\t\t<span class=\"epc-quotes-panel__eyebrow\">Customer quotes</span>\n\t\t\t<h2>")
                .Append(id > 0 ? "Quote #" + Int(id) + " is protected" : "Login required to view quotes")
                .Append("</h2>\n\t\t\t<p>")
                .Append(t(LoginTextStringId))
                .Append(" Please log in with the customer account that created the quote request. Administrators can open quote details in the control panel.</p>\n\t\t\t<div class=\"epc-quotes-panel__chips\">\n\t\t\t\t<span><i class=\"fa fa-lock\" aria-hidden=\"true\"></i> Private customer records</span>\n\t\t\t\t<span><i class=\"fa fa-shopping-cart\" aria-hidden=\"true\"></i> Add quoted items to cart</span>\n\t\t\t</div>\n\t\t\t");
            if (id > 0)
            {
                sb.Append("\t\t\t\t<div class=\"epc-quotes-panel__actions\">\n\t\t\t\t\t<a class=\"btn btn-ar btn-primary\" href=\"/")
                    .Append(H(input.BackendDir)).Append("/shop/quote-requests?quote_id=").Append(Int(id))
                    .Append("\">Open quote #").Append(Int(id)).Append(" in control panel</a>\n\t\t\t\t</div>\n\t\t\t");
            }

            sb.Append("\t\t</div>\n\t</div>\n\t<div class=\"panel panel-primary epc-quotes-login-form\">\n\t")
                .Append(input.LoginFormHtml)
                .Append("\t</div>\n\t");
            return sb.ToString();
        }

        if (id > 0)
        {
            var quote = admin
                ? await RowsAsync(connection, "SELECT * FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1", [id], cancellationToken).ConfigureAwait(false)
                : await RowsAsync(connection, "SELECT * FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1", [id, input.UserId], cancellationToken).ConfigureAwait(false);
            if (quote.Count == 0)
            {
                sb.Append("\t\t<div class=\"epc-quotes-panel epc-quotes-panel--empty\">\n\t\t\t<div class=\"epc-quotes-panel__icon\"><i class=\"fa fa-search\" aria-hidden=\"true\"></i></div>\n\t\t\t<div class=\"epc-quotes-panel__content\">\n\t\t\t\t<span class=\"epc-quotes-panel__eyebrow\">Quote lookup</span>\n\t\t\t\t<h2>Quote #")
                    .Append(Int(id))
                    .Append(" not found</h2>\n\t\t\t\t<p>The quote may not exist, or it may belong to another customer account.</p>\n\t\t\t\t<a class=\"btn btn-ar btn-primary\" href=\"")
                    .Append(lang)
                    .Append("/shop/quotes\">Back to quotes</a>\n\t\t\t</div>\n\t\t</div>\n\t\t");
                return sb.ToString();
            }

            var q = quote[0];
            var lines = await RowsAsync(connection, "SELECT * FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC", [id], cancellationToken).ConfigureAwait(false);
            var status = Get(q, "status");
            sb.Append("\t<div class=\"epc-quotes-panel\">\n\t\t<div class=\"epc-quotes-panel__icon\"><i class=\"fa fa-file-text-o\" aria-hidden=\"true\"></i></div>\n\t\t<div class=\"epc-quotes-panel__content\">\n\t\t\t<span class=\"epc-quotes-panel__eyebrow\">")
                .Append(admin ? "Administrator quote view" : "Customer quote")
                .Append("</span>\n\t\t\t<h2>Quote #").Append(Int(Get(q, "id")))
                .Append("</h2>\n\t\t\t<p>Status: <strong>").Append(H(status ?? string.Empty)).Append("</strong>")
                .Append(admin ? " · Customer user ID: " + Int(Get(q, "user_id")) : string.Empty)
                .Append("</p>\n\t\t\t<div class=\"epc-quotes-panel__chips\">\n\t\t\t\t<span><i class=\"fa fa-clock-o\" aria-hidden=\"true\"></i> Updated ")
                .Append(Truthy(Get(q, "time_updated")) ? date(UmapiImageProxy.PhpIntCast(Get(q, "time_updated"))) : "not yet")
                .Append("</span>\n\t\t\t\t");
            if (admin)
            {
                sb.Append("\t\t\t\t\t<span><i class=\"fa fa-user-secret\" aria-hidden=\"true\"></i> Admin access enabled</span>\n\t\t\t\t");
            }

            sb.Append("\t\t\t</div>\n\t\t</div>\n\t</div>\n\t<div class=\"row epc-quotes-detail\">\n\t\t<div class=\"col-md-12\">\n\t\t\t<p><a class=\"btn btn-ar btn-default\" href=\"")
                .Append(lang)
                .Append("/shop/quotes\">&larr; All quotes</a></p>\n\t\t\t");
            var customerNote = Get(q, "customer_note");
            if (!string.IsNullOrEmpty(customerNote))
            {
                sb.Append("\t\t\t\t<p><strong>Your note:</strong> ").Append(StorefrontOrderNotificationService.Nl2Br(H(customerNote))).Append("</p>\n\t\t\t");
            }

            sb.Append("\t\t\t");
            var adminNote = Get(q, "admin_note");
            if (!string.IsNullOrEmpty(adminNote))
            {
                sb.Append("\t\t\t\t<p><strong>Staff note:</strong> ").Append(StorefrontOrderNotificationService.Nl2Br(H(adminNote))).Append("</p>\n\t\t\t");
            }

            sb.Append("\n\t\t\t<table class=\"table table-bordered table-condensed\">\n\t\t\t\t<thead>\n\t\t\t\t\t<tr>\n\t\t\t\t\t\t<th>Requested</th>\n\t\t\t\t\t\t<th>Our offer</th>\n\t\t\t\t\t\t<th>Qty</th>\n\t\t\t\t\t\t<th>Quoted price</th>\n\t\t\t\t\t\t<th>Lead time (days)</th>\n\t\t\t\t\t\t<th>Line note</th>\n\t\t\t\t\t</tr>\n\t\t\t\t</thead>\n\t\t\t\t<tbody>\n\t\t\t\t");
            foreach (var ln in lines)
            {
                var (m, a, n) = ProductObject(Get(ln, "product_object_json"));
                var useAlt = Truthy(Get(ln, "offer_alternative")) && UmapiImageProxy.PhpIntCast(Get(ln, "offer_alternative")) == 1
                    && Truthy(Get(ln, "alt_manufacturer")) && Truthy(Get(ln, "alt_article"));
                string qty;
                string qp;
                string offerCell;
                if (useAlt)
                {
                    var offerM = H(Get(ln, "alt_manufacturer")!);
                    var offerA = H(Truthy(Get(ln, "alt_article_show")) ? Get(ln, "alt_article_show")! : Get(ln, "alt_article")!);
                    var offerN = Truthy(Get(ln, "alt_name")) ? H(Get(ln, "alt_name")!) : string.Empty;
                    qty = Int(Truthy(Get(ln, "alt_count_need")) ? Get(ln, "alt_count_need") : Get(ln, "count_need"));
                    qp = Get(ln, "alt_quoted_price") is { } altPrice ? H(Money(StorefrontPhpAjax.PhpFloatCast(altPrice))) : "—";
                    offerCell = "<strong>" + offerM + " " + offerA + "</strong>"
                        + (offerN != string.Empty ? "<div class=\"text-muted\" style=\"font-size:12px;\">" + offerN + "</div>" : string.Empty)
                        + "<div><span class=\"label label-warning\">Alternative</span></div>";
                }
                else
                {
                    qty = Int(Get(ln, "count_need"));
                    qp = Get(ln, "quoted_price") is { } price ? H(Money(StorefrontPhpAjax.PhpFloatCast(price))) : "—";
                    offerCell = "<strong>" + m + " " + a + "</strong>"
                        + (n != string.Empty ? "<div class=\"text-muted\" style=\"font-size:12px;\">" + n + "</div>" : string.Empty)
                        + "<div class=\"text-muted\" style=\"font-size:12px;\">As requested</div>";
                }

                var lt = Get(ln, "quoted_time_to_exe") is { } exe ? Int(exe) : "—";
                var lnote = Truthy(Get(ln, "line_admin_note")) ? H(Get(ln, "line_admin_note")!) : string.Empty;
                sb.Append("\t\t\t\t\t<tr>\n\t\t\t\t\t\t<td>\n\t\t\t\t\t\t\t<strong>").Append(m).Append(' ').Append(a).Append("</strong>\n\t\t\t\t\t\t\t");
                if (n != string.Empty)
                {
                    sb.Append("<div class=\"text-muted\" style=\"font-size:12px;\">").Append(n).Append("</div>");
                }

                sb.Append("\t\t\t\t\t\t</td>\n\t\t\t\t\t\t<td>").Append(offerCell)
                    .Append("</td>\n\t\t\t\t\t\t<td>").Append(qty)
                    .Append("</td>\n\t\t\t\t\t\t<td>").Append(qp)
                    .Append("</td>\n\t\t\t\t\t\t<td>").Append(lt)
                    .Append("</td>\n\t\t\t\t\t\t<td>").Append(lnote)
                    .Append("</td>\n\t\t\t\t\t</tr>\n\t\t\t\t");
            }

            sb.Append("\t\t\t\t</tbody>\n\t\t\t</table>\n\n\t\t\t");
            if (!admin && status == "draft" && lines.Count > 0)
            {
                sb.Append("\t\t\t\t<div class=\"form-group\">\n\t\t\t\t\t<label>Message to sales (optional)</label>\n\t\t\t\t\t<textarea class=\"form-control\" id=\"quote_customer_note\" rows=\"3\">")
                    .Append(H(customerNote ?? string.Empty))
                    .Append("</textarea>\n\t\t\t\t</div>\n\t\t\t\t<button type=\"button\" class=\"btn btn-primary\" id=\"btn_submit_quote\">Submit for quote</button>\n\t\t\t\t<script>\n\t\t\t\tjQuery('#btn_submit_quote').on('click', function() {\n\t\t\t\t\tjQuery.post('/content/shop/order_process/ajax_quote_submit.php', {\n\t\t\t\t\t\tquote_id: ")
                    .Append(Int(id))
                    .Append(",\n\t\t\t\t\t\tcustomer_note: jQuery('#quote_customer_note').val()\n\t\t\t\t\t}, function(r) {\n\t\t\t\t\t\tif (r.status) { location.reload(); }\n\t\t\t\t\t\telse { alert(r.message || 'Error'); }\n\t\t\t\t\t}, 'json');\n\t\t\t\t});\n\t\t\t\t</script>\n\t\t\t");
            }

            sb.Append("\n\t\t\t");
            if (!admin && status == "quoted")
            {
                sb.Append("\t\t\t\t<p>Review the prices above, then add everything to your cart and proceed to checkout.</p>\n\t\t\t\t<button type=\"button\" class=\"btn btn-success\" id=\"btn_accept_quote\">Accept and add to cart</button>\n\t\t\t\t<script>\n\t\t\t\tjQuery('#btn_accept_quote').on('click', function() {\n\t\t\t\t\tif (!confirm('Add quoted lines to your cart?')) return;\n\t\t\t\t\tjQuery.post('/content/shop/order_process/ajax_quote_accept.php', {\n\t\t\t\t\t\tquote_id: ")
                    .Append(Int(id))
                    .Append("\t\t\t\t\t}, function(r) {\n\t\t\t\t\t\tif (r.status) {\n\t\t\t\t\t\t\twindow.location.href = '")
                    .Append(lang)
                    .Append("/shop/cart';\n\t\t\t\t\t\t} else {\n\t\t\t\t\t\t\talert(r.message || 'Error');\n\t\t\t\t\t\t}\n\t\t\t\t\t}, 'json');\n\t\t\t\t});\n\t\t\t\t</script>\n\t\t\t");
            }

            sb.Append("\t\t\t");
            if (admin)
            {
                sb.Append("\t\t\t\t<p class=\"epc-quotes-admin-note\">You are viewing this quote with administrator access. Use the control panel to edit prices, lead time, and staff notes.</p>\n\t\t\t\t<a class=\"btn btn-ar btn-primary\" href=\"/")
                    .Append(H(input.BackendDir)).Append("/shop/quote-requests?quote_id=").Append(Int(Get(q, "id")))
                    .Append("\">Open in control panel</a>\n\t\t\t");
            }

            sb.Append("\t\t</div>\n\t</div>\n\t");
            return sb.ToString();
        }

        var rows = admin
            ? await RowsAsync(connection, "SELECT * FROM `shop_quote_requests` ORDER BY `id` DESC", [], cancellationToken).ConfigureAwait(false)
            : await RowsAsync(connection, "SELECT * FROM `shop_quote_requests` WHERE `user_id` = ? ORDER BY `id` DESC", [input.UserId], cancellationToken).ConfigureAwait(false);
        sb.Append("<div class=\"epc-quotes-panel\">\n\t<div class=\"epc-quotes-panel__icon\"><i class=\"fa fa-list-alt\" aria-hidden=\"true\"></i></div>\n\t<div class=\"epc-quotes-panel__content\">\n\t\t<span class=\"epc-quotes-panel__eyebrow\">")
            .Append(admin ? "Administrator quote list" : "Customer quotes")
            .Append("</span>\n\t\t<h2>").Append(admin ? "All customer quotes" : "My quotes")
            .Append("</h2>\n\t\t<p>").Append(admin ? "You are viewing quote requests with backend administrator access." : "Track your requested prices, staff replies, and quoted items.")
            .Append("</p>\n\t</div>\n</div>\n<div class=\"row\">\n\t<div class=\"col-md-12\">\n\t\t");
        if (rows.Count == 0)
        {
            sb.Append("\t\t\t<div class=\"epc-quotes-empty\">No quotes yet. Use &quot;Add to quote&quot; on part search results.</div>\n\t\t");
        }
        else
        {
            sb.Append("\t\t\t<table class=\"table table-striped\">\n\t\t\t\t<thead>\n\t\t\t\t\t<tr>\n\t\t\t\t\t\t<th>ID</th>\n\t\t\t\t\t\t");
            if (admin)
            {
                sb.Append("<th>User</th>");
            }

            sb.Append("\t\t\t\t\t\t<th>Status</th>\n\t\t\t\t\t\t<th>Updated</th>\n\t\t\t\t\t\t<th></th>\n\t\t\t\t\t</tr>\n\t\t\t\t</thead>\n\t\t\t\t<tbody>\n\t\t\t\t");
            foreach (var r in rows)
            {
                sb.Append("\t\t\t\t\t<tr>\n\t\t\t\t\t\t<td>").Append(Int(Get(r, "id"))).Append("</td>\n\t\t\t\t\t\t");
                if (admin)
                {
                    sb.Append("<td>").Append(Int(Get(r, "user_id"))).Append("</td>");
                }

                sb.Append("\t\t\t\t\t\t<td>").Append(H(Get(r, "status") ?? string.Empty))
                    .Append("</td>\n\t\t\t\t\t\t<td>").Append(Truthy(Get(r, "time_updated")) ? date(UmapiImageProxy.PhpIntCast(Get(r, "time_updated"))) : string.Empty)
                    .Append("</td>\n\t\t\t\t\t\t<td><a href=\"").Append(lang).Append("/shop/quotes?id=").Append(Int(Get(r, "id")))
                    .Append("\">View</a></td>\n\t\t\t\t\t</tr>\n\t\t\t\t");
            }

            sb.Append("\t\t\t\t</tbody>\n\t\t\t</table>\n\t\t");
        }

        sb.Append("\t</div>\n</div>\n");
        return sb.ToString();
    }

    /// <summary>
    /// <c>json_decode($ln['product_object_json'], true)</c>, then the escaped <c>manufacturer</c>, <c>article_show</c>
    /// and <c>name</c> values that are set (not null) on the decoded object.
    /// </summary>
    private static (string M, string A, string N) ProductObject(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            string Field(string key)
            {
                if (!doc.RootElement.TryGetProperty(key, out var value))
                {
                    return string.Empty;
                }

                return value.ValueKind switch
                {
                    JsonValueKind.String => H(value.GetString()!),
                    JsonValueKind.Number => H(value.GetRawText()),
                    JsonValueKind.True => "1",
                    _ => string.Empty,
                };
            }

            return (Field("manufacturer"), Field("article_show"), Field("name"));
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty, string.Empty);
        }
    }

    private static string H(string value) => StorefrontSupplierLpoNotifier.H(value);

    private static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Int(string? value) => Int(UmapiImageProxy.PhpIntCast(value));

    /// <summary>PHP truthiness of a column value: not null, not <c>''</c> and not <c>'0'</c>.</summary>
    private static bool Truthy(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    private static string? Get(IReadOnlyDictionary<string, string?> row, string key) => row.TryGetValue(key, out var value) ? value : null;

    private static async Task<List<Dictionary<string, string?>>> RowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i)
                    ? null
                    : reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }
}
