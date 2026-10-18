using System.Data.Common;

namespace EcomAE.Platform.Auth;

public interface ILegacySessionStore
{
    bool IsConfigured { get; }

    Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default);

    Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default);

    Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Identity load on an already-open connection (the login path reuses the connection
    /// where the user was found, so tenant DB and credentials cannot diverge).
    /// Stores that cannot reuse a caller connection ignore it.
    /// </summary>
    Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, DbConnection? sharedConnection, CancellationToken cancellationToken = default)
        => GetAdminIdentityAsync(userId, cancellationToken);
}
