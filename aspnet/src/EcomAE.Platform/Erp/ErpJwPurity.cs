namespace EcomAE.Platform.Erp;

/// <summary>
/// Jewellery metal-purchase valuation: Pure Wt = Gross Wt × Purity
/// (100.01 × 0.999 = 99.91). Metal amount = Pure Wt × metal rate when the
/// line does not already carry an explicit metal amount.
/// </summary>
public static class ErpJwPurity
{
    public static decimal PureWeight(decimal grossWeight, decimal purity)
    {
        var grams = RoundNonNeg(grossWeight, 4);
        var fine = RoundNonNeg(purity, 6);
        if (fine > 1m)
        {
            fine = decimal.Round(fine / 1000m, 6, MidpointRounding.AwayFromZero);
        }

        return RoundNonNeg(grams * fine, 4);
    }

    public static decimal MetalAmount(decimal grossWeight, decimal purity, decimal metalRate, decimal metalAmount = 0)
    {
        if (metalAmount > 0)
        {
            return RoundNonNeg(metalAmount, 2);
        }

        return RoundNonNeg(PureWeight(grossWeight, purity) * RoundNonNeg(metalRate, 5), 2);
    }

    public static decimal MakingAmount(decimal grossWeight, decimal makingRate, decimal makingAmount = 0)
        => makingAmount > 0
            ? RoundNonNeg(makingAmount, 2)
            : RoundNonNeg(RoundNonNeg(grossWeight, 4) * RoundNonNeg(makingRate, 4), 2);

    public static decimal LineTotal(
        decimal grossWeight,
        decimal purity,
        decimal metalRate,
        decimal makingRate,
        decimal stoneAmount = 0,
        decimal discountAmount = 0,
        decimal metalAmount = 0,
        decimal makingAmount = 0)
        => RoundNonNeg(
            MetalAmount(grossWeight, purity, metalRate, metalAmount)
            + MakingAmount(grossWeight, makingRate, makingAmount)
            + RoundNonNeg(stoneAmount, 2)
            - RoundNonNeg(discountAmount, 2),
            2);

    public static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);
}
