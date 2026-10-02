namespace EcomAE.Platform.Erp;

public sealed record ErpEinvoiceCountryProfile(
    string CountryCode,
    string Scheme,
    string TaxLabel,
    IReadOnlyList<string> RequiredFields,
    bool ClearanceRequired);

public static class ErpEinvoiceCountryProfiles
{
    public static ErpEinvoiceCountryProfile ResolveForTenant(
        string? tenantCountry,
        string? previewCountry = null)
        => Resolve(tenantCountry);

    public static ErpEinvoiceCountryProfile Resolve(string? country)
    {
        var code = NormalizeCountry(country);
        return code switch
        {
            "AE" => new("AE", "UAE-FTA", "VAT", ["seller_trn", "buyer_trn"], false),
            "SA" => new("SA", "ZATCA", "VAT", ["seller_trn", "qr"], true),
            "IN" => new("IN", "GST-IRN", "GST", ["seller_gstin", "buyer_gstin", "hsn"], true),
            "GB" => new("GB", "EN16931", "VAT", ["seller_vat"], false),
            "DE" => new("DE", "EN16931", "VAT", ["seller_vat", "buyer_vat"], false),
            "FR" => new("FR", "EN16931", "VAT", ["seller_vat", "buyer_vat"], true),
            "IT" => new("IT", "FatturaPA", "IVA", ["seller_vat", "sdi_code"], true),
            "AU" => new("AU", "PEPPOL", "GST", ["seller_abn"], false),
            _ => new(code, "GENERIC", "TAX", [], false)
        };
    }

    public static string NormalizeCountry(string? country)
    {
        var code = (country ?? string.Empty).Trim().ToUpperInvariant();
        return code switch
        {
            "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E" => "AE",
            _ => code
        };
    }
}
