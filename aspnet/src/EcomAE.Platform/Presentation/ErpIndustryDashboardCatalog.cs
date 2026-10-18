namespace EcomAE.Platform.Presentation;

/// <summary>
/// Which dashboard the executive summary leads with for the company that is open.
/// Jewellery, real estate, and fit-out each get their own board. Everyone else gets the trading board.
/// </summary>
public static class ErpIndustryDashboardCatalog
{
    public const string Jewellery = "jewellery";
    public const string RealEstate = "real_estate";
    public const string FitOut = "fit_out";
    public const string AutoParts = "auto_parts";
    public const string Trading = "trading";

    public static string Kind(bool jewellery, bool realEstate, bool fitOut, string? industryPack)
    {
        if (jewellery)
        {
            return Jewellery;
        }

        if (realEstate)
        {
            return RealEstate;
        }

        if (fitOut)
        {
            return FitOut;
        }

        var pack = (industryPack ?? string.Empty).Trim();
        if (pack.Contains("auto", StringComparison.OrdinalIgnoreCase)
            || pack.Contains("part", StringComparison.OrdinalIgnoreCase))
        {
            return AutoParts;
        }

        return Trading;
    }

    public static string Title(string kind)
        => kind switch
        {
            Jewellery => "Jewellery dashboard",
            RealEstate => "Real estate dashboard",
            FitOut => "Fit-out dashboard",
            AutoParts => "Parts trading dashboard",
            _ => "Trading dashboard",
        };

    public static string Lead(string kind)
        => kind switch
        {
            Jewellery => "Today's gold rate, metal on hand, and repair jobs for this jewellery company. The books underneath stay the same cash, receivables, and VAT.",
            RealEstate => "This year's rent income, property expenses, and return for this real-estate company. Cheques are not cash until they clear.",
            FitOut => "Site delivery, open project purchases, and stock for this fit-out company. Each tile opens the workspace behind the figure.",
            AutoParts => "Orders, fitment stock, suppliers, and purchases for this parts company. VIN and supersession stay on the sales and item workspaces.",
            _ => "Orders, stock, suppliers, and tax for this trading company. Open a tile to see the documents behind the figure.",
        };
}
