namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE real-estate investment, development and construction metrics.
/// Money is decimal. Opportunities are never included in owned-property totals.
/// Fair-value movements are informational until an approved accounting policy posts them.
/// </summary>
public static class ErpRealEstateCalc
{
    public const string Currency = "AED";
    public const int MoneyScale = 2;
    public const int RateScale = 6;

    public sealed record CashFlow(DateOnly Date, decimal Amount);

    public sealed record KpiDefinition(
        string Key,
        string Label,
        string Formula,
        string Period,
        string Currency,
        string Source,
        string Denominator);

    public static readonly IReadOnlyList<KpiDefinition> Definitions =
    [
        new("acquisition_cost", "Acquisition cost", "purchase_price + eligible_acquisition_costs", "transaction", Currency, "epc_erp_re_acquisitions", "1"),
        new("carrying_value", "Accounting carrying value", "acquisition_cost + capex - disposed_basis", "as-of", Currency, "acquisitions + capex + disposals", "1"),
        new("fair_value", "Current fair value", "latest approved valuation.approved_fair_value", "as-of", Currency, "epc_erp_re_valuations", "1"),
        new("unrealized", "Unrealized appreciation/depreciation", "fair_value - carrying_value (not posted to GL)", "as-of", Currency, "valuation vs carrying", "1"),
        new("realized_gain", "Realized capital gain/loss", "net_sale_proceeds - carrying_basis_disposed", "transaction", Currency, "epc_erp_re_disposals", "1"),
        new("gross_yield", "Gross rental yield", "annual_contractual_rent / acquisition_cost", "year", "ratio", "leases / acquisitions", "acquisition_cost"),
        new("noi", "Net operating income", "collected_rent - operating_expenses", "year", Currency, "schedules + opex", "1"),
        new("net_yield", "Net rental yield", "NOI / acquisition_cost", "year", "ratio", "NOI / acquisitions", "acquisition_cost"),
        new("cash_on_cash", "Cash-on-cash return", "(NOI - debt_service) / equity_contribution", "year", "ratio", "NOI, debt, equity", "equity"),
        new("occupancy", "Occupancy rate", "occupied_units / total_units", "as-of", "ratio", "units + leases", "total_units"),
        new("collection", "Rental collection efficiency", "cleared_receipts / invoiced_rent", "period", "ratio", "cheques + invoices", "invoiced_rent"),
        new("ltv", "Loan-to-value", "outstanding_mortgage / fair_value", "as-of", "ratio", "property debt / valuation", "fair_value"),
        new("dscr", "Debt service coverage", "NOI / annual_debt_service", "year", "ratio", "NOI / debt service", "debt_service"),
        new("project_margin", "Project net margin", "(recognized_sales - project_cost) / recognized_sales", "project", "ratio", "sales / project cost", "recognized_sales"),
        new("cost_to_complete", "Cost to complete", "revised_budget - actual_cost", "project", Currency, "project budgets", "1"),
        new("certificate_net", "Certificate net payable", "gross_certified - advance_recovery - retention - other + vat", "certificate", Currency, "epc_erp_re_certificates", "1"),
        new("irr", "Unlevered IRR/XIRR", "XIRR of dated cash flows (acquisition outflow, NOI, sale)", "holding period", "ratio", "dated cash flows", "n/a"),
    ];

    public static decimal Money(decimal value)
        => decimal.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    public static decimal Rate(decimal value)
        => decimal.Round(value, RateScale, MidpointRounding.AwayFromZero);

    public static decimal AcquisitionCost(decimal purchasePrice, decimal eligibleCosts)
        => Money(purchasePrice + eligibleCosts);

    public static decimal CarryingValue(decimal acquisitionCost, decimal capex, decimal disposedBasis)
        => Money(acquisitionCost + capex - disposedBasis);

    public static decimal Unrealized(decimal fairValue, decimal carryingValue)
        => Money(fairValue - carryingValue);

    public static decimal RealizedGain(decimal netProceeds, decimal carryingBasisDisposed)
        => Money(netProceeds - carryingBasisDisposed);

    public static decimal NetSaleProceeds(decimal salePrice, decimal disposalCosts, decimal mortgageSettlement)
        => Money(salePrice - disposalCosts - mortgageSettlement);

    public static decimal GrossYield(decimal annualRent, decimal acquisitionCost)
        => acquisitionCost <= 0 ? 0 : Rate(annualRent / acquisitionCost);

    public static decimal NetOperatingIncome(decimal collectedRent, decimal operatingExpenses)
        => Money(collectedRent - operatingExpenses);

    public static decimal NetYield(decimal noi, decimal acquisitionCost)
        => acquisitionCost <= 0 ? 0 : Rate(noi / acquisitionCost);

    public static decimal CashOnCash(decimal noi, decimal debtService, decimal equity)
        => equity <= 0 ? 0 : Rate((noi - debtService) / equity);

    public static decimal Occupancy(int occupiedUnits, int totalUnits)
        => totalUnits <= 0 ? 0 : Rate((decimal)occupiedUnits / totalUnits);

    public static decimal CollectionEfficiency(decimal clearedReceipts, decimal invoicedRent)
        => invoicedRent <= 0 ? 0 : Rate(clearedReceipts / invoicedRent);

    public static decimal LoanToValue(decimal outstandingMortgage, decimal fairValue)
        => fairValue <= 0 ? 0 : Rate(outstandingMortgage / fairValue);

    public static decimal DebtServiceCoverage(decimal noi, decimal annualDebtService)
        => annualDebtService <= 0 ? 0 : Rate(noi / annualDebtService);

    public static decimal ProjectMargin(decimal recognizedSales, decimal projectCost)
        => recognizedSales <= 0 ? 0 : Rate((recognizedSales - projectCost) / recognizedSales);

    public static decimal CostToComplete(decimal revisedBudget, decimal actualCost)
        => Money(revisedBudget - actualCost);

    public static decimal CertificateNet(
        decimal previousCertified,
        decimal currentMeasured,
        decimal materialsOnSite,
        decimal approvedVariations,
        decimal advanceRecovery,
        decimal retention,
        decimal otherDeductions,
        decimal vatAmount)
    {
        var gross = previousCertified + currentMeasured + materialsOnSite + approvedVariations;
        return Money(gross - advanceRecovery - retention - otherDeductions + vatAmount);
    }

    public static decimal ValuationChangePct(decimal current, decimal previous)
        => previous == 0 ? 0 : Rate((current - previous) / previous);

    public static decimal EquityAfterDebt(decimal fairValue, decimal outstandingMortgage)
        => Money(fairValue - outstandingMortgage);

    /// <summary>
    /// Annualized XIRR on dated cash flows. Outflows are negative.
    /// Returns 0 when the series cannot be solved.
    /// </summary>
    public static decimal Xirr(IReadOnlyList<CashFlow> flows, int maxIterations = 64)
    {
        if (flows.Count < 2)
        {
            return 0;
        }

        var ordered = flows.OrderBy(f => f.Date).ToArray();
        var start = ordered[0].Date;
        var days = ordered.Select(f => (double)f.Date.DayNumber - start.DayNumber).ToArray();
        var amounts = ordered.Select(f => (double)f.Amount).ToArray();
        if (amounts.All(a => a >= 0) || amounts.All(a => a <= 0))
        {
            return 0;
        }

        double rate = 0.1;
        for (var i = 0; i < maxIterations; i++)
        {
            double npv = 0;
            double dNpv = 0;
            for (var j = 0; j < amounts.Length; j++)
            {
                var frac = days[j] / 365.0;
                var denom = Math.Pow(1 + rate, frac);
                if (denom == 0 || double.IsNaN(denom) || double.IsInfinity(denom))
                {
                    return 0;
                }

                npv += amounts[j] / denom;
                dNpv += -frac * amounts[j] / (denom * (1 + rate));
            }

            if (Math.Abs(dNpv) < 1e-12)
            {
                break;
            }

            var next = rate - npv / dNpv;
            if (double.IsNaN(next) || double.IsInfinity(next) || next <= -0.9999)
            {
                return 0;
            }

            if (Math.Abs(next - rate) < 1e-8)
            {
                rate = next;
                break;
            }

            rate = next;
        }

        return Rate((decimal)rate);
    }

    public static int PeriodCount(string frequency, DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            return 0;
        }

        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month + 1;
        return frequency switch
        {
            "weekly" => Math.Max(1, (end.DayNumber - start.DayNumber + 1) / 7),
            "quarterly" => Math.Max(1, (int)Math.Ceiling(months / 3d)),
            "semi_annual" => Math.Max(1, (int)Math.Ceiling(months / 6d)),
            "annual" => Math.Max(1, (int)Math.Ceiling(months / 12d)),
            _ => Math.Max(1, months),
        };
    }

    public static decimal InstalmentAmount(decimal annualRent, string frequency)
        => frequency switch
        {
            "weekly" => Money(annualRent / 52m),
            "quarterly" => Money(annualRent / 4m),
            "semi_annual" => Money(annualRent / 2m),
            "annual" => Money(annualRent),
            _ => Money(annualRent / 12m),
        };
}
