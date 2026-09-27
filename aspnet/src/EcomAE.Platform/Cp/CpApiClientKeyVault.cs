using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Holds a freshly minted API key for exactly one read so the PHP "shown once" panel can render it
/// after the post-redirect-get, without ever putting the key itself in a URL, a log or the database.
/// </summary>
public interface ICpApiClientKeyVault
{
    string Stash(string plainKey);

    string Take(string token);
}

public sealed class CpApiClientKeyVault : ICpApiClientKeyVault
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public CpApiClientKeyVault(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Stash(string plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey))
        {
            return string.Empty;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        _cache.Set(CacheKey(token), plainKey, Lifetime);
        return token;
    }

    public string Take(string token)
    {
        var slot = (token ?? string.Empty).Trim().ToLowerInvariant();
        if (slot.Length == 0 || !_cache.TryGetValue(CacheKey(slot), out string? plainKey))
        {
            return string.Empty;
        }

        _cache.Remove(CacheKey(slot));
        return plainKey ?? string.Empty;
    }

    private static string CacheKey(string token) => "cp-api-client-key:" + token;
}
