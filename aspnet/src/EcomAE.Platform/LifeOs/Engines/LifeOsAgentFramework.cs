using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsAgentFramework : ILifeOsAgentFramework
{
    public IReadOnlyList<LifeOsAgentDescriptor> Catalog { get; } =
    [
        D("memory", "Memory Agent", "cognition", ["retrieve", "store", "summarize"]),
        D("planner", "Planner Agent", "cognition", ["decompose", "prioritize", "track"]),
        D("research", "Research Agent", "knowledge", ["search", "synthesize"]),
        D("code", "Code Agent", "engineering", ["review", "generate", "explain"]),
        D("business", "Business Advisor", "business", ["ops", "strategy"]),
        D("finance", "Finance Agent", "finance", ["analyze", "budget", "invest"]),
        D("legal", "Legal Assistant", "legal", ["draft", "review"]),
        D("health", "Health Coach", "health", ["vitals", "habits"]),
        D("fitness", "Fitness Coach", "health", ["workout", "recovery"]),
        D("nutrition", "Nutrition Agent", "health", ["meals", "macros"]),
        D("tutor", "Learning Tutor", "education", ["explain", "quiz"]),
        D("writing", "Writing Assistant", "productivity", ["draft", "edit"]),
        D("marketing", "Marketing Agent", "business", ["campaigns", "copy"]),
        D("sales", "Sales Agent", "business", ["pipeline", "outreach"]),
        D("support", "Customer Support Agent", "business", ["tickets", "faq"]),
        D("hr", "HR Agent", "business", ["people", "policy"]),
        D("travel", "Travel Planner", "lifestyle", ["itinerary", "booking"]),
        D("home", "Home Manager", "home", ["devices", "routines"]),
        D("calendar", "Calendar Agent", "productivity", ["schedule", "remind"]),
        D("email", "Email Agent", "productivity", ["triage", "draft"]),
        D("notification", "Notification Agent", "system", ["route", "mute"]),
        D("automation", "Automation Agent", "system", ["rules", "jobs"]),
        D("security", "Security Agent", "system", ["policy", "risk"]),
        D("vision", "Vision Agent", "perception", ["scene", "ocr"]),
        D("voice", "Voice Agent", "perception", ["asr", "tts"]),
        D("translation", "Translation Agent", "productivity", ["translate"]),
        D("shopping", "Shopping Agent", "lifestyle", ["compare", "order"]),
        D("investment", "Investment Advisor", "finance", ["portfolio"]),
        D("analytics", "Analytics Agent", "business", ["metrics", "forecast"]),
        D("developer", "Developer Agent", "engineering", ["repos", "ci"]),
    ];

    public IReadOnlyList<string> SelectAgents(string intent, LifeOsContextObject context)
    {
        var text = $"{intent} {context.Summary}".ToLowerInvariant();
        var selected = new List<string> { "memory", "planner" };

        void Add(string key)
        {
            if (!selected.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                selected.Add(key);
            }
        }

        if (text.Contains("meet") || text.Contains("schedule") || text.Contains("calendar"))
        {
            Add("calendar");
            Add("email");
            Add("notification");
        }

        if (text.Contains("code") || text.Contains("api") || text.Contains("architecture"))
        {
            Add("code");
            Add("developer");
            Add("research");
        }

        if (text.Contains("health") || text.Contains("stress") || text.Contains("workout"))
        {
            Add("health");
            Add("fitness");
        }

        if (text.Contains("money") || text.Contains("invest") || text.Contains("budget"))
        {
            Add("finance");
            Add("investment");
        }

        if (text.Contains("travel") || text.Contains("flight"))
        {
            Add("travel");
        }

        if (text.Contains("home") || text.Contains("device"))
        {
            Add("home");
            Add("automation");
        }

        Add("security");
        return selected;
    }

    public Task<LifeOsAgentResult> InvokeAsync(
        string agentKey,
        LifeOsEvent trigger,
        LifeOsContextObject context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var agent = Catalog.FirstOrDefault(a =>
            string.Equals(a.Key, agentKey, StringComparison.OrdinalIgnoreCase));
        if (agent is null)
        {
            return Task.FromResult(new LifeOsAgentResult(
                agentKey, false, "Unknown agent", 0,
                [LifeOsAgentLifecycleStage.Request, LifeOsAgentLifecycleStage.CapabilityCheck]));
        }

        var stages = Enum.GetValues<LifeOsAgentLifecycleStage>().ToList();
        var summary =
            $"{agent.Title} scaffold result for {trigger.Type}: " +
            $"context confidence {context.AggregateConfidence:0.00}; " +
            $"capabilities [{string.Join(", ", agent.Capabilities)}].";

        return Task.FromResult(new LifeOsAgentResult(
            agent.Key,
            true,
            summary,
            Math.Clamp(context.AggregateConfidence * 0.95, 0.4, 0.98),
            stages));
    }

    private static LifeOsAgentDescriptor D(string key, string title, string domain, string[] caps)
        => new(key, title, domain, caps, "scaffold");
}
