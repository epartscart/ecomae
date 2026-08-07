using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsPlanningEngine : ILifeOsPlanningEngine
{
    public IReadOnlyList<string> PlannerTypes { get; } =
    [
        "Reactive Planner", "Daily Planner", "Strategic Planner", "Goal Planner",
        "Business Planner", "Learning Planner", "Travel Planner", "Health Planner",
        "Finance Planner", "Project Planner"
    ];

    public LifeOsPlan SampleLifeOsMvp()
        => Decompose("Launch LifeOS MVP");

    public LifeOsPlan Decompose(string goal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        var now = DateTimeOffset.UtcNow;
        var planId = $"PLAN-{now:yyyyMMddHHmmss}";

        // Part 3 Ch.18 example: Launch LifeOS → Research…Launch
        if (goal.Contains("Launch LifeOS", StringComparison.OrdinalIgnoreCase)
            && !goal.Contains("MVP", StringComparison.OrdinalIgnoreCase))
        {
            return new LifeOsPlan(planId, goal, now,
            [
                T("t1", "Research", 1, []),
                T("t2", "Architecture", 2, ["t1"]),
                T("t3", "Backend", 3, ["t2"]),
                T("t4", "Database", 3, ["t2"]),
                T("t5", "AI", 4, ["t3", "t4"]),
                T("t6", "Mobile", 5, ["t5"]),
                T("t7", "Desktop", 5, ["t5"]),
                T("t8", "Testing", 6, ["t6", "t7"]),
                T("t9", "Deployment", 7, ["t8"]),
                T("t10", "Launch", 8, ["t9"]),
            ]);
        }

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

    public object PlannerTypesDigest() => new
    {
        chapter = 18,
        title = "Planning Engine",
        plannerTypes = PlannerTypes,
        sampleGoal = "Launch LifeOS",
        sampleSteps = new[]
        {
            "Research", "Architecture", "Backend", "Database", "AI",
            "Mobile", "Desktop", "Testing", "Deployment", "Launch"
        }
    };

    private static LifeOsPlanTask T(string id, string title, int priority, string[] deps)
        => new(id, title, priority, deps, deps.Length == 0 ? LifeOsTaskStatus.Ready : LifeOsTaskStatus.Pending);
}
