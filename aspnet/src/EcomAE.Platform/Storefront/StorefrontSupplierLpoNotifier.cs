using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>epc_send_supplier_lpo_notifications()</c>: one <c>lpo_to_supplier</c> e-mail per warehouse on the
/// order (own warehouses and supplier price-list warehouses alike). The LPO number is the customer order number.
/// Every outcome is written to <c>shop_orders_logs</c> exactly as <c>epc_log_order_notification()</c> does.
/// </summary>
public interface IStorefrontSupplierLpoNotifier
{
    Task<StorefrontSupplierLpoResult> SendAsync(long orderId, CancellationToken cancellationToken = default);
}

public sealed record StorefrontSupplierLpoResult(int Sent, int Skipped, IReadOnlyList<string> Log);

public sealed class StorefrontSupplierLpoNotifier : IStorefrontSupplierLpoNotifier
{
    public const string NotificationName = "lpo_to_supplier";
    public const string NoLinesLog = "Supplier LPO: no warehouse lines to notify";
    public const string LoadFailedLog = "Supplier LPO: failed to load order items";
    public const string NoneSentLog = "Supplier LPO: 0 sent — set \"Supplier order email (LPO)\" on each warehouse in CP → Logistics → Warehouses";

    private static readonly string[] OrderEmailKeys = ["order_email", "supplier_order_email", "lpo_email"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IStorefrontNotifyDispatcher _notify;
    private readonly ICpPlatformMailer _mailer;

    public StorefrontSupplierLpoNotifier(
        IErpWriteConnectionFactory connections,
        IStorefrontNotifyDispatcher notify,
        ICpPlatformMailer mailer)
    {
        _connections = connections;
        _notify = notify;
        _mailer = mailer;
    }

    public async Task<StorefrontSupplierLpoResult> SendAsync(long orderId, CancellationToken cancellationToken = default)
    {
        var log = new List<string>();
        if (orderId <= 0 || !_connections.IsConfigured)
        {
            return new StorefrontSupplierLpoResult(0, 0, log);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var storages = new Dictionary<long, string>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`, `name` FROM `shop_storages`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                storages[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] =
                    reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
        catch (DbException)
        {
            return new StorefrontSupplierLpoResult(0, 0, log);
        }

        var order = new List<long>();
        var byStorage = new Dictionary<long, List<LpoLine>>();
        try
        {
            var items = new List<(long Id, long StorageId, LpoLine Line)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`t2_storage_id`,0), `t2_manufacturer`, `t2_article_show`, `t2_article`, `t2_name`, IFNULL(`count_need`,0) "
                    + "FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC");
                ErpDb.AddParameters(command, orderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var article = reader.IsDBNull(3) ? Str(reader, 4) : Str(reader, 3);
                    items.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        new LpoLine(Str(reader, 2), article, Str(reader, 5), PhpIntQty(reader.GetValue(6)))));
                }
            }

            foreach (var item in items)
            {
                var storageId = item.StorageId;
                if (storageId <= 0)
                {
                    storageId = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `storage_id` FROM `shop_orders_items_details` WHERE `order_item_id` = ? ORDER BY `id` ASC LIMIT 1"),
                        cancellationToken,
                        item.Id).ConfigureAwait(false);
                }

                if (storageId <= 0)
                {
                    continue;
                }

                if (!byStorage.TryGetValue(storageId, out var lines))
                {
                    lines = [];
                    byStorage[storageId] = lines;
                    order.Add(storageId);
                }

                lines.Add(item.Line);
            }
        }
        catch (DbException)
        {
            await LogAsync(connection, orderId, LoadFailedLog, log, cancellationToken).ConfigureAwait(false);
            return new StorefrontSupplierLpoResult(0, 0, log);
        }

        if (order.Count == 0)
        {
            await LogAsync(connection, orderId, NoLinesLog, log, cancellationToken).ConfigureAwait(false);
            return new StorefrontSupplierLpoResult(0, 0, log);
        }

        var domain = DomainPath(_mailer.ReadConfig());
        var sent = 0;
        var skipped = 0;
        foreach (var storageId in order)
        {
            var email = await StorageOrderEmailAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
            var storageName = storages.TryGetValue(storageId, out var name) ? name : "Warehouse #" + storageId.ToString(CultureInfo.InvariantCulture);
            if (email.Length == 0)
            {
                skipped++;
                await LogAsync(
                    connection,
                    orderId,
                    "Supplier LPO skipped (no order e-mail): " + storageName + " [ID " + storageId.ToString(CultureInfo.InvariantCulture) + "]",
                    log,
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            var orderNo = orderId.ToString(CultureInfo.InvariantCulture);
            var vars = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["order_id"] = orderNo,
                ["lpo_number"] = orderNo,
                ["storage_name"] = storageName,
                ["order_text"] = BuildLpoHtml(orderId, storageName, byStorage[storageId], domain),
            };

            var ok = await SendOnceAsync(connection, vars, email, cancellationToken).ConfigureAwait(false)
                || await SendOnceAsync(connection, vars, email, cancellationToken).ConfigureAwait(false);
            await LogAsync(
                connection,
                orderId,
                "Supplier LPO to " + email + " (" + storageName + ", LPO #" + orderNo + "): " + (ok ? "sent" : "FAILED"),
                log,
                cancellationToken).ConfigureAwait(false);
            if (ok)
            {
                sent++;
            }
        }

        if (sent == 0 && skipped > 0)
        {
            await LogAsync(connection, orderId, NoneSentLog, log, cancellationToken).ConfigureAwait(false);
        }

        return new StorefrontSupplierLpoResult(sent, skipped, log);
    }

    /// <summary>PHP <c>epc_storage_supplier_order_email()</c>: connection_options e-mail keys, else the price list sender e-mail.</summary>
    public static async Task<string> StorageOrderEmailAsync(DbConnection connection, long storageId, CancellationToken cancellationToken)
    {
        if (storageId <= 0)
        {
            return string.Empty;
        }

        try
        {
            var options = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                storageId).ConfigureAwait(false);
            if (options is null)
            {
                return string.Empty;
            }

            var (email, priceId) = OrderEmailFromOptions(options);
            if (email.Length > 0)
            {
                return email;
            }

            if (priceId > 0)
            {
                var sender = (await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `sender_email` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    priceId).ConfigureAwait(false) ?? string.Empty).Trim().ToLowerInvariant();
                if (IsPlainEmail(sender))
                {
                    return sender;
                }
            }
        }
        catch (DbException)
        {
        }

        return string.Empty;
    }

    public static (string Email, long PriceId) OrderEmailFromOptions(string connectionOptionsJson)
    {
        if (string.IsNullOrWhiteSpace(connectionOptionsJson))
        {
            return (string.Empty, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(connectionOptionsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, 0);
            }

            foreach (var key in OrderEmailKeys)
            {
                if (doc.RootElement.TryGetProperty(key, out var el))
                {
                    var email = Scalar(el).Trim().ToLowerInvariant();
                    if (IsPlainEmail(email))
                    {
                        return (email, 0);
                    }
                }
            }

            long priceId = 0;
            if (doc.RootElement.TryGetProperty("price_id", out var priceEl))
            {
                priceId = PhpLong(Scalar(priceEl));
            }

            return (string.Empty, priceId);
        }
        catch (JsonException)
        {
            return (string.Empty, 0);
        }
    }

    /// <summary>Close to PHP <c>FILTER_VALIDATE_EMAIL</c>: a bare <c>local@domain.tld</c> address, no display name or spaces.</summary>
    public static bool IsPlainEmail(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace) || value.IndexOfAny(['<', '>', '"', ',', ';']) >= 0)
        {
            return false;
        }

        var at = value.LastIndexOf('@');
        if (at <= 0 || at != value.IndexOf('@') || value.IndexOf('.', at) < 0 || value.EndsWith('.'))
        {
            return false;
        }

        return MailAddress.TryCreate(value, out var parsed) && string.Equals(parsed.Address, value, StringComparison.Ordinal);
    }

    /// <summary>PHP <c>epc_build_supplier_lpo_html()</c>.</summary>
    public static string BuildLpoHtml(long orderId, string storageName, IReadOnlyList<LpoLine> items, string domain)
    {
        var no = orderId.ToString(CultureInfo.InvariantCulture);
        var html = new StringBuilder();
        html.Append("<div style=\"font-family:Calibri,Arial,sans-serif;font-size:14px;color:#111;\">");
        html.Append("<p style=\"margin:0 0 12px;\">Dear supplier,</p>");
        html.Append("<p style=\"margin:0 0 12px;\">Please supply the following parts for our customer order. ")
            .Append("Use <strong>LPO / PO number <span style=\"color:#b45309;\">").Append(no).Append("</span></strong> ")
            .Append("on your invoice and delivery note (this is our customer order number).</p>");
        html.Append("<table style=\"border-collapse:collapse;margin:0 0 16px;font-size:14px;\">");
        html.Append("<tr><td style=\"padding:4px 16px 4px 0;font-weight:bold;\">LPO number</td><td>").Append(no).Append("</td></tr>");
        html.Append("<tr><td style=\"padding:4px 16px 4px 0;font-weight:bold;\">Warehouse</td><td>").Append(H(storageName)).Append("</td></tr>");
        html.Append("</table>");
        html.Append("<table style=\"border-collapse:collapse;width:100%;max-width:720px;font-size:13px;\" border=\"1\" cellpadding=\"6\" cellspacing=\"0\">");
        html.Append("<thead><tr style=\"background:#f1f5f9;\">")
            .Append("<th align=\"left\">Brand</th><th align=\"left\">Part no.</th><th align=\"left\">Description</th><th align=\"right\">Qty</th>")
            .Append("</tr></thead><tbody>");
        foreach (var item in items)
        {
            if (item.Qty <= 0)
            {
                continue;
            }

            html.Append("<tr>")
                .Append("<td>").Append(H(item.Brand)).Append("</td>")
                .Append("<td>").Append(H(item.Article)).Append("</td>")
                .Append("<td>").Append(H(item.Name)).Append("</td>")
                .Append("<td align=\"right\">").Append(item.Qty.ToString(CultureInfo.InvariantCulture)).Append("</td>")
                .Append("</tr>");
        }

        html.Append("</tbody></table>");
        html.Append("<p style=\"margin:16px 0 0;font-size:13px;color:#475569;\">Reply to this e-mail if any line is unavailable. ")
            .Append("Reference LPO <strong>").Append(no).Append("</strong> on all correspondence.</p>");
        if (domain.Length > 0)
        {
            html.Append("<p style=\"margin:8px 0 0;font-size:12px;color:#64748b;\">").Append(H(domain)).Append("</p>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    /// <summary>PHP <c>htmlspecialchars(ENT_QUOTES)</c>.</summary>
    public static string H(string value)
        => (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static string DomainPath(IReadOnlyDictionary<string, string> config)
        => config.TryGetValue("domain_path", out var domain) ? domain.Trim().TrimEnd('/') : string.Empty;

    private async Task<bool> SendOnceAsync(DbConnection connection, IReadOnlyDictionary<string, string> vars, string email, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _notify.SendDirectEmailAsync(connection, NotificationName, vars, email, cancellationToken).ConfigureAwait(false);
            return outcome.Found && outcome.EmailSent;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task LogAsync(DbConnection connection, long orderId, string text, List<string> log, CancellationToken cancellationToken)
    {
        log.Add(text);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?, ?, 0, 0, ?, 1)"),
                cancellationToken,
                orderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                text).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static string Str(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static int PhpIntQty(object value)
        => value is DBNull ? 0 : (int)Math.Truncate(Convert.ToDecimal(value, CultureInfo.InvariantCulture));

    private static string Scalar(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? string.Empty,
        JsonValueKind.Number => el.GetRawText(),
        _ => string.Empty,
    };

    private static long PhpLong(string raw)
    {
        raw = raw.Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] == '-')))
        {
            end++;
        }

        return long.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    public sealed record LpoLine(string Brand, string Article, string Name, int Qty);
}
