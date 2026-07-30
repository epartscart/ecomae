namespace EcomAE.Platform.Services;

public interface ITenantResolver
{
    ValueTask<TenantContext> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
