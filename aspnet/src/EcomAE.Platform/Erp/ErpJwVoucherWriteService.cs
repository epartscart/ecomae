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
    IReadOnlyList<ErpJwVoucherLineSaveRequest>? Lines = null);

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
        if (lines.Count > 0 && !await TableExistsAsync(connection, "epc_jewel_voucher_lines", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery voucher line tables are not provisioned");
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
        if (gross == 0)
        {
            gross = RoundNonNeg(net + vat + roundOff, 2);
        }

        var totalWithVat = RoundNonNeg(net + vat, 2);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_voucher` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`party_code`,`party_name`,`party_curr`,`party_curr_rate`,`customer_name`,`salesman`,`supp_inv_no`,`cr_days`,`narration`,`remarks`,`net_amount`,`vat_amount`,`rnd_off_amount`,`rnd_net_amount`,`gross_total`,`total_with_vat`,`sub_total`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
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
            "draft").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        var lineNo = 1;
        foreach (var line in lines)
        {
            var stockCode = Clip((line.StockCode ?? string.Empty).Trim(), 20);
            var description = Clip((line.Description ?? string.Empty).Trim(), 120);
            if (stockCode.Length == 0 && description.Length == 0)
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
                null,
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

        return ErpSimpleWriteResult.Ok(vocType + " voucher saved", id);
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
