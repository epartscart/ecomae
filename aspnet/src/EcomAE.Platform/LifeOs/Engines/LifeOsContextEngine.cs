using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Part3;

namespace EcomAE.Platform.LifeOs.Engines;

public sealed class LifeOsContextEngine : ILifeOsContextEngine
{
    public IReadOnlyList<string> KnownSourceNames { get; } =
    [
        "Voice", "Screen", "Camera", "GPS", "Calendar", "Email", "Documents",
        "Health Sensors", "Recent Conversations", "Running Applications",
        "Browser Tabs", "Device Status", "User Preferences", "Historical Memory",
        "Current Workflow", "Goals", "Notifications", "Time", "Location"
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
            new("Time", now.ToString("O"), 1.0, now),
            new("Goals", "Build LifeOS; Launch BOS v3", 0.75, now),
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

    public LifeOsCurrentRealityModel BuildCurrentReality(LifeOsEvent trigger, LifeOsContextObject context)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(context);

        var transcript = Payload(trigger, "transcript", "");
        var coding = transcript.Contains("code", StringComparison.OrdinalIgnoreCase)
                     || trigger.Type == LifeOsEventType.ScreenEvent;
        var meeting = transcript.Contains("meeting", StringComparison.OrdinalIgnoreCase)
                      || transcript.Contains("schedule", StringComparison.OrdinalIgnoreCase);

        var activity = coding ? "Coding" : meeting ? "Meeting Prep" : "Working";
        var userState = meeting ? "Meeting" : "Working";
        var calendar = meeting ? "Architecture Review" : null;
        var focus = coding ? 92 : meeting ? 80 : 70;
        var energy = 78;
        var interrupt = focus >= 85 ? "LOW" : "MEDIUM";

        var conf = context.Sources.ToDictionary(
            s => s.Name,
            s => s.Confidence,
            StringComparer.OrdinalIgnoreCase);

        return new LifeOsCurrentRealityModel(
            UserState: userState,
            Activity: activity,
            Location: trigger.Source.Contains("Wearable", StringComparison.OrdinalIgnoreCase) ? "Mobile" : "Office",
            Device: trigger.Type == LifeOsEventType.ScreenEvent ? "Desktop" : "Desktop",
            CalendarEvent: calendar,
            EnergyLevel: energy,
            FocusScore: focus,
            Interruptibility: interrupt,
            CapturedAt: DateTimeOffset.UtcNow,
            SourceConfidence: conf);
    }

    public object CrmDigest() => new
    {
        chapter = 15,
        title = "Context Engine — Current Reality Model",
        sources = new[]
        {
            "Current Conversation", "Location", "Time", "Calendar", "Screen", "Camera",
            "Memory", "Goals", "Running Apps", "Recent Activities", "Health", "Notifications"
        },
        sample = new
        {
            userState = "Working",
            activity = "Coding",
            location = "Office",
            device = "Desktop",
            calendarEvent = "Architecture Review",
            energyLevel = 78,
            focusScore = 92,
            interruptibility = "LOW"
        },
        note = "Every LifeOS decision is based on the CRM"
    };

    private static string Payload(LifeOsEvent e, string key, string fallback)
        => e.Payload.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : fallback;
}
