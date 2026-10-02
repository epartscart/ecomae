namespace EcomAE.Platform.Erp;

public sealed record ErpTaxToolkitCountryProfile(
    string CountryCode,
    string KitCode,
    string TaxType,
    decimal StandardRate,
    string TaxLabel,
    string RegistrationLabel,
    string Currency,
    decimal? CorporateTaxRate,
    decimal? CorporateTaxThreshold,
    string CorporateTaxThresholdCurrency,
    string CorporateTaxNotes);

public static class ErpTaxToolkitCountryProfiles
{
    public static ErpTaxToolkitCountryProfile Resolve(string? country)
    {
        var code = NormalizeCountry(country);
        return code switch
        {
            "AE" => new("AE", "AE-UAE-VAT", "vat", 5m, "VAT", "TRN", "AED", 9m, 375000m, "AED", "UAE Federal Decree-Law on CT; 9% above AED 375k taxable income; qualifying free-zone persons may qualify for 0%"),
            "SA" => new("SA", "SA-KSA-VAT", "vat", 15m, "VAT", "VAT Number", "SAR", 20m, null, "", "KSA corporate income tax; 20% standard; Zakat may apply to Saudi/GCC nationals"),
            "IN" => new("IN", "IN-INDIA-GST", "gst", 18m, "GST", "GSTIN", "INR", 30m, null, "", "India headline CIT is approximately 30%; surcharge and cess vary"),
            "GB" => new("GB", "GB-UK-VAT", "vat", 20m, "VAT", "VAT Number", "GBP", 25m, null, "", "UK corporation tax; 25% main rate; small-profits rate may apply"),
            "DE" => new("DE", "DE-GERMANY-VAT", "vat", 19m, "VAT", "VAT ID (USt-IdNr)", "EUR", 15m, null, "", "Germany corporation tax reference; trade tax also applies"),
            "FR" => new("FR", "FR-FRANCE-VAT", "vat", 20m, "VAT", "VAT ID", "EUR", 25m, null, "", "France corporate income tax"),
            "IT" => new("IT", "IT-ITALY-VAT", "vat", 22m, "VAT", "Partita IVA", "EUR", 24m, null, "", "Italy IRES corporate income tax"),
            "AU" => new("AU", "AU-AUSTRALIA-GST", "gst", 10m, "GST", "ABN", "AUD", 30m, null, "", "Australia corporate tax; rate depends on entity eligibility"),
            _ => new(code, code + "-" + code + "-VAT", "vat", 15m, "VAT", "Tax ID", "USD", null, null, "", "Configure corporate tax manually for this jurisdiction")
        };
    }

    public static string NormalizeCountry(string? country)
    {
        var code = (country ?? string.Empty).Trim().ToUpperInvariant();
        return code switch
        {
            "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E" => "AE",
            "UK" => "GB",
            _ => code
        };
    }
}
