using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_vat_refund_save</c> twin. Schema ensure stays PHP.
/// </summary>
public interface IErpBosVatRefundSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? tagRef,
        string? invoiceRef,
        string? customerName,
        string? passportNo,
        string? nationality,
        decimal saleAmount,
        decimal? vatAmount,
        string? saleDate,
        string? status,
        string? notes,
        long adminId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosVatRefundCalc(decimal Refund, decimal Fee, decimal Retained, decimal Vat);

public sealed class ErpBosVatRefundSaveWriteService : IErpBosVatRefundSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosVatRefundSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? tagRef,
        string? invoiceRef,
        string? customerName,
        string? passportNo,
        string? nationality,
        decimal saleAmount,
        decimal? vatAmount,
        string? saleDate,
        string? status,
        string? notes,
        long adminId,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A refund id must be >= 0.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var country = await ResolveCountryAsync(connection, cancellationToken).ConfigureAwait(false);
        var scheme = SchemeFor(country);
        var storedCountry = string.IsNullOrWhiteSpace(scheme.Country) ? country : scheme.Country;
        var sale = decimal.Round(saleAmount, 2, MidpointRounding.AwayFromZero);
        var vat = ResolveVat(vatAmount, sale, scheme.VatRate);
        var calc = Calculate(scheme, vat);
        var nextStatus = NormalizeStatus(status);
        var saleUnix = ResolveSaleDateUnix(saleDate);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var tag = Clip(tagRef, 64);
        var invoice = Clip(invoiceRef, 120);
        var customer = Clip(customerName, 160);
        var passport = Clip(passportNo, 64);
        var nation = Clip(nationality, 80);
        var note = notes ?? string.Empty;
        var schemeName = Clip(scheme.Name, 80);
        var oper = Clip(scheme.Operator, 80);

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_bos_vat_refunds` SET `tag_ref`=?, `country`=?, `scheme`=?, `operator`=?, `invoice_ref`=?, `customer_name`=?, `passport_no`=?, `nationality`=?, `sale_amount`=?, `vat_amount`=?, `refund_amount`=?, `fee_amount`=?, `retained_amount`=?, `status`=?, `sale_date`=?, `notes`=? WHERE `id`=?"),
                cancellationToken,
                tag, storedCountry, schemeName, oper, invoice, customer, passport, nation,
                sale, vat, calc.Refund, calc.Fee, calc.Retained, nextStatus, saleUnix, note, id);
            return ErpSimpleWriteResult.Ok(FormatSavedMessage(calc.Refund), id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_vat_refunds` (`tag_ref`,`country`,`scheme`,`operator`,`invoice_ref`,`customer_name`,`passport_no`,`nationality`,`sale_amount`,`vat_amount`,`refund_amount`,`fee_amount`,`retained_amount`,`status`,`sale_date`,`notes`,`admin_id`,`time`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            tag, storedCountry, schemeName, oper, invoice, customer, passport, nation,
            sale, vat, calc.Refund, calc.Fee, calc.Retained, nextStatus, saleUnix, note,
            adminId < 0 ? 0 : adminId, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(FormatSavedMessage(calc.Refund), inserted);
    }

    public static string FormatSavedMessage(decimal refund)
        => "Refund record saved (refund " + refund.ToString("#,0.00", CultureInfo.InvariantCulture) + ")";

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpBosVatRefundStatusWriteService.Allowed.Contains(status, StringComparer.Ordinal)
            ? status
            : "recorded";
    }

    public static decimal ResolveVat(decimal? vatAmount, decimal sale, decimal vatRate)
    {
        var vat = vatAmount ?? 0;
        if (vat <= 0 && vatRate > 0 && sale > 0)
        {
            vat = decimal.Round(sale * (vatRate / 100m), 2, MidpointRounding.AwayFromZero);
        }

        return decimal.Round(vat < 0 ? 0 : vat, 2, MidpointRounding.AwayFromZero);
    }

    public static ErpBosVatRefundCalc Calculate(ErpBosVatRefundScheme scheme, decimal vatAmount)
    {
        var vat = vatAmount < 0 ? 0 : vatAmount;
        var refund = (vat * scheme.RefundRate) - scheme.FeePerTag;
        if (refund < 0)
        {
            refund = 0;
        }

        refund = decimal.Round(refund, 2, MidpointRounding.AwayFromZero);
        var fee = decimal.Round(scheme.FeePerTag, 2, MidpointRounding.AwayFromZero);
        var retained = decimal.Round(vat - refund, 2, MidpointRounding.AwayFromZero);
        return new ErpBosVatRefundCalc(refund, fee, retained, decimal.Round(vat, 2, MidpointRounding.AwayFromZero));
    }

    public static ErpBosVatRefundScheme SchemeFor(string? country)
    {
        var key = (country ?? string.Empty).Trim().ToUpperInvariant();
        return key switch
        {
            "AE" => new("AE", "Tourist Refund Scheme (Tax-Free)", "Planet", 5.0m, 0.85m, 4.80m),
            "SA" => new("SA", "Tourist VAT Refund", "Authorised operator", 15.0m, 0.85m, 0m),
            _ => new("", "VAT refund scheme", "Authorised operator", 0m, 1.0m, 0m)
        };
    }

    public static long ResolveSaleDateUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
        {
            return unix < 0 ? 0 : unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return 0;
    }

    private static async Task<string> ResolveCountryAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
                cancellationToken,
                "erp_company_country").ConfigureAwait(false);
            var country = (value ?? string.Empty).Trim().ToUpperInvariant();
            return country.Length == 0 ? "AE" : country;
        }
        catch
        {
            return "AE";
        }
    }

    private static string Clip(string? value, int max)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record ErpBosVatRefundScheme(
    string Country,
    string Name,
    string Operator,
    decimal VatRate,
    decimal RefundRate,
    decimal FeePerTag);
