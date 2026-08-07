namespace EcomAE.Platform.LifeOs.Models;

/// <summary>Part 2 Ch.9 — layered memory inspired by human cognition.</summary>
public enum LifeOsMemoryLayer
{
    Sensory,
    Working,
    Conversation,
    Task,
    Project,
    Personal,
    Knowledge,
    Experience,
    Strategic,
    Archive
}

public sealed record LifeOsMemoryEntry(
    string EntryId,
    LifeOsMemoryLayer Layer,
    string Key,
    string Content,
    DateTimeOffset StoredAt,
    TimeSpan? RetentionHint,
    IReadOnlyDictionary<string, string>? Tags = null);

public sealed record LifeOsMemorySnapshot(
    DateTimeOffset CapturedAt,
    IReadOnlyDictionary<string, int> CountsByLayer,
    IReadOnlyList<LifeOsMemoryEntry> Recent);
