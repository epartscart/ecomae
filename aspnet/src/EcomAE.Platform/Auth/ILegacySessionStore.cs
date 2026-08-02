namespace EcomAE.Platform.Auth;

public interface ILegacySessionStore
{
    bool IsConfigured { get; }

    Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default);
}
