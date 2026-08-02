namespace EcomAE.Platform.Auth;

public sealed class MigrationLegacySessionStore : ILegacySessionStore
{
    public bool IsConfigured => false;

    public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
        => Task.FromResult<LegacyAdminIdentity?>(null);
}
