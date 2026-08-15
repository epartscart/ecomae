using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpSalesOrderLineInput(string ItemCode, string Description, decimal Qty, decimal UnitPriceExVat, decimal LineExVat);

public sealed record ErpSalesOrderInput
{
    public long Id { get; init; }

    public int CustomerUserId { get; init; }

    public int ContactId { get; init; }

    public string Title { get; init; } = string.Empty;

    public decimal AmountExVat { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    public bool Export { get; init; }

    /// <summary>PHP <c>lines_json</c> payload (array of item_code/description/qty/unit_price_ex_vat/line_ex_vat).</summary>
    public string? LinesJson { get; init; }

    public IReadOnlyList<ErpSalesOrderLineInput> Lines { get; init; } = [];
}

public sealed record ErpSalesOrderSaveResult(long Id, string SoNo, decimal AmountExVat, decimal VatAmount, decimal TotalAmount, string Status, bool Created);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_sales_order_save</c> / <c>epc_erp_sales_order_set_status</c> /
/// <c>epc_erp_sales_order_delete</c> (<c>content/shop/finance/epc_erp_vouchers.php</c>).
/// Same validation, voucher numbering, tax resolution, line rewrite and audit trail.
/// </summary>
public interface IErpSalesOrderWriteService
{
    Task<ErpSalesOrderSaveResult> SaveAsync(ErpSalesOrderInput input, int adminId, CancellationToken cancellationToken = default);

    Task SetStatusAsync(long salesOrderId, string status, int adminId, CancellationToken cancellationToken = default);

    Task DeleteAsync(long salesOrderId, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpSalesOrderWriteService : IErpSalesOrderWriteService
{
    public static readonly string[] AllowedStatuses = ["draft", "confirmed", "invoiced", "cancelled"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpAuditLogWriter _audit;

    public ErpSalesOrderWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpTaxAmountCalculator tax,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _vouchers = vouchers;
        _tax = tax;
        _audit = audit;
    }

    public async Task<ErpSalesOrderSaveResult> SaveAsync(ErpSalesOrderInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var lines = ResolveLines(input);
        var title = (input.Title ?? string.Empty).Trim();
        var amountEx = ResolveAmountExVat(input.AmountExVat, lines);
        if (input.CustomerUserId <= 0 || title.Length == 0 || amountEx <= 0m)
        {
            throw new ErpWriteException("Customer, title, and amount (or lines) are required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var customerExists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            input.CustomerUserId).ConfigureAwait(false);
        if (customerExists <= 0)
        {
            throw new ErpWriteException("Customer not found");
        }

        var tax = await _tax.CalcAsync(connection, null, amountEx, input.CustomerUserId, input.ContactId, input.Export, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var status = AllowedStatuses.Contains(input.Status, StringComparer.Ordinal) ? input.Status : "draft";
        var notes = (input.Notes ?? string.Empty).Trim();

        long salesOrderId;
        string soNo;
        bool created;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (input.Id > 0)
            {
                var current = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `status` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    input.Id).ConfigureAwait(false);
                if (current is null)
                {
                    throw new ErpWriteException("Sales order not found");
                }

                if (string.Equals(current, "invoiced", StringComparison.Ordinal))
                {
                    throw new ErpWriteException("Invoiced sales orders cannot be edited");
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_sales_orders` SET `customer_user_id`=?, `title`=?, `amount_ex_vat`=?, `vat_amount`=?,"
                        + " `total_amount`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                    cancellationToken,
                    input.CustomerUserId,
                    Clip(title, 255),
                    tax.AmountExVat,
                    tax.VatAmount,
                    tax.TotalAmount,
                    status,
                    notes,
                    now,
                    input.Id).ConfigureAwait(false);

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ?"),
                    cancellationToken,
                    input.Id).ConfigureAwait(false);

                salesOrderId = input.Id;
                soNo = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `so_no` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    salesOrderId).ConfigureAwait(false) ?? string.Empty;
                created = false;
            }
            else
            {
                soNo = await _vouchers.NextAsync(connection, transaction, "SO", cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_sales_orders` (`so_no`, `customer_user_id`, `contact_id`, `title`, `amount_ex_vat`,"
                        + " `vat_amount`, `total_amount`, `status`, `notes`, `admin_id`, `time_created`, `time_updated`)"
                        + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    soNo,
                    input.CustomerUserId,
                    input.ContactId,
                    Clip(title, 255),
                    tax.AmountExVat,
                    tax.VatAmount,
                    tax.TotalAmount,
                    "draft",
                    notes,
                    adminId,
                    now,
                    now).ConfigureAwait(false);
                salesOrderId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                status = "draft";
                created = true;
            }

            var lineNo = 1;
            foreach (var line in lines)
            {
                await InsertLineAsync(connection, transaction, salesOrderId, lineNo++, line, cancellationToken).ConfigureAwait(false);
            }

            if (lines.Count == 0)
            {
                await InsertLineAsync(
                    connection,
                    transaction,
                    salesOrderId,
                    1,
                    new ErpSalesOrderLineInput(string.Empty, title, 1m, tax.AmountExVat, tax.AmountExVat),
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "sales_order_save",
            "sales_order",
            salesOrderId,
            "Sales order saved",
            new Dictionary<string, string?> { ["so_no"] = soNo },
            cancellationToken).ConfigureAwait(false);

        return new ErpSalesOrderSaveResult(salesOrderId, soNo, tax.AmountExVat, tax.VatAmount, tax.TotalAmount, status, created);
    }

    public async Task SetStatusAsync(long salesOrderId, string status, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (!AllowedStatuses.Contains(status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Invalid sales order status");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var affected = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `status` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            status,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            salesOrderId).ConfigureAwait(false);
        if (affected == 0)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                salesOrderId).ConfigureAwait(false);
            if (exists <= 0)
            {
                throw new ErpWriteException("Sales order not found");
            }
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "so_status",
            "sales_order",
            salesOrderId,
            "Sales order status updated",
            new Dictionary<string, string?> { ["status"] = status },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(long salesOrderId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            salesOrderId).ConfigureAwait(false);
        if (status is null)
        {
            throw new ErpWriteException("Sales order not found");
        }

        if (!string.Equals(status, "draft", StringComparison.Ordinal))
        {
            throw new ErpWriteException("Only draft sales orders can be deleted");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ?"),
                cancellationToken,
                salesOrderId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_sales_orders` WHERE `id` = ?"),
                cancellationToken,
                salesOrderId).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "so_delete",
            "sales_order",
            salesOrderId,
            "Sales order deleted",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_sales_order_parse_lines</c>: <c>lines_json</c> wins, then repeated line fields.</summary>
    public static IReadOnlyList<ErpSalesOrderLineInput> ResolveLines(ErpSalesOrderInput input)
    {
        var parsed = ParseLinesJson(input.LinesJson);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        var lines = new List<ErpSalesOrderLineInput>();
        foreach (var line in input.Lines)
        {
            var description = (line.Description ?? string.Empty).Trim();
            if (description.Length == 0)
            {
                continue;
            }

            var qty = decimal.Max(0.0001m, line.Qty);
            var unit = decimal.Round(line.UnitPriceExVat, 4, MidpointRounding.AwayFromZero);
            lines.Add(new ErpSalesOrderLineInput(
                (line.ItemCode ?? string.Empty).Trim(),
                description,
                qty,
                unit,
                ErpTaxAmountCalculator.Round2(qty * unit)));
        }

        return lines;
    }

    public static decimal ResolveAmountExVat(decimal amountExVat, IReadOnlyList<ErpSalesOrderLineInput> lines)
    {
        if (lines.Count == 0)
        {
            return ErpTaxAmountCalculator.Round2(amountExVat);
        }

        var total = 0m;
        foreach (var line in lines)
        {
            total += ErpTaxAmountCalculator.Round2(line.LineExVat);
        }

        return ErpTaxAmountCalculator.Round2(total);
    }

    private static List<ErpSalesOrderLineInput> ParseLinesJson(string? linesJson)
    {
        var lines = new List<ErpSalesOrderLineInput>();
        if (string.IsNullOrWhiteSpace(linesJson))
        {
            return lines;
        }

        try
        {
            using var document = JsonDocument.Parse(linesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return lines;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var description = ReadText(element, "description") ?? ReadText(element, "item_name") ?? "Line";
                var qty = decimal.Max(0.0001m, ReadDecimal(element, "qty") ?? 1m);
                var unit = decimal.Round(ReadDecimal(element, "unit_price_ex_vat") ?? ReadDecimal(element, "unit_price") ?? 0m, 4, MidpointRounding.AwayFromZero);
                var net = ReadDecimal(element, "line_ex_vat") ?? qty * unit;
                lines.Add(new ErpSalesOrderLineInput(
                    (ReadText(element, "item_code") ?? string.Empty).Trim(),
                    description.Trim(),
                    qty,
                    unit,
                    ErpTaxAmountCalculator.Round2(net)));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return lines;
    }

    private static string? ReadText(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static decimal? ReadDecimal(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static Task InsertLineAsync(
        DbConnection connection,
        DbTransaction transaction,
        long salesOrderId,
        int lineNo,
        ErpSalesOrderLineInput line,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`, `line_no`, `item_code`, `description`, `qty`,"
                + " `unit_price_ex_vat`, `line_ex_vat`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            salesOrderId,
            lineNo,
            Clip(line.ItemCode, 32),
            Clip(string.IsNullOrWhiteSpace(line.Description) ? "Line" : line.Description, 255),
            decimal.Max(0.0001m, line.Qty),
            decimal.Round(line.UnitPriceExVat, 4, MidpointRounding.AwayFromZero),
            ErpTaxAmountCalculator.Round2(line.LineExVat));

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    /// <summary>Subset of PHP <c>epc_erp_vouchers_ensure_schema</c> for the tables this service writes.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_voucher_sequences` ("
            + " `voucher_type` varchar(8) NOT NULL,"
            + " `year` int(11) NOT NULL,"
            + " `last_seq` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`voucher_type`, `year`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP voucher number sequences'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sales_orders` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `so_no` varchar(32) NOT NULL,"
            + " `customer_user_id` int(11) NOT NULL DEFAULT 0,"
            + " `contact_id` int(11) NOT NULL DEFAULT 0,"
            + " `title` varchar(255) NOT NULL DEFAULT '',"
            + " `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `status` enum('draft','confirmed','invoiced','cancelled') NOT NULL DEFAULT 'draft',"
            + " `sales_invoice_id` int(11) NOT NULL DEFAULT 0,"
            + " `notes` text,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_so_no` (`so_no`),"
            + " KEY `x_customer` (`customer_user_id`),"
            + " KEY `x_status` (`status`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP manual sales orders (no storefront)'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sales_order_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `sales_order_id` int(11) NOT NULL,"
            + " `line_no` int(11) NOT NULL DEFAULT 1,"
            + " `item_code` varchar(32) NOT NULL DEFAULT '',"
            + " `description` varchar(255) NOT NULL,"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 1.000,"
            + " `unit_price_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_so` (`sales_order_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP sales order lines'",
            cancellationToken).ConfigureAwait(false);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
