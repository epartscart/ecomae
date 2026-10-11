using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Auth;

/// <summary>
/// Short-TTL process-local cache over <see cref="ILegacySessionStore"/>.
/// Does not replace PHP cookies; never caches across tenants incorrectly when key includes host/db.
/// </summary>
public sealed class CachingLegacySessionStore : ILegacySessionStore, ILegacySessionInvalidator
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
            return Canonicalize(cached);
        }

        var identity = await _inner.GetAdminIdentityAsync(userId, cancellationToken).ConfigureAwait(false);
        if (identity is not null)
        {
            identity = Canonicalize(identity);
            _cache.Set(key, identity, TimeSpan.FromSeconds(Math.Clamp(_options.IdentityTtlSeconds, 5, 300)));
        }

        return identity;
    }

    public void Forget(string host, string adminSession, int adminUser, string customerSession, int customerUser)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(adminSession))
        {
            _cache.Remove(ScopeKey("admin-sess", adminUser, adminSession, host));
            if (adminUser != 0)
            {
                _cache.Remove(ScopeKey("admin-sess", 0, adminSession, host));
            }
        }

        if (adminUser > 0)
        {
            _cache.Remove(ScopeKey("admin-id", adminUser, string.Empty, host));
        }

        if (!string.IsNullOrWhiteSpace(customerSession))
        {
            _cache.Remove(ScopeKey("cust-sess", customerUser, customerSession, host));
            if (customerUser != 0)
            {
                _cache.Remove(ScopeKey("cust-sess", 0, customerSession, host));
            }
        }
    }

    private string ScopeKey(string kind, int userId, string token, string? hostOverride = null)
    {
        var host = string.IsNullOrWhiteSpace(hostOverride)
            ? (_http.HttpContext?.Request.Host.Host ?? "unknown")
            : hostOverride;
        var db = string.Empty;
        if (_http.HttpContext?.Items[Middleware.TenantResolutionMiddleware.HttpContextItemKey] is Services.TenantContext tenant
            && !string.IsNullOrWhiteSpace(tenant.DatabaseName))
        {
            db = tenant.DatabaseName;
        }

        var tokenHash = string.IsNullOrEmpty(token) ? "-" : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)))[..16];
        return $"sess:{kind}:{host}:{db}:{userId}:{tokenHash}";
    }

    private LegacyAdminIdentity Canonicalize(LegacyAdminIdentity identity)
    {
        var host = _http.HttpContext?.Request.Host.Host;
        return PlatformHostPolicy.IsSuperCpHost(host)
            ? PlatformOperatorIdentity.WithCanonicalEmail(identity)
            : identity;
    }
}
