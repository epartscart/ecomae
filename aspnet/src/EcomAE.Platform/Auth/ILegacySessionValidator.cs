namespace EcomAE.Platform.Auth;

public interface ILegacySessionValidator
{
    ValueTask<LegacySessionContext> ValidateAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Storefront/customer-only check (PHP <c>DP_User::getUserId</c>).
    /// Prefers <c>session</c>/<c>u_id</c> even when admin cookies are also present,
    /// so cart/account do not treat a CP admin cookie as "not logged in" for storefront.
    /// </summary>
    ValueTask<LegacySessionContext> ValidateCustomerAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
