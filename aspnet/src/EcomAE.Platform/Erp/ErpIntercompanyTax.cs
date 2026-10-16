using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE VAT Decree-Law 8 of 2017 and Corporate Tax Decree-Law 47 of 2022,
/// applied as setup outcomes for intercompany pairs.
/// </summary>
public static class ErpIntercompanyTax
{
    public static string VatTreatment(string scope, bool vatGroup, decimal salesRate, decimal purchaseRate)
    {
        if (IsSameEntity(scope))
        {
            return "Same legal entity. A movement between business units or cost centres is not a supply. No VAT.";
        }

        if (vatGroup)
        {
            return "VAT group. Supplies between members are disregarded. No VAT is charged on those supplies.";
        }

        return "Separate taxable persons. Sales group VAT "
            + FormatRate(salesRate)
            + " and purchase group VAT "
            + FormatRate(purchaseRate)
            + " on taxable supplies in the UAE.";
    }

    public static string CtTreatment(string scope, bool ctGroup)
    {
        if (IsSameEntity(scope))
        {
            return "Same legal entity. A business unit or cost centre is not a separate taxable person.";
        }

        if (ctGroup)
        {
            return "Corporate tax group. Article 40 eliminates intra-group transactions. Article 26 qualifying-group relief is not applied automatically.";
        }

        return "Separate taxable persons. Article 34 requires an arm's length price. Article 26 qualifying-group relief is not applied automatically.";
    }

    public static string TransferPricing(string method)
        => (method ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "resale" => "Resale price method.",
            "cost_plus" => "Cost plus method.",
            "tnmm" => "Transactional net margin method.",
            "profit_split" => "Profit split method.",
            _ => "Comparable uncontrolled price. This is the arm's length method under Article 34.",
        };

    public static bool IsSameEntity(string scope)
        => (scope ?? string.Empty).Trim().ToLowerInvariant() is "business_unit" or "cost_centre";

    public static string FormatRate(decimal rate)
        => rate.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
