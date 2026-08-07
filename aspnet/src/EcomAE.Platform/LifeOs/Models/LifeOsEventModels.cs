namespace EcomAE.Platform.LifeOs.Models;

/// <summary>Part 2 Ch.7 — event-driven LifeOS bus types.</summary>
public enum LifeOsEventType
{
    VoiceEvent,
    VisionEvent,
    ScreenEvent,
    CalendarEvent,
    LocationEvent,
    HealthEvent,
    NotificationEvent,
    MemoryEvent,
    WorkflowEvent,
    AutomationEvent,
    AgentEvent
}

public enum LifeOsEventPriority
{
    Low,
    Normal,
    High,
    Critical
}

/// <summary>Normalized input event on the LifeOS event bus.</summary>
public sealed record LifeOsEvent(
    string EventId,
    LifeOsEventType Type,
    LifeOsEventPriority Priority,
    DateTimeOffset Timestamp,
    string Source,
    IReadOnlyDictionary<string, string> Payload);

public static class LifeOsEventFactory
{
    public static LifeOsEvent Create(
        LifeOsEventType type,
        string source,
        IReadOnlyDictionary<string, string> payload,
        LifeOsEventPriority priority = LifeOsEventPriority.Normal,
        DateTimeOffset? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var id = $"EVT-{ts:yyyyMMddHHmmss}-{Random.Shared.Next(100000, 999999)}";
        return new LifeOsEvent(id, type, priority, ts, source, payload);
    }

    public static LifeOsEvent SampleVoice(string transcript = "Schedule tomorrow's meeting.")
        => Create(
            LifeOsEventType.VoiceEvent,
            "Wearable",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["transcript"] = transcript
            },
            LifeOsEventPriority.High);
}
