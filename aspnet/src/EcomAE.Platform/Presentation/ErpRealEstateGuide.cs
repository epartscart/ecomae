namespace EcomAE.Platform.Presentation;

/// <summary>
/// The whole real-estate area as one numbered path. Each step opens that workspace.
/// Labels stay the tab names so the picture and the page use the same words.
/// </summary>
public static class ErpRealEstateGuide
{
    public const string TabKey = "guide";

    public sealed record Step(int Number, string Key, string Label, string Icon, string Href);

    public sealed record Lane(string Title, string Icon, IReadOnlyList<Step> Steps);

    public static readonly IReadOnlyList<Lane> Lanes = Build();

    public static IReadOnlyList<Step> Steps { get; } = Lanes.SelectMany(lane => lane.Steps).ToArray();

    public static string Normalize(string? raw)
    {
        var tab = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return ErpRealEstateWorkspace.IsContentTab(tab) ? tab : TabKey;
    }

    private static IReadOnlyList<Lane> Build()
    {
        var number = 0;
        Step Step(string key)
        {
            var tab = ErpRealEstateWorkspace.Tabs.Single(item =>
                string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
            number++;
            return new Step(number, tab.Key, tab.Label, tab.Icon, tab.Href);
        }

        return
        [
            new("Take it on", "fa-building",
            [
                Step("opportunities"),
                Step("acquisitions"),
                Step("properties"),
                Step("valuations"),
                Step("yearbook"),
            ]),
            new("Let it", "fa-key",
            [
                Step("leases"),
                Step("documents"),
                Step("expiry"),
            ]),
            new("Collect", "fa-money",
            [
                Step("collections"),
                Step("calendar"),
            ]),
            new("Look after", "fa-wrench",
            [
                Step("maintenance"),
                Step("alerts"),
            ]),
            new("Build and sell", "fa-hard-hat",
            [
                Step("projects"),
                Step("construction"),
                Step("sales"),
            ]),
            new("See the result", "fa-bar-chart",
            [
                Step("dashboard"),
                Step("reports"),
                Step("import"),
            ]),
        ];
    }
}
