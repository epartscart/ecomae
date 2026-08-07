using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsContextEngine : ILifeOsContextEngine
{
    public IReadOnlyList<string> KnownSourceNames { get; } =
    [
        "Voice", "Screen", "Camera", "GPS", "Calendar", "Email", "Documents",
        "Health Sensors", "Recent Conversations", "Running Applications",
        "Browser Tabs", "Device Status", "User Preferences", "Historical Memory",
        "Current Workflow"
    ];

    public LifeOsContextObject Build(LifeOsEvent trigger, IReadOnlyList<LifeOsMemoryEntry>? memoryHints = null)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        var now = DateTimeOffset.UtcNow;
        var sources = new List<LifeOsContextSource>
        {
            new("Voice", Payload(trigger, "transcript", trigger.Type.ToString()),
                trigger.Type == LifeOsEventType.VoiceEvent ? 0.92 : 0.35, trigger.Timestamp),
            new("Device Status", trigger.Source, 0.8, now),
            new("User Preferences", "ambient-assist; explainability=on; privacy=local-first", 0.88, now),
            new("Current Workflow", "lifeos-orchestrator-scaffold", 0.7, now),
            new("Calendar", "next-24h: inferred from intent", 0.55, now),
            new("Historical Memory", memoryHints is { Count: > 0 }
                ? $"{memoryHints.Count} memory hints"
                : "no project hints", 0.6, now),
        };

        var aggregate = sources.Average(s => s.Confidence);
        var summary =
            $"Context for {trigger.Type} from {trigger.Source}: " +
            Payload(trigger, "transcript", "multimodal ambient input");

        return new LifeOsContextObject(
            $"CTX-{trigger.EventId}",
            now,
            sources,
            Math.Round(aggregate, 3),
            summary);
    }

    private static string Payload(LifeOsEvent e, string key, string fallback)
        => e.Payload.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
