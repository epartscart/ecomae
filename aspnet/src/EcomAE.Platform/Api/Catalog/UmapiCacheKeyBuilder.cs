using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Api.Catalog;

/// <summary>
/// Mirrors PHP <c>epc_cache_key</c> / <c>epc_normalize_vin</c>.
/// </summary>
public static partial class UmapiCacheKeyBuilder
{
    public static string Build(string action, string section, string language, string region, IReadOnlyDictionary<string, object?> parameters)
    {
        var ordered = parameters
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var json = JsonSerializer.Serialize(ordered);
        var material = $"{action}|{section}|{language}|{region}|{json}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
    }

    public static string NormalizeVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return string.Empty;
        }

        return VinCleaner().Replace(vin.ToUpperInvariant(), string.Empty);
    }

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex VinCleaner();
}
