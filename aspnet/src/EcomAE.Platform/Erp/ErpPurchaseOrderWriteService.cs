using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpPurchaseOrderLineInput(string ItemCode, string Description, decimal Qty, decimal UnitCostExVat, decimal LineExVat);

public sealed record ErpPurchaseOrderInput
{
    public long Id { get; init; }

    public int SupplierId { get; init; }

    public string Title { get; init; } = string.Empty;

    public decimal AmountExVat { get; init; }

    public string Status { get; init; } = string.Empty;

    public string Notes { get; init; } = string.Empty;

    /// <summary>PHP <c>expected_version</c> — optimistic concurrency guard on edits (0 skips the check).</summary>
    public int ExpectedVersion { get; init; }

    /// <summary>JSON array of item_code/description/qty/unit_cost_ex_vat/line_ex_vat (the PO line grid).</summary>
    public string? LinesJson { get; init; }

    public IReadOnlyList<ErpPurchaseOrderLineInput> Lines { get; init; } = [];
}

public sealed record ErpPurchaseOrderSaveResult(
    long Id,
    string PoNo,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    string Status,
    bool Created,
    int LinesAdded);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_po_save</c> / <c>epc_erp_po_set_status</c> (<c>epc_erp_extended.php</c>),
/// <c>epc_erp_order_fulfillment_append_po_lines</c> and <c>epc_erp_po_delete</c> (<c>epc_erp_doc_lifecycle.php</c>).
/// Same validation, PO voucher numbering, purchase-side tax resolution, line totals and audit trail.
/// </summary>
public interface IErpPurchaseOrderWriteService
{
    Task<ErpPurchaseOrderSaveResult> SaveAsync(ErpPurchaseOrderInput input, int adminId, CancellationToken cancellationToken = default);

    Task SetStatusAsync(long purchaseOrderId, string status, int adminId, CancellationToken cancellationToken = default);

    Task DeleteAsync(long purchaseOrderId, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseOrderWriteService : IErpPurchaseOrderWriteService
{
    public static readonly string[] AllowedStatuses = ["draft", "approved", "partial", "received", "cancelled"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpAuditLogWriter _audit;

    public ErpPurchaseOrderWriteService(
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

    public async Task<ErpPurchaseOrderSaveResult> SaveAsync(ErpPurchaseOrderInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length == 0 || input.SupplierId <= 0)
        {
            throw new ErpWriteException("Supplier and title are required");
        }

        var lines = ResolveLines(input);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var amountEx = ErpTaxAmountCalculator.Round2(input.AmountExVat);
        var tax = await _tax.CalcPurchaseAsync(connection, null, amountEx, input.SupplierId, false, cancellationToken).ConfigureAwait(false);
        var status = AllowedStatuses.Contains(input.Status, StringComparer.Ordinal) ? input.Status : "draft";
        var notes = (input.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        long purchaseOrderId;
        string poNo;
        bool created;
        int linesAdded;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (input.Id > 0)
            {
                await AssertAndBumpVersionAsync(connection, transaction, input.Id, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_purchase_orders` SET `supplier_id`=?, `title`=?, `amount_ex_vat`=?, `vat_amount`=?,"
                        + " `total_amount`=?, `status`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                    cancellationToken,
                    input.SupplierId,
                    Clip(title, 255),
                    tax.AmountExVat,
                    tax.VatAmount,
                    tax.TotalAmount,
                    status,
                    notes,
                    now,
                    input.Id).ConfigureAwait(false);

                purchaseOrderId = input.Id;
                poNo = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `po_no` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    purchaseOrderId).ConfigureAwait(false) ?? string.Empty;
                created = false;
            }
            else
            {
                poNo = await _vouchers.NextAsync(connection, transaction, "PO", cancellationToken).ConfigureAwait(false);

                // When lines are supplied the header starts at zero and the line append below derives the
                // real total from qty x cost, so it is not double-counted (PHP epc_erp_po_save).
                var headerEx = lines.Count > 0 ? 0m : tax.AmountExVat;
                var headerVat = lines.Count > 0 ? 0m : tax.VatAmount;
                var headerTotal = lines.Count > 0 ? 0m : tax.TotalAmount;

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_purchase_orders` (`po_no`, `voucher_no`, `supplier_id`, `title`, `amount_ex_vat`,"
                        + " `vat_amount`, `total_amount`, `status`, `notes`, `admin_id`, `time_created`, `time_updated`)"
                        + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    poNo,
                    poNo,
                    input.SupplierId,
                    Clip(title, 255),
                    headerEx,
                    headerVat,
                    headerTotal,
                    "draft",
                    notes,
                    adminId,
                    now,
                    now).ConfigureAwait(false);

                purchaseOrderId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                status = "draft";
                created = true;
            }

            linesAdded = await AppendLinesAsync(connection, transaction, purchaseOrderId, lines, now, cancellationToken).ConfigureAwait(false);
            var totals = await LoadTotalsAsync(connection, transaction, purchaseOrderId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            await _audit.LogAsync(
                connection,
                null,
                adminId,
                "po_save",
                "purchase_order",
                purchaseOrderId,
                created ? "Purchase order created" : "Purchase order updated",
                new Dictionary<string, string?> { ["po_no"] = poNo },
                cancellationToken).ConfigureAwait(false);

            return new ErpPurchaseOrderSaveResult(
                purchaseOrderId,
                poNo,
                totals.AmountExVat,
                totals.VatAmount,
                totals.TotalAmount,
                status,
                created,
                linesAdded);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task SetStatusAsync(long purchaseOrderId, string status, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (!AllowedStatuses.Contains(status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Invalid PO status");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stamp = status switch
        {
            "approved" => ", `approved_at` = ?",
            "received" => ", `received_at` = ?",
            _ => string.Empty,
        };

        var parameters = stamp.Length > 0
            ? new object?[] { status, now, now, purchaseOrderId }
            : [status, now, purchaseOrderId];

        var affected = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `status` = ?, `time_updated` = ?" + stamp + " WHERE `id` = ?"),
            cancellationToken,
            parameters).ConfigureAwait(false);
        if (affected == 0)
        {
            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                purchaseOrderId).ConfigureAwait(false);
            if (exists <= 0)
            {
                throw new ErpWriteException("Purchase order not found");
            }
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "po_status",
            "purchase_order",
            purchaseOrderId,
            "PO status updated",
            new Dictionary<string, string?> { ["status"] = status },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(long purchaseOrderId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            purchaseOrderId).ConfigureAwait(false);
        if (status is null)
        {
            throw new ErpWriteException("Purchase order not found");
        }

        if (!string.Equals(status, "draft", StringComparison.Ordinal))
        {
            throw new ErpWriteException("Only draft purchase orders can be deleted — cancel posted ones instead");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_po_lines` WHERE `po_id` = ?"),
                cancellationToken,
                purchaseOrderId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_po_receipts` WHERE `po_id` = ?"),
                cancellationToken,
                purchaseOrderId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_purchase_orders` WHERE `id` = ?"),
                cancellationToken,
                purchaseOrderId).ConfigureAwait(false);
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
            "delete",
            "purchase_order",
            purchaseOrderId,
            "Draft PO deleted",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP PO line grid: <c>lines_json</c> wins, then the repeated <c>po_line_*</c> fields.</summary>
    public static IReadOnlyList<ErpPurchaseOrderLineInput> ResolveLines(ErpPurchaseOrderInput input)
    {
        var parsed = ParseLinesJson(input.LinesJson);
        if (parsed.Count > 0)
        {
            return parsed;
        }

        var lines = new List<ErpPurchaseOrderLineInput>();
        foreach (var line in input.Lines)
        {
            var description = (line.Description ?? string.Empty).Trim();
            var qty = decimal.Round(line.Qty, 3, MidpointRounding.AwayFromZero);
            if (description.Length == 0 || qty <= 0m)
            {
                continue;
            }

            var unit = decimal.Round(line.UnitCostExVat, 4, MidpointRounding.AwayFromZero);
            lines.Add(new ErpPurchaseOrderLineInput(
                (line.ItemCode ?? string.Empty).Trim(),
                description,
                qty,
                unit,
                ErpTaxAmountCalculator.Round2(qty * unit)));
        }

        return lines;
    }

    /// <summary>PHP <c>epc_erp_order_fulfillment_append_po_lines</c>: append after the highest line_no, then recompute the header from purchase-side tax.</summary>
    private async Task<int> AppendLinesAsync(
        DbConnection connection,
        DbTransaction transaction,
        long purchaseOrderId,
        IReadOnlyList<ErpPurchaseOrderLineInput> lines,
        long now,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return 0;
        }

        var header = await LoadHeaderAsync(connection, transaction, purchaseOrderId, cancellationToken).ConfigureAwait(false);
        if (header is null
            || string.Equals(header.Value.Status, "cancelled", StringComparison.Ordinal)
            || header.Value.PurchaseId > 0)
        {
            throw new ErpWriteException("Cannot append lines to this purchase order");
        }

        var lineNo = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT IFNULL(MAX(`line_no`), 0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"),
            cancellationToken,
            purchaseOrderId).ConfigureAwait(false);

        var added = 0;
        var amountAdd = 0m;
        foreach (var line in lines)
        {
            var lineEx = ErpTaxAmountCalculator.Round2(line.LineExVat);
            if (lineEx <= 0m)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_po_lines` (`po_id`, `supplier_id`, `line_no`, `item_code`, `description`, `qty`,"
                    + " `unit_cost_ex_vat`, `line_ex_vat`, `time_updated`) VALUES (?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                purchaseOrderId,
                header.Value.SupplierId,
                ++lineNo,
                Clip(line.ItemCode, 32),
                Clip(string.IsNullOrWhiteSpace(line.Description) ? "Line" : line.Description, 255),
                decimal.Round(line.Qty, 3, MidpointRounding.AwayFromZero),
                decimal.Round(line.UnitCostExVat, 4, MidpointRounding.AwayFromZero),
                lineEx,
                now).ConfigureAwait(false);

            amountAdd += lineEx;
            added++;
        }

        if (amountAdd <= 0m)
        {
            return added;
        }

        var newEx = ErpTaxAmountCalculator.Round2(header.Value.AmountExVat + amountAdd);
        var purchaseTax = await _tax.CalcPurchaseAsync(connection, transaction, newEx, header.Value.SupplierId, false, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "UPDATE `epc_erp_purchase_orders` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            newEx,
            purchaseTax.VatAmount,
            purchaseTax.TotalAmount,
            now,
            purchaseOrderId).ConfigureAwait(false);

        return added;
    }

    /// <summary>PHP <c>epc_erp_version_assert_and_bump</c> on <c>epc_erp_purchase_orders.row_version</c>.</summary>
    private static async Task AssertAndBumpVersionAsync(
        DbConnection connection,
        DbTransaction transaction,
        long purchaseOrderId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            purchaseOrderId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Purchase order not found");
        }

        if (expectedVersion <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `row_version` = `row_version` + 1 WHERE `id` = ?"),
                cancellationToken,
                purchaseOrderId).ConfigureAwait(false);
            return;
        }

        var bumped = await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `row_version` = `row_version` + 1 WHERE `id` = ? AND `row_version` = ?"),
            cancellationToken,
            purchaseOrderId,
            expectedVersion).ConfigureAwait(false);
        if (bumped >= 1)
        {
            return;
        }

        var current = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `row_version` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            purchaseOrderId).ConfigureAwait(false);
        throw new ErpWriteException(
            "Version conflict — another user saved this record"
            + (current > 0
                ? " (their version " + current.ToString(CultureInfo.InvariantCulture)
                    + ", yours " + expectedVersion.ToString(CultureInfo.InvariantCulture) + ")"
                : string.Empty)
            + ". Reload and re-apply your changes.");
    }

    private static async Task<(int SupplierId, string Status, long PurchaseId, decimal AmountExVat)?> LoadHeaderAsync(
        DbConnection connection,
        DbTransaction transaction,
        long purchaseOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `supplier_id`, `status`, `purchase_id`, `amount_ex_vat` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = purchaseOrderId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            reader.IsDBNull(1) ? "draft" : reader.GetString(1),
            reader.IsDBNull(2) ? 0L : reader.GetInt64(2),
            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3));
    }

    private static async Task<(decimal AmountExVat, decimal VatAmount, decimal TotalAmount)> LoadTotalsAsync(
        DbConnection connection,
        DbTransaction transaction,
        long purchaseOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `amount_ex_vat`, `vat_amount`, `total_amount` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = purchaseOrderId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0m, 0m, 0m);
        }

        return (
            reader.IsDBNull(0) ? 0m : reader.GetDecimal(0),
            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
            reader.IsDBNull(2) ? 0m : reader.GetDecimal(2));
    }

    private static List<ErpPurchaseOrderLineInput> ParseLinesJson(string? linesJson)
    {
        var lines = new List<ErpPurchaseOrderLineInput>();
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

                var description = (ReadText(element, "description") ?? ReadText(element, "item_name") ?? string.Empty).Trim();
                var qty = decimal.Round(ReadDecimal(element, "qty") ?? 0m, 3, MidpointRounding.AwayFromZero);
                if (description.Length == 0 || qty <= 0m)
                {
                    continue;
                }

                var unit = decimal.Round(
                    ReadDecimal(element, "unit_cost_ex_vat") ?? ReadDecimal(element, "unit_price_ex_vat") ?? 0m,
                    4,
                    MidpointRounding.AwayFromZero);
                var net = ReadDecimal(element, "line_ex_vat") ?? qty * unit;
                lines.Add(new ErpPurchaseOrderLineInput(
                    (ReadText(element, "item_code") ?? string.Empty).Trim(),
                    description,
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

    private static string Clip(string? value, int length)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= length ? text : text[..length];
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    /// <summary>Subset of PHP <c>epc_erp_extended_ensure_schema</c> / <c>epc_erp_order_fulfillment_ensure_schema</c>.</summary>
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
            "CREATE TABLE IF NOT EXISTS `epc_erp_purchase_orders` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `po_no` varchar(32) NOT NULL,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `title` varchar(255) NOT NULL DEFAULT '',"
            + " `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `status` enum('draft','approved','partial','received','cancelled') NOT NULL DEFAULT 'draft',"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `approved_at` int(11) NOT NULL DEFAULT 0,"
            + " `received_at` int(11) NOT NULL DEFAULT 0,"
            + " `notes` text,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_po_no` (`po_no`),"
            + " KEY `x_supplier` (`supplier_id`),"
            + " KEY `x_status` (`status`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Purchase orders with approval workflow'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_po_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `po_id` int(11) NOT NULL,"
            + " `shop_order_item_id` int(11) NOT NULL DEFAULT 0,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `storage_id` int(11) NOT NULL DEFAULT 0,"
            + " `line_no` int(11) NOT NULL DEFAULT 1,"
            + " `item_code` varchar(32) NOT NULL DEFAULT '',"
            + " `description` varchar(255) NOT NULL DEFAULT '',"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `unit_cost_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `qty_cancelled` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_po` (`po_id`),"
            + " KEY `x_order_item` (`shop_order_item_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='PO lines linked to shop order items'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_po_receipts` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `po_id` int(11) NOT NULL,"
            + " `receipt_no` varchar(32) NOT NULL,"
            + " `qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_po` (`po_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='PO goods receipts'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_purchase_orders` ADD COLUMN `voucher_no` varchar(32) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_purchase_orders` ADD COLUMN `row_version` int(11) NOT NULL DEFAULT 1",
            cancellationToken).ConfigureAwait(false);
    }
}
