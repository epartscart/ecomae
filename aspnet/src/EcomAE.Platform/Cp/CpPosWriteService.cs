using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_pos.php</c> twins for <c>open_session</c>, <c>close_session</c>,
/// <c>save_settings</c>, and the POS sale/line INSERT in <c>complete_sale</c>.
/// Walk-in user create, tax-toolkit totals, ERP SO/invoice/voucher, and inventory
/// movement stay PHP. Printable receipt HTML is ASP.NET-live.
/// </summary>
public interface ICpPosWriteService
{
    Task<ErpSimpleWriteResult> OpenSessionAsync(
        decimal openingFloat,
        int adminUserId,
        string? registerName,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CloseSessionAsync(
        long sessionId,
        decimal closingCash,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveSettingsAsync(
        bool posEnabled,
        string? registerName,
        int defaultWarehouseId,
        int defaultCashAccountId,
        int defaultCardAccountId,
        string? receiptHeader,
        string? receiptFooter,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CompleteSaleAsync(
        CpPosCompleteSaleWriteRequest request,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<CpPosReceipt> LoadReceiptAsync(long saleId, CancellationToken cancellationToken = default);
}

public sealed record CpPosSaleLineInput(
    string? Name,
    decimal Qty = 1,
    decimal UnitPriceEx = 0,
    decimal LineDiscountPct = 0,
    decimal LineDiscountAmt = 0,
    string? Sku = null,
    string? Barcode = null,
    string? Source = null,
    string? Ref = null,
    decimal Price = 0);

public sealed record CpPosCompleteSaleWriteRequest(
    long SessionId = 0,
    IReadOnlyList<CpPosSaleLineInput>? Lines = null,
    string? PaymentMethod = null,
    decimal CashAmount = 0,
    decimal CardAmount = 0,
    decimal TaxRate = 0,
    string? TaxKitCode = null,
    long CustomerUserId = 0,
    long ContactId = 0,
    string? CustomerLabel = null,
    string? SaleNotes = null);

public sealed class CpPosWriteService : ICpPosWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPosWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> OpenSessionAsync(
        decimal openingFloat,
        int adminUserId,
        string? registerName,
        CancellationToken cancellationToken = default)
    {
        if (openingFloat < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "opening_float cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var enabled = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`pos_enabled`,0) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (enabled != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "POS is disabled for this tenant");
        }

        var openNo = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `session_no` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(openNo))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A register session is already open (" + openNo + ")");
        }

        var reg = Clip(registerName, 64);
        if (reg.Length == 0)
        {
            reg = Clip(await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `register_name` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken), 64);
        }

        if (reg.Length == 0)
        {
            reg = "Register 1";
        }

        var sessionNo = "REG-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-"
                        + Random.Shared.Next(1, 10000).ToString("0000", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_pos_sessions` (`session_no`,`register_name`,`opened_by`,`opened_at`,`opening_float`,`status`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            sessionNo, reg, adminUserId, now, Math.Round(openingFloat, 2, MidpointRounding.AwayFromZero), "open")
            .ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Session " + sessionNo + " opened.", id);
    }

    public async Task<ErpSimpleWriteResult> CloseSessionAsync(
        long sessionId,
        decimal closingCash,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (closingCash < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "closing_cash cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (sessionId <= 0)
        {
            sessionId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }

        var status = sessionId > 0
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `epc_pos_sessions` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                sessionId).ConfigureAwait(false)
            : null;
        if (!string.Equals(status, "open", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Open session not found");
        }

        var opening = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`opening_float`,0) FROM `epc_pos_sessions` WHERE `id`=?"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var cashTotal = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`cash_amount`),0) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var salesCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var salesTotal = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`total_amount`),0) FROM `epc_pos_sales` WHERE `session_id`=? AND `status`='completed'"),
            cancellationToken,
            sessionId).ConfigureAwait(false);
        var expected = Math.Round(opening + cashTotal, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pos_sessions` SET `closed_at`=?, `closing_cash`=?, `expected_cash`=?, `sales_count`=?, `sales_total`=?, `status`='closed', `notes`=? WHERE `id`=?"),
            cancellationToken,
            now,
            Math.Round(closingCash, 2, MidpointRounding.AwayFromZero),
            expected,
            salesCount,
            Math.Round(salesTotal, 2, MidpointRounding.AwayFromZero),
            Clip(notes, 512),
            sessionId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Session closed.", sessionId);
    }

    public async Task<ErpSimpleWriteResult> SaveSettingsAsync(
        bool posEnabled,
        string? registerName,
        int defaultWarehouseId,
        int defaultCashAccountId,
        int defaultCardAccountId,
        string? receiptHeader,
        string? receiptFooter,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "POS settings row is missing. Schema ensure stays PHP.");
        }

        var footer = Clip(receiptFooter, 512);
        if (footer.Length == 0)
        {
            footer = "Thank you for your purchase";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                UPDATE `epc_pos_settings` SET
                  `pos_enabled`=?, `register_name`=?, `default_warehouse_id`=?,
                  `default_cash_account_id`=?, `default_card_account_id`=?,
                  `receipt_header`=?, `receipt_footer`=?, `time_updated`=?
                WHERE `id`=?
                """),
            cancellationToken,
            posEnabled ? 1 : 0,
            Clip(registerName, 64).Length == 0 ? "Register 1" : Clip(registerName, 64),
            Math.Max(0, defaultWarehouseId),
            Math.Max(0, defaultCashAccountId),
            Math.Max(0, defaultCardAccountId),
            Clip(receiptHeader, 512),
            footer,
            now,
            id).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("POS settings saved.", id);
    }

    public async Task<ErpSimpleWriteResult> CompleteSaleAsync(
        CpPosCompleteSaleWriteRequest request,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var lines = ParseLines(request.Lines);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cart is empty");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sessionId = request.SessionId;
        if (sessionId > 0)
        {
            var st = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `status` FROM `epc_pos_sessions` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                sessionId).ConfigureAwait(false);
            if (!string.Equals(st, "open", StringComparison.OrdinalIgnoreCase))
            {
                sessionId = 0;
            }
        }

        if (sessionId <= 0)
        {
            sessionId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_pos_sessions` WHERE `status`='open' ORDER BY `opened_at` DESC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }

        if (sessionId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Open a register session before completing a sale");
        }

        var taxRate = request.TaxRate < 0 ? 0 : request.TaxRate;
        decimal subtotal = 0;
        decimal discountTotal = 0;
        foreach (var line in lines)
        {
            subtotal += line.LineExVat;
            discountTotal += line.DiscountAmt;
        }

        subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        discountTotal = Math.Round(discountTotal, 2, MidpointRounding.AwayFromZero);
        var vat = Math.Round(subtotal * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(subtotal + vat, 2, MidpointRounding.AwayFromZero);
        var method = (request.PaymentMethod ?? "cash").Trim().ToLowerInvariant();
        if (method is not ("cash" or "card" or "split"))
        {
            method = "cash";
        }

        decimal cash = Math.Round(request.CashAmount, 2, MidpointRounding.AwayFromZero);
        decimal card = Math.Round(request.CardAmount, 2, MidpointRounding.AwayFromZero);
        if (method == "cash")
        {
            cash = total;
            card = 0;
        }
        else if (method == "card")
        {
            card = total;
            cash = 0;
        }
        else
        {
            if (cash + card <= 0)
            {
                cash = total;
            }

            if (Math.Abs(cash + card - total) > 0.02m)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Split payment must equal total");
            }
        }

        var saleNo = await NextSaleNoAsync(connection, cancellationToken).ConfigureAwait(false);
        var label = Clip(request.CustomerLabel, 255);
        if (label.Length == 0)
        {
            label = request.CustomerUserId > 0 ? "Customer" : "Walk-in guest";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                """
                INSERT INTO `epc_pos_sales`
                (`session_id`,`sale_no`,`customer_user_id`,`contact_id`,`customer_label`,`sales_order_id`,`sales_invoice_id`,
                 `receipt_voucher_no`,`subtotal_ex`,`discount_total`,`vat_amount`,`total_amount`,`payment_method`,
                 `cash_amount`,`card_amount`,`tax_kit_code`,`tax_rate`,`admin_id`,`time_created`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            sessionId, saleNo, request.CustomerUserId, request.ContactId, label, 0L, 0L, "",
            subtotal, discountTotal, vat, total, method, cash, card,
            Clip(request.TaxKitCode, 32), taxRate, adminUserId, now).ConfigureAwait(false);
        var saleId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var lineNo = 1;
        foreach (var line in lines)
        {
            var lineVat = Math.Round(line.LineExVat * (taxRate / 100m), 2, MidpointRounding.AwayFromZero);
            var lineTotal = Math.Round(line.LineExVat + lineVat, 2, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_pos_sale_lines`
                    (`sale_id`,`line_no`,`product_source`,`product_ref`,`sku`,`barcode`,`name`,`qty`,`unit_price_ex`,
                     `line_discount_pct`,`line_discount_amt`,`line_ex_vat`,`tax_rate`,`vat_amount`,`line_total`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                saleId, lineNo, line.Source, line.Ref, line.Sku, line.Barcode, line.Name, line.Qty, line.UnitPriceEx,
                line.DiscountPct, line.DiscountAmt, line.LineExVat, taxRate, lineVat, lineTotal).ConfigureAwait(false);
            lineNo++;
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Sale " + saleNo + " completed.", saleId);
    }

    public static IReadOnlyList<CpPosParsedLine> ParseLines(IReadOnlyList<CpPosSaleLineInput>? raw)
    {
        var parsed = new List<CpPosParsedLine>();
        if (raw is null)
        {
            return parsed;
        }

        foreach (var ln in raw)
        {
            var name = Clip(ln.Name, 255);
            if (name.Length == 0)
            {
                continue;
            }

            var qty = ln.Qty < 0.001m ? 0.001m : ln.Qty;
            var unitRaw = ln.UnitPriceEx > 0 ? ln.UnitPriceEx : ln.Price;
            var unit = Math.Round(Math.Max(0, unitRaw), 4, MidpointRounding.AwayFromZero);
            var discPct = Math.Clamp(ln.LineDiscountPct, 0, 100);
            var discAmt = Math.Round(Math.Max(0, ln.LineDiscountAmt), 2, MidpointRounding.AwayFromZero);
            var gross = Math.Round(qty * unit, 2, MidpointRounding.AwayFromZero);
            if (discPct > 0)
            {
                discAmt = Math.Round(gross * discPct / 100m, 2, MidpointRounding.AwayFromZero);
            }

            var net = Math.Round(Math.Max(0, gross - discAmt), 2, MidpointRounding.AwayFromZero);
            parsed.Add(new CpPosParsedLine(
                Clip(ln.Source, 16).Length == 0 ? "manual" : Clip(ln.Source, 16),
                Clip(ln.Ref, 64),
                Clip(ln.Sku, 64),
                Clip(ln.Barcode, 64).Length == 0 ? Clip(ln.Sku, 64) : Clip(ln.Barcode, 64),
                name,
                qty,
                unit,
                discPct,
                discAmt,
                net));
        }

        return parsed;
    }

    public static IReadOnlyList<CpPosSaleLineInput> ParseLinesJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var raw = new List<CpPosSaleLineInput>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                raw.Add(new CpPosSaleLineInput(
                    ReadString(el, "name"),
                    ReadDecimal(el, 1, "qty"),
                    ReadDecimal(el, 0, "unit_price_ex", "unitPriceEx", "UnitPriceEx"),
                    ReadDecimal(el, 0, "line_discount_pct", "lineDiscountPct", "LineDiscountPct"),
                    ReadDecimal(el, 0, "line_discount_amt", "lineDiscountAmt", "LineDiscountAmt"),
                    ReadString(el, "sku"),
                    ReadString(el, "barcode"),
                    ReadString(el, "source"),
                    ReadString(el, "ref"),
                    ReadDecimal(el, 0, "price")));
            }

            return raw;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static decimal ReadDecimal(JsonElement el, decimal fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var prop))
            {
                continue;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value))
            {
                return value;
            }

            if (prop.ValueKind == JsonValueKind.String
                && decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }

    private static async Task<string> NextSaleNoAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var seq = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `last_seq` FROM `epc_pos_sequences` WHERE `year`=? LIMIT 1"),
                cancellationToken,
                year).ConfigureAwait(false);
            if (seq <= 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        "INSERT INTO `epc_pos_sequences` (`year`,`last_seq`) VALUES (?,1) ON DUPLICATE KEY UPDATE `last_seq`=`last_seq`+1"),
                    cancellationToken,
                    year).ConfigureAwait(false);
                seq = 1;
            }
            else
            {
                seq++;
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `epc_pos_sequences` SET `last_seq`=? WHERE `year`=?"),
                    cancellationToken,
                    seq, year).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return "POS-" + year.ToString(CultureInfo.InvariantCulture) + "-"
                   + seq.ToString("00000", CultureInfo.InvariantCulture);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CpPosReceipt> LoadReceiptAsync(long saleId, CancellationToken cancellationToken = default)
    {
        if (saleId <= 0)
        {
            return CpPosReceipt.NotFound;
        }

        if (!_connections.IsConfigured)
        {
            return CpPosReceipt.NotFound;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var saleCmd = connection.CreateCommand();
        saleCmd.CommandText = ErpDb.Positional(
            """
            SELECT `id`, `sale_no`, `customer_label`, `subtotal_ex`, `discount_total`, `vat_amount`,
                   `total_amount`, `payment_method`, `tax_rate`, `sales_invoice_id`, `time_created`
            FROM `epc_pos_sales` WHERE `id` = ? LIMIT 1
            """);
        ErpDb.AddParameters(saleCmd, saleId);
        long invoiceId = 0;
        string saleNo = "";
        string customerLabel = "";
        decimal subtotalEx = 0;
        decimal discountTotal = 0;
        decimal vatAmount = 0;
        decimal totalAmount = 0;
        string paymentMethod = "";
        decimal taxRate = 0;
        long timeCreated = 0;
        await using (var reader = await saleCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return CpPosReceipt.NotFound;
            }

            saleNo = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
            customerLabel = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
            subtotalEx = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            discountTotal = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
            vatAmount = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
            totalAmount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture);
            paymentMethod = reader.IsDBNull(7) ? "" : reader.GetValue(7)?.ToString() ?? "";
            taxRate = reader.IsDBNull(8) ? 0 : Convert.ToDecimal(reader.GetValue(8), CultureInfo.InvariantCulture);
            invoiceId = reader.IsDBNull(9) ? 0 : Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture);
            timeCreated = reader.IsDBNull(10) ? 0 : Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture);
        }

        var lines = new List<CpPosReceiptLine>();
        await using var lineCmd = connection.CreateCommand();
        lineCmd.CommandText = ErpDb.Positional(
            "SELECT `name`, `qty`, `line_discount_amt`, `line_total` FROM `epc_pos_sale_lines` WHERE `sale_id` = ? ORDER BY `line_no`");
        ErpDb.AddParameters(lineCmd, saleId);
        await using (var lineReader = await lineCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await lineReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = lineReader.IsDBNull(0) ? "" : lineReader.GetValue(0)?.ToString() ?? "";
                var qty = lineReader.IsDBNull(1) ? 0 : Convert.ToDecimal(lineReader.GetValue(1), CultureInfo.InvariantCulture);
                var disc = lineReader.IsDBNull(2) ? 0 : Convert.ToDecimal(lineReader.GetValue(2), CultureInfo.InvariantCulture);
                var lineTotal = lineReader.IsDBNull(3) ? 0 : Convert.ToDecimal(lineReader.GetValue(3), CultureInfo.InvariantCulture);
                lines.Add(new CpPosReceiptLine(FormatQty(qty), name, disc, lineTotal));
            }
        }

        var header = "";
        var footer = "Thank you for your purchase";
        try
        {
            header = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`receipt_header`,'') FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false) ?? "";
            var storedFooter = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`receipt_footer`,'') FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(storedFooter))
            {
                footer = storedFooter.Trim();
            }
        }
        catch (System.Data.Common.DbException)
        {
            // Settings stay optional; PHP defaults apply.
        }

        var invoiceNumber = "";
        if (invoiceId > 0)
        {
            try
            {
                invoiceNumber = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `invoice_number` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    invoiceId).ConfigureAwait(false) ?? "";
            }
            catch (System.Data.Common.DbException)
            {
                invoiceNumber = "";
            }
        }

        var soldAt = timeCreated > 0
            ? DateTimeOffset.FromUnixTimeSeconds(timeCreated).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "";
        return new CpPosReceipt(
            true,
            "",
            saleNo,
            soldAt,
            customerLabel,
            header.Trim(),
            footer,
            "VAT",
            discountTotal,
            subtotalEx,
            taxRate,
            vatAmount,
            totalAmount,
            TitleCase(paymentMethod),
            invoiceNumber.Trim(),
            lines);
    }

    /// <summary>PHP <c>rtrim(rtrim(number_format($qty, 3, '.', ''), '0'), '.')</c>.</summary>
    public static string FormatQty(decimal qty)
    {
        var text = qty.ToString("0.000", CultureInfo.InvariantCulture);
        text = text.TrimEnd('0').TrimEnd('.');
        return text.Length == 0 ? "0" : text;
    }

    private static string TitleCase(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "";
        }

        return char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant();
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record CpPosParsedLine(
    string Source,
    string Ref,
    string Sku,
    string Barcode,
    string Name,
    decimal Qty,
    decimal UnitPriceEx,
    decimal DiscountPct,
    decimal DiscountAmt,
    decimal LineExVat);

public sealed record CpPosReceipt(
    bool Ok,
    string Error,
    string SaleNo,
    string SoldAt,
    string CustomerLabel,
    string Header,
    string Footer,
    string TaxLabel,
    decimal DiscountTotal,
    decimal SubtotalEx,
    decimal TaxRate,
    decimal VatAmount,
    decimal TotalAmount,
    string PaymentMethod,
    string InvoiceNumber,
    IReadOnlyList<CpPosReceiptLine> Lines)
{
    public static CpPosReceipt NotFound { get; } = new(
        false,
        "Sale not found",
        "",
        "",
        "",
        "",
        "Thank you for your purchase",
        "VAT",
        0,
        0,
        0,
        0,
        0,
        "",
        "",
        []);

    public decimal SubtotalAfterDiscount => SubtotalEx - DiscountTotal;
}

public sealed record CpPosReceiptLine(
    string QtyText,
    string Name,
    decimal LineDiscountAmt,
    decimal LineTotal);
