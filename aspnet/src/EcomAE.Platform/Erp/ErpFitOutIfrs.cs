namespace EcomAE.Platform.Erp;

/// <summary>
/// IFRS 15 (over-time / POC) account map for the fit-out workspace. Revenue is
/// recognised as work is performed — Dr contract asset / Cr contract revenue;
/// certification reclassifies the asset to receivables; costs hit contract
/// cost of sales as incurred, so the income statement and balance sheet stay
/// aligned with the project P&amp;L/WIP the workspace reports.
/// </summary>
public static class ErpFitOutIfrs
{
    public const string Bank = "1010";
    public const string AccountsReceivable = "1100";
    public const string ContractAsset = "1360";
    public const string RetentionReceivable = "1365";
    public const string AccountsPayable = "2000";
    public const string VatOutput = "2100";
    public const string ContractLiability = "2350";
    public const string ContractRevenue = "4150";
    public const string ContractCost = "6150";

    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description, string Statement)> Accounts =
    [
        (ContractAsset, "Contract assets (POC work done not yet certified)", "asset", "debit", "IFRS 15 contract asset — performance satisfied ahead of billing", "balance_sheet"),
        (RetentionReceivable, "Retention receivable", "asset", "debit", "IFRS 15 withheld retention due on defects-liability expiry", "balance_sheet"),
        (ContractLiability, "Contract liabilities — client advances", "liability", "credit", "IFRS 15 advance/billings ahead of performance", "balance_sheet"),
        (ContractRevenue, "Construction contract revenue", "revenue", "credit", "IFRS 15 over-time revenue (POC) on fit-out contracts", "income_statement"),
        (ContractCost, "Contract cost of sales", "expense", "debit", "IFRS 15 / IAS 2 contract costs expensed as incurred", "income_statement"),
    ];

    public sealed record JournalLine(string Code, decimal Debit, decimal Credit, string Note);

    public static string StatementFor(string code)
        => Accounts.FirstOrDefault(a => a.Code == code).Statement
           ?? code switch
           {
               Bank or AccountsReceivable or RetentionReceivable => "balance_sheet",
               AccountsPayable or VatOutput or ContractLiability => "balance_sheet",
               ContractRevenue or ContractCost => "income_statement",
               _ => "",
           };

    /// <summary>Project cost incurred — expense now, payable to supplier/warehouse.</summary>
    public static IReadOnlyList<JournalLine> CostIncurred(decimal amount)
    {
        var value = ErpFitOutCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(ContractCost, value, 0, "Contract cost expensed as incurred"),
            new(AccountsPayable, 0, value, "Supplier / site cost payable"),
        ];
    }

    /// <summary>POC revenue earned — Dr contract asset, Cr contract revenue.</summary>
    public static IReadOnlyList<JournalLine> RevenueEarned(decimal amount)
    {
        var value = ErpFitOutCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(ContractAsset, value, 0, "IFRS 15 over-time performance — unbilled revenue"),
            new(ContractRevenue, 0, value, "POC revenue recognised in P&L"),
        ];
    }

    /// <summary>Progress claim certified — reclass earned asset to receivable (incl. VAT on the receivable leg).</summary>
    public static IReadOnlyList<JournalLine> ClaimCertified(decimal amount, decimal vat)
    {
        var value = ErpFitOutCalc.Money(amount);
        var tax = ErpFitOutCalc.Money(vat);
        if (value <= 0)
        {
            return [];
        }

        var assetLeg = ErpFitOutCalc.Money(value - tax);
        var lines = new List<JournalLine>
        {
            new(AccountsReceivable, value, 0, "Certified claim receivable incl. VAT"),
        };
        if (assetLeg > 0)
        {
            lines.Add(new(ContractAsset, 0, assetLeg, "Reclass contract asset to receivable on certification"));
        }
        if (tax > 0)
        {
            lines.Add(new(VatOutput, 0, tax, "Output VAT on certified claim"));
        }
        return lines;
    }

    /// <summary>Client advance received — Dr bank, Cr contract liability.</summary>
    public static IReadOnlyList<JournalLine> AdvanceReceived(decimal amount)
    {
        var value = ErpFitOutCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(Bank, value, 0, "Client advance received"),
            new(ContractLiability, 0, value, "IFRS 15 contract liability until performance"),
        ];
    }

    /// <summary>Retention withheld on a certified claim — moves part of AR to retention receivable.</summary>
    public static IReadOnlyList<JournalLine> RetentionWithheld(decimal amount)
    {
        var value = ErpFitOutCalc.Money(amount);
        if (value <= 0)
        {
            return [];
        }

        return
        [
            new(RetentionReceivable, value, 0, "Retention withheld — due after defects liability"),
            new(AccountsReceivable, 0, value, "Reclass retention out of trade receivables"),
        ];
    }
}
