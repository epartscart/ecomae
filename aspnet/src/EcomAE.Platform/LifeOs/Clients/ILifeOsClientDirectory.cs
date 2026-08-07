namespace EcomAE.Platform.LifeOs.Clients;

/// <summary>
/// In-memory LifeOS client join directory + companion session (scaffold).
/// Seeds a test client so mobile-browser tracking can be tried immediately.
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

    LifeOsCompanionSession CompanionSession(string clientId, string? joinToken);

    LifeOsTrackResult RecordTrack(LifeOsTrackEvent evt);

    LifeOsTalkReply Talk(LifeOsTalkRequest request);

    object CompanionDigest();
}
