namespace EcomAE.Platform.LifeOs.Models;

/// <summary>Part 2 Ch.8 — scored context source feeding the unified Context Object.</summary>
public sealed record LifeOsContextSource(
    string Name,
    string Value,
    double Confidence,
    DateTimeOffset ObservedAt);

/// <summary>Unified context object — LifeOS never reasons from a single prompt.</summary>
public sealed record LifeOsContextObject(
    string ContextId,
    DateTimeOffset BuiltAt,
    IReadOnlyList<LifeOsContextSource> Sources,
    double AggregateConfidence,
    string Summary);
