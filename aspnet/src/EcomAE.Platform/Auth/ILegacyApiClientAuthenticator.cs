namespace EcomAE.Platform.Auth;

public interface ILegacyApiClientAuthenticator
{
    Task<LegacyApiClientAuthResult> RequireAsync(
        HttpRequest request,
        string needProduct,
        string? action,
        CancellationToken cancellationToken = default);
}
