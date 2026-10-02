using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_voucher_save</c> / purchase / sale aliases / <c>epc_jewel_voucher_save</c> twin.
/// Writes the provisioned <c>epc_jewel_voucher</c> schema (digest columns). Schema-ensure stays PHP.
/// </summary>
public interface IErpJwVoucherWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwVoucherSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwVoucherLineSaveRequest(
    string? StockCode = null,
    string? TagNo = null,
    string? Division = null,
    string? Description = null,
    int Pcs = 0,
    decimal Qty = 0,
    decimal GrossWeight = 0,
    decimal Purity = 0,
    decimal PureWeight = 0,
    decimal MakingRate = 0,
    decimal MakingAmount = 0,
    decimal MetalRate = 0,
    decimal MetalAmount = 0,
    decimal StoneAmount = 0,
    decimal WastagePercent = 0,
    decimal WastageQuantity = 0,
    decimal DiscountAmount = 0,
    decimal TotalAmount = 0,
    decimal TotalWithVat = 0,
    decimal NetAmount = 0);

public sealed record ErpJwVoucherReceiptSaveRequest(
    string? ReceiptMode = null,
    string? Currency = null,
    decimal CurrencyRate = 0,
    decimal AmountFc = 0,
    decimal AmountLc = 0);

public sealed record ErpJwVoucherSaveRequest(
    int CompanyId = 0,
    string? Action = null,
    string? VocType = null,
    string? Branch = null,
    string? VocDate = null,
    int VocNo = 0,
    string? PartyCode = null,
    string? PartyName = null,
    string? CustomerName = null,
    string? Currency = null,
    decimal CurrencyRate = 0,
    string? Salesman = null,
    string? RefInvoiceNo = null,
    int CreditDays = 0,
    string? Narration = null,
    decimal NetAmount = 0,
    decimal VatAmount = 0,
    decimal RoundOff = 0,
    decimal GrossTotal = 0,
    decimal RefundDue = 0,
    decimal AdjustSaleReturn = 0,
    decimal OldGoldExchange = 0,
    decimal GoldSchemeRedeem = 0,
    IReadOnlyList<ErpJwVoucherLineSaveRequest>? Lines = null,
    IReadOnlyList<ErpJwVoucherReceiptSaveRequest>? Receipts = null);

public sealed class ErpJwVoucherWriteService : IErpJwVoucherWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwVoucherWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwVoucherSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var vocType = NormalizeVocType(request.VocType, request.Action);
        if (vocType.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Voucher type required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_voucher", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery voucher tables are not provisioned");
        }

        var lines = request.Lines ?? [];
        var receipts = request.Receipts ?? [];
        if (lines.Count > 0 && !await TableExistsAsync(connection, "epc_jewel_voucher_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery voucher line tables are not provisioned");
        }

        var taggedLines = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.TagNo))
            .ToArray();
        if (taggedLines.Length > 0 && !await TableExistsAsync(connection, "epc_jw_tags", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery tag tables are not provisioned");
        }

        if (receipts.Count > 0 && !await TableExistsAsync(connection, "epc_jewel_voucher_receipts", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery voucher receipt tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var branch = (request.Branch ?? string.Empty).Trim();
        if (branch.Length == 0)
        {
            branch = "HO";
        }

        branch = Clip(branch, 10);
        var vocDate = NormalizeDate(request.VocDate);
        var vocNo = request.VocNo < 0 ? 0 : request.VocNo;
        var partyCode = Clip((request.PartyCode ?? string.Empty).Trim(), 20);
        var partyName = (request.PartyName ?? string.Empty).Trim();
        if (partyName.Length == 0)
        {
            partyName = (request.CustomerName ?? string.Empty).Trim();
        }

        partyName = Clip(partyName, 120);
        var customerName = Clip((request.CustomerName ?? string.Empty).Trim(), 120);
        if (customerName.Length == 0)
        {
            customerName = partyName;
        }

        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        currency = Clip(currency, 5);
        var currRate = request.CurrencyRate <= 0 ? 1m : RoundNonNeg(request.CurrencyRate, 6);
        var salesman = Clip((request.Salesman ?? string.Empty).Trim(), 20);
        var suppInv = Clip((request.RefInvoiceNo ?? string.Empty).Trim(), 30);
        var crDays = request.CreditDays < 0 ? 0 : request.CreditDays;
        var narration = (request.Narration ?? string.Empty).Trim();
        var net = RoundNonNeg(request.NetAmount, 2);
        var vat = RoundNonNeg(request.VatAmount, 2);
        var roundOff = RoundNonNeg(request.RoundOff, 2);
        var gross = RoundNonNeg(request.GrossTotal, 2);
        var refundDue = RoundNonNeg(request.RefundDue, 2);
        var adjustSaleReturn = RoundNonNeg(request.AdjustSaleReturn, 2);
        var oldGoldExchange = RoundNonNeg(request.OldGoldExchange, 2);
        var goldSchemeRedeem = RoundNonNeg(request.GoldSchemeRedeem, 2);
        if (net == 0)
        {
            var pricedLineTotal = lines
                .Where(line => !string.IsNullOrWhiteSpace(line.StockCode) || !string.IsNullOrWhiteSpace(line.Description))
                .Sum(CalculateLineTotal);
            if (pricedLineTotal > 0)
            {
                net = pricedLineTotal;
            }
        }

        if (gross == 0)
        {
            gross = RoundNonNeg(net + vat + roundOff, 2);
        }

        var totalWithVat = RoundNonNeg(net + vat, 2);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_jewel_voucher` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,`party_name`,`party_curr`,`party_curr_rate`,`customer_name`,`salesman`,`supp_inv_no`,`cr_days`,`narration`,`remarks`,`net_amount`,`vat_amount`,`rnd_off_amount`,`rnd_net_amount`,`gross_total`,`total_with_vat`,`sub_total`,`receipt_total`,`refund_due`,`adjust_sale_return`,`old_gold_exchange`,`gold_scheme_redeem`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                companyId,
                branch,
                vocType,
                vocDate,
                vocNo,
                partyCode,
                partyName,
                currency,
                currRate,
                customerName,
                salesman,
                suppInv,
                crDays,
                narration,
                narration,
                net,
                vat,
                roundOff,
                net,
                gross,
                totalWithVat,
                net,
                receipts.Sum(receipt =>
                {
                    var receiptRate = receipt.CurrencyRate <= 0 ? 1m : RoundNonNeg(receipt.CurrencyRate, 6);
                    var amountFc = RoundNonNeg(receipt.AmountFc, 2);
                    return NormalizeReceiptAmountLc(receipt, receiptRate, amountFc);
                }),
                refundDue,
                adjustSaleReturn,
                oldGoldExchange,
                goldSchemeRedeem,
                "draft").ConfigureAwait(false);

            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (id <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Failed");
            }

            var lineNo = 1;
            foreach (var line in lines)
            {
                var stockCode = Clip((line.StockCode ?? string.Empty).Trim(), 20);
                var description = Clip((line.Description ?? string.Empty).Trim(), 120);
                if (!ShouldPersistLine(line))
                {
                    continue;
                }

                var division = Clip((line.Division ?? string.Empty).Trim().ToUpperInvariant(), 2);
                if (division.Length == 0)
                {
                    division = "G";
                }

                var grossWeight = RoundNonNeg(line.GrossWeight, 4);
                var purity = RoundNonNeg(line.Purity, 6);
                var pureWeight = line.PureWeight > 0
                    ? RoundNonNeg(line.PureWeight, 4)
                    : RoundNonNeg(grossWeight * purity, 4);
                var makingAmount = line.MakingAmount > 0
                    ? RoundNonNeg(line.MakingAmount, 2)
                    : RoundNonNeg(line.MakingRate * grossWeight, 2);
                var metalAmount = line.MetalAmount > 0
                    ? RoundNonNeg(line.MetalAmount, 2)
                    : RoundNonNeg(pureWeight * line.MetalRate, 2);
                var totalAmount = line.TotalAmount > 0
                    ? RoundNonNeg(line.TotalAmount, 2)
                    : RoundNonNeg(metalAmount + makingAmount + line.StoneAmount - line.DiscountAmount, 2);
                var lineTotalWithVat = line.TotalWithVat > 0
                    ? RoundNonNeg(line.TotalWithVat, 2)
                    : totalAmount;
                var netAmount = line.NetAmount > 0
                    ? RoundNonNeg(line.NetAmount, 2)
                    : totalAmount;

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_jewel_voucher_lines` (`voucher_id`,`line_no`,`stock_code`,`division`,`description`,`pcs`,`qty`,`gr_wt`,`purity`,`pure_wt`,`mkg_rate`,`mkg_amount`,`metal_rate`,`metal_amount`,`stone_amount`,`wastage_pct`,`wastage_qty`,`disc_amount`,`total_amount`,`total_with_vat`,`net_amount`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                    cancellationToken,
                        id,
                    lineNo++,
                    stockCode,
                    division,
                    description,
                    Math.Max(0, line.Pcs),
                    RoundNonNeg(line.Qty, 4),
                    grossWeight,
                    purity,
                    pureWeight,
                    RoundNonNeg(line.MakingRate, 4),
                    makingAmount,
                    RoundNonNeg(line.MetalRate, 5),
                    metalAmount,
                    RoundNonNeg(line.StoneAmount, 2),
                    RoundNonNeg(line.WastagePercent, 4),
                    RoundNonNeg(line.WastageQuantity, 4),
                    RoundNonNeg(line.DiscountAmount, 2),
                    totalAmount,
                    lineTotalWithVat,
                    netAmount).ConfigureAwait(false);
            }

            if (taggedLines.Length > 0)
            {
                var isReturn = vocType.Equals("SRN", StringComparison.OrdinalIgnoreCase);
                var isSale = vocType is "RSI" or "MSI";
                if (!isReturn && !isSale)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Tag references are only valid on jewellery sales and returns.");
                }

                foreach (var line in taggedLines)
                {
                    var tag = Clip((line.TagNo ?? string.Empty).Trim(), 100);
                    await using var tagRead = connection.CreateCommand();
                    tagRead.Transaction = transaction;
                    tagRead.CommandText = ErpDb.Positional(
                        "SELECT `id`,`status`,`sold_invoice_id` FROM `epc_jw_tags` WHERE `company_id`=? AND (`tag_no`=? OR `barcode`=?) LIMIT 1 FOR UPDATE");
                    ErpDb.AddParameters(tagRead, companyId, tag, tag);
                    await using var tagReader = await tagRead.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (!await tagReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("not_found", $"Jewellery tag or barcode '{tag}' was not found.");
                    }

                    var tagId = Convert.ToInt64(tagReader["id"], CultureInfo.InvariantCulture);
                    var tagStatus = Convert.ToString(tagReader["status"], CultureInfo.InvariantCulture) ?? string.Empty;
                    var soldInvoiceId = Convert.ToInt64(tagReader["sold_invoice_id"], CultureInfo.InvariantCulture);
                    await tagReader.DisposeAsync().ConfigureAwait(false);
                    if (isSale && tagStatus is not ("in_stock" or "displayed" or "reserved"))
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("invalid", $"Jewellery tag '{tag}' is not available for sale.");
                    }

                    if (isReturn)
                    {
                        if (tagStatus != "sold")
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            return ErpSimpleWriteResult.Fail("invalid", $"Jewellery tag '{tag}' is not sold and cannot be returned.");
                        }

                        if (!int.TryParse(suppInv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sourceInvoiceId)
                            || sourceInvoiceId <= 0
                            || soldInvoiceId != sourceInvoiceId)
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            return ErpSimpleWriteResult.Fail("invalid", $"Sales return for tag '{tag}' must reference its source invoice.");
                        }
                    }

                    var nextStatus = isReturn ? "returned" : "sold";
                    var soldId = isReturn ? 0 : id;
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "UPDATE `epc_jw_tags` SET `status`=?,`sold_invoice_id`=?,`sold_date`=CASE WHEN ?='sold' THEN CURDATE() ELSE NULL END,`time_updated`=? WHERE `id`=? AND `company_id`=?"),
                        cancellationToken,
                        nextStatus,
                        soldId,
                        nextStatus,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        tagId,
                        companyId).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`reference`,`time_created`) VALUES (?,?,?,?)"),
                        cancellationToken,
                        tagId,
                        nextStatus,
                        $"{vocType} voucher #{id}",
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
                }
            }

            foreach (var receipt in receipts)
            {
                var receiptMode = Clip((receipt.ReceiptMode ?? string.Empty).Trim().ToUpperInvariant(), 20);
                if (receiptMode.Length == 0)
                {
                    receiptMode = "CASH";
                }

                var receiptCurrency = Clip((receipt.Currency ?? currency).Trim().ToUpperInvariant(), 5);
                var receiptRate = receipt.CurrencyRate <= 0 ? 1m : RoundNonNeg(receipt.CurrencyRate, 6);
                var amountFc = RoundNonNeg(receipt.AmountFc, 2);
                var amountLc = NormalizeReceiptAmountLc(receipt, receiptRate, amountFc);
                if (amountFc == 0 && amountLc == 0)
                {
                    continue;
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_jewel_voucher_receipts` (`voucher_id`,`receipt_mode`,`currency`,`curr_rate`,`amount_fc`,`amount_lc`) VALUES (?,?,?,?,?,?)"),
                    cancellationToken,
                    id,
                    receiptMode,
                    receiptCurrency,
                    receiptRate,
                    amountFc,
                    amountLc).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(vocType + " voucher saved", id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public static decimal NormalizeReceiptAmountLc(
        ErpJwVoucherReceiptSaveRequest receipt,
        decimal receiptRate,
        decimal amountFc)
        => receipt.AmountLc > 0
            ? RoundNonNeg(receipt.AmountLc, 2)
            : RoundNonNeg(amountFc * receiptRate, 2);

    public static bool ShouldPersistLine(ErpJwVoucherLineSaveRequest line)
        => !string.IsNullOrWhiteSpace(line.StockCode)
            || !string.IsNullOrWhiteSpace(line.Description)
            || !string.IsNullOrWhiteSpace(line.TagNo);

    private static decimal CalculateLineTotal(ErpJwVoucherLineSaveRequest line)
    {
        var grossWeight = RoundNonNeg(line.GrossWeight, 4);
        var purity = RoundNonNeg(line.Purity, 6);
        var pureWeight = line.PureWeight > 0
            ? RoundNonNeg(line.PureWeight, 4)
            : RoundNonNeg(grossWeight * purity, 4);
        var makingAmount = line.MakingAmount > 0
            ? RoundNonNeg(line.MakingAmount, 2)
            : RoundNonNeg(line.MakingRate * grossWeight, 2);
        var metalAmount = line.MetalAmount > 0
            ? RoundNonNeg(line.MetalAmount, 2)
            : RoundNonNeg(pureWeight * line.MetalRate, 2);
        return line.NetAmount > 0
            ? RoundNonNeg(line.NetAmount, 2)
            : line.TotalAmount > 0
                ? RoundNonNeg(line.TotalAmount, 2)
                : RoundNonNeg(metalAmount + makingAmount + line.StoneAmount - line.DiscountAmount, 2);
    }

    public static string NormalizeVocType(string? vocType, string? action)
    {
        var raw = (vocType ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length > 0)
        {
            return Clip(raw, 10);
        }

        var act = (action ?? string.Empty).Trim().ToLowerInvariant();
        return act switch
        {
            "jw_metal_purchase_save" or "jw_metal_purchase" => "MMP",
            "jw_diamond_purchase_save" or "jw_diamond_purchase" => "DMP",
            "jw_retail_sale_save" or "jw_retail_sales_save" or "jw_retail_sales" or "jewellery" or "retail_commerce" => "RSI",
            "jw_metal_sale_save" or "jw_metal_sales" => "MSI",
            "jw_sales_return_save" or "jw_sales_return" => "SRN",
            "jw_pos_advance_save" => "PAD",
            "jw_journal_voucher_save" => "JVG",
            "jw_repair_sale_save" or "jw_repair_sale" => "RSL",
            _ => string.Empty
        };
    }

    private static string NormalizeDate(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
