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
    IReadOnlyList<string> Capabilities);

public sealed record LifeOsJoinRequest(
    string? DisplayName,
    string? Email,
    string? TimeZone,
    bool? UseTestClient);

public sealed record LifeOsJoinResult(
    bool Ok,
    string Message,
    LifeOsClientProfile Client,
    string CompanionUrl,
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
    IReadOnlyList<string> SuggestedActions);

public sealed record LifeOsCompanionSession(
    string ClientId,
    string DisplayName,
    string CloneName,
    bool IsTest,
    int TrackEventCount,
    IReadOnlyList<object> RecentTracks,
    IReadOnlyList<object> GuideBeats,
    object Capabilities);

public sealed record LifeOsTrackResult(
    bool Ok,
    bool Scaffold,
    object Track,
    string CloneAdvice,
    LifeOsCompanionSession Session);
