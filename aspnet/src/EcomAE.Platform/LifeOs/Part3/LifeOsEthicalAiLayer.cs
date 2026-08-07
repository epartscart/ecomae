namespace EcomAE.Platform.LifeOs.Part3;

public sealed class LifeOsEthicalAiLayer : ILifeOsEthicalAiLayer
{
    public LifeOsEthicalVerdict Validate(
        string action,
        double confidence,
        bool userPermission,
        bool irreversible)
    {
        var checks = new List<LifeOsEthicalCheck>
        {
            new("User Permission", userPermission || !irreversible,
                irreversible ? "Irreversible actions require explicit permission" : "Reversible / advisory"),
            new("Privacy Policy", true, "Local-first perception preferred; cloud only after policy"),
            new("Security Policy", true, "No secret exfiltration in scaffold"),
            new("Organizational Rules", true, "Super-CP / tenant isolation respected"),
            new("Regulatory Compliance", true, "Compliance map scaffold — residency roadmap"),
            new("Safety Assessment", !action.Contains("delete-all", StringComparison.OrdinalIgnoreCase),
                "Destructive bulk actions blocked"),
            new("Confidence Threshold", confidence >= 0.45, $"confidence={confidence:0.00}"),
            new("Resource Availability", true, "Scaffold resources available"),
        };

        var allowed = checks.All(c => c.Passed);
        return new LifeOsEthicalVerdict(
            allowed,
            checks,
            allowed
                ? "Ethical AI Layer: all checks passed — execution permitted (scaffold)"
                : "Ethical AI Layer: blocked — await user permission or raise confidence");
    }

    public object Digest() => new
    {
        chapter = 23,
        title = "Ethical AI Layer",
        checks = new[]
        {
            "User Permission", "Privacy Policy", "Security Policy", "Organizational Rules",
            "Regulatory Compliance", "Safety Assessment", "Confidence Threshold", "Resource Availability"
        },
        status = "scaffold"
    };
}
