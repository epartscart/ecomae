namespace EcomAE.Platform.LifeOs.Clients;

/// <summary>
/// LifeOS client join directory + companion activity (scaffold, no login required).
/// Clients access their results via clientId + joinToken.
/// </summary>
public interface ILifeOsClientDirectory
{
    LifeOsClientProfile TestClient { get; }

    IReadOnlyList<LifeOsClientProfile> List();

    LifeOsClientProfile? Find(string? clientId);

    LifeOsClientProfile? Authenticate(string? clientId, string? joinToken);

    LifeOsJoinResult Join(LifeOsJoinRequest request);

    LifeOsJoinResult OpenTestClient();

    object DirectoryDigest();

    object ControlPanelDigest();

    LifeOsCompanionSession CompanionSession(string clientId, string? joinToken);

    LifeOsTrackResult RecordTrack(LifeOsTrackEvent evt);

    LifeOsTalkReply Talk(LifeOsTalkRequest request);

    LifeOsClientResults Results(
        string? clientId,
        string? joinToken,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? kind);

    object CompanionDigest();
}
