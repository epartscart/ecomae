namespace EcomAE.Platform.Presentation;

/// <summary>
/// Tab metadata + step-by-step guide content for the FMCG workspace.
/// </summary>
public static class ErpFmcgWorkspace
{
    public const string GuideTab = "guide";

    public static readonly (string Key, string Label)[] Tabs =
    [
        (GuideTab, "Guide"),
        ("structure", "Companies & structure"),
        ("items", "Items & packs"),
        ("batches", "Batches, expiry & FEFO"),
        ("channels", "Channels"),
        ("routes", "Van sales routes"),
        ("recon", "Route reconciliation"),
        ("promotions", "Promotions & free goods"),
        ("landed", "Landed cost"),
        ("writedowns", "Write-downs"),
        ("tax", "Tax & compliance"),
        ("reports", "Reports to P&L / BS"),
    ];

    public static string Normalize(string? tab)
        => Tabs.Any(t => string.Equals(t.Key, (tab ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            ? Tabs.First(t => string.Equals(t.Key, tab!.Trim(), StringComparison.OrdinalIgnoreCase)).Key
            : GuideTab;

    public static string Label(string? tab)
        => Tabs.FirstOrDefault(t => string.Equals(t.Key, Normalize(tab), StringComparison.OrdinalIgnoreCase)).Label ?? "Guide";

    public static readonly (string Title, string Detail)[] GuideSteps =
    [
        ("Set the tenant's registered country", "The workspace resolves the GCC country pack (currency, tax rules, legal notes) from the tenant's registered country on the company profile — Qatar first; SA/AE/BH/KW/OM packs plug in identically. The country selector on the Tax tab is a preview only."),
        ("Build the structure", "Create the FMCG group's legal entities (holding, distribution, retail), business units, cost centres and departments in Organization setup. One shared register serves every vertical — FMCG adds no duplicate entity tables."),
        ("Load the demo structure", "Use 'Load sample data' (admin) to seed the Qatar demo set: 2 legal entities, 3 business units, 4 cost centres — plus SKUs, batches, channels, routes, a promotion and demo GL journals. Sandbox-routed; 'Remove sample data' deletes only tagged rows."),
        ("Define FMCG items", "Each SKU carries GTIN/barcode, brand → category hierarchy, pack-size conversion (units per case, cases per pallet), shelf-life days, batch/expiry tracking, FEFO flag, minimum remaining shelf-life %, GCC excise category and its three COA links (sales / COGS / inventory)."),
        ("Track batches and expiry", "Every receipt creates a batch with manufacturing + expiry dates. On-hand is valued per batch; FEFO picking takes the earliest expiry first; recall-flagged batches are quarantined; near-expiry shows in the Batches tab."),
        ("Reconcile van sales", "End of day each route reconciles loaded vs sold vs returned stock and expected vs collected cash — the variance surfaces shrinkage before it hits the write-down ledger."),
        ("Cost imports landed", "Every inbound shipment allocates freight, GCC customs duty and insurance into the item's landed unit cost — COGS and inventory valuation then carry the true landed cost, not just the supplier price."),
        ("Write down expiry, damage and shrinkage", "Expired, damaged or missing stock posts Dr Write-down loss / Cr Inventory against the specific batch — the loss lands in the P&L and the write-down register keeps the audit trail."),
        ("Sell across channels", "Modern trade, HORECA, wholesale, van-sales routes and own retail each carry their own sales account so channel P&L rolls straight into the GL."),
        ("Run trade promotions", "Free-goods (10+1), bonus quantity, off-invoice discount and rebate accruals post against a promotion accrual COA so trade spend is visible in the P&L."),
        ("Post to the general ledger", "Invoices post AR→Revenue; COGS posts Inventory→COGS; excise posts Excise expense→Excise payable. Every FMCG amount reaches the COA — trial balance, income statement and balance sheet pick it up unmodified."),
        ("Report IFRS", "IFRS presentation: revenue → COGS → gross profit → channel trade spend → operating result → corporate income tax accrual (10% on the foreign-owned share for Qatar). Balance-sheet mapping: inventory, receivables, accruals, tax payables."),
    ];
}
