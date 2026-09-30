using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_diamond_save</c> / <c>epc_jewel_diamond_save_ajax</c> twin.
/// Schema-ensure stays PHP — missing <c>epc_jewel_diamond_master</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwDiamondWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwDiamondSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwDiamondSaveRequest(
    int CompanyId = 0,
    string? ItemCode = null,
    string? Description = null,
    string? Design = null,
    string? Rfid = null,
    string? Category = null,
    string? SubCategory = null,
    string? Type = null,
    string? Brand = null,
    string? Color = null,
    string? Clarity = null,
    string? Fluorescence = null,
    string? Style = null,
    string? SetRef = null,
    string? Country = null,
    string? Vendor = null,
    string? VendorRef = null,
    string? Currency = null,
    decimal CurrencyRate = 1,
    string? CostCentre = null,
    decimal CostAmount = 0,
    decimal ItemGrWt = 0,
    string? Price1Code = null,
    decimal Price1Pct = 0,
    decimal Price1Fc = 0,
    decimal Price1Lc = 0,
    string? Price2Code = null,
    decimal Price2Pct = 0,
    decimal Price2Fc = 0,
    decimal Price2Lc = 0,
    decimal LandedCost = 0,
    decimal ForeignCost = 0,
    decimal CostDifference = 0,
    string? CertificateNo = null,
    string? CertificateDate = null,
    string? CertificateBy = null,
    string? CertificateNo1 = null,
    string? CertificateDate1 = null,
    int NoOfCertificates = 0,
    decimal SettingCharge = 0,
    decimal PolishingCharge = 0,
    decimal RhodiumCharge = 0,
    decimal LabourCharge = 0,
    decimal MiscCharge = 0,
    bool ExcludeGstMetal = false,
    decimal PureWt = 0,
    bool TrnOnMargin = false,
    bool UaeTrnItem = false,
    string? CustSku = null,
    string? AgeingDate = null,
    bool Promotional = false);

public sealed class ErpJwDiamondWriteService : IErpJwDiamondWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwDiamondWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwDiamondSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.ItemCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_diamond_master", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery diamond tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 20);
        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        currency = Clip(currency, 5);
        var price1Code = (request.Price1Code ?? string.Empty).Trim();
        if (price1Code.Length == 0)
        {
            price1Code = "TAG";
        }

        price1Code = Clip(price1Code, 5);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_diamond_master` (`company_id`,`item_code`,`description`,`design`,`rfid`,`category`,`sub_category`,`type`,`brand`,`color`,`clarity`,`fluorescence`,`style`,`set_ref`,`country`,`vendor`,`vendor_ref`,`currency`,`currency_rate`,`cost_centre`,`cost_amount`,`item_gr_wt`,`price1_code`,`price1_pct`,`price1_fc`,`price1_lc`,`price2_code`,`price2_pct`,`price2_fc`,`price2_lc`,`landed_cost`,`foreign_cost`,`cost_difference`,`certificate_no`,`certificate_date`,`certificate_by`,`certificate_no_1`,`certificate_date_1`,`no_of_certificates`,`setting_charge`,`polishing_charge`,`rhodium_charge`,`labour_charge`,`misc_charge`,`exclude_gst_metal`,`pure_wt`,`trn_on_margin`,`uae_trn_item`,`cust_sku`,`ageing_date`,`promotional`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `design` = VALUES(`design`), `rfid` = VALUES(`rfid`), `category` = VALUES(`category`), `sub_category` = VALUES(`sub_category`), `type` = VALUES(`type`), `brand` = VALUES(`brand`), `color` = VALUES(`color`), `clarity` = VALUES(`clarity`), `fluorescence` = VALUES(`fluorescence`), `style` = VALUES(`style`), `set_ref` = VALUES(`set_ref`), `country` = VALUES(`country`), `vendor` = VALUES(`vendor`), `vendor_ref` = VALUES(`vendor_ref`), `currency` = VALUES(`currency`), `currency_rate` = VALUES(`currency_rate`), `cost_centre` = VALUES(`cost_centre`), `cost_amount` = VALUES(`cost_amount`), `item_gr_wt` = VALUES(`item_gr_wt`), `price1_code` = VALUES(`price1_code`), `price1_pct` = VALUES(`price1_pct`), `price1_fc` = VALUES(`price1_fc`), `price1_lc` = VALUES(`price1_lc`), `price2_code` = VALUES(`price2_code`), `price2_pct` = VALUES(`price2_pct`), `price2_fc` = VALUES(`price2_fc`), `price2_lc` = VALUES(`price2_lc`), `landed_cost` = VALUES(`landed_cost`), `foreign_cost` = VALUES(`foreign_cost`), `cost_difference` = VALUES(`cost_difference`), `certificate_no` = VALUES(`certificate_no`), `certificate_date` = VALUES(`certificate_date`), `certificate_by` = VALUES(`certificate_by`), `certificate_no_1` = VALUES(`certificate_no_1`), `certificate_date_1` = VALUES(`certificate_date_1`), `no_of_certificates` = VALUES(`no_of_certificates`), `setting_charge` = VALUES(`setting_charge`), `polishing_charge` = VALUES(`polishing_charge`), `rhodium_charge` = VALUES(`rhodium_charge`), `labour_charge` = VALUES(`labour_charge`), `misc_charge` = VALUES(`misc_charge`), `exclude_gst_metal` = VALUES(`exclude_gst_metal`), `pure_wt` = VALUES(`pure_wt`), `trn_on_margin` = VALUES(`trn_on_margin`), `uae_trn_item` = VALUES(`uae_trn_item`), `cust_sku` = VALUES(`cust_sku`), `ageing_date` = VALUES(`ageing_date`), `promotional` = VALUES(`promotional`)"),
            cancellationToken,
            companyId,
            code,
            Clip((request.Description ?? string.Empty).Trim(), 120),
            Clip((request.Design ?? string.Empty).Trim(), 30),
            Clip((request.Rfid ?? string.Empty).Trim(), 30),
            Clip((request.Category ?? string.Empty).Trim(), 30),
            Clip((request.SubCategory ?? string.Empty).Trim(), 30),
            Clip((request.Type ?? string.Empty).Trim(), 30),
            Clip((request.Brand ?? string.Empty).Trim(), 60),
            Clip((request.Color ?? string.Empty).Trim(), 20),
            Clip((request.Clarity ?? string.Empty).Trim(), 20),
            Clip((request.Fluorescence ?? string.Empty).Trim(), 20),
            Clip((request.Style ?? string.Empty).Trim(), 20),
            Clip((request.SetRef ?? string.Empty).Trim(), 30),
            Clip((request.Country ?? string.Empty).Trim(), 30),
            Clip((request.Vendor ?? string.Empty).Trim(), 20),
            Clip((request.VendorRef ?? string.Empty).Trim(), 20),
            currency,
            RoundNonNeg(request.CurrencyRate, 5),
            Clip((request.CostCentre ?? string.Empty).Trim(), 20),
            RoundNonNeg(request.CostAmount, 2),
            RoundNonNeg(request.ItemGrWt, 4),
            price1Code,
            RoundNonNeg(request.Price1Pct, 2),
            RoundNonNeg(request.Price1Fc, 2),
            RoundNonNeg(request.Price1Lc, 2),
            Clip((request.Price2Code ?? string.Empty).Trim(), 5) is { Length: > 0 } price2Code ? price2Code : "GEN",
            RoundNonNeg(request.Price2Pct, 2),
            RoundNonNeg(request.Price2Fc, 2),
            RoundNonNeg(request.Price2Lc, 2),
            RoundNonNeg(request.LandedCost, 2),
            RoundNonNeg(request.ForeignCost, 2),
            RoundNonNeg(request.CostDifference, 2),
            Clip((request.CertificateNo ?? string.Empty).Trim(), 40),
            NormalizeDate(request.CertificateDate),
            Clip((request.CertificateBy ?? string.Empty).Trim(), 40),
            Clip((request.CertificateNo1 ?? string.Empty).Trim(), 40),
            NormalizeDate(request.CertificateDate1),
            Math.Max(request.NoOfCertificates, 0),
            RoundNonNeg(request.SettingCharge, 2),
            RoundNonNeg(request.PolishingCharge, 2),
            RoundNonNeg(request.RhodiumCharge, 2),
            RoundNonNeg(request.LabourCharge, 2),
            RoundNonNeg(request.MiscCharge, 2),
            request.ExcludeGstMetal ? 1 : 0,
            RoundNonNeg(request.PureWt, 4),
            request.TrnOnMargin ? 1 : 0,
            request.UaeTrnItem ? 1 : 0,
            Clip((request.CustSku ?? string.Empty).Trim(), 30),
            NormalizeDate(request.AgeingDate),
            request.Promotional ? 1 : 0).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_diamond_master` WHERE `company_id` = ? AND `item_code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Diamond " + code + " saved", id);
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

    private static object NormalizeDate(string? value)
        => DateOnly.TryParse(value, out var date) ? date.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
}
