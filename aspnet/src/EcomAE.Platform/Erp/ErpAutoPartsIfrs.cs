namespace EcomAE.Platform.Erp;

/// <summary>
/// Chart-of-accounts map and balanced journal recipes for the auto-parts
/// workspace. A parts business is a trading company: sales revenue is
/// recognized when the invoice is issued (point in time, IFRS 15 step-5
/// satisfied at delivery/invoice), purchases land in inventory, and 5% UAE VAT
/// is tracked on output sales. Journals post through <see cref="ErpFitOutGl"/>
/// into the shared <c>epc_erp_gl_*</c> ledger so the income statement, balance
/// sheet and dashboard figures include the auto-parts numbers.
/// </summary>
public static class ErpAutoPartsIfrs
{
    public const string ArAccount = "1100";
    public const string InventoryAccount = "1300";
    public const string ApAccount = "2000";
    public const string VatAccount = "2100";
    public const string RevenueAccount = "4000";
    public const string CogsAccount = "5000";
    public const string BankAccount = "1010";

    /// <summary>Accounts the auto-parts bridge ensures exist in the shared COA.</summary>
    public static readonly IReadOnlyList<(string Code, string Name, string Type, string Side, string Description, string Statement)> Accounts =
    [
        (InventoryAccount, "Parts Inventory", "asset", "debit", "Auto-parts stock on hand at weighted average cost", "balance_sheet"),
    ];

    public sealed record JournalLine(string Code, decimal Debit, decimal Credit, string Note);

    /// <summary>Sales invoice issued: Dr AR (gross) / Cr parts revenue (net) / Cr output VAT.</summary>
    public static IReadOnlyList<JournalLine> SaleInvoiced(decimal net, decimal vat)
    {
        var lines = new List<JournalLine>
        {
            new(ArAccount, net + vat, 0m, "Customer invoice receivable"),
            new(RevenueAccount, 0m, net, "Parts sales revenue"),
        };
        if (vat > 0)
        {
            lines.Add(new JournalLine(VatAccount, 0m, vat, "Output VAT 5%"));
        }
        return lines;
    }

    /// <summary>COGS on parts issued against a sale: Dr cost of sales / Cr inventory.</summary>
    public static IReadOnlyList<JournalLine> CostOfSale(decimal amount)
        =>
        [
            new(CogsAccount, amount, 0m, "Cost of parts sold"),
            new(InventoryAccount, 0m, amount, "Inventory issued"),
        ];

    /// <summary>Purchase receipt of parts: Dr inventory / Cr supplier payable.</summary>
    public static IReadOnlyList<JournalLine> PurchaseReceived(decimal amount)
        =>
        [
            new(InventoryAccount, amount, 0m, "Parts received into stock"),
            new(ApAccount, 0m, amount, "Supplier payable"),
        ];

    /// <summary>Customer payment received: Dr bank / Cr AR.</summary>
    public static IReadOnlyList<JournalLine> PaymentReceived(decimal amount)
        =>
        [
            new(BankAccount, amount, 0m, "Customer receipt"),
            new(ArAccount, 0m, amount, "Receivable settled"),
        ];
}
