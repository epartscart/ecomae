namespace EcomAE.Platform.Erp;

/// <summary>
/// UAE real-estate investment, development and construction metrics.
/// Money is decimal. Opportunities are never included in owned-property totals.
/// Fair-value movements post through profit or loss (IAS 40), not OCI.
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
        new("unrealized", "Unrealized appreciation/depreciation", "fair_value - carrying_value (IAS 40 through P&L)", "as-of", Currency, "valuation vs carrying", "1"),
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
        new("boq_amount", "BOQ line amount", "qty * rate", "line", Currency, "epc_erp_re_boq_lines", "1"),
        new("escrow_balance", "Project escrow balance", "escrow_receipts - permitted_disbursements", "project", Currency, "epc_erp_re_escrow", "1"),
        new("offplan_instalment", "Off-plan instalment", "sale_price * milestone_percent / 100", "milestone", Currency, "epc_erp_re_instalments", "sale_price"),
        new("shared_cost", "Shared-cost allocation", "project_cost * unit_weight / sum(weights); last unit takes remainder", "project", Currency, "epc_erp_re_cost_allocs", "sum(weights)"),
        new("irr", "Unlevered IRR/XIRR", "XIRR of dated cash flows (acquisition outflow, NOI, sale)", "holding period", "ratio", "dated cash flows", "n/a"),
        new("recognized_rent", "Recognized rental income", "annual_rent x overlap_days / 365", "period", Currency, "active leases", "365"),
        new("landlord_noi", "Landlord NOI", "recognized_rent - operating_expenses; capex, financing, depreciation and tax stay out", "year", Currency, "leases + posted opex", "1"),
        new("pdc_in_hand", "PDCs in hand", "cheques received but not deposited and not cleared", "as-of", Currency, "epc_erp_re_cheques", "1"),
        new("net_cash_flow", "Net cash flow", "cleared bank receipts - posted cash payments", "period", Currency, "cleared cheques and payments", "1"),
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

    public static decimal BoqAmount(decimal qty, decimal rate)
        => Money(qty * rate);

    public static decimal EscrowBalance(decimal receipts, decimal disbursements)
        => Money(receipts - disbursements);

    public static bool EscrowDisbursementPermitted(decimal receipts, decimal disbursements, decimal requested)
        => requested > 0 && EscrowBalance(receipts, disbursements) >= Money(requested);

    public static decimal OffPlanInstalment(decimal salePrice, decimal percent)
        => percent <= 0 || salePrice <= 0 ? 0 : Money(salePrice * percent / 100m);

    public static decimal UnitCostOfSale(decimal allocatedCost, decimal askingPrice)
        => allocatedCost > 0 ? Money(allocatedCost) : Money(askingPrice);

    /// <summary>
    /// Splits <paramref name="total"/> across units. Zero weights are skipped.
    /// When every weight is zero the split is equal. The last positive unit
    /// receives the remainder so the parts always sum to the money total.
    /// </summary>
    public static IReadOnlyList<decimal> AllocateSharedCost(decimal total, IReadOnlyList<decimal> weights)
    {
        var money = Money(total);
        if (weights is null || weights.Count == 0)
        {
            return [];
        }

        var result = new decimal[weights.Count];
        if (money <= 0)
        {
            return result;
        }

        var effective = weights.Select(w => w > 0 ? w : 0m).ToArray();
        var sum = effective.Sum();
        if (sum <= 0)
        {
            var each = Money(money / weights.Count);
            decimal running = 0;
            for (var i = 0; i < weights.Count; i++)
            {
                result[i] = i == weights.Count - 1 ? Money(money - running) : each;
                running += result[i];
            }

            return result;
        }

        decimal allocated = 0;
        var lastPositive = -1;
        for (var i = 0; i < weights.Count; i++)
        {
            if (effective[i] <= 0)
            {
                continue;
            }

            lastPositive = i;
            result[i] = Money(money * (effective[i] / sum));
            allocated += result[i];
        }

        if (lastPositive >= 0)
        {
            result[lastPositive] = Money(result[lastPositive] + (money - allocated));
        }

        return result;
    }

    public sealed record RentInstalment(int Sequence, DateOnly Due, decimal Amount);

    /// <summary>
    /// Instalments for one lease. The last line takes the rounding remainder so the
    /// parts always sum to instalment x count (a year of quarterly rent is four equal lines).
    /// </summary>
    public static IReadOnlyList<RentInstalment> BuildSchedule(decimal annualRent, string frequency, DateOnly start, DateOnly end)
    {
        var count = PeriodCount(frequency, start, end);
        if (count <= 0 || annualRent <= 0 || end < start)
        {
            return [];
        }

        var each = InstalmentAmount(annualRent, frequency);
        var perYear = frequency switch
        {
            "weekly" => 52m,
            "quarterly" => 4m,
            "semi_annual" => 2m,
            "annual" => 1m,
            _ => 12m,
        };
        var expected = Money(annualRent * count / perYear);
        var rows = new List<RentInstalment>(count);
        var cursor = start;
        decimal running = 0;
        for (var i = 0; i < count; i++)
        {
            if (i > 0 && cursor > end)
            {
                break;
            }

            var amount = i == count - 1 ? Money(expected - running) : each;
            rows.Add(new RentInstalment(i + 1, cursor, amount));
            running = Money(running + amount);
            cursor = Step(frequency, cursor);
        }

        return rows;
    }

    public static decimal ScheduleTotal(IReadOnlyList<RentInstalment> rows)
        => Money(rows.Sum(r => r.Amount));

    /// <summary>
    /// Straight-line contractual rent for the overlap of the lease and the report period.
    /// A cheque received in the period is not income.
    /// </summary>
    public static decimal RecognizedRent(decimal annualRent, DateOnly leaseStart, DateOnly leaseEnd, DateOnly periodStart, DateOnly periodEnd)
    {
        if (annualRent <= 0 || leaseEnd < leaseStart || periodEnd < periodStart)
        {
            return 0;
        }

        var from = leaseStart > periodStart ? leaseStart : periodStart;
        var to = leaseEnd < periodEnd ? leaseEnd : periodEnd;
        if (to < from)
        {
            return 0;
        }

        var overlapDays = to.DayNumber - from.DayNumber + 1;
        return Money(annualRent * overlapDays / 365m);
    }

    public static decimal LandlordNoi(decimal recognizedIncome, decimal operatingExpenses)
        => NetOperatingIncome(recognizedIncome, operatingExpenses);

    public static decimal NetCashFlow(decimal clearedReceipts, decimal cashPayments)
        => Money(clearedReceipts - cashPayments);

    public static bool CountsAsCash(string? status)
        => string.Equals((status ?? string.Empty).Trim(), "cleared", StringComparison.OrdinalIgnoreCase);

    public static string ChequeBucket(string? status, DateOnly? chequeDate, DateOnly today)
    {
        var token = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (token is "cancelled" or "replaced" or "voided" or "reversed")
        {
            return "closed";
        }

        if (token is "bounced" or "returned")
        {
            return "returned";
        }

        if (token == "cleared")
        {
            return "cleared";
        }

        if (token == "deposited")
        {
            return "awaiting_clear";
        }

        if (chequeDate is null)
        {
            return "undated";
        }

        var days = chequeDate.Value.DayNumber - today.DayNumber;
        if (days < 0)
        {
            return "overdue_not_deposited";
        }

        if (days == 0)
        {
            return "due_today";
        }

        if (days == 1)
        {
            return "due_tomorrow";
        }

        if (days <= 7)
        {
            return "due_7";
        }

        if (days <= 15)
        {
            return "due_15";
        }

        if (days <= 30)
        {
            return "due_30";
        }

        return "in_custody";
    }

    public static string AgeingBucket(DateOnly due, DateOnly asOf)
    {
        var days = asOf.DayNumber - due.DayNumber;
        if (days <= 0)
        {
            return "current";
        }

        if (days <= 30)
        {
            return "1_30";
        }

        if (days <= 60)
        {
            return "31_60";
        }

        if (days <= 90)
        {
            return "61_90";
        }

        return "over_90";
    }

    public static bool TenanciesOverlap(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd)
        => aStart <= bEnd && bStart <= aEnd;

    public static bool ReducesOperatingProfit(string? category, bool capital)
    {
        if (capital)
        {
            return false;
        }

        var token = (category ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        return token is not ("mortgage_interest" or "depreciation" or "income_tax" or "capex" or "acquisition");
    }

    public static decimal MissingChequeAmount(decimal scheduleAmount, decimal linkedChequeAmount)
        => Money(Math.Max(0, scheduleAmount - linkedChequeAmount));

    public static IReadOnlyList<string> BulkUnitCodes(string? prefix, int startNumber, int count)
    {
        if (count <= 0)
        {
            return [];
        }

        var n = Math.Min(count, 500);
        var start = startNumber <= 0 ? 1 : startNumber;
        var stem = string.IsNullOrWhiteSpace(prefix) ? "U" : prefix.Trim();
        if (stem.Length > 20)
        {
            stem = stem[..20];
        }

        var codes = new List<string>(n);
        for (var i = 0; i < n; i++)
        {
            codes.Add(stem + "-" + (start + i).ToString("000"));
        }

        return codes;
    }

    private static DateOnly Step(string frequency, DateOnly cursor)
        => frequency switch
        {
            "weekly" => cursor.AddDays(7),
            "quarterly" => cursor.AddMonths(3),
            "semi_annual" => cursor.AddMonths(6),
            "annual" => cursor.AddYears(1),
            _ => cursor.AddMonths(1),
        };
}
