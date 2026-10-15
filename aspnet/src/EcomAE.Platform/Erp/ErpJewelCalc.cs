namespace EcomAE.Platform.Erp;

/// <summary>
/// Jewellery weights stay in grams and stones stay in carats.
/// One carat is 0.2 gram, used only when a stone line must be checked against a gram weight.
/// An exchange keeps the new sale and the old-gold purchase as two amounts.
/// A melt records a variance and does not replace the input weight.
/// </summary>
public static class ErpJewelCalc
{
    public const decimal CaratToGram = 0.2m;
    public const decimal DefaultToleranceGrams = 0.01m;

    public static decimal Grams(decimal value)
        => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public static decimal Money(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal NetMetal(decimal grossGrams, decimal stoneGrams, decimal otherGrams)
        => Grams(grossGrams - stoneGrams - otherGrams);

    public static decimal PureMetal(decimal netGrams, decimal purityPartsPerThousand)
        => Grams(netGrams * purityPartsPerThousand / 1000m);

    public static decimal StoneGrams(decimal carats)
        => Grams(carats * CaratToGram);

    public static bool WithinTolerance(decimal actual, decimal expected, decimal toleranceGrams)
        => Math.Abs(actual - expected) <= toleranceGrams;

    public static decimal MeltVariance(decimal outputPure, decimal recoverablePure, decimal inputPure)
        => Grams(outputPure + recoverablePure - inputPure);

    public static bool MeltNeedsApproval(decimal varianceGrams, decimal toleranceGrams)
        => Math.Abs(varianceGrams) > toleranceGrams;

    public static (decimal SaleValue, decimal BuybackValue, decimal CashDue) Exchange(decimal saleValue, decimal buybackValue)
        => (Money(saleValue), Money(buybackValue), Money(saleValue - buybackValue));

    public static bool CountsAsCompanyStock(string? statusOrOwner)
    {
        var value = (statusOrOwner ?? "").Trim();
        return value.Length > 0
            && !value.Equals("consignment", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("on approval", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("customer", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("in repair", StringComparison.OrdinalIgnoreCase)
            && !value.Equals("customer-owned", StringComparison.OrdinalIgnoreCase);
    }

    public static bool CanSell(string? status)
        => string.Equals(status?.Trim(), "in stock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status?.Trim(), "showroom", StringComparison.OrdinalIgnoreCase);

    public static decimal PosLineTotal(decimal metal, decimal making, decimal stone, decimal discount, decimal vat)
        => Money(metal + making + stone - discount + vat);

    public static decimal StockVariance(decimal counted, decimal system)
        => Grams(counted - system);
}
