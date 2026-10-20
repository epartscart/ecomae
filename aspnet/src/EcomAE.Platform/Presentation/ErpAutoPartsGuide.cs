namespace EcomAE.Platform.Presentation;

/// <summary>
/// The whole auto-parts workspace as one numbered path. Each step opens the
/// tab the tenant works in, matching the real trading flow: catalogue the
/// parts, receive stock, sell to garages, then the monthly GL cycle.
/// </summary>
public static class ErpAutoPartsGuide
{
    public const string TabKey = "guide";

    public sealed record Step(int Number, string Key, string Label, string Icon, string Href, string Detail);

    public sealed record Lane(string Title, string Icon, IReadOnlyList<Step> Steps);

    public static readonly IReadOnlyList<Lane> Lanes = Build();

    public static IReadOnlyList<Step> Steps { get; } = Lanes.SelectMany(lane => lane.Steps).ToArray();

    public static string Normalize(string? raw)
    {
        var tab = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpAutoPartsWorkspace.IsContentTab(tab) ? tab : TabKey;
    }

    private static IReadOnlyList<Lane> Build()
    {
        var number = 0;
        Step Step(string key, string detail)
        {
            var tab = ErpAutoPartsWorkspace.Tabs.Single(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            number++;
            return new Step(number, tab.Key, tab.Label, tab.Icon, tab.Href, detail);
        }

        return
        [
            new("Set up", "fa-cogs",
            [
                Step("catalogue",
                    "Register each part once — SKU, brand, OEM number, category, vehicle fitment, "
                    + "purchase cost and sale price — so every screen prices and filters consistently."),
                Step("stock",
                    "Receive the opening stock; the movements log tracks every receipt, issue and "
                    + "adjustment, and the low-stock list shows what to reorder."),
            ]),
            new("Run the trade", "fa-shopping-cart",
            [
                Step("purchases",
                    "Raise supplier purchase orders, mark them received — stock and the supplier "
                    + "payable update together."),
                Step("sales",
                    "Sell to garages and fleet customers: confirm the order, mark it invoiced, "
                    + "then issue the UAE tax invoice the customer can keep."),
                Step("invoices",
                    "Issue UAE FTA tax invoices for invoiced sales — TRN, 5% VAT breakdown, "
                    + "printable to share with the customer."),
            ]),
            new("Every month", "fa-calendar-check-o",
            [
                Step("month-end",
                    "Post to the general ledger — purchases, invoiced sales with 5% VAT, cost of "
                    + "goods sold and customer receipts — so the P&L and balance sheet are current."),
                Step("reports",
                    "Review the sales register, stock valuation, low stock and the GL journal trail "
                    + "proving the figures reached the statements."),
            ]),
        ];
    }
}
