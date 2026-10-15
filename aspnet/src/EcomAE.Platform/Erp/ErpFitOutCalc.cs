namespace EcomAE.Platform.Erp;

/// <summary>
/// Fit-out costing. Margin and markup stay different numbers.
/// A purchase that has become a supplier invoice is actual, not still committed.
/// An unapproved variation does not raise the contract. A cheque is not cash until it clears.
/// </summary>
public static class ErpFitOutCalc
{
    public static decimal Money(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    public static decimal DirectUnitCost(decimal material, decimal labour, decimal equipment, decimal subcontract)
        => Money(material + labour + equipment + subcontract);

    public static decimal TotalDirectCost(decimal quantity, decimal directUnit, decimal wastagePercent)
    {
        var factor = 1m + (wastagePercent / 100m);
        return Money(quantity * directUnit * factor);
    }

    public static decimal EstimatedTotalCost(decimal totalDirect, decimal overhead, decimal contingency)
        => Money(totalDirect + overhead + contingency);

    /// <summary>Selling = cost / (1 − margin). Margin of 100% or more is refused.</summary>
    public static decimal SellingFromMargin(decimal estimatedCost, decimal targetMarginPercent)
    {
        if (targetMarginPercent >= 100m || targetMarginPercent < 0m)
        {
            return 0m;
        }

        return Money(estimatedCost / (1m - (targetMarginPercent / 100m)));
    }

    public static decimal SellingFromMarkup(decimal estimatedCost, decimal markupPercent)
        => Money(estimatedCost * (1m + (markupPercent / 100m)));

    public static decimal EstimateAtCompletion(decimal actualToDate, decimal forecastRemaining)
        => Money(actualToDate + forecastRemaining);

    public static decimal ForecastProfit(decimal revisedContract, decimal forecastTotalCost)
        => Money(revisedContract - forecastTotalCost);

    public static decimal ForecastMarginPercent(decimal forecastProfit, decimal revisedContract)
        => revisedContract == 0 ? 0 : Money(forecastProfit / revisedContract * 100m);

    public static decimal BudgetVariance(decimal approvedBudget, decimal forecastTotalCost)
        => Money(approvedBudget - forecastTotalCost);

    public static decimal OpenCommitment(decimal ordered, decimal invoiced)
        => Money(Math.Max(0, ordered - invoiced));

    public static decimal RevisedContract(decimal original, decimal approvedVariations)
        => Money(original + approvedVariations);

    public static decimal ContractRevenue(decimal original, decimal approvedVariations, decimal pendingVariations)
        => RevisedContract(original, approvedVariations);

    public static decimal ProgressNet(decimal certified, decimal retention, decimal advanceRecovery, decimal vat)
        => Money(certified - retention - advanceRecovery + vat);

    public static decimal CollectionRate(decimal collectedOnDue, decimal dueReceivables)
        => dueReceivables <= 0 ? 0 : Money(collectedOnDue / dueReceivables * 100m);

    public static bool CountsAsBankCash(string? chequeStatus)
        => string.Equals(chequeStatus?.Trim(), "cleared", StringComparison.OrdinalIgnoreCase);

    public static decimal PettyBalance(decimal advance, decimal settled)
        => Money(advance - settled);

    public static decimal CurrentBudget(decimal original, decimal approvedRevisions)
        => Money(original + approvedRevisions);
}
