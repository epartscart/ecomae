namespace EcomAE.Platform.Erp;

/// <summary>
/// IFRS 18 / IAS 40 / IFRS 15 map for the investment real-estate register.
/// Opportunities stay off both statements. Fair-value movements go through P&amp;L (IAS 40),
/// not OCI. Security deposits are liabilities, not income. Cheques are not cash until clearance.
/// </summary>
public static class ErpRealEstateIfrs
{
    public const string Bank = "1010";
    public const string ChequesUnderCollection = "1120";
    public const string RentReceivable = "1130";
    public const string InvestmentProperty = "1510";
    public const string PropertyUnderConstruction = "1520";
    public const string TenantDeposits = "2200";
    public const string ContractorPayable = "2210";
    public const string RentalIncome = "4100";
    public const string DevelopmentRevenue = "4110";
    public const string FairValueGain = "4200";
    public const string DisposalGain = "4300";
    public const string FairValueLoss = "6200";
    public const string PropertyOpex = "6210";
    public const string DisposalLoss = "6220";

    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description, string Statement)> Accounts =
    [
        (Bank, "Bank", "asset", "debit", "Bank current accounts — cash after clearance", "balance_sheet"),
        (ChequesUnderCollection, "Cheques under collection", "asset", "debit", "PDC / cheques not yet cleared — not cash", "balance_sheet"),
        (RentReceivable, "Rent receivable", "asset", "debit", "Earned rent not yet collected", "balance_sheet"),
        (InvestmentProperty, "Investment property", "asset", "debit", "IAS 40 investment property at cost then fair value", "balance_sheet"),
        (PropertyUnderConstruction, "Investment property under construction", "asset", "debit", "IAS 40 / construction certificates until handover", "balance_sheet"),
        (TenantDeposits, "Tenant security deposits", "liability", "credit", "Refundable deposits — not rental income", "balance_sheet"),
        (ContractorPayable, "Contractor certificates payable", "liability", "credit", "Net certified amounts due to contractors", "balance_sheet"),
        (RentalIncome, "Rental income", "revenue", "credit", "IAS 40 / IFRS 16 lessor operating income", "income_statement"),
        (DevelopmentRevenue, "Development revenue", "revenue", "credit", "IFRS 15 unit sales when performance obligations are met", "income_statement"),
        (FairValueGain, "Fair-value gain on investment property", "revenue", "credit", "IAS 40.35 revaluation through profit or loss", "income_statement"),
        (DisposalGain, "Gain on disposal of investment property", "revenue", "credit", "IAS 40 derecognition gain", "income_statement"),
        (FairValueLoss, "Fair-value loss on investment property", "expense", "debit", "IAS 40.35 revaluation through profit or loss", "income_statement"),
        (PropertyOpex, "Property operating expenses", "expense", "debit", "Repairs and opex that are not capitalised", "income_statement"),
        (DisposalLoss, "Loss on disposal of investment property", "expense", "debit", "IAS 40 derecognition loss", "income_statement"),
    ];

    public sealed record JournalLine(string Code, decimal Debit, decimal Credit, string Note);

    public static string StatementFor(string code)
        => Accounts.FirstOrDefault(a => a.Code == code).Statement
           ?? code switch
           {
               Bank => "balance_sheet",
               "2000" => "balance_sheet",
               _ => "",
           };

    public static bool IsOffStatement(string kind)
        => string.Equals(kind, "opportunity", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<JournalLine> Acquisition(decimal cost)
    {
        var amount = ErpRealEstateCalc.Money(cost);
        if (amount <= 0)
        {
            return [];
        }

        return
        [
            new(InvestmentProperty, amount, 0, "IAS 40 recognition — investment property"),
            new(Bank, 0, amount, "Settlement / bank or payable"),
        ];
    }

    public static IReadOnlyList<JournalLine> FairValueMovement(decimal previousCarrying, decimal newFairValue)
    {
        var delta = ErpRealEstateCalc.Money(newFairValue - previousCarrying);
        if (delta == 0)
        {
            return [];
        }

        return delta > 0
            ?
            [
                new(InvestmentProperty, delta, 0, "IAS 40 fair-value increase"),
                new(FairValueGain, 0, delta, "Fair-value gain through P&L"),
            ]
            :
            [
                new(FairValueLoss, -delta, 0, "Fair-value loss through P&L"),
                new(InvestmentProperty, 0, -delta, "IAS 40 fair-value decrease"),
            ];
    }

    public static IReadOnlyList<JournalLine> Disposal(decimal netProceeds, decimal carrying)
    {
        var proceeds = ErpRealEstateCalc.Money(netProceeds);
        var basis = ErpRealEstateCalc.Money(carrying);
        var gain = ErpRealEstateCalc.Money(proceeds - basis);
        var lines = new List<JournalLine>();
        if (proceeds > 0)
        {
            lines.Add(new(Bank, proceeds, 0, "Net sale proceeds"));
        }

        if (basis > 0)
        {
            lines.Add(new(InvestmentProperty, 0, basis, "Derecognise investment property"));
        }

        if (gain > 0)
        {
            lines.Add(new(DisposalGain, 0, gain, "IAS 40 disposal gain"));
        }
        else if (gain < 0)
        {
            lines.Add(new(DisposalLoss, -gain, 0, "IAS 40 disposal loss"));
        }

        return BalanceOrEmpty(lines);
    }

    public static IReadOnlyList<JournalLine> DepositHeld(decimal amount)
    {
        var value = ErpRealEstateCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(Bank, value, 0, "Cash received for deposit"),
            new(TenantDeposits, 0, value, "Liability — not income"),
        ];
    }

    public static IReadOnlyList<JournalLine> ChequeReceived(decimal amount)
    {
        var value = ErpRealEstateCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(ChequesUnderCollection, value, 0, "PDC — not cash"),
            new(RentalIncome, 0, value, "Rental income earned"),
        ];
    }

    public static IReadOnlyList<JournalLine> ChequeCleared(decimal amount)
    {
        var value = ErpRealEstateCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(Bank, value, 0, "Cleared into bank"),
            new(ChequesUnderCollection, 0, value, "Remove PDC asset"),
        ];
    }

    public static IReadOnlyList<JournalLine> OperatingExpense(decimal amount)
    {
        var value = ErpRealEstateCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(PropertyOpex, value, 0, "Property opex"),
            new(Bank, 0, value, "Paid / accrued"),
        ];
    }

    public static IReadOnlyList<JournalLine> CapitalExpenditure(decimal amount)
    {
        var value = ErpRealEstateCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(InvestmentProperty, value, 0, "Subsequent cost capitalised"),
            new(Bank, 0, value, "Paid / accrued"),
        ];
    }

    public static IReadOnlyList<JournalLine> Certificate(decimal netPayable)
    {
        var value = ErpRealEstateCalc.Money(netPayable);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(PropertyUnderConstruction, value, 0, "Construction / CIP asset"),
            new(ContractorPayable, 0, value, "Certificate payable"),
        ];
    }

    public static IReadOnlyList<JournalLine> DevelopmentSale(decimal recognized, decimal cost)
    {
        var revenue = ErpRealEstateCalc.Money(recognized);
        var cogs = ErpRealEstateCalc.Money(cost);
        var lines = new List<JournalLine>();
        if (revenue > 0)
        {
            lines.Add(new(RentReceivable, revenue, 0, "IFRS 15 contract receivable"));
            lines.Add(new(DevelopmentRevenue, 0, revenue, "IFRS 15 development revenue"));
        }

        if (cogs > 0)
        {
            lines.Add(new(PropertyOpex, cogs, 0, "Cost of unit sold"));
            lines.Add(new(PropertyUnderConstruction, 0, cogs, "Derecognise CIP"));
        }

        return BalanceOrEmpty(lines);
    }

    private static IReadOnlyList<JournalLine> BalanceOrEmpty(List<JournalLine> lines)
    {
        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        return debit > 0 && debit == credit ? lines : [];
    }
}
