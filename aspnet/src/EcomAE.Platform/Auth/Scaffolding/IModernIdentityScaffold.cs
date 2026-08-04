namespace EcomAE.Platform.Auth.Scaffolding;

/// <summary>
/// Unwired modern-identity contract (OAuth 2.1 / MFA) for Enterprise BOS scaffolding.
/// Not registered in DI; PHP cookie bridge remains authoritative.
/// </summary>
public interface IModernIdentityScaffold
{
    Task<bool> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}
