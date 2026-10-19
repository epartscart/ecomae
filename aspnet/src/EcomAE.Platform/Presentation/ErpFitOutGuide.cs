namespace EcomAE.Platform.Presentation;

/// <summary>
/// The whole fit-out workspace as one numbered path. Each step opens the tab
/// the tenant works in, matching the real workflow: project + cost codes,
/// estimate/BOQ to contract, budget, buy or issue, shared-cost allocation,
/// then the monthly claim/recognition cycle.
/// </summary>
public static class ErpFitOutGuide
{
    public const string TabKey = "guide";

    public sealed record Step(int Number, string Key, string Label, string Icon, string Href, string Detail);

    public sealed record Lane(string Title, string Icon, IReadOnlyList<Step> Steps);

    public static readonly IReadOnlyList<Lane> Lanes = Build();

    public static IReadOnlyList<Step> Steps { get; } = Lanes.SelectMany(lane => lane.Steps).ToArray();

    public static string Normalize(string? raw)
    {
        var tab = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpFitOutWorkspace.IsContentTab(tab) ? tab : TabKey;
    }

    private static IReadOnlyList<Lane> Build()
    {
        var number = 0;
        Step Step(string key, string detail)
        {
            var tab = ErpFitOutWorkspace.Tabs.Single(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            number++;
            return new Step(number, tab.Key, tab.Label, tab.Icon, tab.Href, detail);
        }

        return
        [
            new("Set up", "fa-sitemap",
            [
                Step("projects",
                    "Create the project and its hierarchical cost codes, then set the contract terms "
                    + "(advance, retention, VAT) on the contract."),
                Step("costing",
                    "Build the estimate / BOQ with sections, component rates and markup, raise a "
                    + "revision when the scope changes, issue the quotation, and set the approved "
                    + "budget per cost code."),
            ]),
            new("Run the job", "fa-shopping-cart",
            [
                Step("procurement",
                    "Buy through the chain — purchase requisition to RFQ, purchase order, GRN and "
                    + "supplier bill — with the three-way match and tolerance check."),
                Step("materials",
                    "Issue warehouse stock to the project, record returns, and split shared costs "
                    + "across projects with a weighted allocation run."),
            ]),
            new("Every month", "fa-calendar-check-o",
            [
                Step("month-end",
                    "Update the BOQ-line % complete, run weighted progress, and run POC recognition "
                    + "to book earned revenue."),
                Step("claims",
                    "Generate the client progress claim: work-done-to-date less previously certified, "
                    + "retention held back, plus VAT. Track subcontract certifications and recovery."),
                Step("reports",
                    "Review the cost ledger, committed vs actual vs forecast, the material register, "
                    + "claims history, WIP and the project P&L. Download the board pack."),
            ]),
        ];
    }
}
