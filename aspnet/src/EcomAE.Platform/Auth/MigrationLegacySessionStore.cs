namespace EcomAE.Platform.Auth;

/// <summary>Used when TenantRegistry DB is not configured; forces cookie-only bridge.</summary>
public sealed class MigrationLegacySessionStore : ILegacySessionStore
{
    public bool IsConfigured => false;

    public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
