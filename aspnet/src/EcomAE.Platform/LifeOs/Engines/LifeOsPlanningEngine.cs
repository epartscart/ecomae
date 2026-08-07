using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsPlanningEngine : ILifeOsPlanningEngine
{
    public LifeOsPlan SampleLifeOsMvp()
        => Decompose("Launch LifeOS MVP");

    public LifeOsPlan Decompose(string goal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        var now = DateTimeOffset.UtcNow;
        var planId = $"PLAN-{now:yyyyMMddHHmmss}";

        // Spec example goal decomposition (+ intent-aware variants).
        if (goal.Contains("LifeOS", StringComparison.OrdinalIgnoreCase)
            || goal.Contains("MVP", StringComparison.OrdinalIgnoreCase))
        {
            return new LifeOsPlan(planId, goal, now,
            [
                T("t1", "Design Architecture", 1, []),
                T("t2", "Define APIs", 2, ["t1"]),
                T("t3", "Build Backend", 3, ["t2"]),
                T("t4", "Build Mobile App", 4, ["t2"]),
                T("t5", "Build Desktop App", 4, ["t2"]),
                T("t6", "Implement Memory", 3, ["t3"]),
                T("t7", "Add Voice", 5, ["t3"]),
                T("t8", "Add Vision", 5, ["t3"]),
                T("t9", "Testing", 6, ["t4", "t5", "t6", "t7", "t8"]),
                T("t10", "Deployment", 7, ["t9"]),
            ]);
        }

        if (goal.Contains("meet", StringComparison.OrdinalIgnoreCase)
            || goal.Contains("schedule", StringComparison.OrdinalIgnoreCase))
        {
            return new LifeOsPlan(planId, goal, now,
            [
                T("t1", "Retrieve calendar availability", 1, []),
                T("t2", "Propose time slots", 2, ["t1"]),
                T("t3", "Draft invite", 3, ["t2"]),
                T("t4", "Await user confirmation", 4, ["t3"]),
                T("t5", "Send calendar event", 5, ["t4"]),
            ]);
        }

        return new LifeOsPlan(planId, goal, now,
        [
            T("t1", "Clarify goal constraints", 1, []),
            T("t2", "Gather context & memory", 2, ["t1"]),
            T("t3", "Select specialist agents", 3, ["t2"]),
            T("t4", "Execute workflow steps", 4, ["t3"]),
            T("t5", "Validate & present result", 5, ["t4"]),
        ]);
    }

    private static LifeOsPlanTask T(string id, string title, int priority, string[] deps)
        => new(id, title, priority, deps, deps.Length == 0 ? LifeOsTaskStatus.Ready : LifeOsTaskStatus.Pending);
}
