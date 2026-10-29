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
    decimal NetAmount = 0,
    decimal StoneWeight = 0);

public sealed record ErpJwVoucherReceiptSaveRequest(
    string? ReceiptMode = null,
    string? Currency = null,
    decimal CurrencyRate = 0,
    decimal AmountFc = 0,
    decimal AmountLc = 0);

public sealed record ErpJwVoucherAddinSaveRequest(
    string? AddinCode = null,
    string? Description = null,
    string? DebitAccount = null,
    string? CreditAccount = null,
    decimal Percent = 0,
    decimal Amount = 0,
    string? Narration = null);

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
    IReadOnlyList<ErpJwVoucherReceiptSaveRequest>? Receipts = null,
    IReadOnlyList<ErpJwVoucherAddinSaveRequest>? Addins = null,
    string? ItemCurrency = null,
    decimal ItemCurrencyRate = 0,
    decimal MetalRate = 0,
    bool IsFixed = false,
    string? PlaceOfSupply = null,
    string? ShipmentCompany = null,
    string? PortCompany = null,
    decimal OtherAmount = 0,
    int ActorUserId = 0);

public sealed class ErpJwVoucherWriteService : IErpJwVoucherWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpJwVoucherWriteService(
        IErpWriteConnectionFactory connections,
        IErpAuditLogWriter? audit = null)
    {
        _connections = connections;
        _audit = audit ?? new ErpAuditLogWriter();
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

        var metalPurchase = ErpJwVocTypes.Matches(vocType, ErpJwVocTypes.MetalPurchaseAliases);
        var preciousPurchase = metalPurchase || ErpJwVocTypes.Matches(vocType, ErpJwVocTypes.DiamondPurchaseAliases);
        if (metalPurchase && string.IsNullOrWhiteSpace(request.PartyCode))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Party is required. Pick an approved supplier.");
        }

        var incomingLines = request.Lines ?? [];
        if (metalPurchase
            && !incomingLines.Any(line => !string.IsNullOrWhiteSpace(line.StockCode)))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Stock code is required. Pick a metal stock item.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_voucher", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery voucher tables are not provisioned");
        }

        var lines = incomingLines;
        var receipts = request.Receipts ?? [];
        var addins = (request.Addins ?? [])
            .Where(row => !string.IsNullOrWhiteSpace(row.AddinCode) || row.Amount != 0)
            .ToArray();
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

        if (metalPurchase)
        {
            var blocked = await RequireApprovedSupplierByCodeAsync(
                connection,
                partyCodeForGate: (request.PartyCode ?? string.Empty).Trim(),
                cancellationToken).ConfigureAwait(false);
            if (blocked is not null)
            {
                return blocked;
            }
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        if (companyId == 0)
        {
            companyId = (int)await ErpLazySchema.ResolveActiveCompanyIdAsync(connection, 0, cancellationToken).ConfigureAwait(false);
        }
        var branch = (request.Branch ?? string.Empty).Trim();
        if (branch.Length == 0)
        {
            branch = "HQ";
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
        var otherAmount = request.OtherAmount > 0
            ? RoundNonNeg(request.OtherAmount, 2)
            : RoundNonNeg(addins.Sum(row => row.Amount), 2);
        var gross = RoundNonNeg(request.GrossTotal, 2);
        var itemCurr = Clip((request.ItemCurrency ?? string.Empty).Trim().ToUpperInvariant(), 5);
        if (itemCurr.Length == 0)
        {
            itemCurr = currency;
        }

        var metalRate = RoundNonNeg(request.MetalRate, 5);
        if (metalRate <= 0)
        {
            metalRate = RoundNonNeg(
                await ErpGoldRateBoard.ResolveSellAsync(
                    connection,
                    companyId,
                    "24K",
                    currency,
                    vocDate,
                    cancellationToken).ConfigureAwait(false),
                5);
        }
        if (metalPurchase && metalRate > 0)
        {
            // The header metal rate prices every line; client line amounts are recomputed so a stale rate cannot post.
            lines = lines
                .Select(line =>
                {
                    var pure = line.PureWeight > 0
                        ? RoundNonNeg(line.PureWeight, 4)
                        : ErpJwPurity.PureWeight(RoundNonNeg(line.GrossWeight, 4), RoundNonNeg(line.Purity, 6));
                    var metalValue = RoundNonNeg((pure + RoundNonNeg(line.WastageQuantity, 4)) * metalRate, 2);
                    var making = ErpJwPurity.MakingAmount(RoundNonNeg(line.GrossWeight, 4), line.MakingRate, line.MakingAmount);
                    var total = RoundNonNeg(metalValue + making + RoundNonNeg(line.StoneAmount, 2) - RoundNonNeg(line.DiscountAmount, 2), 2);
                    return line with { MetalRate = metalRate, MetalAmount = metalValue, MakingAmount = making, TotalAmount = total, NetAmount = total };
                })
                .ToArray();
            net = 0;
            gross = 0;
        }

        var placeOfSupply = Clip((request.PlaceOfSupply ?? string.Empty).Trim(), 80);
        var shipment = Clip((request.ShipmentCompany ?? string.Empty).Trim(), 80);
        var port = Clip((request.PortCompany ?? string.Empty).Trim(), 80);
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
            gross = RoundNonNeg(net + vat + roundOff + otherAmount, 2);
        }

        var totalWithVat = RoundNonNeg(net + vat, 2);
        if (preciousPurchase)
        {
            var amountAed = currency == "AED" ? totalWithVat : RoundNonNeg(totalWithVat * currRate, 2);
            try
            {
                await ErpSupplierWriteService.RequireKycForJewelPurchaseAsync(
                    connection,
                    partyCode,
                    amountAed,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ErpWriteException ex)
            {
                return ErpSimpleWriteResult.Fail("invalid", ex.Message);
            }
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            vocNo = await ErpJwVocNumbering.NextNoAsync(
                connection,
                transaction,
                "epc_jewel_voucher",
                companyId,
                vocType,
                branch,
                vocNo,
                cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_jewel_voucher` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,`party_name`,`party_curr`,`party_curr_rate`,`customer_name`,`salesman`,`supp_inv_no`,`cr_days`,`narration`,`remarks`,`net_amount`,`vat_amount`,`rnd_off_amount`,`rnd_net_amount`,`gross_total`,`total_with_vat`,`sub_total`,`receipt_total`,`refund_due`,`adjust_sale_return`,`old_gold_exchange`,`gold_scheme_redeem`,`item_curr`,`metal_rate`,`is_fixed`,`other_amount`,`place_of_supply`,`shipment_company`,`port_company`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
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
                itemCurr,
                metalRate,
                request.IsFixed ? 1 : 0,
                otherAmount,
                placeOfSupply,
                shipment,
                port,
                "posted").ConfigureAwait(false);

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
                var lineMetalRate = line.MetalRate > 0 ? line.MetalRate : metalRate;
                var pureWeight = line.PureWeight > 0
                    ? RoundNonNeg(line.PureWeight, 4)
                    : ErpJwPurity.PureWeight(grossWeight, purity);
                var makingAmount = ErpJwPurity.MakingAmount(grossWeight, line.MakingRate, line.MakingAmount);
                var metalAmount = ErpJwPurity.MetalAmount(grossWeight, purity, lineMetalRate, line.MetalAmount);
                var totalAmount = line.TotalAmount > 0
                    ? RoundNonNeg(line.TotalAmount, 2)
                    : ErpJwPurity.LineTotal(
                        grossWeight,
                        purity,
                        lineMetalRate,
                        line.MakingRate,
                        line.StoneAmount,
                        line.DiscountAmount,
                        metalAmount,
                        makingAmount);
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
                        "INSERT INTO `epc_jewel_voucher_lines` (`voucher_id`,`line_no`,`stock_code`,`division`,`description`,`pcs`,`qty`,`gr_wt`,`purity`,`pure_wt`,`mkg_rate`,`mkg_amount`,`metal_rate`,`metal_amount`,`stone_amount`,`wastage_pct`,`wastage_qty`,`disc_amount`,`total_amount`,`total_with_vat`,`net_amount`,`stone_wt`,`net_wt`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
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
                    RoundNonNeg(lineMetalRate, 5),
                    metalAmount,
                    RoundNonNeg(line.StoneAmount, 2),
                    RoundNonNeg(line.WastagePercent, 4),
                    RoundNonNeg(line.WastageQuantity, 4),
                    RoundNonNeg(line.DiscountAmount, 2),
                    totalAmount,
                    lineTotalWithVat,
                    netAmount,
                    RoundNonNeg(line.StoneWeight, 4),
                    RoundNonNeg(Math.Max(0m, grossWeight - RoundNonNeg(line.StoneWeight, 4)), 4)).ConfigureAwait(false);
            }

            if (taggedLines.Length > 0)
            {
                var isReturn = ErpJwVocTypes.IsReturn(vocType);
                var isSale = ErpJwVocTypes.IsSale(vocType);
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
                        await tagReader.DisposeAsync().ConfigureAwait(false);
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
                            "INSERT INTO `epc_jw_tag_history` (`tag_id`,`action`,`reference`,`actor_id`,`time_created`) VALUES (?,?,?,?,?)"),
                        cancellationToken,
                        tagId,
                        nextStatus,
                        $"{vocType} voucher #{id}",
                        Math.Max(0, request.ActorUserId),
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

            if (addins.Length > 0
                && await TableExistsAsync(connection, "epc_jewel_voucher_addins", cancellationToken).ConfigureAwait(false))
            {
                var addinNo = 1;
                foreach (var addin in addins)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `epc_jewel_voucher_addins` (`voucher_id`,`line_no`,`addin_code`,`description`,`debit_ac`,`credit_ac`,`pct`,`amount`,`narration`) VALUES (?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        id,
                        addinNo++,
                        Clip((addin.AddinCode ?? string.Empty).Trim(), 20),
                        Clip((addin.Description ?? string.Empty).Trim(), 120),
                        Clip((addin.DebitAccount ?? string.Empty).Trim(), 20),
                        Clip((addin.CreditAccount ?? string.Empty).Trim(), 20),
                        RoundNonNeg(addin.Percent, 4),
                        RoundNonNeg(addin.Amount, 2),
                        Clip((addin.Narration ?? string.Empty).Trim(), 200)).ConfigureAwait(false);
                }
            }

            await _audit.LogAsync(
                connection,
                transaction,
                request.ActorUserId,
                "jw_voucher_save",
                "epc_jewel_voucher",
                id,
                $"{vocType} voucher saved",
                new Dictionary<string, string?>
                {
                    ["company_id"] = companyId.ToString(CultureInfo.InvariantCulture),
                    ["voucher_type"] = vocType,
                    ["line_count"] = lines.Count.ToString(CultureInfo.InvariantCulture),
                    ["receipt_count"] = receipts.Count.ToString(CultureInfo.InvariantCulture),
                    ["voc_no"] = vocNo.ToString(CultureInfo.InvariantCulture),
                    ["net_amount"] = net.ToString(CultureInfo.InvariantCulture),
                    ["total_with_vat"] = totalWithVat.ToString(CultureInfo.InvariantCulture)
                },
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(vocType + " voucher " + vocNo.ToString(CultureInfo.InvariantCulture) + " saved", id);
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

    public static async Task<ErpSimpleWriteResult?> RequireApprovedSupplierByCodeAsync(
        DbConnection connection,
        string partyCodeForGate,
        CancellationToken cancellationToken)
    {
        var code = (partyCodeForGate ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Party is required. Pick an approved supplier.");
        }

        if (!await TableExistsAsync(connection, "epc_erp_suppliers", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var hasCode = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_suppliers' AND COLUMN_NAME = 'supplier_code'"),
            cancellationToken).ConfigureAwait(false) > 0;
        var hasVendor = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_suppliers' AND COLUMN_NAME = 'vendor_account'"),
            cancellationToken).ConfigureAwait(false) > 0;
        var where = hasCode && hasVendor
            ? "`supplier_code`=? OR `vendor_account`=? OR `name`=?"
            : hasCode
                ? "`supplier_code`=? OR `name`=?"
                : hasVendor
                    ? "`vendor_account`=? OR `name`=?"
                    : "`name`=?";
        var args = hasCode && hasVendor
            ? new object[] { code, code, code }
            : hasCode || hasVendor
                ? new object[] { code, code }
                : new object[] { code };
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE " + where + " LIMIT 1"),
            cancellationToken,
            args).ConfigureAwait(false);
        if (id <= 0)
        {
            return null;
        }

        try
        {
            await ErpSupplierWriteService.RequireApprovedForPurchaseAsync(connection, id, cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
    }

    private static decimal CalculateLineTotal(ErpJwVoucherLineSaveRequest line)
        => line.NetAmount > 0
            ? RoundNonNeg(line.NetAmount, 2)
            : line.TotalAmount > 0
                ? RoundNonNeg(line.TotalAmount, 2)
                : ErpJwPurity.LineTotal(
                    line.GrossWeight,
                    line.Purity,
                    line.MetalRate,
                    line.MakingRate,
                    line.StoneAmount,
                    line.DiscountAmount,
                    line.MetalAmount,
                    line.MakingAmount);

    public static string NormalizeVocType(string? vocType, string? action)
        => Clip(ErpJwVocTypes.Canonical(vocType, action), 10);

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
