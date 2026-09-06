using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>einvoice_save_seller</c> / <c>einvoice_save_buyer</c> / <c>einvoice_save_asp</c> twins.
/// Create / submit / credit-note / ASP poll stay Classic. Schema-ensure stays PHP.
/// </summary>
public interface IErpEinvoiceProfileWriteService
{
    Task<ErpSimpleWriteResult> SaveSellerAsync(ErpEinvoiceSellerWriteRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBuyerAsync(ErpEinvoiceBuyerWriteRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAspAsync(ErpEinvoiceAspWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record ErpEinvoiceSellerWriteRequest(
    string? SellerName = null,
    string? SellerTrn = null,
    string? SellerTin = null,
    string? SellerLegalRegNo = null,
    string? SellerLegalRegType = null,
    string? SellerAuthorityName = null,
    string? SellerAddressLine1 = null,
    string? SellerCity = null,
    string? SellerEmirate = null,
    string? SellerCountryCode = null,
    string? SellerPhone = null,
    string? SellerEmail = null,
    string? SellerBankAccount = null,
    string? PaymentMeansCode = null,
    string? PaymentTerms = null,
    bool CompanyVatRegistered = false);

public sealed record ErpEinvoiceBuyerWriteRequest(
    long UserId = 0,
    string? BuyerName = null,
    string? Trn = null,
    string? Tin = null,
    string? LegalRegNo = null,
    string? LegalRegType = null,
    string? AuthorityName = null,
    string? AddressLine1 = null,
    string? City = null,
    string? Emirate = null,
    string? CountryCode = null,
    string? Phone = null,
    string? Email = null,
    string? PeppolEndpoint = null,
    bool BuyerOnboarded = false);

public sealed record ErpEinvoiceAspWriteRequest(
    string? AspName = null,
    string? AspApiMode = null,
    string? AspApiUrl = null,
    string? AspApiKey = null,
    string? EinvoiceEnabled = null);

public sealed class ErpEinvoiceProfileWriteService : IErpEinvoiceProfileWriteService
{
    private static readonly HashSet<string> AllowedSettingKeys =
    [
        "seller_name", "seller_trn", "seller_tin", "seller_legal_reg_no", "seller_legal_reg_type",
        "seller_authority_name", "seller_address_line1", "seller_city", "seller_emirate",
        "seller_country_code", "seller_phone", "seller_email", "seller_bank_account",
        "payment_means_code", "payment_terms", "asp_name", "asp_api_mode", "asp_api_url",
        "asp_api_key", "einvoice_enabled", "default_doc_category", "default_payment_due_days",
        "auto_validate"
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpEinvoiceProfileWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveSellerAsync(
        ErpEinvoiceSellerWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var country = NormalizeCountry(request.SellerCountryCode);
        if (country != "AE")
        {
            return ErpSimpleWriteResult.Fail("invalid", "Seller country must be AE for UAE FTA e-invoicing");
        }

        var trn = DigitsOnly(request.SellerTrn);
        if (!TrnValid(trn))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Seller TRN must be exactly 15 digits (FTA)");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var tin = string.IsNullOrWhiteSpace(request.SellerTin) ? TinFromTrn(trn) : request.SellerTin.Trim();
        var settings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["seller_name"] = (request.SellerName ?? string.Empty).Trim(),
            ["seller_trn"] = trn,
            ["seller_tin"] = tin,
            ["seller_legal_reg_no"] = (request.SellerLegalRegNo ?? string.Empty).Trim(),
            ["seller_legal_reg_type"] = (request.SellerLegalRegType ?? "TL").Trim(),
            ["seller_authority_name"] = (request.SellerAuthorityName ?? string.Empty).Trim(),
            ["seller_address_line1"] = (request.SellerAddressLine1 ?? string.Empty).Trim(),
            ["seller_city"] = string.IsNullOrWhiteSpace(request.SellerCity) ? "Dubai" : request.SellerCity.Trim(),
            ["seller_emirate"] = string.IsNullOrWhiteSpace(request.SellerEmirate) ? "Dubai" : request.SellerEmirate.Trim(),
            ["seller_country_code"] = country,
            ["seller_phone"] = (request.SellerPhone ?? string.Empty).Trim(),
            ["seller_email"] = (request.SellerEmail ?? string.Empty).Trim(),
            ["seller_bank_account"] = (request.SellerBankAccount ?? string.Empty).Trim(),
            ["payment_means_code"] = string.IsNullOrWhiteSpace(request.PaymentMeansCode) ? "30" : request.PaymentMeansCode.Trim(),
            ["payment_terms"] = string.IsNullOrWhiteSpace(request.PaymentTerms) ? "Within 7 days" : request.PaymentTerms.Trim()
        };

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertSettingsAsync(connection, settings, cancellationToken).ConfigureAwait(false);
        await UpsertCompanyAsync(
            connection,
            country,
            trn,
            settings["seller_name"],
            request.CompanyVatRegistered ? "1" : "0",
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Seller profile saved — FTA company registration updated", 1);
    }

    public async Task<ErpSimpleWriteResult> SaveBuyerAsync(
        ErpEinvoiceBuyerWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid customer");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var trn = (request.Trn ?? string.Empty).Trim();
        var tin = string.IsNullOrWhiteSpace(request.Tin) ? TinFromTrn(trn) : request.Tin.Trim();
        var legalType = request.LegalRegType is "TL" or "EID" or "PAS" or "CD" ? request.LegalRegType : "TL";
        var onboarded = request.BuyerOnboarded ? 1 : 0;
        var endpoint = (request.PeppolEndpoint ?? string.Empty).Trim();
        if (endpoint.Length == 0 && onboarded == 1 && tin.Length > 0)
        {
            endpoint = "0235:" + tin;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_einvoice_buyer_profiles`
                (`user_id`, `buyer_name`, `trn`, `tin`, `legal_reg_no`, `legal_reg_type`, `authority_name`,
                 `address_line1`, `city`, `emirate`, `country_code`, `phone`, `email`, `electronic_id`,
                 `peppol_endpoint`, `buyer_onboarded`, `time_updated`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, '0235', ?, ?, ?)
                ON DUPLICATE KEY UPDATE
                `buyer_name`=VALUES(`buyer_name`), `trn`=VALUES(`trn`), `tin`=VALUES(`tin`),
                `legal_reg_no`=VALUES(`legal_reg_no`), `legal_reg_type`=VALUES(`legal_reg_type`),
                `authority_name`=VALUES(`authority_name`), `address_line1`=VALUES(`address_line1`),
                `city`=VALUES(`city`), `emirate`=VALUES(`emirate`), `country_code`=VALUES(`country_code`),
                `phone`=VALUES(`phone`), `email`=VALUES(`email`), `electronic_id`=VALUES(`electronic_id`),
                `peppol_endpoint`=VALUES(`peppol_endpoint`), `buyer_onboarded`=VALUES(`buyer_onboarded`),
                `time_updated`=VALUES(`time_updated`)
                """),
            cancellationToken,
            request.UserId,
            (request.BuyerName ?? string.Empty).Trim(),
            trn,
            tin,
            (request.LegalRegNo ?? string.Empty).Trim(),
            legalType,
            (request.AuthorityName ?? string.Empty).Trim(),
            (request.AddressLine1 ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(request.City) ? "Dubai" : request.City.Trim(),
            string.IsNullOrWhiteSpace(request.Emirate) ? "Dubai" : request.Emirate.Trim(),
            string.IsNullOrWhiteSpace(request.CountryCode) ? "AE" : request.CountryCode.Trim().ToUpperInvariant(),
            (request.Phone ?? string.Empty).Trim(),
            (request.Email ?? string.Empty).Trim(),
            endpoint,
            onboarded,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Buyer profile saved", request.UserId);
    }

    public async Task<ErpSimpleWriteResult> SaveAspAsync(
        ErpEinvoiceAspWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        if (request.AspName is not null) settings["asp_name"] = request.AspName.Trim();
        if (request.AspApiMode is not null) settings["asp_api_mode"] = request.AspApiMode.Trim();
        if (request.AspApiUrl is not null) settings["asp_api_url"] = request.AspApiUrl.Trim();
        if (request.AspApiKey is not null) settings["asp_api_key"] = request.AspApiKey.Trim();
        if (request.EinvoiceEnabled is not null) settings["einvoice_enabled"] = request.EinvoiceEnabled.Trim();
        if (settings.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "ASP settings required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await UpsertSettingsAsync(connection, settings, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("ASP settings saved", 1);
    }

    private static async Task UpsertSettingsAsync(
        System.Data.Common.DbConnection connection,
        IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var (key, value) in settings)
        {
            if (!AllowedSettingKeys.Contains(key))
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_einvoice_settings` (`setting_key`, `setting_value`, `time_updated`)
                    VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `time_updated` = VALUES(`time_updated`)
                    """),
                cancellationToken,
                key,
                value,
                now).ConfigureAwait(false);
        }
    }

    private static async Task UpsertCompanyAsync(
        System.Data.Common.DbConnection connection,
        string country,
        string trn,
        string legalName,
        string vatRegistered,
        CancellationToken cancellationToken)
    {
        var pairs = new (string Key, string Value)[]
        {
            ("company_country_code", country),
            ("company_trn", trn),
            ("company_legal_name", legalName),
            ("company_vat_registered", vatRegistered)
        };
        foreach (var (key, value) in pairs)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?)
                    ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)
                    """),
                cancellationToken,
                key,
                value).ConfigureAwait(false);
        }
    }

    public static string NormalizeCountry(string? raw)
    {
        var code = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return "AE";
        }

        return code is "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E" ? "AE" : code;
    }

    public static string DigitsOnly(string? raw)
        => Regex.Replace(raw ?? string.Empty, @"\D", string.Empty);

    public static bool TrnValid(string? trn)
    {
        var digits = DigitsOnly(trn);
        return digits.Length == 15;
    }

    public static string TinFromTrn(string? trn)
    {
        var digits = DigitsOnly(trn);
        return digits.Length >= 10 ? digits[..10] : digits;
    }
}
