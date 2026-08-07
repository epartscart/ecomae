namespace EcomAE.Platform.LifeOs.Clients;

public sealed record LifeOsClientProfile(
    string ClientId,
    string DisplayName,
    string CloneName,
    string Email,
    string JoinToken,
    bool IsTest,
    DateTimeOffset JoinedAtUtc,
    string Status,
    IReadOnlyList<string> Capabilities,
    string? Country,
    string? CountryCode,
    string? City,
    string? TimeZone,
    string? Locale,
    string? UserAgent,
    string? Platform,
    string? Referrer,
    string? JoinSource,
    string? IpCountryHint,
    int? OwnerUserId,
    int TrackCount,
    int TalkCount,
    int ActivityCount);

public sealed record LifeOsJoinRequest(
    string? DisplayName,
    string? Email,
    string? Country,
    string? CountryCode,
    string? City,
    string? TimeZone,
    string? Locale,
    string? Platform,
    string? UserAgent,
    string? Referrer,
    string? JoinSource,
    string? IpCountryHint,
    bool? UseTestClient,
    int? OwnerUserId);

public sealed record LifeOsJoinResult(
    bool Ok,
    string Message,
    LifeOsClientProfile Client,
    string CompanionUrl,
    string ResultsUrl,
    string InstallUrl,
    string ManifestUrl,
    IReadOnlyList<string> NextSteps);

public sealed record LifeOsTrackEvent(
    string? ClientId,
    string? JoinToken,
    string? Kind,
    string? Label,
    double? Value,
    string? Note);

public sealed record LifeOsTalkRequest(
    string? ClientId,
    string? JoinToken,
    string? Utterance,
    string? Mode);

public sealed record LifeOsTalkReply(
    bool Ok,
    string HumanName,
    string CloneName,
    string Heard,
    string Reply,
    string GuideStep,
    string Mode,
    string ActivityId,
    DateTimeOffset AtUtc,
    IReadOnlyList<string> SuggestedActions);

public sealed record LifeOsActivityEvent(
    string Id,
    DateTimeOffset AtUtc,
    string Kind,
    string Label,
    string? Detail,
    string? Mode,
    double? Value,
    string? HumanUtterance,
    string? CloneReply);

public sealed record LifeOsCompanionSession(
    string ClientId,
    string DisplayName,
    string CloneName,
    bool IsTest,
    string? Country,
    string? CountryCode,
    string? TimeZone,
    int TrackEventCount,
    int TalkCount,
    int ActivityCount,
    IReadOnlyList<LifeOsActivityEvent> RecentTracks,
    IReadOnlyList<LifeOsActivityEvent> RecentTalks,
    IReadOnlyList<object> GuideBeats,
    object Capabilities);

public sealed record LifeOsTrackResult(
    bool Ok,
    bool Scaffold,
    LifeOsActivityEvent Track,
    string CloneAdvice,
    LifeOsCompanionSession Session);

public sealed record LifeOsClientResults(
    bool Ok,
    LifeOsClientProfile Client,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    int Total,
    IReadOnlyList<LifeOsActivityEvent> Activities,
    object Summary);
