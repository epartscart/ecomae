namespace EcomAE.Platform.Erp;

/// <summary>
/// Layer C of the FMCG industry layer: GCC country configuration packs.
/// Country-specific tax rates, currency, and legal assumptions live HERE —
/// never inside the FMCG business logic — so a Saudi/UAE/Bahrain/Kuwait/Oman
/// tenant plugs in by choosing a pack instead of forking workflows.
/// </summary>
public sealed record ErpGccCountryPack(
    string Code,
    string Name,
    string CurrencyCode,
    string CurrencyName,
    string VatStatus,
    decimal VatRate,
    string CorporateTax,
    string WithholdingTax,
    string Excise,
    string Customs,
    string LegalNotes,
    bool Enabled);

public static class ErpGccLocalizationCatalog
{
    /// <summary>Qatar is the first, fully configured pack.</summary>
    public static readonly ErpGccCountryPack Qatar = new(
        "QA",
        "Qatar",
        "QAR",
        "Qatari riyal",
        "VAT legislated (Law No. 2 of 2022 framework) but NOT yet in force — pack ships configured at 0% with the registration + rates table ready; flip the rate when GAZT/FTA announces the go-live date.",
        0m,
        "Corporate income tax 10% on Qatar-source profits of the foreign-owned share of mainland entities (fully Qatari/GCC-owned entities are exempt; QFC entities taxed at a flat 10%).",
        "WHT 5% on royalties/technical/consulting services paid to non-residents without a Qatar PE; dividends and most other payments are outside WHT.",
        "Excise tax (Law No. 10 of 2018): tobacco and energy drinks 100%, carbonated/sugary drinks and special-purpose goods 50%.",
        "GCC Common External Tariff — generally 5% ad valorem; food staples can be exempt/zero, alcohol/pork 100%.",
        "Commercial Companies Law No. 11 of 2015; consumer protection Law No. 8 of 2008; food labelling must show batch + production/expiry dates (MoPH food control).",
        true);

    public static readonly ErpGccCountryPack SaudiArabia = new(
        "SA", "Saudi Arabia", "SAR", "Saudi riyal",
        "VAT 15% in force (ZATCA; e-invoicing FATOORA mandatory).", 15m,
        "CIT 20% on non-GCC-owned share; Zakat 2.5% for GCC-owned share.",
        "WHT 5–20% by payment type.",
        "Excise: tobacco/energy drinks 100%, sugary drinks 50%.",
        "GCC CET ~5%.",
        "Companies Law 2022; SFDA food registration.",
        false);

    public static readonly ErpGccCountryPack Uae = new(
        "AE", "United Arab Emirates", "AED", "UAE dirham",
        "VAT 5% in force (FTA).", 5m,
        "Corporate tax 9% over AED 375,000 (0% below; qualifying free-zone 0%).",
        "No WHT.",
        "Excise: tobacco/energy drinks 100%, sugary drinks 50%.",
        "GCC CET ~5%.",
        "Commercial Companies Law 32/2021.",
        false);

    public static readonly ErpGccCountryPack Bahrain = new(
        "BH", "Bahrain", "BHD", "Bahraini dinar",
        "VAT 10% in force (NBR).", 10m,
        "No general CIT (oil/gas sector excepted); DMTT 15% for large MNEs from 2025.",
        "No WHT.",
        "Excise: tobacco/energy drinks 100%, sugary drinks 50%.",
        "GCC CET ~5%.",
        "Commercial Companies Law 21/2001.",
        false);

    public static readonly ErpGccCountryPack Kuwait = new(
        "KW", "Kuwait", "KWD", "Kuwaiti dinar",
        "VAT not implemented.", 0m,
        "CIT 15% on foreign corporate bodies.",
        "Tax retention 5% on contractor payments.",
        "No broad excise yet.",
        "GCC CET ~5%.",
        "Commercial Companies Law 1/2016.",
        false);

    public static readonly ErpGccCountryPack Oman = new(
        "OM", "Oman", "OMR", "Omani rial",
        "VAT 5% in force.", 5m,
        "CIT 15%.",
        "WHT 10% on specific non-resident payments.",
        "Excise: tobacco/energy drinks 100%, sugary drinks 50%.",
        "GCC CET ~5%.",
        "Commercial Companies Law 18/2019.",
        false);

    public static IReadOnlyList<ErpGccCountryPack> All { get; } =
        [Qatar, SaudiArabia, Uae, Bahrain, Kuwait, Oman];

    public static ErpGccCountryPack Resolve(string? code)
        => All.FirstOrDefault(p => string.Equals(p.Code, (code ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
           ?? Qatar;
}
