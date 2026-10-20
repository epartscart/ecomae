namespace EcomAE.Platform.Presentation;

/// <summary>
/// The whole jewellery workspace as one numbered path. Each step opens the
/// tab the tenant works in, matching the real flow: stock the pieces by
/// karat and weight, buy metal, sell at the counter, run repairs, then the
/// monthly GL cycle.
/// </summary>
public static class ErpJewelleryGuide
{
    public const string TabKey = "guide";

    public sealed record Step(int Number, string Key, string Label, string Icon, string Href, string Detail);

    public sealed record Lane(string Title, string Icon, IReadOnlyList<Step> Steps);

    public static readonly IReadOnlyList<Lane> Lanes = Build();

    public static IReadOnlyList<Step> Steps { get; } = Lanes.SelectMany(lane => lane.Steps).ToArray();

    public static string Normalize(string? raw)
    {
        var tab = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpJewelleryWorkspace.IsContentTab(tab) ? tab : TabKey;
    }

    private static IReadOnlyList<Lane> Build()
    {
        var number = 0;
        Step Step(string key, string detail)
        {
            var tab = ErpJewelleryWorkspace.Tabs.Single(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            number++;
            return new Step(number, tab.Key, tab.Label, tab.Icon, tab.Href, detail);
        }

        return
        [
            new("Set up", "fa-diamond",
            [
                Step("stock",
                    "Register each item by metal and karat — code, description, gram cost and "
                    + "sale price — so stock weight and value stay exact."),
            ]),
            new("Run the counter", "fa-tags",
            [
                Step("purchases",
                    "Book metal purchase vouchers (RMP/RDP) — weight, purity and rate drive the "
                    + "supplier payable and the stock-on-hand value."),
                Step("sales",
                    "Raise retail invoices (RIN) and metal sales (MSL) — grams, making charge and "
                    + "5% VAT per line, then issue the customer-facing tax invoice."),
                Step("repairs",
                    "Take in repair jobs, track them through the workshop and bill on delivery."),
                Step("invoices",
                    "Issue UAE FTA tax invoices for retail sales — TRN, 5% VAT breakdown, "
                    + "printable to share with the customer."),
            ]),
            new("Every month", "fa-calendar-check-o",
            [
                Step("month-end",
                    "Post to the general ledger — metal purchases and sale vouchers with VAT — so "
                    + "the P&L and balance sheet are current."),
                Step("reports",
                    "Review the stock valuation, sales register, repair pipeline and the GL journal "
                    + "trail proving the figures reached the statements."),
            ]),
        ];
    }
}
