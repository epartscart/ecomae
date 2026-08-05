using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Short-TTL process-local cache over <see cref="ILegacySessionStore"/>.
/// Does not replace PHP cookies; never caches across tenants incorrectly when key includes host/db.
/// </summary>
public sealed class CachingLegacySessionStore : ILegacySessionStore
{
    private readonly ILegacySessionStore _inner;
    private readonly IMemoryCache _cache;
    private readonly SessionCacheOptions _options;
    private readonly IHttpContextAccessor _http;

    public CachingLegacySessionStore(
        ILegacySessionStore inner,
        IMemoryCache cache,
        IOptions<SessionCacheOptions> options,
        IHttpContextAccessor http)
    {
        _inner = inner;
        _cache = cache;
        _options = options.Value;
        _http = http;
    }

    public bool IsConfigured => _inner.IsConfigured;

    public async Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await _inner.AdminSessionExistsAsync(sessionToken, userId, cancellationToken).ConfigureAwait(false);
        }

        var key = ScopeKey("admin-sess", userId, sessionToken);
        if (_cache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        var exists = await _inner.AdminSessionExistsAsync(sessionToken, userId, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, exists, TimeSpan.FromSeconds(Math.Clamp(_options.SessionExistsTtlSeconds, 5, 120)));
        return exists;
    }

    public async Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await _inner.CustomerSessionExistsAsync(sessionToken, userId, cancellationToken).ConfigureAwait(false);
        }

        var key = ScopeKey("cust-sess", userId, sessionToken);
        if (_cache.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        var exists = await _inner.CustomerSessionExistsAsync(sessionToken, userId, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, exists, TimeSpan.FromSeconds(Math.Clamp(_options.SessionExistsTtlSeconds, 5, 120)));
        return exists;
    }

    public async Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return await _inner.GetAdminIdentityAsync(userId, cancellationToken).ConfigureAwait(false);
        }

        var key = ScopeKey("admin-id", userId, string.Empty);
        if (_cache.TryGetValue(key, out LegacyAdminIdentity? cached) && cached is not null)
        {
            return cached;
        }

        var identity = await _inner.GetAdminIdentityAsync(userId, cancellationToken).ConfigureAwait(false);
        if (identity is not null)
        {
            _cache.Set(key, identity, TimeSpan.FromSeconds(Math.Clamp(_options.IdentityTtlSeconds, 5, 300)));
        }

        return identity;
    }

    private string ScopeKey(string kind, int userId, string token)
    {
        var host = _http.HttpContext?.Request.Host.Host ?? "unknown";
        var db = string.Empty;
        if (_http.HttpContext?.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] is Services.TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.DatabaseName))
        {
            db = tenant.DatabaseName;
        }

        var tokenHash = string.IsNullOrEmpty(token) ? "-" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)))[..16];
        return $"sess:{kind}:{host}:{db}:{userId}:{tokenHash}";
    }
}
