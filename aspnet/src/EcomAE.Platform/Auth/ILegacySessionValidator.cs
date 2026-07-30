namespace EcomAE.Platform.Auth;

public interface ILegacySessionValidator
{
    ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
