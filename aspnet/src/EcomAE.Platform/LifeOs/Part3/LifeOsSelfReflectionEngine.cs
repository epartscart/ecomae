namespace EcomAE.Platform.LifeOs.Part3;

public sealed class LifeOsSelfReflectionEngine : ILifeOsSelfReflectionEngine
{
    public LifeOsReflectionReport Reflect(
        string goal,
        string outcome,
        bool ethicsAllowed,
        double decisionScore)
    {
        var achieved = ethicsAllowed && decisionScore > 2.0;
        var errors = new List<string>();
        if (!ethicsAllowed)
        {
            errors.Add("Execution blocked by Ethical AI Layer");
        }

        var updates = new List<string>();
        if (outcome.Contains("meeting", StringComparison.OrdinalIgnoreCase))
        {
            updates.Add("Prefer calendar prep reminders before meetings");
        }

        return new LifeOsReflectionReport(
            $"REF-{DateTimeOffset.UtcNow:HHmmssfff}",
            GoalAchieved: achieved,
            UserSatisfiedEstimate: achieved,
            Accurate: ethicsAllowed,
            EfficiencyNote: achieved
                ? "Workflow could reuse prior project memory next time"
                : "Await confirmation before irreversible steps",
            Errors: errors,
            PreferenceUpdates: updates);
    }

    public object Digest() => new
    {
        chapter = 24,
        title = "Self-Reflection Engine",
        criteria = new[]
        {
            "Was the goal achieved?",
            "Was the user satisfied?",
            "Was the response accurate?",
            "Could the task have been completed more efficiently?",
            "Were any errors detected?",
            "Should preferences or planning strategies be updated?"
        },
        status = "scaffold"
    };
}
