namespace EcomAE.Platform.Erp;

public sealed record ErpCountryObligation(
    string Code,
    string Title,
    string Regime,
    string Authority,
    string Frequency,
    int LeadDays,
    string DocumentRequirements);

public static class ErpCountryObligationProfiles
{
    public static IReadOnlyList<ErpCountryObligation> Resolve(string? country)
    {
        var code = ErpTaxToolkitCountryProfiles.NormalizeCountry(country);
        var obligations = new List<ErpCountryObligation>
        {
            new("einvoice", "E-invoicing transmission", "e-invoicing", "Tax authority / Peppol", "monthly", 5, "Issued sales invoices in approved XML/JSON; clearance/reporting receipts."),
            new("payroll", "Payroll / wage protection run", "labour", "Labour / WPS", "monthly", 10, "Salary register, bank/WPS file, employee acknowledgements.")
        };

        if (code == "AE")
        {
            obligations.AddRange(
            [
                new("vat_return", "VAT return (FTA)", "VAT", "UAE FTA (EmaraTax)", "quarterly", 28, "Sales/purchase ledgers, output & input VAT summary, adjustments."),
                new("corporate_tax", "Corporate Tax return", "corporate-tax", "UAE FTA", "annual", 270, "Financial statements, tax computation, transfer-pricing disclosures."),
                new("esr", "Economic Substance notification", "ESR", "Ministry of Finance", "annual", 180, "Relevant-activity assessment, substance evidence.")
            ]);
        }
        else if (code == "SA")
        {
            obligations.AddRange(
            [
                new("vat_return", "VAT return (ZATCA)", "VAT", "ZATCA (Saudi Arabia)", "monthly", 30, "Sales/purchase ledgers, output & input VAT summary."),
                new("corporate_tax", "Zakat / Corporate income tax return", "corporate-tax", "ZATCA (Saudi Arabia)", "annual", 120, "Financial statements, zakat/tax computation.")
            ]);
        }
        else
        {
            obligations.AddRange(
            [
                new("vat_return", "VAT / GST return", "VAT", "Tax authority", "quarterly", 28, "Sales/purchase ledgers, output & input tax summary."),
                new("corporate_tax", "Corporate income tax return", "corporate-tax", "Tax authority", "annual", 270, "Financial statements, tax computation.")
            ]);
        }

        return obligations;
    }
}
