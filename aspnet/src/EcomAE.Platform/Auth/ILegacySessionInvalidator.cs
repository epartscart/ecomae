namespace EcomAE.Platform.Auth;

/// <summary>
/// Drops process-local session-exists cache entries so logout cannot stay "already logged in"
/// until TTL. Host/db scoped — Super and every tenant have separate keys.
/// </summary>
public interface ILegacySessionInvalidator
{
    void Forget(string host, string adminSession, int adminUser, string customerSession, int customerUser);
}

public sealed class NoopLegacySessionInvalidator : ILegacySessionInvalidator
{
    public void Forget(string host, string adminSession, int adminUser, string customerSession, int customerUser)
    {
    }
}
